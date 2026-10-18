using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace OpenMic;

/// <summary>
/// Plays received PCM through a Windows output device. Pick the default
/// speakers to hear it, or a virtual cable (e.g. "CABLE Input") so other apps
/// see OpenMic as a microphone.
/// </summary>
public sealed class AudioOutput : IDisposable
{
    private readonly BufferedWaveProvider _buffer;
    private readonly WasapiOut _out;
    private readonly int _maxBytes;   // latency cap: drop backlog beyond this

    public AudioOutput(int sampleRate, int channels, string? deviceName)
    {
        var fmt = new WaveFormat(sampleRate, 16, channels);
        _buffer = new BufferedWaveProvider(fmt)
        {
            BufferDuration = TimeSpan.FromSeconds(1),
            DiscardOnBufferOverflow = true,
        };
        // keep playback within ~200 ms of live; if the buffer grows past that
        // (e.g. clock drift or a hiccup), drop the backlog to catch up.
        _maxBytes = fmt.AverageBytesPerSecond / 5;

        MMDevice device = ResolveDevice(deviceName);
        _out = new WasapiOut(device, AudioClientShareMode.Shared, true, 30);
        _out.Init(_buffer);
        _out.Play();
    }

    public void Write(byte[] pcm)
    {
        if (_buffer.BufferedBytes > _maxBytes) _buffer.ClearBuffer();
        _buffer.AddSamples(pcm, 0, pcm.Length);
    }

    public void Dispose()
    {
        try { _out.Stop(); } catch { }
        _out.Dispose();
    }

    /// <summary>Friendly names of active output devices, for the UI picker.</summary>
    public static List<string> OutputDevices()
    {
        using var en = new MMDeviceEnumerator();
        return en.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active)
                 .Select(d => d.FriendlyName)
                 .ToList();
    }

    private static MMDevice ResolveDevice(string? name)
    {
        var en = new MMDeviceEnumerator();
        if (!string.IsNullOrWhiteSpace(name))
        {
            foreach (var d in en.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
                if (d.FriendlyName == name) return d;
        }
        return en.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
    }
}
