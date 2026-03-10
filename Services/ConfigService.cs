using Newtonsoft.Json;
using VRC_cantalkcn.Models;

namespace VRC_cantalkcn.Services;

public sealed class ConfigService
{
    private const string DefaultAccentHex = "#8A8A8A";
    private const string LegacyDefaultAccentHex = "#4CC2FF";
    private const string AppDataFolderName = "Ucantalk";
    private const string LegacyAppDataFolderName = "VRC_cantalkcn";
    private static readonly string ConfigDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        AppDataFolderName);
    private static readonly string LegacyConfigDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        LegacyAppDataFolderName);
    private static readonly string ConfigPath = Path.Combine(ConfigDir, "config.json");
    private static readonly string LegacyUserConfigPath = Path.Combine(LegacyConfigDir, "config.json");
    private static readonly string LegacyConfigPath = Path.Combine(AppContext.BaseDirectory, "config.json");

    public AppConfig Load()
    {
        try
        {
            EnsureConfigDirectory();
            TryMigrateLegacyConfig();

            if (!File.Exists(ConfigPath))
            {
                return new AppConfig();
            }

            var json = File.ReadAllText(ConfigPath);
            var cfg = JsonConvert.DeserializeObject<AppConfig>(
                json,
                new JsonSerializerSettings
                {
                    ObjectCreationHandling = ObjectCreationHandling.Replace
                }) ?? new AppConfig();
            Normalize(cfg);
            return cfg;
        }
        catch
        {
            return new AppConfig();
        }
    }

    public void Save(AppConfig config)
    {
        Normalize(config);
        EnsureConfigDirectory();
        var json = JsonConvert.SerializeObject(config, Formatting.Indented);
        File.WriteAllText(ConfigPath, json);
    }

    private static void EnsureConfigDirectory()
    {
        Directory.CreateDirectory(ConfigDir);
    }

    private static void TryMigrateLegacyConfig()
    {
        if (File.Exists(ConfigPath))
        {
            return;
        }

        try
        {
            if (File.Exists(LegacyUserConfigPath))
            {
                File.Copy(LegacyUserConfigPath, ConfigPath, overwrite: false);
                return;
            }

            if (File.Exists(LegacyConfigPath))
            {
                File.Copy(LegacyConfigPath, ConfigPath, overwrite: false);
            }
        }
        catch
        {
            // Ignore migration failures; app can continue with defaults.
        }
    }

    private static void Normalize(AppConfig config)
    {
        config.UiLanguage = UiLocalizationService.NormalizeLanguage(config.UiLanguage);
        config.ThemeMode = NormalizeThemeMode(config.ThemeMode);
        config.AccentColorHex = NormalizeHexColor(config.AccentColorHex, DefaultAccentHex);
        if (string.Equals(config.AccentColorHex, LegacyDefaultAccentHex, StringComparison.OrdinalIgnoreCase))
        {
            config.AccentColorHex = DefaultAccentHex;
        }
        config.BackgroundColorHex = NormalizeHexColor(config.BackgroundColorHex, "#1F1F1F");
        config.BackgroundImagePath = (config.BackgroundImagePath ?? string.Empty).Trim();
        config.BackgroundBlur = Math.Clamp(config.BackgroundBlur, 0, 100);
        config.BackgroundBrightness = Math.Clamp(config.BackgroundBrightness, -100, 100);
        config.Hotkey = (config.Hotkey ?? string.Empty).Trim();
        config.SpeechHotkey = (config.SpeechHotkey ?? string.Empty).Trim();
        config.SendHotkey = (config.SendHotkey ?? string.Empty).Trim();
        config.PlayerMonitorDeviceId = (config.PlayerMonitorDeviceId ?? string.Empty).Trim();
        config.PlayerVrcDeviceId = (config.PlayerVrcDeviceId ?? string.Empty).Trim();
        config.PlayerVolumePercent = Math.Clamp(config.PlayerVolumePercent, 0, 300);
        config.RecentSpeechHistory ??= new List<string>();
        config.RecentSpeechHistory = config.RecentSpeechHistory
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Take(10)
            .ToList();

        config.Translation ??= new TranslationConfig();
        config.SpeechInput ??= new SpeechInputConfig();
        config.SpeechInput.Engine = NormalizeSpeechEngine(config.SpeechInput.Engine);
        config.SpeechInput.TriggerMode = NormalizeSpeechTriggerMode(config.SpeechInput.TriggerMode);
        config.SpeechInput.MicrophoneDeviceId = NormalizeSpeechMicrophone(config.SpeechInput.MicrophoneDeviceId);
        config.SpeechInput.VoskModelPath = (config.SpeechInput.VoskModelPath ?? string.Empty).Trim();
        config.SpeechInput.SherpaModelPath = (config.SpeechInput.SherpaModelPath ?? string.Empty).Trim();
        config.SpeechInput.SherpaProvider = NormalizeSherpaProvider(config.SpeechInput.SherpaProvider);
        config.SpeechInput.SherpaNumThreads = Math.Clamp(config.SpeechInput.SherpaNumThreads, 1, 16);
        config.SpeechInput.SherpaDecodingMethod = NormalizeSherpaDecoding(config.SpeechInput.SherpaDecodingMethod);

        config.Profiles ??= new List<VoiceProfile>();
        if (config.Profiles.Count == 0)
        {
            config.Profiles.Add(new VoiceProfile { Name = "Default" });
        }
        EnsureUniqueProfileNames(config.Profiles);

        config.CurrentProfile = Math.Clamp(config.CurrentProfile, 0, config.Profiles.Count - 1);

        // Keep translation targets bounded and deduplicated to avoid oversized UI states.
        config.Translation.Targets = config.Translation.Targets
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(3)
            .ToList();

        var oldUniversalPrompt = TranslationConfig.LegacyUniversalPrompt;
        var oldStrictPrompt = TranslationConfig.StrictUniversalPromptV1;
        if (string.IsNullOrWhiteSpace(config.Translation.UniversalPrompt) ||
            string.Equals(config.Translation.UniversalPrompt.Trim(), oldUniversalPrompt, StringComparison.Ordinal) ||
            string.Equals(config.Translation.UniversalPrompt.Trim(), oldStrictPrompt, StringComparison.Ordinal))
        {
            config.Translation.UniversalPrompt = TranslationConfig.DefaultUniversalPrompt;
        }

        // Defensive cap: huge prompt text can freeze large TextBox rendering in WinUI.
        const int maxPromptLength = 4000;
        if (!string.IsNullOrEmpty(config.Translation.UniversalPrompt) &&
            config.Translation.UniversalPrompt.Length > maxPromptLength)
        {
            config.Translation.UniversalPrompt = config.Translation.UniversalPrompt[..maxPromptLength];
        }
    }

    private static string NormalizeThemeMode(string? mode)
    {
        var m = (mode ?? string.Empty).Trim().ToLowerInvariant();
        return m switch
        {
            "dark" => "dark",
            "light" => "light",
            _ => "system",
        };
    }

    private static string NormalizeHexColor(string? color, string fallback)
    {
        var c = (color ?? string.Empty).Trim();
        if (c.StartsWith("#", StringComparison.Ordinal))
        {
            c = c[1..];
        }

        if (c.Length != 6 && c.Length != 8)
        {
            return fallback;
        }

        for (var i = 0; i < c.Length; i++)
        {
            if (!Uri.IsHexDigit(c[i]))
            {
                return fallback;
            }
        }

        return $"#{c.ToUpperInvariant()}";
    }

    private static string NormalizeSpeechEngine(string? engine)
    {
        var e = (engine ?? string.Empty).Trim();
        if (string.Equals(e, "Vosk", StringComparison.OrdinalIgnoreCase))
        {
            return "Vosk";
        }

        if (string.Equals(e, "Sherpa-ONNX", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(e, "Sherpa", StringComparison.OrdinalIgnoreCase))
        {
            return "Sherpa-ONNX";
        }

        if (string.Equals(e, "Paraformer-ZH-Streaming", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(e, "paraformer-zh-streaming", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(e, "Paraformer", StringComparison.OrdinalIgnoreCase))
        {
            return "Sherpa-ONNX";
        }

        if (string.Equals(e, "Windows", StringComparison.OrdinalIgnoreCase))
        {
            return "Windows";
        }

        return "Sherpa-ONNX";
    }

    private static string NormalizeSpeechTriggerMode(string? mode)
    {
        var m = (mode ?? string.Empty).Trim().ToLowerInvariant();
        return m switch
        {
            "ptt" => "ptt",
            "toggle" => "toggle",
            "continuous" => "continuous",
            _ => "continuous",
        };
    }

    private static string NormalizeSherpaProvider(string? provider)
    {
        var p = (provider ?? string.Empty).Trim().ToLowerInvariant();
        return p switch
        {
            "cuda" => "cuda",
            "dml" => "dml",
            _ => "cpu",
        };
    }

    private static string NormalizeSherpaDecoding(string? method)
    {
        var m = (method ?? string.Empty).Trim().ToLowerInvariant();
        return m == "modified_beam_search" ? "modified_beam_search" : "greedy_search";
    }

    private static string NormalizeSpeechMicrophone(string? deviceId)
    {
        var value = (deviceId ?? string.Empty).Trim();
        return string.IsNullOrWhiteSpace(value) ? "default" : value;
    }

    private static void EnsureUniqueProfileNames(List<VoiceProfile> profiles)
    {
        var seen = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < profiles.Count; i++)
        {
            var profile = profiles[i] ?? new VoiceProfile();
            profiles[i] = profile;

            var raw = (profile.Name ?? string.Empty).Trim();
            var baseName = string.IsNullOrWhiteSpace(raw) ? "Default" : raw;

            if (!seen.TryGetValue(baseName, out var count))
            {
                seen[baseName] = 1;
                profile.Name = baseName;
                continue;
            }

            count++;
            seen[baseName] = count;
            profile.Name = $"{baseName} {count}";
        }
    }
}
