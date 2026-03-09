using System.Diagnostics;

namespace VRC_cantalkcn.Services;

public static class FfmpegService
{
    public static string? ResolveFfmpegPath()
    {
        var embedded = Path.Combine(AppContext.BaseDirectory, "tools", "ffmpeg", "ffmpeg.exe");
        if (File.Exists(embedded))
        {
            return embedded;
        }

        var local = Path.Combine(AppContext.BaseDirectory, "ffmpeg.exe");
        if (File.Exists(local))
        {
            return local;
        }

        return null;
    }

    public static async Task<string?> TryConvertToPcmWavAsync(string inputPath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(inputPath))
        {
            return null;
        }

        var ffmpegPath = ResolveFfmpegPath() ?? "ffmpeg";
        var outputPath = Path.Combine(Path.GetTempPath(), $"cantalk_{Guid.NewGuid():N}_ffmpeg.wav");

        var args =
            $"-y -hide_banner -loglevel error -i \"{inputPath}\" -vn -ac 2 -ar 48000 -sample_fmt s16 \"{outputPath}\"";

        var psi = new ProcessStartInfo
        {
            FileName = ffmpegPath,
            Arguments = args,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true
        };

        try
        {
            using var process = Process.Start(psi);
            if (process is null)
            {
                return null;
            }

            var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);
            var stderr = await errorTask;

            if (process.ExitCode != 0 || !File.Exists(outputPath))
            {
                try
                {
                    if (File.Exists(outputPath))
                    {
                        File.Delete(outputPath);
                    }
                }
                catch
                {
                    // Ignore cleanup failure.
                }

                Debug.WriteLine($"[ffmpeg] convert failed: code={process.ExitCode}, err={stderr}");
                return null;
            }

            return outputPath;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ffmpeg] convert exception: {ex.Message}");
            try
            {
                if (File.Exists(outputPath))
                {
                    File.Delete(outputPath);
                }
            }
            catch
            {
                // Ignore cleanup failure.
            }

            return null;
        }
    }
}
