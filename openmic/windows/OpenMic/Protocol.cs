using System.Buffers.Binary;

namespace OpenMic;

/// <summary>
/// OpenMic wire protocol — must stay byte-compatible with the phone
/// (android/.../OpenMicProtocol.kt) and the reference Python side.
/// All multi-byte integers are little-endian.
/// </summary>
public static class Protocol
{
    public const int ProtoVersion = 1;
    public const int DefaultPort = 8126;
    public const int DiscoveryPort = 8127;
    public const string DiscoveryMagic = "OPENMIC";

    public const byte THello = 0x01;
    public const byte TStart = 0x02;
    public const byte TAudio = 0x03;
    public const byte TPing = 0x04;
    public const byte TPong = 0x05;
    public const byte TStop = 0x06;

    public const int CodecPcm16 = 0;
    public const int CodecOpus = 1;

    public static void WriteFrame(Stream s, byte type, byte[]? payload = null)
    {
        payload ??= Array.Empty<byte>();
        Span<byte> head = stackalloc byte[5];
        head[0] = type;
        BinaryPrimitives.WriteUInt32LittleEndian(head[1..], (uint)payload.Length);
        lock (s)
        {
            s.Write(head);
            if (payload.Length > 0) s.Write(payload, 0, payload.Length);
            s.Flush();
        }
    }

    public static (byte Type, byte[] Payload) ReadFrame(Stream s)
    {
        byte[] head = ReadExactly(s, 5);
        byte type = head[0];
        uint len = BinaryPrimitives.ReadUInt32LittleEndian(head.AsSpan(1));
        byte[] payload = len > 0 ? ReadExactly(s, (int)len) : Array.Empty<byte>();
        return (type, payload);
    }

    /// <summary>AUDIO payload = seq(u32) + tsMs(u32) + encoded bytes.</summary>
    public static (uint Seq, uint TsMs, byte[] Encoded) ParseAudio(byte[] payload)
    {
        uint seq = BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(0));
        uint ts = BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(4));
        return (seq, ts, payload[8..]);
    }

    private static byte[] ReadExactly(Stream s, int n)
    {
        var buf = new byte[n];
        int off = 0;
        while (off < n)
        {
            int r = s.Read(buf, off, n - off);
            if (r <= 0) throw new EndOfStreamException("peer closed connection");
            off += r;
        }
        return buf;
    }
}
