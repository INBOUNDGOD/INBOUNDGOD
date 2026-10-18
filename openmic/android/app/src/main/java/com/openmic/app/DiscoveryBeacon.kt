package com.openmic.app

import android.util.Log
import org.json.JSONObject
import java.net.DatagramPacket
import java.net.DatagramSocket
import java.net.InetAddress
import java.util.concurrent.atomic.AtomicBoolean

/**
 * Broadcasts a small UDP discovery beacon on the LAN so the desktop can find
 * this phone without the user typing an IP. Byte-compatible with
 * desktop/openmic_discovery.py.
 *
 * Beacon JSON: {magic, name, port, proto, auth}. Never contains the PIN.
 */
class DiscoveryBeacon(
    private val deviceName: String,
    private val port: Int,
    private val requiresPin: Boolean,
) {
    private val running = AtomicBoolean(false)
    private var thread: Thread? = null

    fun start() {
        if (running.getAndSet(true)) return
        thread = Thread({ loop() }, "openmic-beacon").also { it.start() }
    }

    fun stop() {
        running.set(false)
        thread?.interrupt()
    }

    private fun loop() {
        val payload = JSONObject().apply {
            put("magic", OpenMicProtocol.DISCOVERY_MAGIC)
            put("name", deviceName)
            put("port", port)
            put("proto", OpenMicProtocol.PROTO_VERSION)
            put("auth", requiresPin)
        }.toString().toByteArray(Charsets.UTF_8)

        try {
            DatagramSocket().use { sock ->
                sock.broadcast = true
                val addr = InetAddress.getByName("255.255.255.255")
                val packet = DatagramPacket(payload, payload.size, addr,
                    OpenMicProtocol.DISCOVERY_PORT)
                while (running.get()) {
                    try {
                        sock.send(packet)
                        Thread.sleep(1000)
                    } catch (_: InterruptedException) {
                        break
                    }
                }
            }
        } catch (e: Exception) {
            Log.w("OpenMicBeacon", "beacon stopped: ${e.message}")
        }
    }
}
