namespace VRC_cantalkcn.Models;

public sealed class QuickPhraseEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Text { get; set; } = string.Empty;
}
