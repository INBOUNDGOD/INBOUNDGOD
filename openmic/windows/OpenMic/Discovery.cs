using System.Net;
using System.Net.Sockets;
using System.Text.Json;

namespace OpenMic;

public record Phone(string Name, string Ip, int Port, bool Auth)
{
    public override string ToString() => $"{Name}   {Ip}:{Port}{(Auth ? "   [PIN]" : "")}";
}

/// <summary>
/// Listens for the phone's UDP discovery beacons (port 8127) so the user never
/// has to type an IP. Matches desktop/openmic_discovery.py and the Android
/// DiscoveryBeacon.
/// </summary>
public static class Discovery
{
    public static List<Phone> Discover(int timeoutMs = 3000)
    {
        var found = new Dictionary<string, Phone>();
        using var udp = new UdpClient();
        udp.Client.SetSocketOption(SocketOptionLevel.Socket,
            SocketOptionName.ReuseAddress, true);
        udp.Client.Bind(new IPEndPoint(IPAddress.Any, Protocol.DiscoveryPort));
        udp.Client.ReceiveTimeout = 500;

        var end = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < end)
        {
            IPEndPoint ep = new(IPAddress.Any, 0);
            byte[] data;
            try
            {
                data = udp.Receive(ref ep);
            }
            catch (SocketException)
            {
                continue; // receive timeout — keep listening until the window ends
            }

            try
            {
                using var doc = JsonDocument.Parse(data);
                var root = doc.RootElement;
                if (root.GetProperty("magic").GetString() != Protocol.DiscoveryMagic)
                    continue;
                string ip = ep.Address.ToString();
                string name = root.TryGetProperty("name", out var n)
                    ? n.GetString() ?? "phone" : "phone";
                int port = root.TryGetProperty("port", out var p)
                    ? p.GetInt32() : Protocol.DefaultPort;
                bool auth = root.TryGetProperty("auth", out var a) && a.GetBoolean();
                found[ip] = new Phone(name, ip, port, auth);
            }
            catch
            {
                // not one of ours / malformed — ignore
            }
        }
        return found.Values.ToList();
    }
}
