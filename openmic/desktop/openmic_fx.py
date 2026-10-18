"""OpenMic voice-changer effects.

Real-time DSP applied to the received audio on the desktop, before it reaches
your speakers / virtual mic. Pure numpy (BSD licence) — no GPL, nothing to worry
about when you ship your own app.

Each effect is a small stateful object with:
    process(samples_float32) -> samples_float32   (mono, -1.0..1.0, same length)

Samples stay float32 in -1..1 inside the chain; openmic_client converts to/from
int16 PCM at the edges. Effects are combined into named PRESETS.

All effects are designed to work frame-by-frame on a live stream (they keep the
state they need between frames), so there are no clicks at frame boundaries.
"""

import math

try:
    import numpy as np
    HAVE_NUMPY = True
except ImportError:  # effects simply disabled if numpy is missing
    HAVE_NUMPY = False


class Effect:
    def process(self, x):
        return x
    def reset(self):
        pass


class PitchShift(Effect):
    """Real-time pitch shifter (keeps tempo) using a two-tap modulated delay
    line with a raised-cosine crossfade. semitones>0 = higher (chipmunk),
    <0 = lower (deep). Classic, cheap, fully streaming."""

    def __init__(self, semitones, sample_rate, window_ms=40.0):
        self.ratio = 2.0 ** (semitones / 12.0)
        self.win = max(64, int(sample_rate * window_ms / 1000.0))
        self.buf = np.zeros(self.win * 2, dtype=np.float32)
        self.wpos = 0          # write position in the ring buffer
        self.phase = 0.0       # 0..1 sweep across the window

    def reset(self):
        self.buf[:] = 0.0
        self.wpos = 0
        self.phase = 0.0

    def process(self, x):
        n = len(x)
        out = np.empty(n, dtype=np.float32)
        buf, N = self.buf, len(self.buf)
        W = self.win
        # per-sample phase increment; sign set so ratio>1 raises pitch
        inc = (1.0 - self.ratio) / W
        phase = self.phase
        wpos = self.wpos
        for i in range(n):
            buf[wpos] = x[i]
            d1 = phase * W
            d2 = ((phase + 0.5) % 1.0) * W
            # read positions behind the write head (fractional, linear interp)
            s1 = self._read(buf, N, wpos - d1)
            s2 = self._read(buf, N, wpos - d2)
            # raised-cosine crossfade between the two taps
            g1 = 0.5 - 0.5 * math.cos(2.0 * math.pi * phase)
            g2 = 1.0 - g1
            out[i] = g1 * s1 + g2 * s2
            phase += inc
            if phase >= 1.0:
                phase -= 1.0
            elif phase < 0.0:
                phase += 1.0
            wpos += 1
            if wpos >= N:
                wpos = 0
        self.phase = phase
        self.wpos = wpos
        return out

    @staticmethod
    def _read(buf, N, pos):
        pos %= N
        i0 = int(pos)
        frac = pos - i0
        i1 = i0 + 1
        if i1 >= N:
            i1 = 0
        return buf[i0] * (1.0 - frac) + buf[i1] * frac


class RingMod(Effect):
    """Ring modulation — the classic robot/Dalek tone. Multiplies the voice by
    a sine carrier (continuous phase across frames)."""

    def __init__(self, freq, sample_rate):
        self.step = 2.0 * math.pi * freq / sample_rate
        self.phase = 0.0

    def reset(self):
        self.phase = 0.0

    def process(self, x):
        n = len(x)
        idx = self.phase + self.step * np.arange(n, dtype=np.float32)
        self.phase = float((self.phase + self.step * n) % (2.0 * math.pi))
        return (x * np.sin(idx)).astype(np.float32)


class Echo(Effect):
    """Feedback delay (echo). delay_ms apart, feedback 0..0.9."""

    def __init__(self, delay_ms, feedback, mix, sample_rate):
        self.d = max(1, int(sample_rate * delay_ms / 1000.0))
        self.fb = feedback
        self.mix = mix
        self.buf = np.zeros(self.d, dtype=np.float32)
        self.pos = 0

    def reset(self):
        self.buf[:] = 0.0
        self.pos = 0

    def process(self, x):
        out = np.empty_like(x)
        buf, d = self.buf, self.d
        pos = self.pos
        fb, mix = self.fb, self.mix
        for i in range(len(x)):
            echoed = buf[pos]
            y = x[i] + mix * echoed
            buf[pos] = x[i] + fb * echoed
            pos += 1
            if pos >= d:
                pos = 0
            out[i] = y
        self.pos = pos
        return out


class Drive(Effect):
    """Soft overdrive/distortion via tanh. gain>1 adds grit."""

    def __init__(self, gain):
        self.gain = gain

    def process(self, x):
        return np.tanh(x * self.gain).astype(np.float32)


class Gain(Effect):
    def __init__(self, g):
        self.g = g
    def process(self, x):
        return np.clip(x * self.g, -1.0, 1.0).astype(np.float32)


class Chain(Effect):
    """Runs several effects in series, with a final safety clip."""

    def __init__(self, effects):
        self.effects = effects

    def reset(self):
        for e in self.effects:
            e.reset()

    def process(self, x):
        for e in self.effects:
            x = e.process(x)
        return np.clip(x, -1.0, 1.0).astype(np.float32)


# Preset names shown in the UI. build_effect(name, sample_rate) -> Effect | None
PRESETS = ["None", "Chipmunk", "Deep voice", "Robot", "Alien", "Echo",
           "Megaphone", "Monster"]


def process_pcm16(effect, pcm_bytes):
    """Apply an effect to little-endian int16 mono PCM bytes, returning PCM
    bytes. No-op if effect is None or numpy is unavailable."""
    if effect is None or not HAVE_NUMPY:
        return pcm_bytes
    a = np.frombuffer(pcm_bytes, dtype="<i2").astype(np.float32) / 32768.0
    y = np.clip(effect.process(a), -1.0, 1.0)
    return (y * 32767.0).astype("<i2").tobytes()


def build_effect(name, sample_rate):
    """Return an Effect for a preset name, or None for 'None'/unknown/no-numpy."""
    if not HAVE_NUMPY or name in (None, "None"):
        return None
    sr = sample_rate
    if name == "Chipmunk":
        return PitchShift(+7, sr)
    if name == "Deep voice":
        return PitchShift(-6, sr)
    if name == "Robot":
        return Chain([RingMod(70, sr), Gain(1.4)])
    if name == "Alien":
        return Chain([PitchShift(+4, sr), RingMod(30, sr)])
    if name == "Echo":
        return Echo(260, 0.45, 0.6, sr)
    if name == "Megaphone":
        return Chain([Drive(6.0), Gain(0.8)])
    if name == "Monster":
        return Chain([PitchShift(-9, sr), Drive(2.5)])
    return None
