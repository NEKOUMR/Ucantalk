using Newtonsoft.Json;
using System.Net;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Text.RegularExpressions;
using VRC_cantalkcn.Models;
using Windows.Media.SpeechSynthesis;

namespace VRC_cantalkcn.Services;

public sealed class TtsService
{
    public async Task<string> GenerateSpeechFileAsync(TtsRequest request, string? proxy, CancellationToken cancellationToken = default)
    {
        var rawPath = request.Engine switch
        {
            "GPT-SoVITS" => await GenerateGptSpeechAsync(request, proxy, cancellationToken),
            "FishAudio" => await GenerateFishSpeechAsync(request, proxy, cancellationToken),
            _ => await GenerateEdgeSpeechAsync(request, cancellationToken),
        };

        return await NormalizeByFfmpegIfNeededAsync(rawPath, cancellationToken);
    }

    private static async Task<string> GenerateEdgeSpeechAsync(TtsRequest request, CancellationToken cancellationToken)
    {
        using var synth = new SpeechSynthesizer();
        var targetLang = NormalizeSpeechLanguageCode(request.TextLanguage);
        var voices = SpeechSynthesizer.AllVoices.ToList();
        VoiceInformation? matchedVoice = null;

        if (!string.IsNullOrWhiteSpace(request.EdgeVoice))
        {
            // Prefer selected voice only when it matches current target language.
            matchedVoice = voices.FirstOrDefault(v =>
                (string.Equals(v.Id, request.EdgeVoice, StringComparison.OrdinalIgnoreCase)
                 || string.Equals(v.DisplayName, request.EdgeVoice, StringComparison.OrdinalIgnoreCase))
                && VoiceMatchesLanguage(v, targetLang));
        }

        if (matchedVoice is null && !string.IsNullOrWhiteSpace(targetLang))
        {
            matchedVoice = voices.FirstOrDefault(v => VoiceMatchesLanguage(v, targetLang));
        }

        if (matchedVoice is null && !string.IsNullOrWhiteSpace(request.EdgeVoice))
        {
            matchedVoice = voices.FirstOrDefault(v =>
                string.Equals(v.Id, request.EdgeVoice, StringComparison.OrdinalIgnoreCase)
                || string.Equals(v.DisplayName, request.EdgeVoice, StringComparison.OrdinalIgnoreCase));
        }

        if (matchedVoice is not null)
        {
            synth.Voice = matchedVoice;
        }

        var stream = await synth.SynthesizeTextToStreamAsync(request.Text);
        var extension = stream.ContentType.Contains("mpeg", StringComparison.OrdinalIgnoreCase) ? ".mp3" : ".wav";
        var outputPath = Path.Combine(Path.GetTempPath(), $"cantalk_{Guid.NewGuid():N}{extension}");

        using var file = File.Create(outputPath);
        await stream.AsStreamForRead().CopyToAsync(file, cancellationToken);
        return outputPath;
    }

    private static async Task<string> GenerateGptSpeechAsync(TtsRequest request, string? proxy, CancellationToken cancellationToken)
    {
        var text = request.CleanPunctuation ? CleanForGpt(request.Text) : request.Text;
        using var client = CreateHttpClient(proxy, 25);
        var url = request.GptApiUrl.TrimEnd('/') + "/tts";

        var payload = BuildGptPayload(request, text, includeReference: true);
        RuntimeLogService.Info(
            $"GPT /tts request. lang={payload.GetValueOrDefault("text_lang")}, has_ref={payload.ContainsKey("ref_audio_path")}, speed={request.GptSpeed:0.00}");
        using var resp = await client.PostAsync(
            url,
            new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json"),
            cancellationToken);

        if (!resp.IsSuccessStatusCode)
        {
            var body = await resp.Content.ReadAsStringAsync(cancellationToken);
            RuntimeLogService.Warn($"GPT /tts failed first try: {(int)resp.StatusCode} {ShortBody(body)}");

            // Some GPT-SoVITS variants reject ref fields when they are empty/invalid.
            // Retry once with minimum payload and use API-side defaults.
            if (resp.StatusCode == HttpStatusCode.BadRequest)
            {
                var fallbackPayload = BuildGptPayload(request, text, includeReference: false);
                RuntimeLogService.Info("GPT /tts fallback retry without ref/prompt fields.");
                using var retryResp = await client.PostAsync(
                    url,
                    new StringContent(JsonConvert.SerializeObject(fallbackPayload), Encoding.UTF8, "application/json"),
                    cancellationToken);

                if (retryResp.IsSuccessStatusCode)
                {
                    RuntimeLogService.Info("GPT /tts fallback retry succeeded.");
                    var retryBytes = await retryResp.Content.ReadAsByteArrayAsync(cancellationToken);
                    var retryOutputPath = Path.Combine(Path.GetTempPath(), $"cantalk_{Guid.NewGuid():N}.wav");
                    await File.WriteAllBytesAsync(retryOutputPath, retryBytes, cancellationToken);
                    return retryOutputPath;
                }

                var retryBody = await retryResp.Content.ReadAsStringAsync(cancellationToken);
                RuntimeLogService.Error($"GPT /tts fallback retry failed: {(int)retryResp.StatusCode} {ShortBody(retryBody)}");
                throw new InvalidOperationException(
                    $"GPT-SoVITS /tts 失败: {(int)retryResp.StatusCode} {ShortBody(retryBody)}");
            }

            RuntimeLogService.Error($"GPT /tts failed: {(int)resp.StatusCode} {ShortBody(body)}");
            throw new InvalidOperationException(
                $"GPT-SoVITS /tts 失败: {(int)resp.StatusCode} {ShortBody(body)}");
        }

        RuntimeLogService.Info("GPT /tts request succeeded.");
        var bytes = await resp.Content.ReadAsByteArrayAsync(cancellationToken);
        var outputPath = Path.Combine(Path.GetTempPath(), $"cantalk_{Guid.NewGuid():N}.wav");
        await File.WriteAllBytesAsync(outputPath, bytes, cancellationToken);
        return outputPath;
    }

