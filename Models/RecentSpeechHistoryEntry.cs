namespace VRC_cantalkcn.Models;

public sealed class RecentSpeechHistoryEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Text { get; set; } = string.Empty;
    public string ReplayText { get; set; } = string.Empty;
    public string ChatText { get; set; } = string.Empty;
    public string SpokenText { get; set; } = string.Empty;

    public void Normalize()
    {
        Text = (Text ?? string.Empty).Trim();
        ReplayText = (ReplayText ?? string.Empty).Trim();
        ChatText = (ChatText ?? string.Empty).Trim();
        SpokenText = (SpokenText ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(Text))
        {
            Text = !string.IsNullOrWhiteSpace(SpokenText)
                ? SpokenText
                : !string.IsNullOrWhiteSpace(ChatText)
                    ? ChatText
                    : ReplayText;
        }

        if (string.IsNullOrWhiteSpace(ReplayText))
        {
            ReplayText = !string.IsNullOrWhiteSpace(ChatText)
                ? ChatText
                : !string.IsNullOrWhiteSpace(SpokenText)
                    ? SpokenText
                    : Text;
        }

        if (string.IsNullOrWhiteSpace(Id))
        {
            Id = Guid.NewGuid().ToString("N");
        }
    }

    public RecentSpeechHistoryEntry Clone()
    {
        return new RecentSpeechHistoryEntry
        {
            Id = Id,
            Text = Text,
            ReplayText = ReplayText,
            ChatText = ChatText,
            SpokenText = SpokenText
        };
    }

    public static RecentSpeechHistoryEntry Create(string replayText, string? chatText, string? spokenText)
    {
        var entry = new RecentSpeechHistoryEntry
        {
            ReplayText = replayText ?? string.Empty,
            ChatText = chatText ?? string.Empty,
            SpokenText = spokenText ?? string.Empty
        };
        entry.Normalize();
        return entry;
    }
}
