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
│ (Android)   │   PCM16 audio frames, our proto   │ (Windows / Python)│
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
| `windows/` | **The real desktop app** — native C#/.NET, installs with Setup.exe, auto-updates, voice changer | source complete, build on Windows |
| `desktop/*.py` | Reference/testing implementation (cross-platform, and the phone simulator) | **working** |
| `desktop/openmic_proto.py` | Shared framing | done |
| `desktop/openmic_discovery.py` | LAN discovery (UDP beacon, no deps) | **working** |
| `desktop/openmic_client.py` | Shared receive core (connect/decode/sink, PIN) | **working** |
| `desktop/openmic_fx.py` | Voice-changer effects (numpy): pitch, robot, echo… | **working** |
| `desktop/openmic_gui.py` | Desktop app window (scan, pick output, voice changer, Start/Stop) | **working** |
| `desktop/openmic_server.py` | Command-line receiver (`--discover`, `--pin`, sinks) | **working** |
| `desktop/phone_sim.py` | Phone simulator for testing without a phone | **working** |
| `android/` | The real phone app (Kotlin, PCM16, Wi-Fi/USB, PIN, discovery beacon) | source complete, build locally |

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
2. Open OpenMic on the phone (same Wi-Fi as the PC), optionally type a **PIN**,
   tap **Start**.
3. On the PC, install and open the **Windows app** (`windows/`): run its
   `Setup.exe` once (see `windows/README.md` to build it), then open OpenMic,
   press **Scan Wi-Fi**, pick your phone, choose where the audio goes, pick a
   voice changer if you want, and press **Start**. It updates itself when you
   publish new versions — no reinstall.

   There's also a cross-platform **Python** version (same features) if you're on
   macOS/Linux or just testing: `pip install sounddevice numpy` then
   `python3 desktop/openmic_gui.py` (Linux GUI needs `sudo apt install
   python3-tk`). Prefer a command line? Same thing without a window:
   ```bash
   python3 desktop/openmic_server.py --discover --sink speaker
   # if you set a PIN on the phone, add:  --pin 1234
   # a voice changer:               add:  --effect "Chipmunk"
   # no broadcast on your network? type the IP: openmic_server.py <phone-ip>
   ```

### Voice changer
Pick a preset in the GUI's **Voice changer** box (change it live while
streaming), or pass `--effect "<name>"` on the command line. Presets:
`Chipmunk`, `Deep voice`, `Robot`, `Alien`, `Echo`, `Megaphone`, `Monster`.
The effects run on the PC (pure **numpy**, BSD-licensed — no GPL baggage), so
whatever you pick is what other apps hear through the virtual mic. Needs
`pip install numpy` (already required for live playback). Add your own in
`desktop/openmic_fx.py` — each effect is a tiny numpy class, and presets are one
line in `build_effect()`.

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
Done:
- [x] **PIN/token auth** so only your PC can connect.
- [x] **Auto-discovery** (UDP beacon) — no typing IPs.
- [x] **Desktop apps**: native Windows app (`windows/`, auto-updating) + a
      cross-platform Python GUI (`desktop/openmic_gui.py`).
- [x] **Voice changer** — 24 presets, changeable live, with a search box.
- [x] **Mute** button (phone).
- [x] **Mic volume** slider (phone) + **output volume** slider (desktop).
- [x] **Noise suppression** (both) — frequency-domain **spectral denoiser**
      (keeps the voice, removes steady noise; tested: 84–97% voice kept while
      cutting noise 94–99%). Windows also offers an optional **RNNoise** engine
      for near-Krisp quality (drop in `rnnoise.dll`).
- [x] **Voice changer** — 30 distinct presets (no more copy-paste duplicates)
      with a search box.
- [x] **Redesigned UI** — Windows app rebuilt with a header + scrolling card
      layout (no overlapping controls) in a clean light scheme; Android app
      redesigned with cards + header and automatic light/dark.
- [x] **Mic-release on calls** — the phone yields the mic to a call/other app
      and resumes after (toggle in the app).
- [x] **USB direct mode** — `adb forward`, lower latency, no Wi-Fi jitter.
- [x] **Switch output device live** — no restart.
- [x] **Lower latency** — tighter buffers both ends (was ~1.5s; the desktop's
      2-second audio buffer was the cause).

Next:
- [ ] **Opus codec** (`codec=1`): far less *data* over the network and lower
      latency (the voice codec Discord/Zoom use — nothing to do with the AI
      model of the same name). Encode on the phone via libopus/NDK; the desktop
      receiver already has the decode hook.
- [ ] **DeepFilterNet** engine (top-tier denoise) via ONNX Runtime, as a third
      NS engine beside Spectral and RNNoise.
- [ ] **Custom voice** preset with live pitch/formant sliders — unlimited
      variations (there is no big free "voice pack" to pull from; voice changers
      are just parameterised DSP, so this is how you get endless options).
- [ ] **Auto-reconnect** for the desktop app.

### A note on noise suppression quality
Three tiers, best-effort free:
1. **Spectral** (built in, default) — a decision-directed Wiener filter with
   minimum-statistics noise tracking. Removes **steady** noise (fans, hiss, hum)
   and keeps your voice even at max strength (high strength no longer blocks the
   voice). **It cannot remove keyboard clicks while you talk** — a transient that
   overlaps speech isn't in the steady noise profile, so classical single-channel
   DSP can't separate it. That's an inherent limit, not a tuning bug.
2. **RNNoise** (optional, Windows) — an ML denoiser that *does* handle
   keyboard/clicks and other non-stationary noise; roughly Discord-grade. This
   is the one to use for gaming + mechanical keyboard. Needs `rnnoise.dll`.
3. **DeepFilterNet** (roadmap) — the closest free thing to Krisp, heavier.
- [ ] **UDP audio** for lower latency (control stays on TCP).
- [ ] **Bluetooth** transport (RFCOMM).
- [ ] **Level meter / mute / gain** in the phone UI (gain hook already in `MicServer`).
- [ ] **Stereo + 48 kHz** mode for music.
- [ ] Your own **virtual-mic driver** (hardest; or bundle a virtual cable installer).

## Legal / ethical note
Built for interoperability and learning, using apps you own. Don't copy WO Mic's
code, name, icon or binaries into OpenMic — give it your own identity before you
distribute it. Ship your own work.