    private static Dictionary<string, object?> BuildGptPayload(TtsRequest request, string text, bool includeReference)
    {
        var payload = new Dictionary<string, object?>
        {
            ["text"] = text,
            ["text_lang"] = string.IsNullOrWhiteSpace(request.TextLanguage) ? "zh" : request.TextLanguage.Trim(),
            ["speed_factor"] = request.GptSpeed,
        };

        if (!includeReference)
        {
            return payload;
        }

        var refPath = request.RefAudioPath?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(refPath) || !File.Exists(refPath))
        {
            return payload;
        }

        payload["ref_audio_path"] = refPath;

        if (!string.IsNullOrWhiteSpace(request.PromptText))
        {
            payload["prompt_text"] = request.PromptText.Trim();
        }

        if (!string.IsNullOrWhiteSpace(request.PromptLanguage))
        {
            payload["prompt_lang"] = request.PromptLanguage.Trim();
        }

        return payload;
    }

    private static string ShortBody(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return "(empty response)";
        }

        var line = body.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return line.Length <= 280 ? line : line[..280];
    }

    private static async Task<string> GenerateFishSpeechAsync(TtsRequest request, string? proxy, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.FishApiKey))
        {
            throw new InvalidOperationException("FishAudio API Key 为空。");
        }

        using var client = CreateHttpClient(proxy, 25);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", request.FishApiKey);

        var payload = new
        {
            text = request.Text,
            reference_id = request.FishReferenceId,
            format = "mp3",
            mp3_bitrate = 128,
        };

        using var resp = await client.PostAsync(
            "https://api.fish.audio/v1/tts",
            new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json"),
            cancellationToken);

        resp.EnsureSuccessStatusCode();

        var bytes = await resp.Content.ReadAsByteArrayAsync(cancellationToken);
        var outputPath = Path.Combine(Path.GetTempPath(), $"cantalk_{Guid.NewGuid():N}.mp3");
        await File.WriteAllBytesAsync(outputPath, bytes, cancellationToken);
        return outputPath;
    }

    private static bool VoiceMatchesLanguage(VoiceInformation voice, string targetLang)
    {
        if (string.IsNullOrWhiteSpace(targetLang))
        {
            return true;
        }

        var lang = (voice.Language ?? string.Empty).Trim().ToLowerInvariant();
        return targetLang switch
        {
            "zh" => lang.StartsWith("zh", StringComparison.OrdinalIgnoreCase),
            "en" => lang.StartsWith("en", StringComparison.OrdinalIgnoreCase),
            "ja" => lang.StartsWith("ja", StringComparison.OrdinalIgnoreCase),
            "ko" => lang.StartsWith("ko", StringComparison.OrdinalIgnoreCase),
            _ => false,
        };
    }

    private static string NormalizeSpeechLanguageCode(string? value)
    {
        var v = (value ?? string.Empty).Trim().ToLowerInvariant();
        return v switch
        {
            "zh" or "zh-cn" => "zh",
            "en" or "en-us" => "en",
            "ja" or "ja-jp" => "ja",
            "ko" or "ko-kr" => "ko",
            _ => string.Empty,
        };
    }

    private static string CleanForGpt(string text)
    {
        // Remove common CJK/ASCII punctuation to reduce unstable pauses in GPT-SoVITS output.
        return Regex.Replace(text, @"[,\.;:'""“”‘’!?！？，。；：、（）()\[\]{}\-—…·]+", string.Empty);
    }

    private static HttpClient CreateHttpClient(string? proxy, int timeoutSeconds)
    {
        if (string.IsNullOrWhiteSpace(proxy))
        {
            return new HttpClient { Timeout = TimeSpan.FromSeconds(timeoutSeconds) };
        }

        var handler = new HttpClientHandler
        {
            Proxy = new WebProxy(proxy),
            UseProxy = true,
        };

        return new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(timeoutSeconds)
        };
    }

    private static async Task<string> NormalizeByFfmpegIfNeededAsync(string rawPath, CancellationToken cancellationToken)
    {
        var converted = await FfmpegService.TryConvertToPcmWavAsync(rawPath, cancellationToken);
        if (string.IsNullOrWhiteSpace(converted))
        {
            return rawPath;
        }

        try
        {
            if (!string.Equals(
                Path.GetFullPath(rawPath),
                Path.GetFullPath(converted),
                StringComparison.OrdinalIgnoreCase))
            {
                File.Delete(rawPath);
            }
        }
        catch
        {
            // Ignore cleanup failure.
        }

        return converted;
    }
}
