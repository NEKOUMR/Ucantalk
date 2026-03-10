using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace VRC_cantalkcn.Services;

public sealed class MobileControlService : IDisposable
{
    private TcpListener? _listener;
    private CancellationTokenSource? _cts;
    private Task? _loopTask;
    private Func<MobileControlSnapshot>? _snapshotProvider;
    private Action<MobileControlSubmitRequest>? _submitHandler;

    public async Task StartAsync(
        string hostIp,
        int port,
        Func<MobileControlSnapshot> snapshotProvider,
        Action<MobileControlSubmitRequest> submitHandler)
    {
        await StopAsync();

        _snapshotProvider = snapshotProvider;
        _submitHandler = submitHandler;

        var listener = new TcpListener(IPAddress.Any, port);
        listener.Start();
        _listener = listener;
        _cts = new CancellationTokenSource();
        _loopTask = Task.Run(() => AcceptLoopAsync(listener, _cts.Token));
        RuntimeLogService.Info($"Mobile control server started. bind=0.0.0.0:{port}, preferred_host={hostIp}");
    }

    public async Task StopAsync()
    {
        var cts = _cts;
        var listener = _listener;
        var loop = _loopTask;

        _cts = null;
        _listener = null;
        _loopTask = null;

        if (cts is null && listener is null && loop is null)
        {
            return;
        }

        try
        {
            cts?.Cancel();
        }
        catch
        {
            // Ignore.
        }

        listener?.Stop();

        if (loop is not null)
        {
            try
            {
                await loop;
            }
            catch
            {
                // Ignore shutdown races.
            }
        }

        cts?.Dispose();
        RuntimeLogService.Info("Mobile control server stopped.");
    }

    public void Dispose()
    {
        StopAsync().GetAwaiter().GetResult();
    }

