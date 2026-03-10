using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net;
using System.Text;
using VRC_cantalkcn.Models;

namespace VRC_cantalkcn.Services;

public sealed class TranslatorService
{
    private static readonly Dictionary<string, string> LangDisplay = new(StringComparer.OrdinalIgnoreCase)
    {
        ["zh"] = "中文",
        ["en"] = "英语",
        ["ja"] = "日语",
        ["ko"] = "韩语",
        ["fr"] = "法语",
        ["de"] = "德语",
        ["es"] = "西语",
        ["ru"] = "俄语",
        ["th"] = "泰语",
        ["vi"] = "越语",
        ["id"] = "印尼语",
        ["ar"] = "阿语",
    };
    private static readonly HashSet<string> SupportedTargetCodes = new(
        LangDisplay.Keys,
        StringComparer.OrdinalIgnoreCase);

    public async Task<TranslationResult> TranslateAsync(
        string text,
        TranslationConfig config,
        string? proxy,
        CancellationToken cancellationToken = default)
    {
        if (!config.Enabled)
        {
            RuntimeLogService.Info("Translation skipped: disabled.");
            return new TranslationResult
            {
                TtsText = text,
                DisplayText = text,
                HasMainTargetTranslation = false,
                MainTargetLanguage = string.Empty
            };
        }

        var mainTarget = NormalizeLanguageCode(config.MainTarget);
        var targets = BuildTargets(config, mainTarget);
        if (targets.Count == 0)
        {
            RuntimeLogService.Info("Translation skipped: no targets.");
            return new TranslationResult
            {
                TtsText = text,
                DisplayText = text,
                HasMainTargetTranslation = false,
                MainTargetLanguage = string.Empty
            };
        }

        RuntimeLogService.Info(
            $"Translation start. engine={config.Engine}, targets={string.Join(",", targets)}, text_len={text.Length}");

        var tasks = targets.Select(async t =>
        {
            var translated = await TranslateOneAsync(text, t, config, proxy, cancellationToken);
            return (Target: t, Text: translated);
        });

        var pairs = await Task.WhenAll(tasks);
        var map = pairs
            .Where(x => !string.IsNullOrWhiteSpace(x.Text))
            .ToDictionary(x => x.Target, x => x.Text!, StringComparer.OrdinalIgnoreCase);

        var display = text;
        string ttsText = text;
        var hasMainTargetTranslation = false;

        if (!string.IsNullOrWhiteSpace(mainTarget) && map.TryGetValue(mainTarget, out var main))
        {
            if (!string.Equals(main.Trim(), text.Trim(), StringComparison.Ordinal))
            {
                display += $" | {main}";
            }

            ttsText = main;
            hasMainTargetTranslation = true;
        }

        foreach (var target in targets)
        {
            if (string.Equals(target, mainTarget, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!map.TryGetValue(target, out var translated))
            {
                continue;
            }

            if (string.Equals(translated.Trim(), text.Trim(), StringComparison.Ordinal))
            {
                continue;
            }

            if (display.Contains(translated, StringComparison.Ordinal))
            {
                continue;
            }

            display += $" | {translated}";
        }

        return new TranslationResult
        {
            TtsText = string.IsNullOrWhiteSpace(ttsText) ? text : ttsText,
            DisplayText = display,
            HasMainTargetTranslation = hasMainTargetTranslation,
            MainTargetLanguage = hasMainTargetTranslation ? mainTarget : string.Empty
        };
    }

    private static List<string> BuildTargets(TranslationConfig config, string mainTarget)
    {
        var list = config.Targets
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(NormalizeLanguageCode)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(3)
            .ToList();

        if (!string.IsNullOrWhiteSpace(mainTarget))
        {
            list.RemoveAll(x => string.Equals(x, mainTarget, StringComparison.OrdinalIgnoreCase));
            list.Insert(0, mainTarget);
        }

        return list.Take(3).ToList();
    }

    private static async Task<string?> TranslateOneAsync(
        string text,
        string target,
        TranslationConfig config,
        string? proxy,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = config.Engine switch
            {
                "DeepL" => await TranslateByDeepLAsync(text, target, config, proxy, cancellationToken),
                "Google" => await TranslateByGoogleAsync(text, target, proxy, cancellationToken),
                _ => await TranslateByUniversalApiAsync(text, target, config, proxy, cancellationToken),
            };

            RuntimeLogService.Info(
                $"Translation target completed. engine={config.Engine}, target={target}, changed={!string.Equals((result ?? text).Trim(), text.Trim(), StringComparison.Ordinal)}, has_result={!string.IsNullOrWhiteSpace(result)}");
            return result;
        }
        catch (Exception ex)
        {
            RuntimeLogService.Warn(
                $"Translation target failed. engine={config.Engine}, target={target}, error={ex.Message}");
            return text;
        }
    }

