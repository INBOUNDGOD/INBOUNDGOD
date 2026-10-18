using System.Runtime.InteropServices;

namespace OpenMic;

/// <summary>
/// Near-Krisp noise suppression using RNNoise (Xiph, BSD) via a native
/// <c>rnnoise.dll</c>. RNNoise is an ML denoiser that handles non-stationary
/// noise (keyboard, clicks) far better than the built-in spectral gate.
///
/// Setup: ship a 64-bit <c>rnnoise.dll</c> (Xiph C API) with the app — build it
/// from github.com/xiph/rnnoise (CMake + MSVC), or use an x64 prebuilt from
/// github.com/katruud/rnnoise-binaries. Put it in the publish/ folder before
/// `vpk pack` (or next to OpenMic.exe for a quick test). NOT the werman VST DLL.
/// If the DLL is missing, construction throws and the app falls back to
/// SpectralDenoise — so this engine is strictly optional.
///
/// RNNoise runs at 48 kHz in 480-sample frames with samples in int16 range;
/// our stream is 16 kHz [-1,1], so we resample 16k↔48k (×3) around it.
/// NOTE: hand-written, not compiled/tested in this environment — verify on a
/// real build and tune the resampler if needed.
/// </summary>
public sealed class RnnoiseDenoiser : IEffect, IDisposable
{
    private const int Frame = 480;   // rnnoise_get_frame_size() at 48 kHz

    [DllImport("rnnoise", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr rnnoise_create(IntPtr model);
    [DllImport("rnnoise", CallingConvention = CallingConvention.Cdecl)]
    private static extern void rnnoise_destroy(IntPtr st);
    [DllImport("rnnoise", CallingConvention = CallingConvention.Cdecl)]
    private static extern float rnnoise_process_frame(IntPtr st, float[] outFrame, float[] inFrame);

    private IntPtr _st;
    private float _lastIn;                       // resampler history (16k side)
    private float[] _up = Array.Empty<float>();  // 48k samples awaiting framing
    private float[] _down = Array.Empty<float>(); // denoised 48k awaiting downsample
    private float[] _ready = Array.Empty<float>(); // 16k output FIFO
    private readonly float[] _inFrame = new float[Frame];
    private readonly float[] _outFrame = new float[Frame];

    public RnnoiseDenoiser()
    {
        _st = rnnoise_create(IntPtr.Zero);   // throws DllNotFoundException if no DLL
        if (_st == IntPtr.Zero) throw new InvalidOperationException("rnnoise_create failed");
    }

    public float[] Process(float[] x)
    {
        // 16k -> 48k (linear, x3), keeping one sample of history
        var up = new float[x.Length * 3];
        float prev = _lastIn;
        for (int i = 0; i < x.Length; i++)
        {
            float cur = x[i];
            up[i * 3] = prev + (cur - prev) * (1f / 3f);
            up[i * 3 + 1] = prev + (cur - prev) * (2f / 3f);
            up[i * 3 + 2] = cur;
            prev = cur;
        }
        _lastIn = prev;
        _up = Concat(_up, up);

        // process whole 480-sample 48k frames
        while (_up.Length >= Frame)
        {
            for (int i = 0; i < Frame; i++) _inFrame[i] = _up[i] * 32768f; // int16 range
            rnnoise_process_frame(_st, _outFrame, _inFrame);
            var den = new float[Frame];
            for (int i = 0; i < Frame; i++) den[i] = _outFrame[i] / 32768f;
            _down = Concat(_down, den);
            _up = _up[Frame..];
        }

        // 48k -> 16k (average every 3, crude anti-alias)
        int outCount = _down.Length / 3;
        if (outCount > 0)
        {
            var d16 = new float[outCount];
            for (int i = 0; i < outCount; i++)
                d16[i] = (_down[i * 3] + _down[i * 3 + 1] + _down[i * 3 + 2]) / 3f;
            _ready = Concat(_ready, d16);
            _down = _down[(outCount * 3)..];
        }

        int want = x.Length;
        var o = new float[want];
        if (_ready.Length >= want) { Array.Copy(_ready, 0, o, 0, want); _ready = _ready[want..]; }
        else { Array.Copy(_ready, 0, o, want - _ready.Length, _ready.Length); _ready = Array.Empty<float>(); }
        return o;
    }

    public void Dispose()
    {
        if (_st != IntPtr.Zero) { rnnoise_destroy(_st); _st = IntPtr.Zero; }
    }

    private static float[] Concat(float[] a, float[] b)
    {
        var r = new float[a.Length + b.Length];
        Array.Copy(a, r, a.Length);
        Array.Copy(b, 0, r, a.Length, b.Length);
        return r;
    }
}
