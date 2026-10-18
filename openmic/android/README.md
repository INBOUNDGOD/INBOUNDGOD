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
│     │  ├─ MicServer.kt        ServerSocket + AudioRecord capture + stream
│     │  ├─ MicService.kt       foreground service, wake/wifi locks
│     │  └─ MainActivity.kt     shows IP:port, Start/Stop, permissions
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

## Notes / next steps
- **Opus**: add libopus via the NDK, implement an `OpusEncoder` that fills the
  same AUDIO payload with `codec=1`. The desktop receiver already decodes Opus
  if `opuslib` is installed.
- **Auth**: add a token to HELLO and verify it in START before streaming.
- **minSdk 26** (Android 8.0) covers foreground-service + notification-channel
  APIs cleanly. Lower it only if you add the older-API fallbacks.
