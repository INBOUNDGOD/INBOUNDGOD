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

    public AudioOutput(int sampleRate, int channels, string? deviceName)
    {
        _buffer = new BufferedWaveProvider(new WaveFormat(sampleRate, 16, channels))
        {
            BufferDuration = TimeSpan.FromSeconds(2),
            DiscardOnBufferOverflow = true,
        };

        MMDevice device = ResolveDevice(deviceName);
        _out = new WasapiOut(device, AudioClientShareMode.Shared, true, 50);
        _out.Init(_buffer);
        _out.Play();
    }

    public void Write(byte[] pcm) => _buffer.AddSamples(pcm, 0, pcm.Length);

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
