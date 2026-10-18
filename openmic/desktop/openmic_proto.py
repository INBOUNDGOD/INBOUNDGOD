"""OpenMic wire protocol — shared framing used by the desktop receiver and the
phone simulator (and mirrored by the Android client).

This is an original, clean-room protocol. It was *informed* by studying how a
phone-as-microphone app is structured (capture -> encode -> stream over the
LAN), but it does not copy any third-party wire format. You own it and can
change it freely.

Design goals:
  * One TCP connection carries both control and audio. Simplest thing that
    works reliably on a LAN; no UDP port coordination, no NAT headaches.
    (UDP audio is documented in PROTOCOL.md as a later latency optimisation.)
  * The PHONE is the server (it prints its IP + port). The DESKTOP connects.
    This matches how people expect it to work: open the app on the phone, type
    the phone's IP on the PC.
  * Little-endian everywhere. 16-bit signed PCM, mono, by default.

Frame layout (all multi-byte fields little-endian):

    +--------+------------------+-----------------------+
    | type   | length (uint32)  | payload (length bytes)|
    | uint8  |                  |                       |
    +--------+------------------+-----------------------+

Message types:
    0x01 HELLO  phone -> desktop : JSON {proto, sampleRate, channels, codec,
                                        frameSamples, device, appVersion}
    0x02 START  desktop -> phone : JSON {sampleRate, channels, codec} (accepted)
    0x03 AUDIO  phone -> desktop : [seq uint32][tsMs uint32][encoded bytes...]
    0x04 PING   either            : empty
    0x05 PONG   either            : empty
    0x06 STOP   either            : empty
"""

import json
import struct

PROTO_VERSION = 1

# control port the phone listens on. Deliberately NOT 8125 (that is a different,
# unrelated app) — this is our own default.
DEFAULT_PORT = 8126

# UDP port the phone broadcasts a discovery beacon on, so the desktop can find
# it without the user typing an IP. See openmic_discovery.py.
DISCOVERY_PORT = 8127
DISCOVERY_MAGIC = "OPENMIC"

# message types
T_HELLO = 0x01
T_START = 0x02
T_AUDIO = 0x03
T_PING = 0x04
T_PONG = 0x05
T_STOP = 0x06

# codec ids
CODEC_PCM16 = 0      # raw 16-bit signed PCM, no dependencies, always works
CODEC_OPUS = 1       # Opus — smaller/lower-latency, needs a codec lib both ends

_HEADER = struct.Struct("<BI")        # type, length
_AUDIO_HEAD = struct.Struct("<II")    # seq, tsMs


def pack_frame(msg_type: int, payload: bytes = b"") -> bytes:
    """Serialise one framed message."""
    return _HEADER.pack(msg_type, len(payload)) + payload


def pack_json(msg_type: int, obj: dict) -> bytes:
    return pack_frame(msg_type, json.dumps(obj).encode("utf-8"))


def pack_audio(seq: int, ts_ms: int, encoded: bytes) -> bytes:
    return pack_frame(T_AUDIO, _AUDIO_HEAD.pack(seq & 0xFFFFFFFF,
                                                ts_ms & 0xFFFFFFFF) + encoded)


def unpack_audio(payload: bytes):
    """Return (seq, ts_ms, encoded_bytes)."""
    seq, ts_ms = _AUDIO_HEAD.unpack_from(payload, 0)
    return seq, ts_ms, payload[_AUDIO_HEAD.size:]


def recv_exact(sock, n: int) -> bytes:
    """Read exactly n bytes or raise ConnectionError on EOF."""
    buf = bytearray()
    while len(buf) < n:
        chunk = sock.recv(n - len(buf))
        if not chunk:
            raise ConnectionError("peer closed connection")
        buf.extend(chunk)
    return bytes(buf)


def read_frame(sock):
    """Read one framed message. Return (msg_type, payload)."""
    head = recv_exact(sock, _HEADER.size)
    msg_type, length = _HEADER.unpack(head)
    payload = recv_exact(sock, length) if length else b""
    return msg_type, payload
