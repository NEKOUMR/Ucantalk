namespace VRC_cantalkcn.Models;

public sealed class AppConfig
{
    public string UiLanguage { get; set; } = "zh";
    public string ThemeMode { get; set; } = "system";
    public string AccentColorHex { get; set; } = "#8A8A8A";
    public string BackgroundColorHex { get; set; } = "#1F1F1F";
    public string BackgroundImagePath { get; set; } = string.Empty;
    public double BackgroundBlur { get; set; } = 35;
    public double BackgroundBrightness { get; set; }

    public string HostIp { get; set; } = "127.0.0.1";
    public string Proxy { get; set; } = string.Empty;
    public string Hotkey { get; set; } = string.Empty;
    public string SpeechHotkey { get; set; } = string.Empty;
    public string SendHotkey { get; set; } = string.Empty;
    public string BatPath { get; set; } = string.Empty;

    public string TtsEngine { get; set; } = "Edge";
    public string TextLanguage { get; set; } = "zh";
    public string PromptLanguage { get; set; } = "zh";
    public string EdgeVoice { get; set; } = "zh-CN-XiaoxiaoNeural";

    public double EdgeRate { get; set; }
    public double EdgePitch { get; set; }
    public double VolumePercent { get; set; } = 100;
    public double GptSpeed { get; set; } = 1.0;

    public bool EnableTts { get; set; } = true;
    public bool ForceSync { get; set; } = true;
    public bool CleanPunctuation { get; set; }

    public string GptApiUrl { get; set; } = "http://127.0.0.1:9880";
    public string GptModelPath { get; set; } = string.Empty;
    public string SovitsModelPath { get; set; } = string.Empty;
    public string RefAudioPath { get; set; } = string.Empty;
    public string PromptText { get; set; } = string.Empty;

    public string FishApiKey { get; set; } = string.Empty;
    public string FishReferenceId { get; set; } = string.Empty;

    public string MonitorDeviceId { get; set; } = string.Empty;
    public string VrcDeviceId { get; set; } = string.Empty;
    public string PlayerMonitorDeviceId { get; set; } = string.Empty;
    public string PlayerVrcDeviceId { get; set; } = string.Empty;
    public double PlayerVolumePercent { get; set; } = 100;
    public bool EnableRecentSpeechHistory { get; set; } = true;
    public List<string> RecentSpeechHistory { get; set; } = new();

    public List<VoiceProfile> Profiles { get; set; } = new()
    {
        new VoiceProfile { Name = "Default" }
    };
    public int CurrentProfile { get; set; }

    public TranslationConfig Translation { get; set; } = new();
    public SpeechInputConfig SpeechInput { get; set; } = new();
}
