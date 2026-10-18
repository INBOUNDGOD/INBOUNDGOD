# Reverse-engineering findings: WO Mic (v5.0 Android + v6.3 desktop)

This is an analysis of how the WO Mic apps work, done so we can build our **own
original** equivalent (`OpenMic`). It documents *architecture and behaviour*,
not WO Mic source code. OpenMic's code is written from scratch; none of WO
Mic's (obfuscated) code is reused.

> Scope / ethics: reverse engineering for interoperability, learning, and
> building an independent, competing implementation. Don't copy WO Mic's code,
> assets, name or branding into OpenMic, and don't redistribute WO Mic's
> binaries. Build and ship your own.

---

## Files examined
| File | What it is |
|---|---|
| `WOMic_v5_0.apk` | Android client, package `com.wo.voice2`, R8/ProGuard-obfuscated |
| `WOMicClientSetup6.3.exe` | Windows desktop client installer (Nullsoft NSIS v3, requires admin) |
| `VC_redist.x86.exe` | **Microsoft** Visual C++ Redistributable — a runtime dependency, *not* part of WO Mic. See README "About VC_redist". |

Tooling: `jadx` (APK → Java), `unzip`, `strings`. The desktop client payload is
LZMA-compressed inside the NSIS installer and was not extracted; its
architecture is inferred from the admin requirement and the well-known
virtual-audio-driver model.

---

## Android app architecture

```
AudioRecord (mic, 16-bit PCM, mono)
   -> optional AcousticEchoCanceler + NoiseSuppressor
   -> resample (native) to the negotiated rate
   -> ArrayBlockingQueue of frames  (capture thread, priority 10)
   -> encoder: Opus (libcodec-o.so) or Speex (libcodec-s.so)
   -> packetiser
   -> transport socket  ---> desktop
   (all driven by a foreground Service holding a wake lock + wifi lock)
```

Concrete details recovered from the decompiled classes:

- **Capture** (`Q2.b`): `AudioRecord` at 16-bit, mono. If the requested rate
  fails it falls back to 44100 Hz. Turns on `AcousticEchoCanceler` and
  `NoiseSuppressor` when the device supports them. A dedicated thread
  (priority 10 = `MAX`) reads fixed-size frames into a 5-slot
  `ArrayBlockingQueue` (back-pressure: drops when full).
- **Frame** (`Q2.a`): carries a sequence index, an elapsed-milliseconds
  timestamp, and the PCM samples. Also has a software **volume/gain** step
  (bit-shift up/down, with clamping) — that is the app's volume slider.
- **Opus encoder** (`com.wo.voice2.audio.OpusEncoder`, native `libcodec-o.so`):
  accepts 8000/12000/16000/24000/48000 Hz, mono, 20 or 40 ms frames.
- **Speex encoder** (via `libcodec-s.so`) is the alternate codec.
- **Settings** (`O2.a`, `SharedPreferences "settings"`): `transport` (default
  `2`), `controlPort` (default **8125**), `audioSource` (default `0` = mic),
  `volume` (default `4`), `autoMute`, `screenOn`, plus purchase/theme state.
- **Service / state machine** (`O2.f`, `VoiceService`): states idle → listening
  → connected. On "connected" it grabs a `PARTIAL_WAKE_LOCK` (`:WOMic`) and, for
  the Wi-Fi transport, a `WifiLock` (`:WOWiFi`). A foreground notification with
  a STOP action keeps the service alive.
- **The phone is the server.** It opens a listening socket and waits; the
  **desktop connects to the phone's IP**.

### Transports (package `Y2`)
| Transport | Setting id | Mechanism (classes) |
|---|---|---|
| Wi-Fi (default) | 2 | TCP control on **port 8125** (`ServerSocket`, `Y2.j`/`Y2.g`) + **UDP** audio (`DatagramSocket`, `Y2.f`/`Y2.h`) |
| Wi-Fi Direct | — | same as Wi-Fi over the p2p interface |
| USB | — | TCP over `adb forward` (the PC tunnels a local port to the phone; needs USB debugging) |
| Bluetooth | — | RFCOMM `BluetoothServerSocket` (`Y2.c`) |

So the audio path is: TCP carries control/handshake, UDP carries the
low-latency audio stream (for Wi-Fi); USB and Bluetooth carry both over their
reliable stream sockets.

---

## Desktop client architecture (inferred)

- NSIS installer **requires administrator** — because it installs a
  **kernel-mode virtual audio device driver**. After install, Windows shows a
  new recording device ("WO Mic Device"); any app (Discord, OBS, browser) can
  pick it as its microphone.
- A **user-mode client app** connects to the phone (Wi-Fi/USB/Bluetooth),
  receives + decodes the audio, and writes the PCM into the virtual device so
  the OS presents it as live mic input.
- Built with MSVC, hence the **VC++ Redistributable** dependency.

This driver + client split is the one genuinely hard part to reproduce
(signed Windows audio drivers are a big undertaking). OpenMic's README explains
the pragmatic alternatives (a ready-made virtual audio cable now; your own
driver later).

---

## What this means for OpenMic (our build)
1. The proven architecture is: **phone captures + encodes + serves; desktop
   connects, decodes, and injects into a virtual mic.** We keep that shape.
2. We don't need WO Mic's exact byte format. OpenMic defines its **own** clean
   protocol (`PROTOCOL.md`): one TCP connection, PCM16 now, Opus later.
3. v1 ships PCM over Wi-Fi/USB (no native code, works immediately — already
   verified end-to-end with the Python receiver + phone simulator). Opus,
   Bluetooth and a native virtual-mic driver are the roadmap.
