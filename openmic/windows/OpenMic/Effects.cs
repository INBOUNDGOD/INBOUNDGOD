namespace OpenMic;

/// <summary>
/// Voice-changer effects, ported from the tested numpy versions in
/// desktop/openmic_fx.py. Each effect processes a mono float frame in-place
/// range -1..1 and keeps whatever state it needs between frames so there are no
/// clicks at frame boundaries.
/// </summary>
public interface IEffect
{
    float[] Process(float[] x);
}

public sealed class PitchShift : IEffect
{
    private readonly double _ratio;
    private readonly int _win;
    private readonly float[] _buf;
    private int _wpos;
    private double _phase;

    public PitchShift(double semitones, int sampleRate, double windowMs = 40.0)
    {
        _ratio = Math.Pow(2.0, semitones / 12.0);
        _win = Math.Max(64, (int)(sampleRate * windowMs / 1000.0));
        _buf = new float[_win * 2];
    }

    public float[] Process(float[] x)
    {
        int n = x.Length;
        var outp = new float[n];
        int N = _buf.Length;
        int W = _win;
        double inc = (1.0 - _ratio) / W;   // sign: ratio>1 raises pitch
        for (int i = 0; i < n; i++)
        {
            _buf[_wpos] = x[i];
            double d1 = _phase * W;
            double d2 = ((_phase + 0.5) % 1.0) * W;
            float s1 = Read(_buf, N, _wpos - d1);
            float s2 = Read(_buf, N, _wpos - d2);
            double g1 = 0.5 - 0.5 * Math.Cos(2.0 * Math.PI * _phase);
            outp[i] = (float)(g1 * s1 + (1.0 - g1) * s2);
            _phase += inc;
            if (_phase >= 1.0) _phase -= 1.0;
            else if (_phase < 0.0) _phase += 1.0;
            if (++_wpos >= N) _wpos = 0;
        }
        return outp;
    }

    private static float Read(float[] buf, int N, double pos)
    {
        pos %= N;
        if (pos < 0) pos += N;
        int i0 = (int)pos;
        double frac = pos - i0;
        int i1 = i0 + 1; if (i1 >= N) i1 = 0;
        return (float)(buf[i0] * (1.0 - frac) + buf[i1] * frac);
    }
}

public sealed class RingMod : IEffect
{
    private readonly double _step;
    private double _phase;

    public RingMod(double freq, int sampleRate) =>
        _step = 2.0 * Math.PI * freq / sampleRate;

    public float[] Process(float[] x)
    {
        var outp = new float[x.Length];
        for (int i = 0; i < x.Length; i++)
        {
            outp[i] = (float)(x[i] * Math.Sin(_phase));
            _phase += _step;
            if (_phase > 2.0 * Math.PI) _phase -= 2.0 * Math.PI;
        }
        return outp;
    }
}

public sealed class Echo : IEffect
{
    private readonly int _d;
    private readonly float _fb;
    private readonly float _mix;
    private readonly float[] _buf;
    private int _pos;

    public Echo(double delayMs, float feedback, float mix, int sampleRate)
    {
        _d = Math.Max(1, (int)(sampleRate * delayMs / 1000.0));
        _fb = feedback; _mix = mix;
        _buf = new float[_d];
    }

    public float[] Process(float[] x)
    {
        var outp = new float[x.Length];
        for (int i = 0; i < x.Length; i++)
        {
            float echoed = _buf[_pos];
            outp[i] = x[i] + _mix * echoed;
            _buf[_pos] = x[i] + _fb * echoed;
            if (++_pos >= _d) _pos = 0;
        }
        return outp;
    }
}

public sealed class Drive : IEffect
{
    private readonly double _gain;
    public Drive(double gain) => _gain = gain;
    public float[] Process(float[] x)
    {
        var outp = new float[x.Length];
        for (int i = 0; i < x.Length; i++) outp[i] = (float)Math.Tanh(x[i] * _gain);
        return outp;
    }
}

