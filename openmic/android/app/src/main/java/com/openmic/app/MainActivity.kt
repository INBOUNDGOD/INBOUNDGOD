package com.openmic.app

import android.Manifest
import android.content.Context
import android.content.pm.PackageManager
import android.net.wifi.WifiManager
import android.os.Build
import android.os.Bundle
import android.widget.Button
import android.widget.CheckBox
import android.widget.EditText
import android.widget.SeekBar
import android.widget.TextView
import androidx.activity.ComponentActivity
import androidx.activity.result.contract.ActivityResultContracts
import androidx.core.content.ContextCompat
import java.net.Inet4Address
import java.net.NetworkInterface

/**
 * Minimal UI: shows the phone's LAN IP + port to type on the PC, and a
 * Start/Stop button. The heavy lifting lives in [MicService] / [MicServer].
 */
class MainActivity : ComponentActivity() {

    private lateinit var status: TextView
    private lateinit var addr: TextView
    private lateinit var toggle: Button
    private lateinit var pin: EditText
    private lateinit var mute: Button
    private lateinit var micGain: SeekBar
    private lateinit var micLabel: TextView
    private lateinit var noise: SeekBar
    private lateinit var nsLabel: TextView
    private lateinit var releaseOnCall: CheckBox
    private var streaming = false

    private val perms = registerForActivityResult(
        ActivityResultContracts.RequestMultiplePermissions()) { result ->
        if (result.values.all { it }) startStreaming()
        else status.text = "Microphone permission is required."
    }

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        setContentView(R.layout.activity_main)
        status = findViewById(R.id.status)
        addr = findViewById(R.id.addr)
        toggle = findViewById(R.id.toggle)
        pin = findViewById(R.id.pin)
        mute = findViewById(R.id.mute)
        micGain = findViewById(R.id.micGain)
        micLabel = findViewById(R.id.micLabel)
        noise = findViewById(R.id.noise)
        nsLabel = findViewById(R.id.nsLabel)
        releaseOnCall = findViewById(R.id.releaseOnCall)
        addr.text = "${localIpv4() ?: "no Wi-Fi"} : ${OpenMicProtocol.DEFAULT_PORT}"
        toggle.setOnClickListener { if (streaming) stopStreaming() else requestAndStart() }

        mute.setOnClickListener {
            RuntimeSettings.muted = !RuntimeSettings.muted
            mute.text = if (RuntimeSettings.muted) "Unmute" else "Mute"
        }
        micGain.setOnSeekBarChangeListener(object : SeekBar.OnSeekBarChangeListener {
            override fun onProgressChanged(sb: SeekBar, p: Int, fromUser: Boolean) {
                RuntimeSettings.micGain = p / 100f
                micLabel.text = "Mic volume: $p%"
            }
            override fun onStartTrackingTouch(sb: SeekBar) {}
            override fun onStopTrackingTouch(sb: SeekBar) {}
        })
        noise.setOnSeekBarChangeListener(object : SeekBar.OnSeekBarChangeListener {
            override fun onProgressChanged(sb: SeekBar, p: Int, fromUser: Boolean) {
                RuntimeSettings.noiseStrength = p / 100f
                nsLabel.text = if (p == 0) "Noise suppression: off" else "Noise suppression: $p%"
            }
            override fun onStartTrackingTouch(sb: SeekBar) {}
            override fun onStopTrackingTouch(sb: SeekBar) {}
        })
        releaseOnCall.setOnCheckedChangeListener { _, checked ->
            RuntimeSettings.releaseOnCall = checked
        }
        // apply initial control values to RuntimeSettings
        RuntimeSettings.micGain = micGain.progress / 100f
        RuntimeSettings.noiseStrength = noise.progress / 100f
        RuntimeSettings.releaseOnCall = releaseOnCall.isChecked
    }

    private fun requestAndStart() {
        val needed = buildList {
            add(Manifest.permission.RECORD_AUDIO)
            if (Build.VERSION.SDK_INT >= 33) add(Manifest.permission.POST_NOTIFICATIONS)
        }
        if (needed.all {
                ContextCompat.checkSelfPermission(this, it) == PackageManager.PERMISSION_GRANTED
            }) startStreaming() else perms.launch(needed.toTypedArray())
    }

    private fun startStreaming() {
        val code = pin.text?.toString()?.trim()?.takeIf { it.isNotEmpty() }
        MicService.start(this, code)
        streaming = true
        pin.isEnabled = false
        val pinArg = if (code != null) " --pin $code" else ""
        status.text = "Listening. On your PC run:\n" +
                "python3 openmic_server.py --discover$pinArg\n" +
                "(or use this phone's IP instead of --discover)"
        toggle.text = "Stop"
    }

    private fun stopStreaming() {
        MicService.stop(this)
        streaming = false
        pin.isEnabled = true
        status.text = "Stopped."
        toggle.text = "Start"
    }

    /** First non-loopback IPv4 address (the Wi-Fi address in practice). */
    private fun localIpv4(): String? {
        // Prefer the Wi-Fi interface address.
        try {
            for (nif in NetworkInterface.getNetworkInterfaces()) {
                if (!nif.isUp || nif.isLoopback) continue
                for (a in nif.inetAddresses) {
                    if (a is Inet4Address && !a.isLoopbackAddress) return a.hostAddress
                }
            }
        } catch (_: Exception) {}
        // Fallback to the deprecated WifiManager value.
        @Suppress("DEPRECATION")
        val wm = applicationContext.getSystemService(Context.WIFI_SERVICE) as WifiManager
        @Suppress("DEPRECATION")
        val ip = wm.connectionInfo.ipAddress
        return if (ip != 0) "%d.%d.%d.%d".format(
            ip and 0xff, ip shr 8 and 0xff, ip shr 16 and 0xff, ip shr 24 and 0xff) else null
    }
}
