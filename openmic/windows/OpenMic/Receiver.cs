using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace OpenMic;

/// <summary>
/// One receive session on a background thread: connect to the phone, do the
/// HELLO/START handshake (with PIN), then receive AUDIO frames, apply the
/// voice effect, and play them. Mirrors desktop/openmic_client.py.
/// </summary>
public sealed class Receiver
{
    private readonly string _host;
    private readonly int _port;
    private readonly string? _pin;
    private readonly string? _deviceName;
    private readonly string? _effectName;

    private volatile bool _stop;
    private Thread? _thread;
    private TcpClient? _client;
    private int _sampleRate;
    private IEffect? _effect;

    public event Action<string>? Status;
    public event Action<int, int>? Stats;              // frames, lost
    public event Action<Exception?>? Stopped;           // null = clean stop

    public Receiver(string host, int port, string? pin, string? deviceName,
                    string? effectName)
    {
        _host = host; _port = port;
        _pin = string.IsNullOrWhiteSpace(pin) ? null : pin;
        _deviceName = deviceName;
        _effectName = effectName;
    }

    public void Start()
    {
        _thread = new Thread(Run) { IsBackground = true, Name = "openmic-rx" };
        _thread.Start();
    }

    public void Stop()
    {
        _stop = true;
        try { _client?.Close(); } catch { }
    }

    /// <summary>Change the voice preset live while streaming.</summary>
    public void SetEffect(string? name)
    {
        if (_sampleRate > 0) _effect = Fx.Build(name, _sampleRate);
    }

    private void Run()
    {
        AudioOutput? output = null;
        int frames = 0, lost = 0;
        try
        {
            Status?.Invoke($"Connecting to {_host}:{_port} ...");
            _client = new TcpClient();
            _client.Connect(_host, _port);
            _client.NoDelay = true;
            var stream = _client.GetStream();

            var (htype, hpayload) = Protocol.ReadFrame(stream);
            if (htype != Protocol.THello)
                throw new IOException($"expected HELLO, got 0x{htype:X2}");
            using var hello = JsonDocument.Parse(hpayload);
            var root = hello.RootElement;
            _sampleRate = root.TryGetProperty("sampleRate", out var sr) ? sr.GetInt32() : 16000;
            int channels = root.TryGetProperty("channels", out var ch) ? ch.GetInt32() : 1;
            int codec = root.TryGetProperty("codec", out var cd) ? cd.GetInt32() : Protocol.CodecPcm16;
            bool auth = root.TryGetProperty("auth", out var au) && au.GetBoolean();
            string phoneName = root.TryGetProperty("device", out var dv) ? dv.GetString() ?? "phone" : "phone";

            if (codec != Protocol.CodecPcm16)
                throw new IOException("This build supports PCM only (phone sent Opus).");
            if (auth && _pin == null)
                throw new IOException("This phone requires a PIN.");

            var start = new Dictionary<string, object>
            {
                ["sampleRate"] = _sampleRate,
                ["channels"] = channels,
                ["codec"] = codec,
            };
            if (_pin != null) start["token"] = _pin;
            byte[] startJson = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(start));
            Protocol.WriteFrame(stream, Protocol.TStart, startJson);

            // Wrong PIN -> phone replies STOP immediately.
            _client.ReceiveTimeout = 2000;
            var peek = Protocol.ReadFrame(stream);
            _client.ReceiveTimeout = 0;
            if (peek.Type == Protocol.TStop)
                throw new IOException("Phone rejected the connection (wrong PIN?).");

            _effect = Fx.Build(_effectName, _sampleRate);
            output = new AudioOutput(_sampleRate, channels, _deviceName);
            Status?.Invoke($"Streaming {_sampleRate} Hz from {phoneName}");

            (byte Type, byte[] Payload)? pending = peek;
            long lastSeq = -1;
            while (!_stop)
            {
                var (type, payload) = pending ?? Protocol.ReadFrame(stream);
                pending = null;

                if (type == Protocol.TAudio)
                {
                    var (seq, _, enc) = Protocol.ParseAudio(payload);
                    if (lastSeq >= 0 && seq != lastSeq + 1) lost += (int)(seq - lastSeq - 1);
                    lastSeq = seq;
                    output.Write(Fx.ProcessPcm16(_effect, enc));
                    if (++frames % 25 == 0) Stats?.Invoke(frames, lost);
                }
                else if (type == Protocol.TPing)
                {
                    Protocol.WriteFrame(stream, Protocol.TPong);
                }
                else if (type == Protocol.TStop)
                {
                    break;
                }
            }
            Stopped?.Invoke(null);
        }
        catch (Exception ex)
        {
            Stopped?.Invoke(_stop ? null : ex);
        }
        finally
        {
            try { if (_client is { Connected: true }) Protocol.WriteFrame(_client.GetStream(), Protocol.TStop); }
            catch { }
            output?.Dispose();
            try { _client?.Close(); } catch { }
        }
    }
}
