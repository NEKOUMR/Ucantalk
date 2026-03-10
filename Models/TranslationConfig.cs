namespace VRC_cantalkcn.Models;

public sealed class TranslationConfig
{
    public const string LegacyUniversalPrompt = "You are a professional translation engine. Translate the following text into {target}. Only output the translated text, do not explain.";
    public const string StrictUniversalPromptV1 = "You are a strict translation engine. Your only task is to translate the source text into {target}. Output only the translation result and nothing else. Never answer the user's question. Never explain, summarize, interpret, infer intent, add context, add notes, or complete missing meaning. Preserve tone, punctuation, structure, and question form exactly. If the source is a question, output only the translated question. If the source is a short phrase, output only the translated short phrase. Examples: source='What is apple?' -> output='[translated question only]'; source='元神是什么？' -> output='[translated question only]'; never output definitions or answers.";
    public const string DefaultUniversalPrompt = "你是一个专业翻译引擎，只负责把用户提供的原文翻译成{target}。只输出翻译结果，不要回答问题，不要解释，不要补充背景，不要下定义，不要扩写。保留原文的语气、标点、句式和疑问形式。如果原文是问题，只输出翻译后的问题。 You are a professional translation engine. Translate the source text into {target}. Output only the translation. Do not answer, explain, define, add notes, or expand the content. Preserve tone, punctuation, and question form exactly.";

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
