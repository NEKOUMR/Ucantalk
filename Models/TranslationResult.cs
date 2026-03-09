namespace VRC_cantalkcn.Models;

public sealed class TranslationResult
{
    public string TtsText { get; set; } = string.Empty;
    public string DisplayText { get; set; } = string.Empty;
    public bool HasMainTargetTranslation { get; set; }
    public string MainTargetLanguage { get; set; } = string.Empty;
}