public sealed class Gain : IEffect
{
    private readonly float _g;
    public Gain(float g) => _g = g;
    public float[] Process(float[] x)
    {
        var outp = new float[x.Length];
        for (int i = 0; i < x.Length; i++) outp[i] = Math.Clamp(x[i] * _g, -1f, 1f);
        return outp;
    }
}

public sealed class LowPass : IEffect
{
    private readonly double _a;
    private double _y;
    public LowPass(double cutoff, int sr) => _a = 1.0 - Math.Exp(-2.0 * Math.PI * cutoff / sr);
    public float[] Process(float[] x)
    {
        for (int i = 0; i < x.Length; i++) { _y += _a * (x[i] - _y); x[i] = (float)_y; }
        return x;
    }
}

public sealed class HighPass : IEffect
{
    private readonly double _a;
    private double _y;
    public HighPass(double cutoff, int sr) => _a = 1.0 - Math.Exp(-2.0 * Math.PI * cutoff / sr);
    public float[] Process(float[] x)
    {
        for (int i = 0; i < x.Length; i++) { _y += _a * (x[i] - _y); x[i] = (float)(x[i] - _y); }
        return x;
    }
}

public sealed class Tremolo : IEffect
{
    private readonly double _step, _depth;
    private double _phase;
    public Tremolo(double rate, double depth, int sr) { _step = 2.0 * Math.PI * rate / sr; _depth = depth; }
    public float[] Process(float[] x)
    {
        for (int i = 0; i < x.Length; i++)
        {
            double lfo = 1.0 - _depth * (0.5 - 0.5 * Math.Cos(_phase));
            x[i] = (float)(x[i] * lfo);
            _phase += _step; if (_phase > 2 * Math.PI) _phase -= 2 * Math.PI;
        }
        return x;
    }
}

public sealed class Vibrato : IEffect
{
    private readonly double _step, _depth, _base;
    private readonly float[] _buf;
    private int _wpos;
    private double _phase;
    public Vibrato(double rate, double depthMs, int sr)
    {
        _step = 2.0 * Math.PI * rate / sr;
        _depth = depthMs * sr / 1000.0;
        _base = _depth + 2;
        _buf = new float[(int)(_base * 4) + 8];
    }
    public float[] Process(float[] x)
    {
        int N = _buf.Length;
        for (int i = 0; i < x.Length; i++)
        {
            _buf[_wpos] = x[i];
            double d = _base + _depth * Math.Sin(_phase);
            double r = _wpos - d; r %= N; if (r < 0) r += N;
            int i0 = (int)r; double frac = r - i0; int i1 = (i0 + 1) % N;
            x[i] = (float)(_buf[i0] * (1 - frac) + _buf[i1] * frac);
            _phase += _step; if (_phase > 2 * Math.PI) _phase -= 2 * Math.PI;
            if (++_wpos >= N) _wpos = 0;
        }
        return x;
    }
}

public sealed class Chorus : IEffect
{
    private readonly double _step, _depth, _base, _mix;
    private readonly float[] _buf;
    private int _wpos;
    private double _phase;
    public Chorus(int sr, double rate = 0.8, double depthMs = 8.0, double baseMs = 18.0, double mix = 0.5)
    {
        _step = 2.0 * Math.PI * rate / sr;
        _depth = depthMs * sr / 1000.0;
        _base = baseMs * sr / 1000.0;
        _mix = mix;
        _buf = new float[(int)(_base + _depth) * 2 + 8];
    }
    public float[] Process(float[] x)
    {
        int N = _buf.Length;
        for (int i = 0; i < x.Length; i++)
        {
            _buf[_wpos] = x[i];
            double d = _base + _depth * (0.5 + 0.5 * Math.Sin(_phase));
            double r = _wpos - d; r %= N; if (r < 0) r += N;
            int i0 = (int)r; double frac = r - i0; int i1 = (i0 + 1) % N;
            double wet = _buf[i0] * (1 - frac) + _buf[i1] * frac;
            x[i] = (float)((1 - _mix) * x[i] + _mix * wet);
            _phase += _step; if (_phase > 2 * Math.PI) _phase -= 2 * Math.PI;
            if (++_wpos >= N) _wpos = 0;
        }
        return x;
    }
}

