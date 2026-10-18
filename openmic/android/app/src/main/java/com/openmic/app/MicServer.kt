package com.openmic.app

import android.media.AudioFormat
import android.media.AudioRecord
import android.media.MediaRecorder
import android.media.audiofx.AcousticEchoCanceler
import android.media.audiofx.NoiseSuppressor
import android.util.Log
import org.json.JSONObject
import java.io.DataInputStream
import java.net.ServerSocket
import java.net.Socket
import java.util.concurrent.atomic.AtomicBoolean

/**
 * Runs the OpenMic server on the phone: listens for the desktop, captures the
 * mic as 16-bit PCM mono, and streams AUDIO frames. v1 is PCM16 (no NDK).
 *
 * The phone is the server; the desktop connects to the phone's IP:port.
 */
class MicServer(
    private val port: Int = OpenMicProtocol.DEFAULT_PORT,
    private val sampleRate: Int = 16000,
    private val frameMs: Int = 20,
    private val deviceName: String = android.os.Build.MODEL ?: "Android",
    /** Optional PIN. When set, the desktop must send a matching token in START. */
    private val pin: String? = null,
    private val onState: (State) -> Unit = {},
) {
    enum class State { LISTENING, CONNECTED, STOPPED, ERROR }

    private val running = AtomicBoolean(false)
    @Volatile private var serverSocket: ServerSocket? = null
    @Volatile var gainShift: Int = 0   // software volume: <<n, see applyGain

    val frameSamples get() = sampleRate * frameMs / 1000

    fun start() {
        if (running.getAndSet(true)) return
        Thread({ acceptLoop() }, "openmic-accept").start()
    }

    fun stop() {
        running.set(false)
        try { serverSocket?.close() } catch (_: Exception) {}
        onState(State.STOPPED)
    }

    private fun acceptLoop() {
        try {
            val srv = ServerSocket(port)
            srv.reuseAddress = true
            serverSocket = srv
            while (running.get()) {
                onState(State.LISTENING)
                val client = srv.accept()
                try {
                    serveClient(client)
                } catch (e: Exception) {
                    Log.w(TAG, "client ended: ${e.message}")
                } finally {
                    try { client.close() } catch (_: Exception) {}
                }
            }
        } catch (e: Exception) {
            if (running.get()) { Log.e(TAG, "accept error", e); onState(State.ERROR) }
        }
    }

    private fun serveClient(client: Socket) {
        client.tcpNoDelay = true
        val out = client.getOutputStream()
        val input = DataInputStream(client.getInputStream())

        OpenMicProtocol.writeJson(out, OpenMicProtocol.T_HELLO, JSONObject().apply {
            put("proto", OpenMicProtocol.PROTO_VERSION)
            put("sampleRate", sampleRate)
            put("channels", 1)
            put("codec", OpenMicProtocol.CODEC_PCM16)
            put("frameSamples", frameSamples)
            put("device", deviceName)
            put("appVersion", BuildInfo.VERSION)
            put("auth", pin != null)
        })

        val start = OpenMicProtocol.readFrame(input)
        if (start.type != OpenMicProtocol.T_START) return
        // Verify the PIN before streaming any audio.
        if (pin != null) {
            val token = try {
                org.json.JSONObject(String(start.payload, Charsets.UTF_8))
                    .optString("token", "")
            } catch (_: Exception) { "" }
            if (token != pin) {
                OpenMicProtocol.writeFrame(out, OpenMicProtocol.T_STOP)
                return
            }
        }
        onState(State.CONNECTED)

        // A reader thread answers PING / STOP from the desktop.
        val clientAlive = AtomicBoolean(true)
        Thread({
            try {
                while (clientAlive.get()) {
                    val f = OpenMicProtocol.readFrame(input)
                    when (f.type) {
                        OpenMicProtocol.T_PING ->
                            synchronized(out) { OpenMicProtocol.writeFrame(out, OpenMicProtocol.T_PONG) }
                        OpenMicProtocol.T_STOP -> { clientAlive.set(false); return@Thread }
                    }
                }
            } catch (_: Exception) { clientAlive.set(false) }
        }, "openmic-reader").start()

        captureAndSend(out, clientAlive)
    }

    private fun captureAndSend(out: java.io.OutputStream, alive: AtomicBoolean) {
        val minBuf = AudioRecord.getMinBufferSize(
            sampleRate, AudioFormat.CHANNEL_IN_MONO, AudioFormat.ENCODING_PCM_16BIT)
        val bufSize = maxOf(minBuf, frameSamples * 2 * 10)
        val record = AudioRecord(
            MediaRecorder.AudioSource.MIC, sampleRate,
            AudioFormat.CHANNEL_IN_MONO, AudioFormat.ENCODING_PCM_16BIT, bufSize)

        val aec = if (AcousticEchoCanceler.isAvailable())
            AcousticEchoCanceler.create(record.audioSessionId)?.apply { enabled = true } else null
        val ns = if (NoiseSuppressor.isAvailable())
            NoiseSuppressor.create(record.audioSessionId)?.apply { enabled = true } else null

        val samples = ShortArray(frameSamples)
        val bytes = ByteArray(frameSamples * 2)
        var seq = 0
        val t0 = System.currentTimeMillis()
        try {
            record.startRecording()
            while (running.get() && alive.get()) {
                var off = 0
                while (off < frameSamples) {
                    val r = record.read(samples, off, frameSamples - off)
                    if (r < 0) throw IllegalStateException("AudioRecord read $r")
                    off += r
                }
                applyGain(samples)
                pcm16ToBytes(samples, bytes)
                val tsMs = (System.currentTimeMillis() - t0).toInt()
                synchronized(out) {
                    OpenMicProtocol.writeAudio(out, seq, tsMs, bytes, bytes.size)
                }
                seq++
            }
        } finally {
            try { record.stop() } catch (_: Exception) {}
            record.release(); aec?.release(); ns?.release()
        }
    }

    /** Software volume: shift left to amplify (gainShift>0) with clamping. */
    private fun applyGain(s: ShortArray) {
        val g = gainShift
        if (g == 0) return
        for (i in s.indices) {
            var v = s[i].toInt()
            v = if (g > 0) v shl g else v shr (-g)
            if (v > 32767) v = 32767
            if (v < -32768) v = -32768
            s[i] = v.toShort()
        }
    }

    private fun pcm16ToBytes(s: ShortArray, b: ByteArray) {
        for (i in s.indices) {
            b[i * 2] = (s[i].toInt() and 0xFF).toByte()
            b[i * 2 + 1] = ((s[i].toInt() ushr 8) and 0xFF).toByte()
        }
    }

    companion object { private const val TAG = "OpenMicServer" }
}

object BuildInfo { const val VERSION = "1.0" }
