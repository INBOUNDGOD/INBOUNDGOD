package com.openmic.app

import android.app.Notification
import android.app.NotificationChannel
import android.app.NotificationManager
import android.app.PendingIntent
import android.app.Service
import android.content.Context
import android.content.Intent
import android.net.wifi.WifiManager
import android.os.Build
import android.os.IBinder
import android.os.PowerManager

/**
 * Foreground service that owns the [MicServer] and the wake/wifi locks so
 * streaming survives the screen turning off.
 */
class MicService : Service() {

    private var server: MicServer? = null
    private var beacon: DiscoveryBeacon? = null
    private var wakeLock: PowerManager.WakeLock? = null
    private var wifiLock: WifiManager.WifiLock? = null

    override fun onBind(intent: Intent?): IBinder? = null

    override fun onStartCommand(intent: Intent?, flags: Int, startId: Int): Int {
        if (intent?.action == ACTION_STOP) { stopSelf(); return START_NOT_STICKY }
        startForeground(1, buildNotification("OpenMic is streaming"))
        acquireLocks()
        if (server == null) {
            val name = Build.MODEL ?: "Android"
            val pin = intent?.getStringExtra(EXTRA_PIN)?.takeIf { it.isNotBlank() }
            server = MicServer(deviceName = name, pin = pin).also { it.start() }
            beacon = DiscoveryBeacon(name, OpenMicProtocol.DEFAULT_PORT, pin != null)
                .also { it.start() }
        }
        return START_STICKY
    }

    override fun onDestroy() {
        beacon?.stop(); beacon = null
        server?.stop(); server = null
        releaseLocks()
        super.onDestroy()
    }

    private fun acquireLocks() {
        val pm = getSystemService(Context.POWER_SERVICE) as PowerManager
        wakeLock = pm.newWakeLock(PowerManager.PARTIAL_WAKE_LOCK, "OpenMic:cpu")
            .also { it.acquire() }
        val wm = applicationContext.getSystemService(Context.WIFI_SERVICE) as WifiManager
        val mode = if (Build.VERSION.SDK_INT >= 34)
            WifiManager.WIFI_MODE_FULL_LOW_LATENCY else WifiManager.WIFI_MODE_FULL_HIGH_PERF
        wifiLock = wm.createWifiLock(mode, "OpenMic:wifi").also { it.acquire() }
    }

    private fun releaseLocks() {
        wakeLock?.let { if (it.isHeld) it.release() }; wakeLock = null
        wifiLock?.let { if (it.isHeld) it.release() }; wifiLock = null
    }

    private fun buildNotification(text: String): Notification {
        val mgr = getSystemService(NotificationManager::class.java)
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
            mgr.createNotificationChannel(NotificationChannel(
                CHANNEL, "OpenMic", NotificationManager.IMPORTANCE_LOW))
        }
        val stopIntent = PendingIntent.getService(
            this, 0, Intent(this, MicService::class.java).setAction(ACTION_STOP),
            PendingIntent.FLAG_IMMUTABLE or PendingIntent.FLAG_UPDATE_CURRENT)
        return Notification.Builder(this, CHANNEL)
            .setContentTitle(getString(R.string.app_name))
            .setContentText(text)
            .setSmallIcon(R.drawable.ic_mic)
            .addAction(Notification.Action.Builder(null, "Stop", stopIntent).build())
            .setOngoing(true)
            .build()
    }

    companion object {
        private const val CHANNEL = "openmic"
        const val ACTION_STOP = "com.openmic.app.STOP"
        const val EXTRA_PIN = "pin"
        fun start(ctx: Context, pin: String? = null) =
            ctx.startForegroundService(
                Intent(ctx, MicService::class.java).putExtra(EXTRA_PIN, pin))
        fun stop(ctx: Context) =
            ctx.startService(Intent(ctx, MicService::class.java).setAction(ACTION_STOP))
    }
}