    private static async Task<string?> TranslateByUniversalApiAsync(
        string text,
        string target,
        TranslationConfig config,
        string? proxy,
        CancellationToken cancellationToken)
    {
        var api = config.UniversalApi.Trim();
        if (string.IsNullOrWhiteSpace(api))
        {
            return text;
        }

        var isLocal = api.Contains("localhost", StringComparison.OrdinalIgnoreCase) || api.Contains("127.0.0.1");
        if (!isLocal && string.IsNullOrWhiteSpace(config.UniversalKey))
        {
            RuntimeLogService.Warn(
                $"Universal translation skipped. target={target}, reason=missing_api_key");
            return null;
        }

        using var client = CreateHttpClient(proxy);
        var url = api.TrimEnd('/') + "/chat/completions";
        RuntimeLogService.Info(
            $"Universal translation request. target={target}, api={api}, model={config.UniversalModel}");

        if (!string.IsNullOrWhiteSpace(config.UniversalKey))
        {
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", config.UniversalKey);
        }

        var prompt = BuildUniversalPrompt(config, target, strictRetry: false);
        var translated = await SendUniversalTranslationRequestAsync(
            client,
            url,
            config,
            prompt,
            BuildUserTranslationMessage(text, target, strictRetry: false),
            cancellationToken);

        if (ShouldRetryAsStrictQuestionTranslation(text, translated, target))
        {
            RuntimeLogService.Warn(
                $"Universal translation looked like an answer. target={target}, source={ShortBody(text)}, first={ShortBody(translated ?? string.Empty)}");

            var retryPrompt = BuildUniversalPrompt(config, target, strictRetry: true);
            var retryTranslated = await SendUniversalTranslationRequestAsync(
                client,
                url,
                config,
                retryPrompt,
                BuildUserTranslationMessage(text, target, strictRetry: true),
                cancellationToken);

            if (!string.IsNullOrWhiteSpace(retryTranslated))
            {
                translated = retryTranslated;
            }
        }

        return translated ?? text;
    }

    private static async Task<string?> TranslateByGoogleAsync(
        string text,
        string target,
        string? proxy,
        CancellationToken cancellationToken)
    {
        RuntimeLogService.Info($"Google translation request. target={target}");
        using var client = CreateHttpClient(proxy);
        var googleTarget = string.Equals(target, "zh", StringComparison.OrdinalIgnoreCase) ? "zh-CN" : target;
        var url = $"https://translate.googleapis.com/translate_a/single?client=gtx&sl=auto&tl={Uri.EscapeDataString(googleTarget)}&dt=t&q={Uri.EscapeDataString(text)}";
        using var resp = await client.GetAsync(url, cancellationToken);
        if (!resp.IsSuccessStatusCode)
        {
            var failBody = await resp.Content.ReadAsStringAsync(cancellationToken);
            RuntimeLogService.Warn(
                $"Google translation failed. target={target}, status={(int)resp.StatusCode}, body={ShortBody(failBody)}");
            return text;
        }

        var body = await resp.Content.ReadAsStringAsync(cancellationToken);
        var token = JToken.Parse(body);
        var first = token[0]?[0]?[0]?.ToString();
        return string.IsNullOrWhiteSpace(first) ? text : first.Trim();
    }

    private static async Task<string?> TranslateByDeepLAsync(
        string text,
        string target,
        TranslationConfig config,
        string? proxy,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(config.DeepLKey))
        {
            RuntimeLogService.Warn($"DeepL translation skipped. target={target}, reason=missing_api_key");
            return null;
        }

        RuntimeLogService.Info($"DeepL translation request. target={target}");
        using var client = CreateHttpClient(proxy);
        var targetCode = target.ToUpperInvariant();
        if (targetCode == "EN")
        {
            targetCode = "EN-US";
        }

