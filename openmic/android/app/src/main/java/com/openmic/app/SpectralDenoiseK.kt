package com.openmic.app

import kotlin.math.cos
import kotlin.math.sin
import kotlin.math.sqrt

/**
 * Frequency-domain noise suppression for the phone — the Kotlin port of the
 * tested desktop/openmic_fx.py SpectralDenoise (and windows NoiseSuppress.cs).
 * Estimates noise per frequency bin, only updating during non-speech frames
 * (energy VAD), so steady background noise is removed while the voice passes.
 * strength 0..1. ~32 ms latency. PCM16 in/out, length preserved.
 */
class SpectralDenoiseK(sampleRate: Int, strength: Float) {
    private val n = 512
    private val hop = 256
    private val win = FloatArray(n) { sqrt(0.5 - 0.5 * cos(2.0 * Math.PI * it / (n - 1)) + 1e-8).toFloat() }
    private val oversub: Double
    private val floor: Double

    private var inbuf = FloatArray(0)
    private val acc = FloatArray(n)
    private var ready = FloatArray(0)
    private var noise: DoubleArray? = null
    private var emin = Double.NaN
    private val prevGain = DoubleArray(n) { 1.0 }

    init {
        val s = strength.coerceIn(0f, 1f).toDouble()
        oversub = 1.0 + 2.0 * s
        floor = 0.06 + 0.10 * (1.0 - s)
    }

    fun processShorts(input: ShortArray): ShortArray {
        val x = FloatArray(input.size) { input[it] / 32768f }
        inbuf = concat(inbuf, x)

        val re = DoubleArray(n)
        val im = DoubleArray(n)
        while (inbuf.size >= n) {
            for (i in 0 until n) { re[i] = (inbuf[i] * win[i]).toDouble(); im[i] = 0.0 }
            fft(re, im, false)

            val mag2 = DoubleArray(n)
            var e = 0.0
            for (i in 0 until n) { mag2[i] = re[i] * re[i] + im[i] * im[i]; e += mag2[i] }

            if (emin.isNaN()) emin = e
            else if (e < emin) emin = e
            else emin *= 1.0015
            val isSpeech = e > emin * 3.0 + 1e-9

            val nz = noise
            if (nz == null) {
                noise = DoubleArray(n) { mag2[it] + 1e-9 }
            } else if (!isSpeech) {
                for (i in 0 until n) nz[i] = 0.9 * nz[i] + 0.1 * mag2[i]
            }
            val nz2 = noise!!

            for (i in 0 until n) {
                var g = (mag2[i] - oversub * nz2[i]) / (mag2[i] + 1e-9)
                g = g.coerceIn(floor, 1.0)
                g = 0.6 * g + 0.4 * prevGain[i]
                prevGain[i] = g
                re[i] *= g; im[i] *= g
            }

            fft(re, im, true)
            for (i in 0 until n) acc[i] += (re[i] * win[i]).toFloat()

            ready = concat(ready, acc.copyOfRange(0, hop))
            System.arraycopy(acc, hop, acc, 0, n - hop)
            for (i in n - hop until n) acc[i] = 0f
            inbuf = inbuf.copyOfRange(hop, inbuf.size)
        }

        val want = input.size
        val out = ShortArray(want)
        if (ready.size >= want) {
            for (i in 0 until want) out[i] = toShort(ready[i])
            ready = ready.copyOfRange(want, ready.size)
        } else {
            val pad = want - ready.size
            for (i in ready.indices) out[pad + i] = toShort(ready[i])
            ready = FloatArray(0)
        }
        return out
    }

    private fun toShort(f: Float): Short {
        val v = (f * 32767f).toInt().coerceIn(-32768, 32767)
        return v.toShort()
    }

    private fun concat(a: FloatArray, b: FloatArray): FloatArray {
        val r = FloatArray(a.size + b.size)
        System.arraycopy(a, 0, r, 0, a.size)
        System.arraycopy(b, 0, r, a.size, b.size)
        return r
    }

    /** In-place iterative radix-2 FFT. */
    private fun fft(re: DoubleArray, im: DoubleArray, inverse: Boolean) {
        val size = re.size
        var j = 0
        for (i in 1 until size) {
            var bit = size shr 1
            while (j and bit != 0) { j = j xor bit; bit = bit shr 1 }
            j = j xor bit
            if (i < j) {
                val tr = re[i]; re[i] = re[j]; re[j] = tr
                val ti = im[i]; im[i] = im[j]; im[j] = ti
            }
        }
        var len = 2
        while (len <= size) {
            val ang = 2.0 * Math.PI / len * (if (inverse) 1 else -1)
            val wr = cos(ang); val wi = sin(ang)
            var i = 0
            while (i < size) {
                var curR = 1.0; var curI = 0.0
                for (k in 0 until len / 2) {
                    val aR = re[i + k]; val aI = im[i + k]
                    val bR = re[i + k + len / 2] * curR - im[i + k + len / 2] * curI
                    val bI = re[i + k + len / 2] * curI + im[i + k + len / 2] * curR
                    re[i + k] = aR + bR; im[i + k] = aI + bI
                    re[i + k + len / 2] = aR - bR; im[i + k + len / 2] = aI - bI
                    val nR = curR * wr - curI * wi
                    curI = curR * wi + curI * wr
                    curR = nR
                }
                i += len
            }
            len = len shl 1
        }
        if (inverse) for (i in 0 until size) { re[i] /= size; im[i] /= size }
    }
}
