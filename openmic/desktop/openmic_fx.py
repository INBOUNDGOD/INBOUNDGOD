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


class LowPass(Effect):
    """One-pole low-pass. cutoff in Hz."""
    def __init__(self, cutoff, sample_rate):
        self.a = 1.0 - math.exp(-2.0 * math.pi * cutoff / sample_rate)
        self.y = 0.0
    def reset(self):
        self.y = 0.0
    def process(self, x):
        out = np.empty_like(x)
        y, a = self.y, self.a
        for i in range(len(x)):
            y += a * (x[i] - y)
            out[i] = y
        self.y = y
        return out.astype(np.float32)


class HighPass(Effect):
    """One-pole high-pass (x minus its low-passed version)."""
    def __init__(self, cutoff, sample_rate):
        self.a = 1.0 - math.exp(-2.0 * math.pi * cutoff / sample_rate)
        self.y = 0.0
    def reset(self):
        self.y = 0.0
    def process(self, x):
        out = np.empty_like(x)
        y, a = self.y, self.a
        for i in range(len(x)):
            y += a * (x[i] - y)
            out[i] = x[i] - y
        self.y = y
        return out.astype(np.float32)


class Tremolo(Effect):
    """Amplitude wobble. rate Hz, depth 0..1."""
    def __init__(self, rate, depth, sample_rate):
        self.step = 2.0 * math.pi * rate / sample_rate
        self.depth = depth
        self.phase = 0.0
    def reset(self):
        self.phase = 0.0
    def process(self, x):
        n = len(x)
        idx = self.phase + self.step * np.arange(n, dtype=np.float32)
        self.phase = float((self.phase + self.step * n) % (2.0 * math.pi))
        lfo = 1.0 - self.depth * (0.5 - 0.5 * np.cos(idx))
        return (x * lfo).astype(np.float32)


class Vibrato(Effect):
    """Pitch wobble via a short modulated delay. rate Hz, depth ms."""
    def __init__(self, rate, depth_ms, sample_rate):
        self.step = 2.0 * math.pi * rate / sample_rate
        self.depth = depth_ms * sample_rate / 1000.0
        self.base = int(self.depth) + 2
        self.buf = np.zeros(self.base * 4 + 4, dtype=np.float32)
        self.wpos = 0
        self.phase = 0.0
    def reset(self):
        self.buf[:] = 0.0; self.wpos = 0; self.phase = 0.0
    def process(self, x):
        out = np.empty_like(x)
        buf, N = self.buf, len(self.buf)
        for i in range(len(x)):
            buf[self.wpos] = x[i]
            d = self.base + self.depth * math.sin(self.phase)
            r = self.wpos - d
            r %= N
            i0 = int(r); frac = r - i0; i1 = (i0 + 1) % N
            out[i] = buf[i0] * (1 - frac) + buf[i1] * frac
            self.phase += self.step
            if self.phase > 2 * math.pi:
                self.phase -= 2 * math.pi
            self.wpos = (self.wpos + 1) % N
        return out.astype(np.float32)


class Chorus(Effect):
    """Dry voice plus a slowly detuned copy — thickens/doubles the voice."""
    def __init__(self, sample_rate, rate=0.8, depth_ms=8.0, base_ms=18.0, mix=0.5):
        self.step = 2.0 * math.pi * rate / sample_rate
        self.depth = depth_ms * sample_rate / 1000.0
        self.base = base_ms * sample_rate / 1000.0
        self.mix = mix
        self.buf = np.zeros(int(self.base + self.depth) * 2 + 8, dtype=np.float32)
        self.wpos = 0
        self.phase = 0.0
    def reset(self):
        self.buf[:] = 0.0; self.wpos = 0; self.phase = 0.0
    def process(self, x):
        out = np.empty_like(x)
        buf, N = self.buf, len(self.buf)
        for i in range(len(x)):
            buf[self.wpos] = x[i]
            d = self.base + self.depth * (0.5 + 0.5 * math.sin(self.phase))
            r = (self.wpos - d) % N
            i0 = int(r); frac = r - i0; i1 = (i0 + 1) % N
            wet = buf[i0] * (1 - frac) + buf[i1] * frac
            out[i] = (1 - self.mix) * x[i] + self.mix * wet
            self.phase += self.step
            if self.phase > 2 * math.pi:
                self.phase -= 2 * math.pi
            self.wpos = (self.wpos + 1) % N
        return out.astype(np.float32)


