namespace VRC_cantalkcn.Models;

public sealed class VoiceProfile
{
    public string Name { get; set; } = "Default";
    public string GptModelPath { get; set; } = string.Empty;
    public string SovitsModelPath { get; set; } = string.Empty;
    public string RefAudioPath { get; set; } = string.Empty;
    public string PromptText { get; set; } = string.Empty;
    public string PromptLanguage { get; set; } = "zh";
}
