namespace VRC_cantalkcn.Models;

public sealed class TranslationConfig
{
    public const string DefaultUniversalPrompt = "You are a strict translation engine. Your only task is to translate the source text into {target}. Output only the translation result and nothing else. Never answer the user's question. Never explain, summarize, interpret, infer intent, add context, add notes, or complete missing meaning. Preserve tone, punctuation, structure, and question form exactly. If the source is a question, output only the translated question. If the source is a short phrase, output only the translated short phrase. Examples: source='What is apple?' -> output='[translated question only]'; source='元神是什么？' -> output='[translated question only]'; never output definitions or answers.";

    public bool Enabled { get; set; } = true;
    public string Engine { get; set; } = "Universal";
    public List<string> Targets { get; set; } = new() { "en", "ja" };
    public string MainTarget { get; set; } = string.Empty;

    public string UniversalApi { get; set; } = "https://open.bigmodel.cn/api/paas/v4/";
    public string UniversalKey { get; set; } = string.Empty;
    public string UniversalModel { get; set; } = "glm-4-flash";
    public string UniversalPrompt { get; set; } = DefaultUniversalPrompt;

    public string DeepLKey { get; set; } = string.Empty;
}
