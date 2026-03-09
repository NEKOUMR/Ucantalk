namespace VRC_cantalkcn.Models;

public sealed class TtsRequest
{
    public string Engine { get; set; } = "Edge";
    public string Text { get; set; } = string.Empty;

    public string TextLanguage { get; set; } = "zh";
    public string PromptLanguage { get; set; } = "zh";
    public string PromptText { get; set; } = string.Empty;
    public string RefAudioPath { get; set; } = string.Empty;

    public string GptApiUrl { get; set; } = "http://127.0.0.1:9880";
    public double GptSpeed { get; set; } = 1.0;

    public string EdgeVoice { get; set; } = "zh-CN-XiaoxiaoNeural";
    public double EdgeRate { get; set; }
    public double EdgePitch { get; set; }

    public string FishApiKey { get; set; } = string.Empty;
    public string FishReferenceId { get; set; } = string.Empty;

    public bool CleanPunctuation { get; set; }
}
