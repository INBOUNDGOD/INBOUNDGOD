# OpenMic protocol (v1)

An original, clean protocol for streaming phone-microphone audio to a desktop
over a LAN. Informed by studying the phone-as-mic pattern, but not a copy of any
third-party wire format — it's yours to change.

## Roles
- **Phone = server.** It opens a listening TCP socket and shows its IP + port.
- **Desktop = client.** The user types the phone's IP on the PC; the desktop
  connects.

(Over **USB** it's the same TCP protocol, but on `127.0.0.1` after
`adb forward tcp:8126 tcp:8126`, so no IP typing is needed.)

## Transport (v1)
One **TCP** connection carries everything (control + audio). This is the
simplest reliable design and needs no UDP port coordination. UDP audio is a
future latency optimisation (see "Roadmap").

Default TCP port: **8126** (our own; deliberately not WO Mic's 8125).

## Discovery (so you don't type IPs)
The phone broadcasts a small UDP **beacon** on port **8127** (`255.255.255.255`)
a few times a second while listening. The desktop listens on 8127 and lists the
phones it hears, then connects over TCP as usual.

Beacon payload (UTF-8 JSON):
```json
{ "magic": "OPENMIC", "name": "Pixel 7", "port": 8126, "proto": 1, "auth": true }
```
The beacon never contains the PIN. Discovery is optional — typing the phone's IP
still works, and it's the fallback if broadcast is blocked on the network.

## Framing
All multi-byte integers are **little-endian**.

```
+--------------+-------------------+--------------------------+
| type uint8   | length uint32     | payload (length bytes)   |
+--------------+-------------------+--------------------------+
```

| type | name  | direction        | payload |
|------|-------|------------------|---------|
| 0x01 | HELLO | phone → desktop  | JSON (see below) |
| 0x02 | START | desktop → phone  | JSON (accepted params) |
| 0x03 | AUDIO | phone → desktop  | `seq uint32` + `tsMs uint32` + encoded audio |
| 0x04 | PING  | either           | empty |
| 0x05 | PONG  | either           | empty |
| 0x06 | STOP  | either           | empty |

### HELLO payload (JSON)
```json
{
  "proto": 1,
  "sampleRate": 16000,
  "channels": 1,
  "codec": 0,
  "frameSamples": 320,
  "device": "Pixel 7",
  "appVersion": "1.0"
}
```
`codec`: `0` = PCM16 (raw 16-bit signed), `1` = Opus.
`frameSamples`: samples per channel in each AUDIO frame (16000 Hz × 20 ms = 320).

`auth`: `true` if the phone requires a PIN (then START must carry a `token`).

### START payload (JSON)
Desktop echoes the parameters it accepts (it may lower the sample rate), plus
the PIN if one is required:
```json
{ "sampleRate": 16000, "channels": 1, "codec": 0, "token": "1234" }
```
The phone must honour what START returns. If the phone requires a PIN and the
`token` is missing or wrong, the phone replies **STOP** and closes before any
audio is sent. (The PIN is a simple shared secret; it is sent in clear over the
LAN, so treat it as "keep casual snoops off", not strong security.)

### AUDIO payload
```
+-----------+-----------+-----------------------------+
| seq u32   | tsMs u32  | encoded audio bytes         |
+-----------+-----------+-----------------------------+
```
- `seq` increments by 1 per frame; the desktop uses gaps to count lost frames.
- `tsMs` is milliseconds since capture start (for jitter handling later).
- For PCM16: the bytes are `frameSamples × channels × 2` raw bytes.
- For Opus: one encoded Opus packet for that frame.

## Session flow
```
desktop                         phone (server)
   |  TCP connect  ------------->  accept
   |  <----------- HELLO (params)
   |  START (accepted) --------->
   |  <----------- AUDIO (seq 0)
   |  <----------- AUDIO (seq 1)
   |                ...
   |  PING -------------------->  (optional keepalive)
   |  <----------- PONG
   |  STOP  (either side ends)
```

## Defaults
| Param | Default | Notes |
|---|---|---|
| sampleRate | 16000 Hz | good for voice; Opus-friendly. Use 48000 for music/quality. |
| channels | 1 (mono) | mic is mono |
| codec | 0 (PCM16) | no dependencies; ~256 kbit/s at 16 kHz. Opus later. |
| frame | 20 ms | 320 samples at 16 kHz |
| port | 8126 TCP | |

## Done since v1
- **Auth/PIN**: `auth` in HELLO + `token` in START (above). Implemented on both
  sides and tested.
- **Discovery**: UDP beacon (above). Implemented on both sides and tested.

## Roadmap (not yet built)
- **Opus** (`codec=1`): ~16–32 kbit/s, lower latency. Encode on phone with
  libopus/NDK, decode on desktop with `opuslib`. The desktop receiver already
  has the decode hook.
- **UDP audio**: keep control on TCP; add a UDP socket for AUDIO frames to cut
  latency. `seq`/`tsMs` are already in the frame for reordering/jitter.
- **Bluetooth**: RFCOMM socket on the phone; same framing.
