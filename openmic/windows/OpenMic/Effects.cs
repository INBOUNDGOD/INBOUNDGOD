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
        { "None", "Chipmunk", "Deep voice", "Robot", "Alien", "Echo",
          "Megaphone", "Monster" };

    public static IEffect? Build(string? name, int sr) => name switch
    {
        "Chipmunk" => new PitchShift(+7, sr),
        "Deep voice" => new PitchShift(-6, sr),
        "Robot" => new Chain(new RingMod(70, sr), new Gain(1.4f)),
        "Alien" => new Chain(new PitchShift(+4, sr), new RingMod(30, sr)),
        "Echo" => new Echo(260, 0.45f, 0.6f, sr),
        "Megaphone" => new Chain(new Drive(6.0), new Gain(0.8f)),
        "Monster" => new Chain(new PitchShift(-9, sr), new Drive(2.5)),
        _ => null,
    };

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
