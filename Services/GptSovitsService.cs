using System.Net;
using System.Web;

namespace VRC_cantalkcn.Services;

public sealed class GptSovitsService
{
    public async Task SetModelAsync(
        string apiUrl,
        string gptModelPath,
        string sovitsModelPath,
        string? proxy,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(apiUrl))
        {
            throw new InvalidOperationException("API 地址为空。");
        }

        if (string.IsNullOrWhiteSpace(gptModelPath) || string.IsNullOrWhiteSpace(sovitsModelPath))
        {
            throw new InvalidOperationException("GPT/SoVITS 模型路径不能为空。");
        }

        var normalizedGptModelPath = NormalizeModelPath(gptModelPath);
        var normalizedSovitsModelPath = NormalizeModelPath(sovitsModelPath);

        using var client = CreateHttpClient(proxy);
        var baseUrl = apiUrl.TrimEnd('/');

        if (await TrySetCombinedModelAsync(client, baseUrl, normalizedGptModelPath, normalizedSovitsModelPath, cancellationToken))
        {
            return;
        }

        // Backward compatibility for GPT-SoVITS APIs that do not expose /set_model.
        await SetWeightsAsync(client, baseUrl, "set_gpt_weights", normalizedGptModelPath, cancellationToken);
        await SetWeightsAsync(client, baseUrl, "set_sovits_weights", normalizedSovitsModelPath, cancellationToken);
    }

    private static async Task<bool> TrySetCombinedModelAsync(
        HttpClient client,
        string baseUrl,
        string gptModelPath,
        string sovitsModelPath,
        CancellationToken cancellationToken)
    {
        var query = HttpUtility.ParseQueryString(string.Empty);
        query["gpt_model_path"] = gptModelPath;
        query["sovits_model_path"] = sovitsModelPath;

        var url = $"{baseUrl}/set_model?{query}";
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        using var resp = await client.SendAsync(req, cancellationToken);

        if (resp.IsSuccessStatusCode)
        {
            return true;
        }

        if (resp.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }

        var body = await resp.Content.ReadAsStringAsync(cancellationToken);
        throw new InvalidOperationException($"切换模型失败: {(int)resp.StatusCode} {body}");
    }

    private static async Task SetWeightsAsync(
        HttpClient client,
        string baseUrl,
        string endpoint,
        string modelPath,
        CancellationToken cancellationToken)
    {
        var query = HttpUtility.ParseQueryString(string.Empty);
        query["weights_path"] = modelPath;
        var url = $"{baseUrl}/{endpoint}?{query}";

        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        using var resp = await client.SendAsync(req, cancellationToken);
        if (resp.IsSuccessStatusCode)
        {
            return;
        }

        var body = await resp.Content.ReadAsStringAsync(cancellationToken);
        throw new InvalidOperationException($"切换模型失败({endpoint}): {(int)resp.StatusCode} {body}");
    }

    private static string NormalizeModelPath(string path)
    {
        var value = Uri.UnescapeDataString(path.Trim());

        // Handle mistakenly pasted query strings.
        if (value.Contains("gpt_model_path=", StringComparison.OrdinalIgnoreCase))
        {
            value = value[(value.IndexOf("gpt_model_path=", StringComparison.OrdinalIgnoreCase) + "gpt_model_path=".Length)..];
        }

        if (value.Contains("weights_path=", StringComparison.OrdinalIgnoreCase))
        {
            value = value[(value.IndexOf("weights_path=", StringComparison.OrdinalIgnoreCase) + "weights_path=".Length)..];
        }

        var amp = value.IndexOf('&');
        if (amp >= 0)
        {
            value = value[..amp];
        }

        // Remove accidental query tails from plain file paths.
        var q = value.IndexOf('?');
        if (q >= 0)
        {
            value = value[..q];
        }

        return value.Trim();
    }

    private static HttpClient CreateHttpClient(string? proxy)
    {
        if (string.IsNullOrWhiteSpace(proxy))
        {
            return new HttpClient { Timeout = TimeSpan.FromSeconds(12) };
        }

        var handler = new HttpClientHandler
        {
            Proxy = new WebProxy(proxy),
            UseProxy = true,
        };

        return new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(12) };
    }
}