    private async Task AcceptLoopAsync(TcpListener listener, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            TcpClient? client = null;
            try
            {
                client = await listener.AcceptTcpClientAsync(token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (SocketException)
            {
                if (!listener.Server.IsBound)
                {
                    break;
                }

                continue;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (Exception ex)
            {
                RuntimeLogService.Warn($"Mobile control accept failed: {ex.Message}");
                continue;
            }

            _ = Task.Run(() => HandleClientAsync(client, token), token);
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken token)
    {
        using var _ = client;
        try
        {
            using var stream = client.GetStream();
            var request = await ReadRequestAsync(stream, token);
            if (request is null)
            {
                return;
            }

            RuntimeLogService.Info($"Mobile control request: {request.Method} {request.Path}");

            if (string.Equals(request.Path, "/healthz", StringComparison.OrdinalIgnoreCase))
            {
                await WriteTextResponseAsync(stream, 200, "OK", "text/plain; charset=utf-8", "ok");
                return;
            }

            if (string.Equals(request.Path, "/", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(request.Method, "GET", StringComparison.OrdinalIgnoreCase))
            {
                await HandleIndexAsync(stream, request);
                return;
            }

            if (string.Equals(request.Path, "/s", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(request.Method, "POST", StringComparison.OrdinalIgnoreCase))
            {
                await HandleSubmitAsync(stream, request);
                return;
            }

            await WriteTextResponseAsync(stream, 404, "Not Found", "text/plain; charset=utf-8", "not found");
        }
        catch (Exception ex)
        {
            RuntimeLogService.Warn($"Mobile control request failed: {ex.Message}");
            try
            {
                using var stream = client.GetStream();
                await WriteTextResponseAsync(stream, 500, "Internal Server Error", "text/plain; charset=utf-8", "internal error");
            }
            catch
            {
                // Ignore.
            }
        }
    }

    private async Task HandleIndexAsync(NetworkStream stream, MobileHttpRequest request)
    {
        var snapshot = _snapshotProvider?.Invoke() ?? MobileControlSnapshot.Empty;
        var labels = MobileControlLabels.ForLanguage(snapshot.UiLanguage);
        var message = request.Query.TryGetValue("m", out var value) ? value : string.Empty;
        var html = BuildHtml(snapshot, labels, message);
        await WriteTextResponseAsync(stream, 200, "OK", "text/html; charset=utf-8", html);
    }

    private async Task HandleSubmitAsync(NetworkStream stream, MobileHttpRequest request)
    {
        var form = ParseFormUrlEncoded(request.Body);

        var text = GetFormValue(form, "t");
        var profile = ParseInt(GetFormValue(form, "p"), 0);
        var lang = NormalizeSimpleCode(GetFormValue(form, "l"), "zh");
        var forced = NormalizeSimpleCode(GetFormValue(form, "m"), string.Empty);
        var speed = Math.Clamp(ParseDouble(GetFormValue(form, "s"), 1.0), 0.5, 2.0);
        var volume = Math.Clamp(ParseDouble(GetFormValue(form, "v"), 100.0), 0.0, 200.0);

        _submitHandler?.Invoke(new MobileControlSubmitRequest
        {
            Text = text,
            ProfileIndex = profile,
            InputLanguage = lang,
            TtsForcedLanguage = forced,
            GptSpeed = speed,
            VolumePercent = volume,
        });

        await WriteRedirectAsync(
            stream,
            string.IsNullOrWhiteSpace(text) ? "/?m=EMPTY" : "/?m=OK");
    }

    private static Dictionary<string, string> ParseFormUrlEncoded(string body)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(body))
        {
            return map;
        }

        var pairs = body.Split('&', StringSplitOptions.RemoveEmptyEntries);
        foreach (var pair in pairs)
        {
            var idx = pair.IndexOf('=');
            string key;
            string value;
            if (idx < 0)
            {
                key = pair;
                value = string.Empty;
            }
            else
            {
                key = pair[..idx];
                value = pair[(idx + 1)..];
            }

            key = Uri.UnescapeDataString(key.Replace('+', ' ')).Trim();
            value = Uri.UnescapeDataString(value.Replace('+', ' ')).Trim();

            if (!string.IsNullOrWhiteSpace(key))
            {
                map[key] = value;
            }
        }

        return map;
    }

    private static string GetFormValue(Dictionary<string, string> form, string key)
    {
        return form.TryGetValue(key, out var value) ? value : string.Empty;
    }

    private static int ParseInt(string value, int fallback)
    {
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : fallback;
    }

    private static double ParseDouble(string value, double fallback)
    {
        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        if (double.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture, out parsed))
        {
            return parsed;
        }

        return fallback;
    }

    private static string NormalizeSimpleCode(string? value, string fallback)
    {
        var v = (value ?? string.Empty).Trim().ToLowerInvariant();
        return v switch
        {
            "zh" => "zh",
            "en" => "en",
            "ja" => "ja",
            "ko" => "ko",
            _ => fallback,
        };
    }

    private static async Task<MobileHttpRequest?> ReadRequestAsync(NetworkStream stream, CancellationToken token)
    {
        var buffer = new byte[4096];
        var received = new List<byte>(8192);
        var headerEnd = -1;

        while (headerEnd < 0 && received.Count < 64 * 1024)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), token);
            if (read <= 0)
            {
                return null;
            }

            for (var i = 0; i < read; i++)
            {
                received.Add(buffer[i]);
            }

            headerEnd = IndexOfHeaderTerminator(received);
        }

        if (headerEnd < 0)
        {
            return null;
        }

        var all = received.ToArray();
        var headerText = Encoding.UTF8.GetString(all, 0, headerEnd);
        var lines = headerText.Split(["\r\n"], StringSplitOptions.None);
        if (lines.Length == 0 || string.IsNullOrWhiteSpace(lines[0]))
        {
            return null;
        }

        var requestLineParts = lines[0].Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (requestLineParts.Length < 2)
        {
            return null;
        }

