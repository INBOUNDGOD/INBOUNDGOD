"""OpenMic desktop client core — the receive logic shared by the command-line
tool (openmic_server.py) and the GUI (openmic_gui.py).

Keeping it in one place means both front-ends use the same tested pipeline:
connect -> HELLO -> START (+PIN) -> receive AUDIO -> decode -> sink.
"""

import json
import socket
import threading
import time
import wave

import openmic_fx as fx
import openmic_proto as p


# ---------------------------------------------------------------- sinks ----

def list_output_devices():
    """Names of output devices, for the GUI's device picker. [] if sounddevice
    isn't installed."""
    try:
        import sounddevice as sd
    except ImportError:
        return []
    names = []
    for d in sd.query_devices():
        if d.get("max_output_channels", 0) > 0:
            names.append(d["name"])
    # de-duplicate preserving order
    seen, out = set(), []
    for n in names:
        if n not in seen:
            seen.add(n); out.append(n)
    return out


def make_sink(kind, sample_rate, channels, out_path=None, device=None):
    """Return (write(pcm_bytes), close()). Raises RuntimeError with a friendly
    message if a needed dependency is missing."""
    if kind == "wav":
        wf = wave.open(out_path or "openmic_capture.wav", "wb")
        wf.setnchannels(channels)
        wf.setsampwidth(2)
        wf.setframerate(sample_rate)
        return (lambda b: wf.writeframes(b)), wf.close

    try:
        import sounddevice as sd  # noqa: F401
    except ImportError:
        raise RuntimeError("The '%s' output needs: pip install sounddevice numpy"
                           % kind)
    import sounddevice as sd
    dev = device if kind == "virtual" else None
    stream = sd.RawOutputStream(samplerate=sample_rate, channels=channels,
                                dtype="int16", device=dev)
    stream.start()
    return (lambda b: stream.write(b)), (lambda: (stream.stop(), stream.close()))


def make_decoder(codec, sample_rate, channels):
    """Return decode(encoded_bytes) -> pcm16_bytes."""
    if codec == p.CODEC_PCM16:
        return lambda enc: enc
    if codec == p.CODEC_OPUS:
        try:
            from opuslib import Decoder
        except ImportError:
            raise RuntimeError("Opus stream but 'opuslib' isn't installed.")
        dec = Decoder(sample_rate, channels)
        frame = int(sample_rate * 0.02)
        return lambda enc: dec.decode(enc, frame)
    raise RuntimeError("Unknown codec id %d" % codec)


# ------------------------------------------------------------- receiver ----

class Receiver:
    """Runs one receive session on a background thread.

    Callbacks (all optional, called from the worker thread):
        on_status(str)              human-readable status line
        on_stats(frames, lost, s)   periodic counters
        on_stopped(reason, error)   session ended (error is None on clean stop)
    """

    def __init__(self, host, port=p.DEFAULT_PORT, pin=None, sink="speaker",
                 device=None, out_path="openmic_capture.wav", rate=None,
                 effect=None, on_status=None, on_stats=None, on_stopped=None):
        self.host = host
        self.port = port
        self.pin = pin or None
        self.sink_kind = sink
        self.device = device
        self.out_path = out_path
        self.rate = rate
        self.effect_name = effect       # voice-changer preset name, or None
        self._effect = None             # built once we know the sample rate
        self._sample_rate = None
        self.on_status = on_status or (lambda s: None)
        self.on_stats = on_stats or (lambda f, l, s: None)
        self.on_stopped = on_stopped or (lambda reason, error: None)
        self._stop = threading.Event()
        self._thread = None
        self._sock = None

    def start(self):
        self._thread = threading.Thread(target=self._run, name="openmic-rx",
                                        daemon=True)
        self._thread.start()

    def set_effect(self, name):
        """Change the voice-changer preset live (safe to call while running)."""
        self.effect_name = name
        if self._sample_rate:
            self._effect = fx.build_effect(name, self._sample_rate)

    def stop(self):
        self._stop.set()
        try:
            if self._sock:
                self._sock.shutdown(socket.SHUT_RDWR)
        except OSError:
            pass

    def join(self, timeout=None):
        if self._thread:
            self._thread.join(timeout)

    def _run(self):
        close_sink = None
        frames = lost = 0
        try:
            self.on_status(f"Connecting to {self.host}:{self.port} ...")
            sock = socket.create_connection((self.host, self.port), timeout=10)
            sock.settimeout(None)
            self._sock = sock

            mtype, payload = p.read_frame(sock)
            if mtype != p.T_HELLO:
                raise RuntimeError(f"expected HELLO, got {mtype:#x}")
            hello = json.loads(payload.decode("utf-8"))

            sample_rate = self.rate or hello.get("sampleRate", 16000)
            channels = hello.get("channels", 1)
            codec = hello.get("codec", p.CODEC_PCM16)

            if hello.get("auth") and not self.pin:
                raise RuntimeError("This phone requires a PIN.")
            start_msg = {"sampleRate": sample_rate, "channels": channels,
                         "codec": codec}
            if self.pin:
                start_msg["token"] = self.pin
            sock.sendall(p.pack_json(p.T_START, start_msg))

            # Wrong PIN -> phone sends STOP immediately.
            sock.settimeout(2.0)
            try:
                peek = p.read_frame(sock)
            except socket.timeout:
                raise RuntimeError("No response after START.")
            sock.settimeout(None)
            if peek[0] == p.T_STOP:
                raise RuntimeError("Phone rejected the connection (wrong PIN?).")

            sink, close_sink = make_sink(self.sink_kind, sample_rate, channels,
                                         self.out_path, self.device)
            decode = make_decoder(codec, sample_rate, channels)
            self._sample_rate = sample_rate
            self._effect = fx.build_effect(self.effect_name, sample_rate)
            self.on_status(f"Streaming {sample_rate} Hz, {channels} ch "
                           f"from {hello.get('device', 'phone')}")

            pending = peek
            last_seq = -1
            t0 = time.time()
            while not self._stop.is_set():
                if pending is not None:
                    mtype, payload = pending
                    pending = None
                else:
                    mtype, payload = p.read_frame(sock)
                if mtype == p.T_AUDIO:
                    seq, _ts, enc = p.unpack_audio(payload)
                    if last_seq >= 0 and seq != last_seq + 1:
                        lost += seq - last_seq - 1
                    last_seq = seq
                    sink(fx.process_pcm16(self._effect, decode(enc)))
                    frames += 1
                    if frames % 25 == 0:
                        self.on_stats(frames, lost, time.time() - t0)
                elif mtype == p.T_PING:
                    sock.sendall(p.pack_frame(p.T_PONG))
                elif mtype == p.T_STOP:
                    break
            self.on_stopped("stopped", None)
        except Exception as e:  # noqa: BLE001 — report everything to the UI
            self.on_stopped("error", e)
        finally:
            try:
                if self._sock:
                    self._sock.sendall(p.pack_frame(p.T_STOP))
            except OSError:
                pass
            try:
                if self._sock:
                    self._sock.close()
            except OSError:
                pass
            if close_sink:
                try:
                    close_sink()
                except Exception:  # noqa: BLE001
                    pass
