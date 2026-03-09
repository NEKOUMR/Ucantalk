namespace VRC_cantalkcn.Models;

public sealed class TranslationConfig
{
    public bool Enabled { get; set; } = true;
    public string Engine { get; set; } = "Universal";
    public List<string> Targets { get; set; } = new() { "en", "ja" };
    public string MainTarget { get; set; } = string.Empty;

    public string UniversalApi { get; set; } = "https://open.bigmodel.cn/api/paas/v4/";
    public string UniversalKey { get; set; } = string.Empty;
    public string UniversalModel { get; set; } = "glm-4-flash";
    public string UniversalPrompt { get; set; } = "You are a professional translation engine. Translate the following text into {target}. Only output the translated text, do not explain.";

    public string DeepLKey { get; set; } = string.Empty;
}