        var method = requestLineParts[0].Trim().ToUpperInvariant();
        var rawTarget = requestLineParts[1].Trim();
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 1; i < lines.Length; i++)
        {
            var line = lines[i];
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var sep = line.IndexOf(':');
            if (sep <= 0)
            {
                continue;
            }

            var key = line[..sep].Trim();
            var value = line[(sep + 1)..].Trim();
            headers[key] = value;
        }

        var contentLength = 0;
        if (headers.TryGetValue("Content-Length", out var contentLengthText))
        {
            _ = int.TryParse(contentLengthText, NumberStyles.Integer, CultureInfo.InvariantCulture, out contentLength);
        }

        var bodyStart = headerEnd + 4;
        var bodyBytes = all.Length > bodyStart ? all[bodyStart..] : [];
        while (bodyBytes.Length < contentLength)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), token);
            if (read <= 0)
            {
                break;
            }

            var merged = new byte[bodyBytes.Length + read];
            Buffer.BlockCopy(bodyBytes, 0, merged, 0, bodyBytes.Length);
            Buffer.BlockCopy(buffer, 0, merged, bodyBytes.Length, read);
            bodyBytes = merged;
        }

        var body = contentLength > 0
            ? Encoding.UTF8.GetString(bodyBytes, 0, Math.Min(contentLength, bodyBytes.Length))
            : string.Empty;

        var uri = Uri.TryCreate($"http://localhost{rawTarget}", UriKind.Absolute, out var parsedUri)
            ? parsedUri
            : new Uri("http://localhost/");

        return new MobileHttpRequest
        {
            Method = method,
            Path = uri.AbsolutePath,
            Query = ParseFormUrlEncoded(uri.Query.TrimStart('?')),
            Body = body,
            Headers = headers,
        };
    }

    private static int IndexOfHeaderTerminator(List<byte> buffer)
    {
        for (var i = 0; i <= buffer.Count - 4; i++)
        {
            if (buffer[i] == '\r' &&
                buffer[i + 1] == '\n' &&
                buffer[i + 2] == '\r' &&
                buffer[i + 3] == '\n')
            {
                return i;
            }
        }

        return -1;
    }

    private static async Task WriteTextResponseAsync(
        NetworkStream stream,
        int statusCode,
        string statusText,
        string contentType,
        string text)
    {
        var body = Encoding.UTF8.GetBytes(text);
        await WriteResponseAsync(stream, statusCode, statusText, contentType, body, null);
    }

    private static async Task WriteRedirectAsync(NetworkStream stream, string location)
    {
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Location"] = location
        };
        await WriteResponseAsync(stream, 302, "Found", "text/plain; charset=utf-8", [], headers);
    }

    private static async Task WriteResponseAsync(
        NetworkStream stream,
        int statusCode,
        string statusText,
        string contentType,
        byte[] body,
        Dictionary<string, string>? extraHeaders)
    {
        var builder = new StringBuilder();
        builder.Append("HTTP/1.1 ").Append(statusCode).Append(' ').Append(statusText).Append("\r\n");
        builder.Append("Content-Type: ").Append(contentType).Append("\r\n");
        builder.Append("Content-Length: ").Append(body.Length.ToString(CultureInfo.InvariantCulture)).Append("\r\n");
        builder.Append("Connection: close\r\n");
        if (extraHeaders is not null)
        {
            foreach (var pair in extraHeaders)
            {
                builder.Append(pair.Key).Append(": ").Append(pair.Value).Append("\r\n");
            }
        }

        builder.Append("\r\n");
        var headerBytes = Encoding.UTF8.GetBytes(builder.ToString());
        await stream.WriteAsync(headerBytes);
        if (body.Length > 0)
        {
            await stream.WriteAsync(body);
        }
        await stream.FlushAsync();
    }

    private static string BuildHtml(MobileControlSnapshot state, MobileControlLabels labels, string message)
    {
        var sb = new StringBuilder();
        sb.Append("<!DOCTYPE html><html><head><meta charset='utf-8'/>");
        sb.Append("<meta name='viewport' content='width=device-width, initial-scale=1'/>");
        sb.Append("<title>").Append(E(labels.Title)).Append("</title>");
        sb.Append("<style>body{background:#1e1f22;color:#eee;font-family:'Segoe UI',sans-serif;padding:14px;margin:0}");
        sb.Append(".wrap{max-width:640px;margin:0 auto}.card{background:#2b2c30;border:1px solid #404249;border-radius:10px;padding:14px}");
        sb.Append("label{display:block;margin-top:10px;color:#cfd3dc;font-size:13px}");
        sb.Append("select,input,button{width:100%;box-sizing:border-box;padding:12px;margin-top:6px;border-radius:8px;border:1px solid #4a4d55;background:#34363d;color:#fff;font-size:16px}");
        sb.Append("input[type=range]{padding:0;height:36px;background:transparent;border:none}");
        sb.Append("button{background:#0b7bd6;border-color:#0b7bd6;font-weight:600;margin-top:16px}");
        sb.Append(".hint{font-size:12px;color:#9da4b5;margin-top:8px}.ok{color:#7ddc6f;margin-top:10px;font-weight:600}");
        sb.Append(".range-value{display:inline-block;margin-left:8px;color:#9da4b5;font-size:13px}");
        sb.Append("</style></head><body><div class='wrap'><div class='card'>");
        sb.Append("<h3 style='margin:0 0 8px 0'>").Append(E(labels.Title)).Append("</h3>");

        if (string.Equals(message, "OK", StringComparison.OrdinalIgnoreCase))
        {
            sb.Append("<div class='ok'>").Append(E(labels.OkMessage)).Append("</div>");
        }
        else if (string.Equals(message, "EMPTY", StringComparison.OrdinalIgnoreCase))
        {
            sb.Append("<div class='hint'>").Append(E(labels.EmptyMessage)).Append("</div>");
        }

        sb.Append("<form method='post' action='/s'>");
        sb.Append("<label>").Append(E(labels.Profile)).Append("</label>");
        sb.Append("<select name='p'>");
        foreach (var profile in state.Profiles)
        {
            sb.Append("<option value='").Append(profile.Index).Append("'");
            if (profile.Index == state.CurrentProfileIndex)
            {
                sb.Append(" selected");
            }

            sb.Append(">").Append(E(profile.Name)).Append("</option>");
        }

        sb.Append("</select>");

        sb.Append("<label>").Append(E(labels.InputLanguage)).Append("</label>");
        sb.Append("<select name='l'>");
        foreach (var option in state.InputLanguageOptions)
        {
            sb.Append("<option value='").Append(E(option.Value)).Append("'");
            if (string.Equals(option.Value, state.InputLanguage, StringComparison.OrdinalIgnoreCase))
            {
                sb.Append(" selected");
            }

            sb.Append(">").Append(E(option.Label)).Append("</option>");
        }

        sb.Append("</select>");

        sb.Append("<label>").Append(E(labels.TtsForcedLanguage)).Append("</label>");
        sb.Append("<select name='m'>");
        foreach (var option in state.TtsForcedLanguageOptions)
        {
            sb.Append("<option value='").Append(E(option.Value)).Append("'");
            if (string.Equals(option.Value, state.TtsForcedLanguage, StringComparison.OrdinalIgnoreCase))
            {
                sb.Append(" selected");
            }

            sb.Append(">").Append(E(option.Label)).Append("</option>");
        }

        sb.Append("</select>");

        var gptSpeed = Math.Clamp(state.GptSpeed, 0.5, 2.0);
        var volumePercent = Math.Clamp(Math.Round(state.VolumePercent), 0, 200);

        sb.Append("<label>").Append(E(labels.GptSpeed))
            .Append("<span class='range-value' id='speedValue'>")
            .Append(gptSpeed.ToString("0.0", CultureInfo.InvariantCulture)).Append("x</span></label>");
        sb.Append("<input type='range' step='0.1' min='0.5' max='2.0' name='s' id='speedRange' value='")
            .Append(gptSpeed.ToString("0.0", CultureInfo.InvariantCulture)).Append("' ")
            .Append("oninput=\"document.getElementById('speedValue').textContent=this.value+'x'\"/>");

        sb.Append("<label>").Append(E(labels.VolumePercent))
            .Append("<span class='range-value' id='volumeValue'>")
            .Append(volumePercent.ToString(CultureInfo.InvariantCulture)).Append("%</span></label>");
        sb.Append("<input type='range' step='5' min='0' max='200' name='v' id='volumeRange' value='")
            .Append(volumePercent.ToString(CultureInfo.InvariantCulture)).Append("' ")
            .Append("oninput=\"document.getElementById('volumeValue').textContent=this.value+'%'\"/>");

        sb.Append("<label>").Append(E(labels.TextInput)).Append("</label>");
        sb.Append("<input name='t' autocomplete='off' placeholder='").Append(E(labels.TextPlaceholder)).Append("' autofocus/>");
        sb.Append("<button type='submit'>").Append(E(labels.Send)).Append("</button>");
        sb.Append("</form></div></div></body></html>");
        return sb.ToString();
    }

    private static string E(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);
}