class SpectralDenoise(Effect):
    """Frequency-domain noise suppression (spectral gating / Wiener-style).

    Unlike a broadband gate, this estimates the noise level *per frequency bin*
    and only attenuates bins dominated by noise, so steady background noise
    (fans, hiss, hum) is removed while the speech bands pass through at full
    level. strength 0..1 sets how aggressive the subtraction is.

    Streaming overlap-add STFT: sqrt-Hann window, 50% overlap, so it runs frame
    by frame on the live stream. Adds ~one window (~32 ms) of latency. The noise
    profile adapts on its own (no noise sample needed): each bin tracks the quiet
    passages fast and the loud ones slowly.
    """
    def __init__(self, sample_rate, strength, n_fft=512):
        self.n = n_fft
        self.hop = n_fft // 2
        w = np.sqrt(np.hanning(n_fft).astype(np.float32) + 1e-8)
        self.win = w
        self.strength = max(0.0, min(1.0, strength))
        self.oversub = 1.0 + 2.0 * self.strength   # how hard to subtract noise
        self.floor = 0.06 + 0.10 * (1.0 - self.strength)  # min bin gain (avoids total mute)
        self.inbuf = np.zeros(0, dtype=np.float32)
        self.acc = np.zeros(n_fft, dtype=np.float32)   # overlap-add accumulator
        self.ready = np.zeros(0, dtype=np.float32)     # finalized output samples
        self.noise = None                               # per-bin noise power
        self.emin = None                                # running frame-energy floor (VAD)
        self.prev_gain = np.ones(n_fft // 2 + 1, dtype=np.float32)

    def reset(self):
        self.inbuf = np.zeros(0, dtype=np.float32)
        self.acc = np.zeros(self.n, dtype=np.float32)
        self.ready = np.zeros(0, dtype=np.float32)
        self.noise = None
        self.emin = None
        self.prev_gain = np.ones(self.n // 2 + 1, dtype=np.float32)

    def process(self, x):
        self.inbuf = np.concatenate((self.inbuf, x))
        while len(self.inbuf) >= self.n:
            frame = self.inbuf[:self.n] * self.win
            spec = np.fft.rfft(frame)
            mag2 = (spec.real * spec.real + spec.imag * spec.imag).astype(np.float32)
            # Energy VAD: update the noise profile only on non-speech frames, so
            # the voice never gets added to the noise estimate (which was what
            # made it "block your speaking"). The per-bin profile is frozen while
            # you talk and refreshed in the gaps.
            e = float(mag2.sum())
            if self.emin is None:
                self.emin = e
            elif e < self.emin:
                self.emin = e                       # follow the floor down instantly
            else:
                self.emin *= 1.0015                 # leak up slowly (~9%/s), not during speech
            is_speech = e > self.emin * 3.0 + 1e-9
            if self.noise is None:
                self.noise = mag2.copy() + 1e-9
            elif not is_speech:
                self.noise = (0.9 * self.noise + 0.1 * mag2).astype(np.float32)
            # (during speech the noise estimate is left frozen)
            # spectral-subtraction gain, clamped to a floor (prevents musical noise/muting)
            gain = (mag2 - self.oversub * self.noise) / (mag2 + 1e-9)
            gain = np.clip(gain, self.floor, 1.0).astype(np.float32)
            # smooth the mask over time to reduce warble
            gain = (0.6 * gain + 0.4 * self.prev_gain).astype(np.float32)
            self.prev_gain = gain
            out_frame = np.fft.irfft(spec * gain).astype(np.float32) * self.win
            self.acc += out_frame
            self.ready = np.concatenate((self.ready, self.acc[:self.hop].copy()))
            self.acc = np.concatenate((self.acc[self.hop:], np.zeros(self.hop, dtype=np.float32)))
            self.inbuf = self.inbuf[self.hop:]
        # return exactly len(x) samples (zero-padded during the initial fill)
        n = len(x)
        if len(self.ready) >= n:
            out = self.ready[:n]
            self.ready = self.ready[n:]
        else:
            out = np.concatenate((np.zeros(n - len(self.ready), dtype=np.float32), self.ready))
            self.ready = np.zeros(0, dtype=np.float32)
        return out.astype(np.float32)


class NoiseGate(Effect):
    """Adjustable noise suppression: a smoothed downward expander/gate.
    strength 0..1 — higher cuts quieter sounds harder. Cheap, dependency-free
    (not a neural denoiser; see README for the RNNoise upgrade path)."""
    def __init__(self, strength, sample_rate, frame_ms=20.0):
        self.strength = max(0.0, min(1.0, strength))
        # threshold rises with strength; floor gain falls with strength
        self.threshold = 0.006 + 0.05 * self.strength
        self.floor = 1.0 - 0.95 * self.strength
        # smoothing time constants
        self.gain = 1.0
        self.attack = 0.4
        self.release = 0.12
    def reset(self):
        self.gain = 1.0
    def process(self, x):
        rms = float(np.sqrt(np.mean(x * x))) if len(x) else 0.0
        target = 1.0 if rms >= self.threshold else self.floor
        # smooth toward target (fast to open, slow to close)
        coeff = self.attack if target > self.gain else self.release
        self.gain += coeff * (target - self.gain)
        return (x * self.gain).astype(np.float32)


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
PRESETS = [
    "None",
    "Chipmunk", "Helium", "Squirrel", "Child",
    "Deep voice", "Giant", "Monster", "Demon", "Villain",
    "Robot", "Cyborg", "Android", "Glitch",
    "Alien", "Ghost", "Space Echo",
    "Telephone", "Walkie-talkie", "Radio", "Megaphone", "Underwater",
    "Cave", "Cathedral", "Stadium",
    "Chorus", "Vibrato", "Tremolo", "Drunk", "Fan (Leslie)",
]


def process_pcm16(effect, pcm_bytes):
    """Apply an effect to little-endian int16 mono PCM bytes, returning PCM
    bytes. No-op if effect is None or numpy is unavailable."""
    if effect is None or not HAVE_NUMPY:
        return pcm_bytes
    a = np.frombuffer(pcm_bytes, dtype="<i2").astype(np.float32) / 32768.0
    y = np.clip(effect.process(a), -1.0, 1.0)
    return (y * 32767.0).astype("<i2").tobytes()


def _build(name, sr):
    match name:
        # higher-pitched
        case "Chipmunk":      return PitchShift(+7, sr)
        case "Helium":        return PitchShift(+12, sr)
        case "Squirrel":      return Chain([PitchShift(+9, sr), Tremolo(9.0, 0.4, sr)])
        case "Child":         return Chain([PitchShift(+5, sr), HighPass(300, sr)])
        # deeper / scarier (each distinct in character, not just pitch)
        case "Deep voice":    return PitchShift(-5, sr)
        case "Giant":         return Chain([PitchShift(-10, sr), LowPass(1500, sr), Echo(300, 0.5, 0.4, sr)])
        case "Monster":       return Chain([PitchShift(-7, sr), Drive(3.0), RingMod(40, sr)])
        case "Demon":         return Chain([PitchShift(-12, sr), Drive(3.5), Echo(140, 0.3, 0.4, sr)])
        case "Villain":       return Chain([PitchShift(-4, sr), Echo(320, 0.55, 0.5, sr), Drive(1.4)])
        # robotic / metallic
        case "Robot":         return Chain([RingMod(70, sr), Gain(1.4)])
        case "Cyborg":        return Chain([RingMod(120, sr), Drive(1.8), HighPass(300, sr)])
        case "Android":       return Chain([RingMod(250, sr), LowPass(3500, sr)])
        case "Glitch":        return Chain([RingMod(400, sr), Tremolo(12.0, 0.8, sr)])
        # alien / ghostly
        case "Alien":         return Chain([PitchShift(+4, sr), RingMod(30, sr)])
        case "Ghost":         return Chain([PitchShift(-3, sr), Vibrato(6.0, 2.5, sr), Echo(220, 0.5, 0.6, sr)])
        case "Space Echo":    return Chain([Vibrato(0.8, 2.0, sr), Echo(400, 0.6, 0.6, sr)])
        # EQ / environment
        case "Telephone":     return Chain([HighPass(400, sr), LowPass(3200, sr), Drive(1.4)])
        case "Walkie-talkie": return Chain([HighPass(500, sr), LowPass(2800, sr), Drive(4.0), Gain(0.8)])
        case "Radio":         return Chain([HighPass(250, sr), LowPass(4500, sr), Drive(1.6)])
        case "Megaphone":     return Chain([Drive(6.0), Gain(0.8)])
        case "Underwater":    return Chain([LowPass(700, sr), Vibrato(1.2, 3.0, sr)])
        case "Cave":          return Echo(180, 0.55, 0.7, sr)
        case "Cathedral":     return Chain([Echo(320, 0.6, 0.6, sr), Echo(520, 0.4, 0.4, sr)])
        case "Stadium":       return Chain([Echo(500, 0.5, 0.5, sr), Echo(900, 0.35, 0.4, sr)])
        # modulation
        case "Chorus":        return Chorus(sr)
        case "Vibrato":       return Vibrato(5.5, 2.0, sr)
        case "Tremolo":       return Tremolo(6.0, 0.7, sr)
        case "Drunk":         return Vibrato(1.5, 6.0, sr)
        case "Fan (Leslie)":  return Chain([Tremolo(5.0, 0.5, sr), Vibrato(5.0, 1.2, sr)])
        case _:               return None


def build_effect(name, sample_rate):
    """Return an Effect for a preset name, or None for 'None'/unknown/no-numpy."""
    if not HAVE_NUMPY or name in (None, "None"):
        return None
    return _build(name, sample_rate)


def build_noise_gate(strength, sample_rate):
    """Noise suppressor for strength 0..1, or None at/near 0.

    Returns the frequency-domain SpectralDenoise (keeps speech, removes steady
    noise per band) rather than the old broadband gate.
    """
    if not HAVE_NUMPY or strength <= 0.02:
        return None
    return SpectralDenoise(sample_rate, strength)


def apply_volume(pcm_bytes, volume):
    """Scale int16 PCM by a linear volume (1.0 = unchanged). No-op without numpy."""
    if not HAVE_NUMPY or abs(volume - 1.0) < 1e-3:
        return pcm_bytes
    a = np.frombuffer(pcm_bytes, dtype="<i2").astype(np.float32) * volume
    return np.clip(a, -32768, 32767).astype("<i2").tobytes()
