namespace VRC_cantalkcn.Models;

public sealed class SpeechInputConfig
{
    public string Engine { get; set; } = "Sherpa-ONNX";
    public string TriggerMode { get; set; } = "continuous";
    public string MicrophoneDeviceId { get; set; } = "default";
    public string VoskModelPath { get; set; } = string.Empty;
    public string SherpaModelPath { get; set; } = string.Empty;
    public string SherpaProvider { get; set; } = "cpu";
    public int SherpaNumThreads { get; set; } = 1;
    public string SherpaDecodingMethod { get; set; } = "greedy_search";
    public bool AutoSend { get; set; } = true;
    public bool CaptureWhileTtsPlaying { get; set; } = true;
    public bool CueEnabled { get; set; } = true;
}
