using System.Numerics;

namespace OpenMic;

/// <summary>Minimal in-place iterative radix-2 FFT (power-of-two lengths).</summary>
internal static class Fft
{
    public static void Transform(Complex[] a, bool inverse)
    {
        int n = a.Length;
        for (int i = 1, j = 0; i < n; i++)
        {
            int bit = n >> 1;
            for (; (j & bit) != 0; bit >>= 1) j ^= bit;
            j ^= bit;
            if (i < j) (a[i], a[j]) = (a[j], a[i]);
        }
        for (int len = 2; len <= n; len <<= 1)
        {
            double ang = 2 * Math.PI / len * (inverse ? 1 : -1);
            var wlen = new Complex(Math.Cos(ang), Math.Sin(ang));
            for (int i = 0; i < n; i += len)
            {
                Complex w = Complex.One;
                for (int k = 0; k < len / 2; k++)
                {
                    Complex u = a[i + k];
                    Complex v = a[i + k + len / 2] * w;
                    a[i + k] = u + v;
                    a[i + k + len / 2] = u - v;
                    w *= wlen;
                }
            }
        }
        if (inverse)
            for (int i = 0; i < n; i++) a[i] /= n;
    }
}

/// <summary>
/// Frequency-domain noise suppression — the C# port of the tested
/// desktop/openmic_fx.py SpectralDenoise. Estimates noise per frequency bin
/// (only updating during non-speech frames, via an energy VAD) and attenuates
/// noise-dominated bins, so steady background noise is removed while the voice
/// bands pass at full level. strength 0..1. ~32 ms latency.
/// </summary>
public sealed class SpectralDenoise : IEffect
{
    private readonly int _n = 512;
    private readonly int _hop = 256;
    private readonly float[] _win;
    private readonly double _oversub;
    private readonly double _floor;

    private float[] _inbuf = Array.Empty<float>();
    private readonly float[] _acc;
    private float[] _ready = Array.Empty<float>();
    private double[]? _noise;
    private double? _emin;
    private readonly double[] _prevGain;

    public SpectralDenoise(int sampleRate, double strength)
    {
        strength = Math.Clamp(strength, 0, 1);
        _oversub = 1.0 + 2.0 * strength;
        _floor = 0.06 + 0.10 * (1.0 - strength);
        _win = new float[_n];
        for (int i = 0; i < _n; i++)
            _win[i] = (float)Math.Sqrt(0.5 - 0.5 * Math.Cos(2 * Math.PI * i / (_n - 1)) + 1e-8);
        _acc = new float[_n];
        _prevGain = new double[_n];
        for (int i = 0; i < _n; i++) _prevGain[i] = 1.0;
    }

    public float[] Process(float[] x)
    {
        _inbuf = Concat(_inbuf, x);
        while (_inbuf.Length >= _n)
        {
            var spec = new Complex[_n];
            for (int i = 0; i < _n; i++) spec[i] = _inbuf[i] * _win[i];
            Fft.Transform(spec, false);

            var mag2 = new double[_n];
            double e = 0;
            for (int i = 0; i < _n; i++) { mag2[i] = spec[i].Real * spec[i].Real + spec[i].Imaginary * spec[i].Imaginary; e += mag2[i]; }

            // energy VAD: only refresh the noise profile when not speaking
            if (_emin == null) _emin = e;
            else if (e < _emin) _emin = e;
            else _emin *= 1.0015;
            bool isSpeech = e > _emin * 3.0 + 1e-9;
            if (_noise == null)
            {
                _noise = new double[_n];
                for (int i = 0; i < _n; i++) _noise[i] = mag2[i] + 1e-9;
            }
            else if (!isSpeech)
            {
                for (int i = 0; i < _n; i++) _noise[i] = 0.9 * _noise[i] + 0.1 * mag2[i];
            }

            for (int i = 0; i < _n; i++)
            {
                double g = (mag2[i] - _oversub * _noise[i]) / (mag2[i] + 1e-9);
                g = Math.Clamp(g, _floor, 1.0);
                g = 0.6 * g + 0.4 * _prevGain[i];
                _prevGain[i] = g;
                spec[i] *= g;
            }

            Fft.Transform(spec, true);
            for (int i = 0; i < _n; i++) _acc[i] += (float)spec[i].Real * _win[i];

            var finalized = new float[_hop];
            Array.Copy(_acc, 0, finalized, 0, _hop);
            _ready = Concat(_ready, finalized);
            Array.Copy(_acc, _hop, _acc, 0, _n - _hop);
            Array.Clear(_acc, _n - _hop, _hop);
            _inbuf = _inbuf[_hop..];
        }

        int want = x.Length;
        var outp = new float[want];
        if (_ready.Length >= want)
        {
            Array.Copy(_ready, 0, outp, 0, want);
            _ready = _ready[want..];
        }
        else
        {
            // startup: left-pad with zeros until the pipeline fills
            Array.Copy(_ready, 0, outp, want - _ready.Length, _ready.Length);
            _ready = Array.Empty<float>();
        }
        return outp;
    }

    private static float[] Concat(float[] a, float[] b)
    {
        var r = new float[a.Length + b.Length];
        Array.Copy(a, r, a.Length);
        Array.Copy(b, 0, r, a.Length, b.Length);
        return r;
    }
}
