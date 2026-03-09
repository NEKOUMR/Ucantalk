using System.Media;

namespace VRC_cantalkcn.Services;

public static class AudioCueService
{
    private static readonly object SyncRoot = new();
    private static Dictionary<string, SoundPlayer>? _players;

    public static void PlayStart() => Play("start");
    public static void PlayStop() => Play("stop");
    public static void PlaySend() => Play("send");

    private static void Play(string cue)
    {
        try
        {
            EnsurePlayers();
            if (_players is null || !_players.TryGetValue(cue, out var player))
            {
                return;
            }

            player.Play();
        }
        catch
        {
            // Ignore cue failures.
        }
    }

    private static void EnsurePlayers()
    {
        if (_players is not null)
        {
            return;
        }

        lock (SyncRoot)
        {
            if (_players is not null)
            {
                return;
            }

            var tempRoot = Path.Combine(Path.GetTempPath(), "ucantalk-cues");
            Directory.CreateDirectory(tempRoot);

            _players = new Dictionary<string, SoundPlayer>(StringComparer.OrdinalIgnoreCase)
            {
                ["start"] = CreatePlayer(Path.Combine(tempRoot, "speech_start.wav"), 800, 0.10),
                ["stop"] = CreatePlayer(Path.Combine(tempRoot, "speech_stop.wav"), 420, 0.10),
                ["send"] = CreatePlayer(Path.Combine(tempRoot, "speech_send.wav"), 1200, 0.10),
            };
        }
    }

    private static SoundPlayer CreatePlayer(string path, double frequency, double durationSeconds)
    {
        if (!File.Exists(path))
        {
            WriteSineWave(path, frequency, durationSeconds);
        }

        var player = new SoundPlayer(path);
        player.LoadAsync();
        return player;
    }

    private static void WriteSineWave(string path, double frequency, double durationSeconds)
    {
        const int sampleRate = 44100;
        const short channels = 1;
        const short bitsPerSample = 16;
        const short blockAlign = channels * (bitsPerSample / 8);
        const int byteRate = sampleRate * blockAlign;
        const short amplitude = 8000;

        var sampleCount = Math.Max(1, (int)(sampleRate * durationSeconds));
        var dataLength = sampleCount * blockAlign;

        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream);

        writer.Write("RIFF"u8.ToArray());
        writer.Write(36 + dataLength);
        writer.Write("WAVE"u8.ToArray());
        writer.Write("fmt "u8.ToArray());
        writer.Write(16);
        writer.Write((short)1);
        writer.Write(channels);
        writer.Write(sampleRate);
        writer.Write(byteRate);
        writer.Write(blockAlign);
        writer.Write(bitsPerSample);
        writer.Write("data"u8.ToArray());
        writer.Write(dataLength);

        for (var i = 0; i < sampleCount; i++)
        {
            var value = (short)(amplitude * Math.Sin(2 * Math.PI * frequency * i / sampleRate));
            writer.Write(value);
        }
    }
}
