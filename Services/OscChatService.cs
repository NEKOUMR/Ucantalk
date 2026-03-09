using System.Net.Sockets;
using System.Text;

namespace VRC_cantalkcn.Services;

public sealed class OscChatService
{
    private const int Port = 9000;
    private const string Host = "127.0.0.1";

    public void SendChatboxInput(string text, bool sendImmediately = true, bool playNotification = false)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        // Build OSC packet manually to avoid CoreOSC bool serialization bug.
        var payload = BuildChatboxPacket(text, sendImmediately, playNotification);

        using var udp = new UdpClient();
        udp.Connect(Host, Port);
        udp.Send(payload, payload.Length);
    }

    private static byte[] BuildChatboxPacket(string text, bool sendImmediately, bool playNotification)
    {
        using var ms = new MemoryStream(256);

        WriteOscString(ms, "/chatbox/input");

        // Types: string + bool + bool
        var typeTags = new StringBuilder(4);
        typeTags.Append(",s");
        typeTags.Append(sendImmediately ? "T" : "F");
        typeTags.Append(playNotification ? "T" : "F");
        WriteOscString(ms, typeTags.ToString());

        // String argument payload.
        WriteOscString(ms, text);

        // OSC bool tags (T/F) do not carry payload bytes.
        return ms.ToArray();
    }

    private static void WriteOscString(Stream stream, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        stream.Write(bytes, 0, bytes.Length);
        stream.WriteByte(0);

        var pad = (4 - (int)(stream.Position % 4)) % 4;
        for (var i = 0; i < pad; i++)
        {
            stream.WriteByte(0);
        }
    }
}