        if (targetCode == "ZH")
        {
            targetCode = "ZH";
        }

        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["auth_key"] = config.DeepLKey,
            ["text"] = text,
            ["target_lang"] = targetCode,
        });

        using var resp = await client.PostAsync("https://api-free.deepl.com/v2/translate", content, cancellationToken);
        if (!resp.IsSuccessStatusCode)
        {
            var failBody = await resp.Content.ReadAsStringAsync(cancellationToken);
            RuntimeLogService.Warn(
                $"DeepL translation failed. target={target}, status={(int)resp.StatusCode}, body={ShortBody(failBody)}");
            return text;
        }

        var body = await resp.Content.ReadAsStringAsync(cancellationToken);
        var jo = JObject.Parse(body);
        return jo["translations"]?[0]?["text"]?.ToString()?.Trim() ?? text;
    }

    private static string NormalizeLanguageCode(string? value)
    {
        var v = (value ?? string.Empty).Trim().ToLowerInvariant();
        v = v switch
        {
            "zh-cn" => "zh",
            "en-us" => "en",
            "ja-jp" => "ja",
            "ko-kr" => "ko",
            _ => v
        };

        return SupportedTargetCodes.Contains(v) ? v : string.Empty;
    }

    private static HttpClient CreateHttpClient(string? proxy)
    {
        if (string.IsNullOrWhiteSpace(proxy))
        {
            return new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(20)
            };
        }

        var handler = new HttpClientHandler
        {
            Proxy = new WebProxy(proxy),
            UseProxy = true
        };

        return new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(20)
        };
    }

    private static string ShortBody(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return "(empty response)";
        }

        var line = body.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return line.Length <= 240 ? line : line[..240];
    }

    private static string BuildUniversalPrompt(TranslationConfig config, string target, bool strictRetry)
    {
        var lang = LangDisplay.TryGetValue(target, out var d) ? d : target;
        var basePrompt = (config.UniversalPrompt ?? string.Empty)
            .Replace("{target}", lang, StringComparison.OrdinalIgnoreCase);

        if (!strictRetry)
        {
            return basePrompt;
        }

        return basePrompt +
               $" CRITICAL RULE: Translate into {lang} only. If the source asks a question, output only the translated question. Never answer it. Never define terms. Never explain concepts. Never write any extra sentence before or after the translation. If you answer instead of translate, the output is wrong.";
    }

    private static string BuildUserTranslationMessage(string text, string target, bool strictRetry)
    {
        var lang = LangDisplay.TryGetValue(target, out var d) ? d : target;
        var instruction = strictRetry
            ? $"Translate the SOURCE_TEXT below into {lang}. Return the translated text only. Do not answer the question. Do not explain anything."
            : $"Translate the SOURCE_TEXT below into {lang}. Return the translated text only.";

        return $"{instruction}\nSOURCE_TEXT_BEGIN\n{text}\nSOURCE_TEXT_END";
    }

    private static async Task<string?> SendUniversalTranslationRequestAsync(
        HttpClient client,
        string url,
        TranslationConfig config,
        string systemPrompt,
        string userMessage,
        CancellationToken cancellationToken)
    {
        var payload = new
        {
            model = string.IsNullOrWhiteSpace(config.UniversalModel) ? "glm-4-flash" : config.UniversalModel,
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userMessage }
            },
            temperature = 0.0
        };

        using var req = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json")
        };

        using var resp = await client.SendAsync(req, cancellationToken);
        if (!resp.IsSuccessStatusCode)
        {
            var failBody = await resp.Content.ReadAsStringAsync(cancellationToken);
            RuntimeLogService.Warn(
                $"Universal translation failed. status={(int)resp.StatusCode}, body={ShortBody(failBody)}");
            return null;
        }

        var body = await resp.Content.ReadAsStringAsync(cancellationToken);
        var jo = JObject.Parse(body);
        return jo["choices"]?[0]?["message"]?["content"]?.ToString()?.Trim();
    }

    private static bool ShouldRetryAsStrictQuestionTranslation(string source, string? translated, string target)
    {
        if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(translated))
        {
            return false;
        }

        var src = source.Trim();
        var dst = translated.Trim();
        if (!LooksLikeQuestion(src))
        {
            return false;
        }

        if (dst.Length > Math.Max(src.Length * 3, src.Length + 30))
        {
            return true;
        }

        if (!LooksLikeQuestion(dst) && LooksLikeAnswer(dst, target))
        {
            return true;
        }

        return false;
    }

    private static bool LooksLikeQuestion(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var value = text.Trim();
        if (value.Contains('?') || value.Contains('？'))
        {
            return true;
        }

        string[] markers =
        {
            "什么", "是啥", "是什么", "什么意思", "怎么", "为什么", "如何",
            "what", "who", "why", "how", "where", "when", "which",
            "とは", "何", "どう", "なぜ",
            "무엇", "뭐", "왜", "어떻게", "무슨"
        };

        return markers.Any(marker => value.Contains(marker, StringComparison.OrdinalIgnoreCase));
    }

    private static bool LooksLikeAnswer(string text, string target)
    {
        var value = text.Trim();
        string[] generalMarkers =
        {
            "是指", "通常", "一种", "一个", "概念", "代表", "指的是", "本质",
            "refers to", "is a", "is an", "usually", "means", "concept",
            "とは", "です", "ます", "指します",
            "입니다", "뜻", "의미", "가리킵니다"
        };

        if (generalMarkers.Any(marker => value.Contains(marker, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return target switch
        {
            "zh" => value.Contains('。') && !value.Contains('？'),
            "en" => value.Contains('.') && !value.Contains('?'),
            "ja" => value.Contains('。') && !value.Contains('？'),
            "ko" => value.Contains('.') && !value.Contains('?'),
            _ => false
        };
    }
}
