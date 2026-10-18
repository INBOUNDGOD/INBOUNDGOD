package com.openmic.app

import org.json.JSONObject
import java.io.DataInputStream
import java.io.DataOutputStream
import java.io.OutputStream

/**
 * OpenMic wire protocol — Kotlin side. Must stay byte-compatible with
 * desktop/openmic_proto.py. All multi-byte integers are little-endian.
 */
object OpenMicProtocol {
    const val PROTO_VERSION = 1
    const val DEFAULT_PORT = 8126

    // UDP discovery beacon (must match desktop/openmic_discovery.py).
    const val DISCOVERY_PORT = 8127
    const val DISCOVERY_MAGIC = "OPENMIC"

    const val T_HELLO = 0x01
    const val T_START = 0x02
    const val T_AUDIO = 0x03
    const val T_PING = 0x04
    const val T_PONG = 0x05
    const val T_STOP = 0x06

    const val CODEC_PCM16 = 0
    const val CODEC_OPUS = 1

    private fun le32(v: Int): ByteArray = byteArrayOf(
        (v and 0xFF).toByte(),
        ((v ushr 8) and 0xFF).toByte(),
        ((v ushr 16) and 0xFF).toByte(),
        ((v ushr 24) and 0xFF).toByte(),
    )

    /** Write one framed message. Synchronise on [out] if shared across threads. */
    fun writeFrame(out: OutputStream, type: Int, payload: ByteArray = ByteArray(0)) {
        out.write(type)
        out.write(le32(payload.size))
        if (payload.isNotEmpty()) out.write(payload)
        out.flush()
    }

    fun writeJson(out: OutputStream, type: Int, obj: JSONObject) =
        writeFrame(out, type, obj.toString().toByteArray(Charsets.UTF_8))

    /** AUDIO payload: seq(u32) + tsMs(u32) + encoded bytes. */
    fun writeAudio(out: OutputStream, seq: Int, tsMs: Int, encoded: ByteArray, len: Int) {
        val payload = ByteArray(8 + len)
        System.arraycopy(le32(seq), 0, payload, 0, 4)
        System.arraycopy(le32(tsMs), 0, payload, 4, 4)
        System.arraycopy(encoded, 0, payload, 8, len)
        writeFrame(out, T_AUDIO, payload)
    }

    data class Frame(val type: Int, val payload: ByteArray)

    fun readFrame(input: DataInputStream): Frame {
        val type = input.read()
        if (type < 0) throw java.io.EOFException("peer closed")
        val b = ByteArray(4)
        input.readFully(b)
        val len = (b[0].toInt() and 0xFF) or
                ((b[1].toInt() and 0xFF) shl 8) or
                ((b[2].toInt() and 0xFF) shl 16) or
                ((b[3].toInt() and 0xFF) shl 24)
        val payload = ByteArray(len)
        if (len > 0) input.readFully(payload)
        return Frame(type, payload)
    }
}
