# OpenMic

Turn your phone into a wireless microphone for your PC — your **own** app, built
from scratch. This is an independent project inspired by how apps like WO Mic
work; it reuses none of their code, assets or branding. Change it, rename it,
add features and ship it as your own.

It grew out of a reverse-engineering study of WO Mic (see `FINDINGS.md`): the
proven design is *phone captures + encodes + serves; desktop connects, decodes,
and feeds a virtual microphone*. OpenMic keeps that shape with its own clean
protocol (`PROTOCOL.md`).

```
┌─────────────┐   Wi-Fi / USB (TCP, port 8126)   ┌──────────────────┐
│  Phone app  │ ───────────────────────────────▶ │ Desktop receiver │
│ (Android)   │   PCM16 audio frames, our proto   │ (Python)         │
│  = server   │                                   │  = client        │
└─────────────┘                                   └──────┬───────────┘
   captures mic                                          │ plays to a
   streams audio                                         ▼ speaker / virtual mic
                                              other apps (Discord, OBS, browser)
```

## What's here
| Path | What it is | Status |
|---|---|---|
| `FINDINGS.md` | Reverse-engineering analysis of WO Mic (Android + desktop) | done |
| `PROTOCOL.md` | OpenMic's own wire protocol spec | done, v1 |
| `desktop/openmic_proto.py` | Shared framing | done |
| `desktop/openmic_server.py` | Desktop receiver (speaker / wav / virtual-mic sinks) | **working** |
| `desktop/phone_sim.py` | Phone simulator for testing without a phone | **working** |
| `android/` | The real phone app (Kotlin, PCM16, Wi-Fi/USB) | source complete, build locally |

The desktop receiver + simulator are **verified working** (tone streamed and
decoded end-to-end). The Android app is complete source; build it in Android
Studio (it can't be built in this cloud container — Google's Maven is blocked
here).

## Try it right now (no phone needed)
```bash
cd desktop
# terminal 1 — pretend to be the phone, stream a 440 Hz tone:
python3 phone_sim.py --seconds 5
# terminal 2 — receive it and save to a wav:
python3 openmic_server.py 127.0.0.1 --sink wav --out test.wav
# play test.wav — you'll hear the tone. That proves the whole pipeline.
```

## Use it for real
1. **Build the phone app** (`android/`) in Android Studio and install it.
2. Open OpenMic on the phone (same Wi-Fi as the PC), tap **Start**. It shows
   the phone's IP and port.
3. On the PC:
   ```bash
   pip install sounddevice numpy          # for live playback
   python3 desktop/openmic_server.py <phone-ip> --sink speaker
   ```
   You should hear your voice. Use `--sink wav` first if you have no audio out.

### Making it a real microphone other apps can use
`--sink speaker` just plays the audio. To make OpenMic show up as a
**microphone** that Discord/OBS/your browser can select, send the audio into a
virtual audio device:

- **Windows:** install **VB-CABLE** (free, VB-Audio) — it creates "CABLE Input"
  (playback) and "CABLE Output" (recording). Run
  `python3 openmic_server.py <phone-ip> --sink virtual --device "CABLE Input"`.
  Then pick **CABLE Output** as the microphone in any app.
- **macOS:** install **BlackHole** (free) and target it the same way.
- **Linux (PipeWire/PulseAudio):**
  ```bash
  pactl load-module module-null-sink sink_name=OpenMic
  python3 openmic_server.py <phone-ip> --sink virtual --device "OpenMic"
  ```
  Apps then record from the "Monitor of OpenMic" source.

This virtual-cable approach is how you get a working mic **without writing a
kernel driver**. Writing your own signed Windows audio driver (like WO Mic's)
is possible but a large, separate project — see the roadmap.

### USB instead of Wi-Fi
No code change needed — tunnel the port over adb:
```bash
adb forward tcp:8126 tcp:8126
python3 desktop/openmic_server.py 127.0.0.1 --sink speaker
```

## About `VC_redist.x86.exe` (your question)
`VC_redist.x86.exe` is **Microsoft's Visual C++ Redistributable** — a free,
Microsoft-signed runtime that C/C++ Windows programs built with Visual Studio
need. It is **not part of WO Mic**; WO Mic's installer just bundles it because
its client is a native C++ program. You never "installed it" separately because
installers run it silently.

For OpenMic:
- The current desktop receiver is **Python**, so it needs **no** VC++ redist —
  just Python.
- If you later rewrite the desktop side in C++ (e.g. to build a native tray app
  or your own driver), you'd then ship VC++ redist with it. Microsoft lets you
  redistribute it freely; download it fresh from Microsoft's site rather than
  reusing WO Mic's copy. The `x86` one is for 32-bit builds; most new apps want
  the `x64` one.

## Roadmap — "its own features and more"
Easy wins first:
- [ ] **PIN/token auth** so only your PC can connect (add a field to HELLO/START).
- [ ] **Opus codec** (`codec=1`): far less bandwidth, lower latency. Encode on
      the phone via libopus/NDK; the receiver already has the decode hook.
- [ ] **Desktop tray app / GUI** with device picker and auto-reconnect.
- [ ] **Auto-discovery** (mDNS/zeroconf) so the PC finds the phone — no typing IPs.
- [ ] **UDP audio** for lower latency (control stays on TCP).
- [ ] **Bluetooth** transport (RFCOMM).
- [ ] **Level meter / mute / gain** in the phone UI (gain hook already in `MicServer`).
- [ ] **Stereo + 48 kHz** mode for music.
- [ ] Your own **virtual-mic driver** (hardest; or bundle a virtual cable installer).

## Legal / ethical note
Built for interoperability and learning, using apps you own. Don't copy WO Mic's
code, name, icon or binaries into OpenMic — give it your own identity before you
distribute it. Ship your own work.
