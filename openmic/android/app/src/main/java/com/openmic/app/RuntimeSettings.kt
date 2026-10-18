package com.openmic.app

/**
 * Live controls shared between the UI (MainActivity), the service (MicService)
 * and the capture loop (MicServer). All fields are read every audio frame, so
 * changes take effect immediately with no restart.
 */
object RuntimeSettings {
    @Volatile var muted: Boolean = false
    @Volatile var micGain: Float = 1.0f        // 0..3 multiplier on the mic
    @Volatile var noiseStrength: Float = 0.0f  // 0..1 software noise suppression
    @Volatile var releaseOnCall: Boolean = true  // yield the mic to calls/other apps

    /** Set by MicService's audio-focus listener: true = another app/call has the mic. */
    @Volatile var paused: Boolean = false
}
