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
    private val bias: Double
    private val gmin: Double
    private val alpha = 0.98   // decision-directed smoothing

    private var inbuf = FloatArray(0)
    private val acc = FloatArray(n)
    private var ready = FloatArray(0)
    private var noiseMin: DoubleArray? = null
    private val prevG = DoubleArray(n) { 1.0 }
    private val prevGamma = DoubleArray(n) { 1.0 }

    init {
        val s = strength.coerceIn(0f, 1f).toDouble()
        bias = 1.5 + 1.5 * s
        gmin = 0.12 - 0.09 * s
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
            for (i in 0 until n) mag2[i] = re[i] * re[i] + im[i] * im[i]

            // minimum-statistics noise floor per bin (drop fast, leak up slowly)
            val nm = noiseMin
            if (nm == null) {
                noiseMin = DoubleArray(n) { mag2[it] + 1e-9 }
            } else {
                for (i in 0 until n) {
                    nm[i] = if (mag2[i] < nm[i]) mag2[i] else nm[i] + 0.002 * (mag2[i] - nm[i])
                }
            }
            val nmin = noiseMin!!

            // decision-directed Wiener gain: speech bins keep ~1 at any strength
            for (i in 0 until n) {
                val noise = nmin[i] * bias + 1e-12
                val gamma = Math.min(mag2[i] / noise, 1e3)
                var xi = alpha * prevG[i] * prevG[i] * prevGamma[i] +
                    (1.0 - alpha) * Math.max(gamma - 1.0, 0.0)
                if (xi < 1e-3) xi = 1e-3
                var g = xi / (1.0 + xi)
                if (g < gmin) g = gmin
                prevG[i] = g
                prevGamma[i] = gamma
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
