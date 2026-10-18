# OpenMic — Windows app (C# / .NET, native, auto-updating)

A real Windows desktop app (no Python). It finds your phone on the Wi-Fi,
receives the audio, runs the voice changer, and plays it to your speakers or a
virtual mic device. It installs with a `Setup.exe` and **updates itself** from
GitHub Releases — ship a new version and installed copies upgrade on their own.

- UI + audio: **WinForms** + **NAudio** (WASAPI output)
- Networking/DSP: our own protocol + effects, ported from the tested reference
- Auto-update: **Velopack**

> Built in this cloud session but **not compiled here** (.NET is Windows-only
> tooling). Your build machine compiles it; if anything doesn't build first try,
> send me the error and I'll fix it.

## Prerequisites (on the build machine)
- **.NET 8 SDK** — https://dotnet.microsoft.com/download
- The Velopack CLI (for packaging + updates):
  `dotnet tool install -g vpk` (or `dotnet tool update -g vpk`). **Keep `vpk`
  and the `Velopack` NuGet on the same 1.2.x line** — the csproj pins
  `1.2.161`; if your `vpk` is newer, bump the csproj to match, or packing warns
  and may break auto-updates.

## Build & run (for testing)
```powershell
cd openmic/windows
dotnet restore
dotnet run --project OpenMic/OpenMic.csproj
```
(Running this way, auto-update stays off — it only runs for an installed copy.)

## Make the installable app
1. Publish a self-contained build (so users don't need .NET installed):
   ```powershell
   dotnet publish OpenMic/OpenMic.csproj -c Release -r win-x64 --self-contained true -o publish
   ```
2. Package it into an installer + update feed with Velopack:
   ```powershell
   vpk pack --packId OpenMic --packVersion 1.0.0 --packDir publish --mainExe OpenMic.exe --runtime win-x64
   ```
   This creates a `Releases/` folder containing **`OpenMic-win-Setup.exe`** (the
   installer you hand out) plus the update packages.
3. Double-click `OpenMic-win-Setup.exe` to install. It installs per-user (no
   admin), adds a Start-menu shortcut, and runs like any app.

## Turn on auto-update (publish to GitHub Releases)
The app checks this repo's Releases for newer versions
(`UpdateService.cs` → `RepoUrl`). Publish each build there:

```powershell
vpk upload github --repoUrl https://github.com/INBOUNDGOD/INBOUNDGOD ^
    --publish --releaseName "OpenMic 1.0.0" --tag v1.0.0 --token <github-token>
```
(The token needs `repo` scope. You can also host the `Releases/` files on any
static server or your NAS and switch `UpdateService` to a `SimpleWebSource`.)

### Shipping an update later — this is the whole point
1. Bump `<Version>` in `OpenMic/OpenMic.csproj`, e.g. `1.0.1`.
2. `dotnet publish ... -o publish` again.
3. `vpk pack --packId OpenMic --packVersion 1.0.1 --packDir publish --mainExe OpenMic.exe --runtime win-x64`
4. `vpk upload github --repoUrl ... --publish --releaseName "OpenMic 1.0.1" --tag v1.0.1 --token ...`

Installed copies notice 1.0.1 on next launch, download just the delta, and show
**"Update to 1.0.1 ready — click to restart."** No reinstall. Keep `--packId`
exactly `OpenMic` every time, or updates won't chain.

## Files
```
windows/
├─ OpenMic.sln
└─ OpenMic/
   ├─ OpenMic.csproj     net8.0-windows, NAudio + Velopack
   ├─ Program.cs         entry point (Velopack hook first, then the window)
   ├─ Protocol.cs        wire framing (matches phone + Python)
   ├─ Discovery.cs       UDP beacon listener (auto-find the phone)
   ├─ Effects.cs         voice changer (pitch/robot/echo/… presets)
   ├─ AudioOutput.cs     NAudio WASAPI playback + device picker
   ├─ Receiver.cs        connect, handshake, PIN, receive loop
   ├─ UpdateService.cs   Velopack auto-update
   └─ MainForm.cs        the window
```

## Controls
- **USB cable (adb)** checkbox — tunnels over USB (`adb forward`) for lower
  latency; needs Android platform-tools (`adb`) on PATH and USB debugging on.
- **Volume** and **Noise cut** sliders, a **voice-changer search box** + 24
  presets, all live. The **output device switches live** — no stop/start.
- Latency is bounded to ~200 ms on the output side; Wi-Fi adds a bit more, USB
  is lowest.
- **System tray:** closing the window hides OpenMic to the tray and keeps
  streaming. Double-click the tray icon to reopen, or right-click → **Quit** to
  actually exit.

## Noise suppression engines
- **Spectral (built-in)** — default, no setup. Frequency-domain denoiser that
  keeps the voice and removes steady noise. The "Noise cut" slider sets strength.
- **RNNoise (best)** — near-Krisp ML denoiser. To enable it: drop a 64-bit
  **`rnnoise.dll`** next to `OpenMic.exe`, then choose "RNNoise" in the engine
  box. Prebuilt DLL: github.com/jagger2048/rnnoise-windows (or build
  github.com/xiph/rnnoise). If the DLL is missing, OpenMic silently falls back
  to the spectral engine. (The RNNoise path is hand-written and not yet tested
  on a real build — report issues.)

## Notes
- **Virtual mic:** install VB-CABLE (free) and pick **"CABLE Input"** in the
  device box; other apps then record from **"CABLE Output."**
- **Dark UI** with grouped cards. The voice-changer box has a search field and
  30 distinct presets.
- **Codec:** this app plays PCM (what the phone sends today). When the phone
  gains Opus, add a decoder (e.g. the `Concentus` NuGet) in `Receiver.cs`.
- **Icon/signing:** add `--icon` to `vpk pack` for a custom icon; code-signing
  is optional but removes SmartScreen warnings (`vpk pack --signParams ...`).
