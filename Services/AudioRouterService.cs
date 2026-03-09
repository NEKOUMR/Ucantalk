using NAudio.CoreAudioApi;
using NAudio.Wave;
using VRC_cantalkcn.Models;

namespace VRC_cantalkcn.Services;

public sealed class AudioRouterService
{
    public List<AudioDeviceInfo> GetInputDevices()
    {
        var devices = new List<AudioDeviceInfo>();
        for (var i = 0; i < WaveIn.DeviceCount; i++)
        {
            var caps = WaveIn.GetCapabilities(i);
            devices.Add(new AudioDeviceInfo
            {
                Id = i.ToString(),
                Name = caps.ProductName,
            });
        }

        return devices;
    }

    public List<AudioDeviceInfo> GetOutputDevices()
    {
        using var enumerator = new MMDeviceEnumerator();
        return enumerator
            .EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active)
            .Select(d => new AudioDeviceInfo { Id = d.ID, Name = d.FriendlyName })
            .ToList();
    }

    public async Task PlayToDevicesAsync(
        string filePath,
        string? monitorDeviceId,
        string? vrcDeviceId,
        float volume,
        CancellationToken cancellationToken = default)
    {
        var targets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(monitorDeviceId))
        {
            targets.Add(monitorDeviceId);
        }

        if (!string.IsNullOrWhiteSpace(vrcDeviceId))
        {
            targets.Add(vrcDeviceId);
        }

        if (targets.Count == 0)
        {
            await PlayOnDefaultDeviceAsync(filePath, volume, cancellationToken);
            return;
        }

        var availableIds = new HashSet<string>(
            GetOutputDevices().Select(d => d.Id),
            StringComparer.OrdinalIgnoreCase);

        var validTargets = targets
            .Where(id => availableIds.Contains(id))
            .ToList();

        // Stored device IDs can become stale (device unplugged/disabled/renamed).
        // Fall back to default device instead of failing the whole TTS playback.
        if (validTargets.Count == 0)
        {
            await PlayOnDefaultDeviceAsync(filePath, volume, cancellationToken);
            return;
        }

        var tasks = validTargets.Select(id => PlayOnSpecificDeviceAsync(filePath, id, volume, cancellationToken));
        await Task.WhenAll(tasks);
    }

    private static async Task PlayOnDefaultDeviceAsync(string filePath, float volume, CancellationToken cancellationToken)
    {
        using var reader = new AudioFileReader(filePath) { Volume = volume };
        using var player = new WaveOutEvent();
        await PlayInternalAsync(player, reader, cancellationToken);
    }

    private static async Task PlayOnSpecificDeviceAsync(string filePath, string deviceId, float volume, CancellationToken cancellationToken)
    {
        using var enumerator = new MMDeviceEnumerator();
        try
        {
            using var device = enumerator.GetDevice(deviceId);
            using var reader = new AudioFileReader(filePath) { Volume = volume };
            using var player = new WasapiOut(device, AudioClientShareMode.Shared, false, 200);
            await PlayInternalAsync(player, reader, cancellationToken);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"音频设备不可用: {deviceId}", ex);
        }
    }

    private static Task PlayInternalAsync(IWavePlayer player, AudioFileReader reader, CancellationToken cancellationToken)
    {
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        player.PlaybackStopped += (_, e) =>
        {
            if (e.Exception is not null)
            {
                tcs.TrySetException(e.Exception);
            }
            else
            {
                tcs.TrySetResult();
            }
        };

        if (cancellationToken.CanBeCanceled)
        {
            cancellationToken.Register(() =>
            {
                try
                {
                    player.Stop();
                }
                catch
                {
                    // Ignore cancellation race.
                }
            });
        }

        player.Init(reader);
        player.Play();
        return tcs.Task;
    }
}