/// <summary>Adjustable noise suppression — smoothed downward gate. strength 0..1.</summary>
public sealed class NoiseGate : IEffect
{
    private readonly double _threshold, _floor;
    private double _gain = 1.0;
    public NoiseGate(double strength, int sr)
    {
        strength = Math.Clamp(strength, 0, 1);
        _threshold = 0.006 + 0.05 * strength;
        _floor = 1.0 - 0.95 * strength;
    }
    public float[] Process(float[] x)
    {
        double sum = 0;
        for (int i = 0; i < x.Length; i++) sum += x[i] * (double)x[i];
        double rms = x.Length > 0 ? Math.Sqrt(sum / x.Length) : 0;
        double target = rms >= _threshold ? 1.0 : _floor;
        double c = target > _gain ? 0.4 : 0.12;
        _gain += c * (target - _gain);
        for (int i = 0; i < x.Length; i++) x[i] = (float)(x[i] * _gain);
        return x;
    }
}

public sealed class Chain : IEffect
{
    private readonly IEffect[] _fx;
    public Chain(params IEffect[] fx) => _fx = fx;
    public float[] Process(float[] x)
    {
        foreach (var e in _fx) x = e.Process(x);
        for (int i = 0; i < x.Length; i++) x[i] = Math.Clamp(x[i], -1f, 1f);
        return x;
    }
}

public static class Fx
{
    public static readonly string[] Presets =
        { "None",
          "Chipmunk", "Helium", "Squirrel", "Child",
          "Deep voice", "Giant", "Monster", "Demon", "Villain",
          "Robot", "Cyborg", "Android", "Glitch",
          "Alien", "Ghost", "Space Echo",
          "Telephone", "Walkie-talkie", "Radio", "Megaphone", "Underwater",
          "Cave", "Cathedral", "Stadium",
          "Chorus", "Vibrato", "Tremolo", "Drunk", "Fan (Leslie)" };