internal sealed class MobileHttpRequest
{
    public string Method { get; set; } = "GET";
    public string Path { get; set; } = "/";
    public Dictionary<string, string> Query { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> Headers { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public string Body { get; set; } = string.Empty;
}

public sealed class MobileControlSnapshot
{
    public static MobileControlSnapshot Empty { get; } = new();

    public string UiLanguage { get; set; } = "zh";
    public int CurrentProfileIndex { get; set; }
    public string InputLanguage { get; set; } = "zh";
    public string TtsForcedLanguage { get; set; } = string.Empty;
    public double GptSpeed { get; set; } = 1.0;
    public double VolumePercent { get; set; } = 100.0;
    public List<MobileOptionItem> Profiles { get; set; } = new();
    public List<MobileOptionItem> InputLanguageOptions { get; set; } = new();
    public List<MobileOptionItem> TtsForcedLanguageOptions { get; set; } = new();
}

public sealed class MobileControlSubmitRequest
{
    public string Text { get; set; } = string.Empty;
    public int ProfileIndex { get; set; }
    public string InputLanguage { get; set; } = "zh";
    public string TtsForcedLanguage { get; set; } = string.Empty;
    public double GptSpeed { get; set; } = 1.0;
    public double VolumePercent { get; set; } = 100.0;
}

public sealed class MobileOptionItem
{
    public int Index { get; set; }
    public string Value { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

internal sealed class MobileControlLabels
{
    public string Title { get; set; } = "手机控制端";
    public string Profile { get; set; } = "角色档案";
    public string InputLanguage { get; set; } = "输入语言";
    public string TtsForcedLanguage { get; set; } = "TTS 强制语言";
    public string GptSpeed { get; set; } = "GPT语速";
    public string VolumePercent { get; set; } = "音量(%)";
    public string TextInput { get; set; } = "输入内容";
    public string TextPlaceholder { get; set; } = "输入要说的话";
    public string Send { get; set; } = "发送";
    public string OkMessage { get; set; } = "发送成功";
    public string EmptyMessage { get; set; } = "内容为空，未发送。";

    public static MobileControlLabels ForLanguage(string? languageCode)
    {
        var lang = (languageCode ?? string.Empty).Trim().ToLowerInvariant();
        return lang switch
        {
            "en" => new MobileControlLabels
            {
                Title = "Mobile Control",
                Profile = "Voice Profile",
                InputLanguage = "Input Language",
                TtsForcedLanguage = "Forced TTS Language",
                GptSpeed = "GPT Speed",
                VolumePercent = "Volume (%)",
                TextInput = "Input Text",
                TextPlaceholder = "Text to speak",
                Send = "Send",
                OkMessage = "Sent",
                EmptyMessage = "Text is empty, nothing sent.",
            },
            "ja" => new MobileControlLabels
            {
                Title = "モバイル操作",
                Profile = "音声プロファイル",
                InputLanguage = "入力言語",
                TtsForcedLanguage = "TTS強制言語",
                GptSpeed = "GPT速度",
                VolumePercent = "音量(%)",
                TextInput = "入力内容",
                TextPlaceholder = "話す内容を入力",
                Send = "送信",
                OkMessage = "送信しました",
                EmptyMessage = "内容が空のため送信していません。",
            },
            "ko" => new MobileControlLabels
            {
                Title = "모바일 제어",
                Profile = "음성 프로필",
                InputLanguage = "입력 언어",
                TtsForcedLanguage = "TTS 강제 언어",
                GptSpeed = "GPT 속도",
                VolumePercent = "볼륨(%)",
                TextInput = "입력 내용",
                TextPlaceholder = "말할 내용을 입력",
                Send = "전송",
                OkMessage = "전송 완료",
                EmptyMessage = "내용이 비어 있어 전송하지 않았습니다.",
            },
            _ => new MobileControlLabels(),
        };
    }
}
