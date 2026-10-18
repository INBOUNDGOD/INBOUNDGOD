# OpenMic — Android app

The phone side: captures the mic and streams it to the desktop over Wi-Fi/USB
using the OpenMic protocol (`../PROTOCOL.md`). v1 is 16-bit PCM, mono, 16 kHz —
no native code, so it builds with just the Android SDK.

## Build
This project **cannot be built in the cloud session** (Google's Maven repo is
blocked here). Build it on your own machine:

1. Install **Android Studio** (it bundles the SDK and JDK).
2. `File > Open` this `android/` folder. Android Studio will generate the Gradle
   wrapper (`gradlew`, `gradle-wrapper.jar`) and sync dependencies.
   - CLI alternative, if you have Gradle 8.9+ installed:
     `gradle wrapper` then `./gradlew assembleDebug`.
3. Plug in your phone (USB debugging on) and press **Run**, or install the APK:
   `./gradlew installDebug`.

The debug APK lands in `app/build/outputs/apk/debug/app-debug.apk`.

## Project layout
```
android/
├─ settings.gradle.kts        project + repositories
├─ build.gradle.kts           plugin versions (AGP 8.5.2, Kotlin 1.9.24)
├─ app/
│  ├─ build.gradle.kts        module config (minSdk 26, targetSdk 34)
│  └─ src/main/
│     ├─ AndroidManifest.xml  permissions + foreground service
│     ├─ java/com/openmic/app/
│     │  ├─ OpenMicProtocol.kt  wire framing (matches the Python side)
│     │  ├─ MicServer.kt        ServerSocket + AudioRecord capture + stream + PIN
│     │  ├─ DiscoveryBeacon.kt  UDP broadcast so the PC auto-finds this phone
│     │  ├─ MicService.kt       foreground service, wake/wifi locks, beacon
│     │  └─ MainActivity.kt     shows IP:port, optional PIN, Start/Stop
│     └─ res/                   layout, strings, theme, mic icon
```

## How it works
- `MicServer` opens a `ServerSocket` on port 8126 and waits for the desktop.
- On connect it sends **HELLO** (sample rate, codec, etc.), waits for **START**,
  then records from `AudioRecord` (with echo-canceller + noise-suppressor when
  available) and sends **AUDIO** frames.
- `MicService` keeps it alive with a foreground notification + wake/wifi locks,
  so streaming continues when the screen is off.
- `MainActivity` shows the phone's LAN IP to type on the PC.

## Controls (on the phone)
- **Mute** — sends silence without dropping the connection.
- **Mic volume** slider (0–300%) and **Noise suppression** slider (0–100%,
  an adjustable gate on top of the hardware noise suppressor).
- **Release mic to calls / other apps** — when a call or another app takes the
  mic (audio focus), OpenMic releases it and streams silence, then resumes
  automatically. (Real-device behaviour varies by OEM; this uses audio focus.)
- All of these change live while streaming (`RuntimeSettings`), read every frame
  by `MicServer`.
- **Noise suppression** uses the same frequency-domain spectral denoiser as the
  desktop (`SpectralDenoiseK.kt`) — keeps the voice, removes steady noise. Use
  it on the phone *or* the desktop, not both (avoids double-processing).
- **Dark mode** follows the system setting (`values-night/themes.xml`).

## Notes / next steps
- **PIN auth** and **LAN discovery** are built in: type a PIN (optional) before
  Start; the app broadcasts a beacon so the PC's `--discover` finds it.
- **USB:** no app change needed — on the PC use USB mode (it runs
  `adb forward` and connects to the phone over the cable). Enable USB debugging
  on the phone.
- **Opus**: add libopus via the NDK, implement an `OpusEncoder` that fills the
  same AUDIO payload with `codec=1`. The desktop receiver already decodes Opus
  if `opuslib` is installed.
- **minSdk 26** (Android 8.0) covers foreground-service + notification-channel
  APIs cleanly. Lower it only if you add the older-API fallbacks.