    public static IEffect? Build(string? name, int sr) => name switch
    {
        "Chipmunk" => new PitchShift(+7, sr),
        "Helium" => new PitchShift(+12, sr),
        "Squirrel" => new Chain(new PitchShift(+9, sr), new Tremolo(9.0, 0.4, sr)),
        "Child" => new Chain(new PitchShift(+5, sr), new HighPass(300, sr)),
        "Deep voice" => new PitchShift(-5, sr),
        "Giant" => new Chain(new PitchShift(-10, sr), new LowPass(1500, sr), new Echo(300, 0.5f, 0.4f, sr)),
        "Monster" => new Chain(new PitchShift(-7, sr), new Drive(3.0), new RingMod(40, sr)),
        "Demon" => new Chain(new PitchShift(-12, sr), new Drive(3.5), new Echo(140, 0.3f, 0.4f, sr)),
        "Villain" => new Chain(new PitchShift(-4, sr), new Echo(320, 0.55f, 0.5f, sr), new Drive(1.4)),
        "Robot" => new Chain(new RingMod(70, sr), new Gain(1.4f)),
        "Cyborg" => new Chain(new RingMod(120, sr), new Drive(1.8), new HighPass(300, sr)),
        "Android" => new Chain(new RingMod(250, sr), new LowPass(3500, sr)),
        "Glitch" => new Chain(new RingMod(400, sr), new Tremolo(12.0, 0.8, sr)),
        "Alien" => new Chain(new PitchShift(+4, sr), new RingMod(30, sr)),
        "Ghost" => new Chain(new PitchShift(-3, sr), new Vibrato(6, 2.5, sr), new Echo(220, 0.5f, 0.6f, sr)),
        "Space Echo" => new Chain(new Vibrato(0.8, 2.0, sr), new Echo(400, 0.6f, 0.6f, sr)),
        "Telephone" => new Chain(new HighPass(400, sr), new LowPass(3200, sr), new Drive(1.4)),
        "Walkie-talkie" => new Chain(new HighPass(500, sr), new LowPass(2800, sr), new Drive(4.0), new Gain(0.8f)),
        "Radio" => new Chain(new HighPass(250, sr), new LowPass(4500, sr), new Drive(1.6)),
        "Megaphone" => new Chain(new Drive(6.0), new Gain(0.8f)),
        "Underwater" => new Chain(new LowPass(700, sr), new Vibrato(1.2, 3.0, sr)),
        "Cave" => new Echo(180, 0.55f, 0.7f, sr),
        "Cathedral" => new Chain(new Echo(320, 0.6f, 0.6f, sr), new Echo(520, 0.4f, 0.4f, sr)),
        "Stadium" => new Chain(new Echo(500, 0.5f, 0.5f, sr), new Echo(900, 0.35f, 0.4f, sr)),
        "Chorus" => new Chorus(sr),
        "Vibrato" => new Vibrato(5.5, 2.0, sr),
        "Tremolo" => new Tremolo(6.0, 0.7, sr),
        "Drunk" => new Vibrato(1.5, 6.0, sr),
        "Fan (Leslie)" => new Chain(new Tremolo(5.0, 0.5, sr), new Vibrato(5.0, 1.2, sr)),
        _ => null,
    };

    public static readonly string[] NoiseEngines =
        { "Spectral (built-in)", "RNNoise (best, needs DLL)" };

    /// <summary>
    /// Build the noise suppressor for the chosen engine, or null at/near 0.
    /// RNNoise falls back to spectral if rnnoise.dll isn't present.
    /// </summary>
    public static IEffect? BuildNoiseSuppressor(double strength, int sr, string? engine)
    {
        if (strength <= 0.02) return null;
        if (engine != null && engine.StartsWith("RNNoise"))
        {
            try { return new RnnoiseDenoiser(); }
            catch { /* no DLL / load failed — use the built-in engine instead */ }
        }
        return new SpectralDenoise(sr, strength);
    }

    /// <summary>Scale int16 PCM by a linear volume in place (1.0 = unchanged).</summary>
    public static byte[] ApplyVolume(byte[] pcm, double volume)
    {
        if (Math.Abs(volume - 1.0) < 1e-3) return pcm;
        for (int i = 0; i + 1 < pcm.Length; i += 2)
        {
            short s = (short)(pcm[i] | (pcm[i + 1] << 8));
            int v = (int)Math.Clamp(s * volume, -32768, 32767);
            pcm[i] = (byte)(v & 0xFF);
            pcm[i + 1] = (byte)((v >> 8) & 0xFF);
        }
        return pcm;
    }

    /// <summary>Apply an effect to little-endian int16 mono PCM; no-op if null.</summary>
    public static byte[] ProcessPcm16(IEffect? effect, byte[] pcm)
    {
        if (effect == null) return pcm;
        int n = pcm.Length / 2;
        var f = new float[n];
        for (int i = 0; i < n; i++)
        {
            short s = (short)(pcm[i * 2] | (pcm[i * 2 + 1] << 8));
            f[i] = s / 32768f;
        }
        f = effect.Process(f);
        var outp = new byte[n * 2];
        for (int i = 0; i < n; i++)
        {
            int v = (int)Math.Clamp(f[i] * 32767f, -32768f, 32767f);
            outp[i * 2] = (byte)(v & 0xFF);
            outp[i * 2 + 1] = (byte)((v >> 8) & 0xFF);
        }
        return outp;
    }
}
