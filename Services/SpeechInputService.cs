using NAudio.Wave;
using Newtonsoft.Json.Linq;
using SherpaOnnx;
using System.Globalization;
using System.Speech.AudioFormat;
using System.Speech.Recognition;
using Vosk;
using VRC_cantalkcn.Models;

namespace VRC_cantalkcn.Services;

public sealed class SpeechInputService : IDisposable
{
    private static readonly string BundledSherpaRoot = Path.Combine(AppContext.BaseDirectory, "tools", "sherpa-default");
    private readonly object _lock = new();

    private Model? _model;
    private string _modelPath = string.Empty;
    private VoskRecognizer? _recognizer;

    private OnlineRecognizer? _sherpaRecognizer;
    private OnlineStream? _sherpaStream;
    private string _sherpaLastText = string.Empty;
    private string _sherpaLoadedModelPath = string.Empty;
    private string _sherpaLoadedProvider = "cpu";
    private int _sherpaLoadedNumThreads = 1;
    private string _sherpaLoadedDecoding = "greedy_search";
    private bool _captureSuppressed;

    private WaveInEvent? _waveIn;
    private SpeechRecognitionEngine? _windowsEngine;
    private PcmBufferedReadStream? _windowsAudioStream;
    private bool _windowsRecognitionStarted;

    public event EventHandler<string>? TextRecognized;

    public bool IsRunning
    {
        get
        {
            lock (_lock)
            {
                return _waveIn is not null || _windowsEngine is not null;
            }
        }
    }

    public void Start(SpeechInputConfig config)
    {
        StartAsync(config).GetAwaiter().GetResult();
    }

    public Task PreloadAsync(SpeechInputConfig config)
    {
        var snapshot = new SpeechInputConfig
        {
            Engine = config.Engine,
            TriggerMode = config.TriggerMode,
            MicrophoneDeviceId = config.MicrophoneDeviceId,
            VoskModelPath = config.VoskModelPath,
            SherpaModelPath = config.SherpaModelPath,
            SherpaProvider = config.SherpaProvider,
            SherpaNumThreads = config.SherpaNumThreads,
            SherpaDecodingMethod = config.SherpaDecodingMethod,
            AutoSend = config.AutoSend,
            CaptureWhileTtsPlaying = config.CaptureWhileTtsPlaying,
        };

        return Task.Run(() =>
        {
            var engine = NormalizeEngine(snapshot.Engine);
            if (!string.Equals(engine, "Sherpa-ONNX", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            lock (_lock)
            {
                EnsureSherpaRecognizerLocked(snapshot);
            }
        });
    }

    public async Task StartAsync(SpeechInputConfig config)
    {
        var engine = NormalizeEngine(config.Engine);
        RuntimeLogService.Info($"Speech input start requested. engine={engine}, mode={config.TriggerMode}");
        if (string.Equals(engine, "Windows", StringComparison.OrdinalIgnoreCase))
        {
            await StartWindowsAsync(config);
            return;
        }

        if (string.Equals(engine, "Vosk", StringComparison.OrdinalIgnoreCase))
        {
            StartVosk(config);
            return;
        }

        if (string.Equals(engine, "Sherpa-ONNX", StringComparison.OrdinalIgnoreCase))
        {
            StartSherpa(config);
            return;
        }

        throw new NotSupportedException("不支持的语音识别引擎。");
    }

    public void Stop()
    {
        StopAsync().GetAwaiter().GetResult();
    }

    public Task StopAsync()
    {
        SpeechRecognitionEngine? windowsEngine;
        PcmBufferedReadStream? windowsAudioStream;
        var shouldStopWindowsRecognition = false;
        var hadActiveRecognizer = false;

        lock (_lock)
        {
            if (_waveIn is not null)
            {
                hadActiveRecognizer = true;
                _waveIn.StopRecording();
                CleanupWaveIn();
            }

            windowsEngine = _windowsEngine;
            windowsAudioStream = _windowsAudioStream;
            shouldStopWindowsRecognition = _windowsRecognitionStarted;
            hadActiveRecognizer |= windowsEngine is not null;
            _windowsEngine = null;
            _windowsAudioStream = null;
            _windowsRecognitionStarted = false;

            if (_sherpaRecognizer is not null && _sherpaStream is not null)
            {
                _sherpaRecognizer.Reset(_sherpaStream);
            }
            _sherpaLastText = string.Empty;
            _captureSuppressed = false;
        }

        if (windowsEngine is not null)
        {
            windowsEngine.SpeechRecognized -= WindowsEngine_SpeechRecognized;
            windowsEngine.RecognizeCompleted -= WindowsEngine_RecognizeCompleted;
            windowsEngine.AudioSignalProblemOccurred -= WindowsEngine_AudioSignalProblemOccurred;
            windowsAudioStream?.Complete();

            if (shouldStopWindowsRecognition)
            {
                try
                {
                    windowsEngine.RecognizeAsyncCancel();
                    windowsEngine.RecognizeAsyncStop();
                }
                catch
                {
                    // Ignore shutdown errors.
                }
            }

            windowsEngine.Dispose();
        }

        windowsAudioStream?.Dispose();

        if (hadActiveRecognizer)
        {
            RuntimeLogService.Info("Speech input stopped.");
        }

        return Task.CompletedTask;
    }

    private void WaveIn_DataAvailable(object? sender, WaveInEventArgs e)
    {
        try
        {
            lock (_lock)
            {
                if (_captureSuppressed)
                {
                    return;
                }

                if (_windowsAudioStream is not null)
                {
                    _windowsAudioStream.Write(e.Buffer, 0, e.BytesRecorded);
                    return;
                }

                if (_recognizer is not null)
                {
                    if (_recognizer.AcceptWaveform(e.Buffer, e.BytesRecorded))
                    {
                        EmitFromJson(_recognizer.Result());
                    }

                    return;
                }

                if (_sherpaRecognizer is null || _sherpaStream is null)
                {
                    return;
                }

                ProcessSherpaAudio(e.Buffer, e.BytesRecorded);
            }
        }
        catch (Exception ex)
        {
            RuntimeLogService.Error("Speech input audio callback failed.", ex);
        }
    }

    private void WaveIn_RecordingStopped(object? sender, StoppedEventArgs e)
    {
        try
        {
            lock (_lock)
            {
                if (_recognizer is not null)
                {
                    EmitFromJson(_recognizer.FinalResult());
                }

                if (_sherpaRecognizer is not null && _sherpaStream is not null)
                {
                    EmitSherpaFinalIfAny();
                    _sherpaRecognizer.Reset(_sherpaStream);
                }

                _windowsAudioStream?.Complete();

                CleanupWaveIn();
            }
        }
        catch (Exception ex)
        {
            RuntimeLogService.Error("Speech input stop callback failed.", ex);
        }

        if (e.Exception is not null)
        {
            RuntimeLogService.Error("Speech input recording stopped with exception.", e.Exception);
        }
    }

    private void WindowsEngine_SpeechRecognized(object? sender, SpeechRecognizedEventArgs e)
    {
        var text = e.Result?.Text?.Trim();
        if (!string.IsNullOrWhiteSpace(text))
        {
            RuntimeLogService.Info($"Windows speech recognized: {ShortTextForLog(text)}");
            TextRecognized?.Invoke(this, text);
        }
    }

    private void WindowsEngine_RecognizeCompleted(object? sender, RecognizeCompletedEventArgs e)
    {
        lock (_lock)
        {
            _windowsRecognitionStarted = false;
        }

        RuntimeLogService.Info(
            $"Windows speech session completed. cancelled={e.Cancelled}, error={(e.Error is null ? "(none)" : e.Error.Message)}");
    }

    private void WindowsEngine_AudioSignalProblemOccurred(object? sender, AudioSignalProblemOccurredEventArgs e)
    {
        RuntimeLogService.Warn($"Windows speech audio signal problem: {e.AudioSignalProblem}");
    }

    private void EmitFromJson(string json)
    {
        try
        {
            var jo = JObject.Parse(json);
            var text = jo["text"]?.ToString()?.Trim();
            if (!string.IsNullOrWhiteSpace(text))
            {
                RuntimeLogService.Info($"Vosk speech recognized: {ShortTextForLog(text)}");
                TextRecognized?.Invoke(this, text);
            }
        }
        catch
        {
            // Ignore malformed result payload.
        }
    }

    private void CleanupWaveIn()
    {
        if (_waveIn is null)
        {
            return;
        }

        _waveIn.DataAvailable -= WaveIn_DataAvailable;
        _waveIn.RecordingStopped -= WaveIn_RecordingStopped;
        _waveIn.Dispose();
        _waveIn = null;
    }

    public void Dispose()
    {
        StopAsync().GetAwaiter().GetResult();
        _windowsEngine?.Dispose();
        _windowsAudioStream?.Dispose();
        _recognizer?.Dispose();
        _model?.Dispose();
        _sherpaStream?.Dispose();
        _sherpaRecognizer?.Dispose();
        _windowsEngine = null;
        _windowsAudioStream = null;
        _recognizer = null;
        _model = null;
        _sherpaStream = null;
        _sherpaRecognizer = null;
        _sherpaLoadedModelPath = string.Empty;
        _sherpaLoadedProvider = "cpu";
        _sherpaLoadedNumThreads = 1;
        _sherpaLoadedDecoding = "greedy_search";
        _modelPath = string.Empty;
        _sherpaLastText = string.Empty;
    }

    private void StartVosk(SpeechInputConfig config)
    {
        lock (_lock)
        {
            if (_waveIn is not null || _windowsEngine is not null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(config.VoskModelPath) || !Directory.Exists(config.VoskModelPath))
            {
                throw new InvalidOperationException("Vosk 模型目录不存在。请先配置 VoskModelPath。");
            }

            if (_model is null || !string.Equals(_modelPath, config.VoskModelPath, StringComparison.OrdinalIgnoreCase))
            {
                _recognizer?.Dispose();
                _model?.Dispose();
                _model = new Model(config.VoskModelPath);
                _modelPath = config.VoskModelPath;
            }

            var inputDeviceNumber = ResolveWaveInputDeviceNumber(config.MicrophoneDeviceId);
            RuntimeLogService.Info(
                $"Starting Vosk. model='{config.VoskModelPath}', input={DescribeWaveInputDevice(inputDeviceNumber)}");

            _recognizer?.Dispose();
            _recognizer = new VoskRecognizer(_model, 16000.0f);
            _sherpaStream?.Dispose();
            _sherpaRecognizer?.Dispose();
            _sherpaStream = null;
            _sherpaRecognizer = null;
            _sherpaLoadedModelPath = string.Empty;
            _sherpaLoadedProvider = "cpu";
            _sherpaLoadedNumThreads = 1;
            _sherpaLoadedDecoding = "greedy_search";
            _sherpaLastText = string.Empty;

            _waveIn = new WaveInEvent
            {
                DeviceNumber = inputDeviceNumber,
                WaveFormat = new WaveFormat(16000, 1),
                BufferMilliseconds = 120,
            };

            _waveIn.DataAvailable += WaveIn_DataAvailable;
            _waveIn.RecordingStopped += WaveIn_RecordingStopped;
            _waveIn.StartRecording();
            RuntimeLogService.Info("Vosk recording started.");
        }
    }

    private void StartSherpa(SpeechInputConfig config)
    {
        lock (_lock)
        {
            if (_waveIn is not null || _windowsEngine is not null)
            {
                return;
            }

            var modelPath = ResolveSherpaModelDirectory(config.SherpaModelPath);
            if (string.IsNullOrWhiteSpace(modelPath) || !Directory.Exists(modelPath))
            {
                throw new InvalidOperationException("Sherpa-ONNX 模型目录不存在。请先配置 SherpaModelPath，或使用内置模型。");
            }

            var provider = NormalizeSherpaProvider(config.SherpaProvider);
            var decoding = NormalizeSherpaDecoding(config.SherpaDecodingMethod);

            var inputDeviceNumber = ResolveWaveInputDeviceNumber(config.MicrophoneDeviceId);
            RuntimeLogService.Info(
                $"Starting Sherpa-ONNX. model='{modelPath}', provider={provider}, decoding={decoding}, input={DescribeWaveInputDevice(inputDeviceNumber)}");

            _recognizer?.Dispose();
            _recognizer = null;

            EnsureSherpaRecognizerLocked(config);

            _sherpaLastText = string.Empty;
            _captureSuppressed = false;

            _waveIn = new WaveInEvent
            {
                DeviceNumber = inputDeviceNumber,
                WaveFormat = new WaveFormat(16000, 16, 1),
                BufferMilliseconds = 80,
            };

            _waveIn.DataAvailable += WaveIn_DataAvailable;
            _waveIn.RecordingStopped += WaveIn_RecordingStopped;
            _waveIn.StartRecording();
            RuntimeLogService.Info("Sherpa-ONNX recording started.");
        }
    }

    public void SetCaptureSuppressed(bool suppressed, string reason = "")
    {
        var changed = false;

        lock (_lock)
        {
            if (_captureSuppressed == suppressed)
            {
                return;
            }

            _captureSuppressed = suppressed;
            changed = true;

            if (suppressed)
            {
                if (_sherpaRecognizer is not null && _sherpaStream is not null)
                {
                    _sherpaRecognizer.Reset(_sherpaStream);
                    _sherpaLastText = string.Empty;
                }
            }
        }

        if (changed)
        {
            var suffix = string.IsNullOrWhiteSpace(reason) ? string.Empty : $" ({reason})";
            RuntimeLogService.Info($"Speech input capture {(suppressed ? "suppressed" : "resumed")}{suffix}.");
        }
    }

    private Task StartWindowsAsync(SpeechInputConfig config)
    {
        lock (_lock)
        {
            if (_waveIn is not null || _windowsEngine is not null)
            {
                return Task.CompletedTask;
            }
        }

        var inputDeviceNumber = ResolveWaveInputDeviceNumber(config.MicrophoneDeviceId);
        var recognizerInfo = SelectWindowsRecognizerInfo();
        SpeechRecognitionEngine? engine = null;
        PcmBufferedReadStream? audioStream = null;
        WaveInEvent? waveIn = null;

        try
        {
            engine = recognizerInfo is null ? new SpeechRecognitionEngine() : new SpeechRecognitionEngine(recognizerInfo);
            audioStream = new PcmBufferedReadStream();

            engine.SpeechRecognized += WindowsEngine_SpeechRecognized;
            engine.RecognizeCompleted += WindowsEngine_RecognizeCompleted;
            engine.AudioSignalProblemOccurred += WindowsEngine_AudioSignalProblemOccurred;
            engine.LoadGrammar(new DictationGrammar());
            engine.SetInputToAudioStream(
                audioStream,
                new SpeechAudioFormatInfo(EncodingFormat.Pcm, 16000, 16, 1, 32000, 2, null));

            waveIn = new WaveInEvent
            {
                DeviceNumber = inputDeviceNumber,
                WaveFormat = new WaveFormat(16000, 16, 1),
                BufferMilliseconds = 120,
            };
            waveIn.DataAvailable += WaveIn_DataAvailable;
            waveIn.RecordingStopped += WaveIn_RecordingStopped;
            waveIn.StartRecording();

            engine.RecognizeAsync(RecognizeMode.Multiple);

            lock (_lock)
            {
                _windowsEngine = engine;
                _windowsAudioStream = audioStream;
                _waveIn = waveIn;
                _windowsRecognitionStarted = true;
            }

            RuntimeLogService.Info(
                $"Windows speech session started. recognizer={(engine.RecognizerInfo?.Culture.Name ?? "unknown")}, input={DescribeWaveInputDevice(inputDeviceNumber)}");

            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            try
            {
                if (waveIn is not null)
                {
                    waveIn.DataAvailable -= WaveIn_DataAvailable;
                    waveIn.RecordingStopped -= WaveIn_RecordingStopped;
                    waveIn.Dispose();
                }
            }
            catch
            {
                // Ignore cleanup errors.
            }

            if (engine is not null)
            {
                engine.SpeechRecognized -= WindowsEngine_SpeechRecognized;
                engine.RecognizeCompleted -= WindowsEngine_RecognizeCompleted;
                engine.AudioSignalProblemOccurred -= WindowsEngine_AudioSignalProblemOccurred;
                engine.Dispose();
            }

            audioStream?.Dispose();
            throw new InvalidOperationException($"Windows 语音识别启动失败: {ex.Message}", ex);
        }
    }

    private void ProcessSherpaAudio(byte[] buffer, int bytesRecorded)
    {
        if (_sherpaRecognizer is null || _sherpaStream is null)
        {
            return;
        }

        var sampleCount = bytesRecorded / 2;
        if (sampleCount <= 0)
        {
            return;
        }

        var samples = new float[sampleCount];
        for (var i = 0; i < sampleCount; i++)
        {
            var lo = buffer[i * 2];
            var hi = buffer[i * 2 + 1];
            short value = (short)(lo | (hi << 8));
            samples[i] = value / 32768.0f;
        }

        _sherpaStream.AcceptWaveform(16000, samples);

        while (_sherpaRecognizer.IsReady(_sherpaStream))
        {
            _sherpaRecognizer.Decode(_sherpaStream);
        }

        var text = _sherpaRecognizer.GetResult(_sherpaStream).Text?.Trim();
        if (!string.IsNullOrWhiteSpace(text))
        {
            _sherpaLastText = text;
        }

        if (_sherpaRecognizer.IsEndpoint(_sherpaStream))
        {
            EmitSherpaFinalIfAny();
            _sherpaRecognizer.Reset(_sherpaStream);
            _sherpaLastText = string.Empty;
        }
    }

    private void EmitSherpaFinalIfAny()
    {
        if (!string.IsNullOrWhiteSpace(_sherpaLastText))
        {
            RuntimeLogService.Info($"Sherpa-ONNX recognized: {ShortTextForLog(_sherpaLastText)}");
            TextRecognized?.Invoke(this, _sherpaLastText);
        }
    }

    private void EnsureSherpaRecognizerLocked(SpeechInputConfig config)
    {
        var modelPath = ResolveSherpaModelDirectory(config.SherpaModelPath);
        if (string.IsNullOrWhiteSpace(modelPath) || !Directory.Exists(modelPath))
        {
            throw new InvalidOperationException("Sherpa-ONNX 模型目录不存在。请先配置 SherpaModelPath，或使用内置模型。");
        }

        var provider = NormalizeSherpaProvider(config.SherpaProvider);
        var decoding = NormalizeSherpaDecoding(config.SherpaDecodingMethod);
        var numThreads = Math.Clamp(config.SherpaNumThreads, 1, 16);
        var needsRecreate =
            _sherpaRecognizer is null ||
            _sherpaStream is null ||
            !string.Equals(_sherpaLoadedModelPath, modelPath, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(_sherpaLoadedProvider, provider, StringComparison.OrdinalIgnoreCase) ||
            _sherpaLoadedNumThreads != numThreads ||
            !string.Equals(_sherpaLoadedDecoding, decoding, StringComparison.OrdinalIgnoreCase);

        if (needsRecreate)
        {
            var paths = ResolveSherpaPaths(modelPath);
            var recognizerConfig = new OnlineRecognizerConfig();
            recognizerConfig.FeatConfig.SampleRate = 16000;
            recognizerConfig.FeatConfig.FeatureDim = 80;

            if (paths.IsTransducer)
            {
                recognizerConfig.ModelConfig.Transducer.Encoder = paths.Encoder;
                recognizerConfig.ModelConfig.Transducer.Decoder = paths.Decoder;
                recognizerConfig.ModelConfig.Transducer.Joiner = paths.Joiner;
            }
            else
            {
                recognizerConfig.ModelConfig.Paraformer.Encoder = paths.ParaformerEncoder;
                recognizerConfig.ModelConfig.Paraformer.Decoder = paths.ParaformerDecoder;
            }

            recognizerConfig.ModelConfig.Tokens = paths.Tokens;
            recognizerConfig.ModelConfig.Provider = provider;
            recognizerConfig.ModelConfig.NumThreads = numThreads;
            recognizerConfig.DecodingMethod = decoding;
            recognizerConfig.MaxActivePaths = 4;
            recognizerConfig.EnableEndpoint = 1;
            recognizerConfig.Rule1MinTrailingSilence = 1.8F;
            recognizerConfig.Rule2MinTrailingSilence = 0.65F;
            recognizerConfig.Rule3MinUtteranceLength = 10.0F;

            _sherpaStream?.Dispose();
            _sherpaRecognizer?.Dispose();
            _sherpaRecognizer = new OnlineRecognizer(recognizerConfig);
            _sherpaStream = _sherpaRecognizer.CreateStream();
            _sherpaLoadedModelPath = modelPath;
            _sherpaLoadedProvider = provider;
            _sherpaLoadedNumThreads = numThreads;
            _sherpaLoadedDecoding = decoding;
            RuntimeLogService.Info("Sherpa-ONNX model loaded into memory.");
        }
        else
        {
            _sherpaRecognizer!.Reset(_sherpaStream!);
            RuntimeLogService.Info("Sherpa-ONNX reusing resident model.");
        }

        _sherpaLastText = string.Empty;
    }

    private static SherpaModelPaths ResolveSherpaPaths(string modelDir)
    {
        var tokens = Directory.EnumerateFiles(modelDir, "tokens.txt", SearchOption.AllDirectories)
            .FirstOrDefault();
        tokens ??= Directory.EnumerateFiles(modelDir, "*tokens*.txt", SearchOption.AllDirectories)
            .FirstOrDefault();

        if (string.IsNullOrWhiteSpace(tokens))
        {
            throw new InvalidOperationException("Sherpa-ONNX 模型目录缺少 tokens.txt。");
        }

        var onnxFiles = Directory.EnumerateFiles(modelDir, "*.onnx", SearchOption.AllDirectories)
            .ToList();

        if (onnxFiles.Count == 0)
        {
            throw new InvalidOperationException("Sherpa-ONNX 模型目录中没有 .onnx 文件。");
        }

        static string Name(string p) => Path.GetFileNameWithoutExtension(p).ToLowerInvariant();

        var paraEncoder = onnxFiles.FirstOrDefault(f => Name(f).Contains("encoder", StringComparison.Ordinal));
        var paraDecoder = onnxFiles.FirstOrDefault(f => Name(f).Contains("decoder", StringComparison.Ordinal));
        var hasParaformer = !string.IsNullOrWhiteSpace(paraEncoder) && !string.IsNullOrWhiteSpace(paraDecoder);

        var joiner = onnxFiles.FirstOrDefault(f => Name(f).Contains("joiner", StringComparison.Ordinal));
        if (!string.IsNullOrWhiteSpace(joiner))
        {
            var encoder = onnxFiles.FirstOrDefault(f => Name(f).Contains("encoder", StringComparison.Ordinal));
            var decoder = onnxFiles.FirstOrDefault(f => Name(f).Contains("decoder", StringComparison.Ordinal));
            if (string.IsNullOrWhiteSpace(encoder) || string.IsNullOrWhiteSpace(decoder))
            {
                throw new InvalidOperationException("Sherpa-ONNX Transducer 模型缺少 encoder/decoder/joiner 文件。");
            }

            return new SherpaModelPaths
            {
                Tokens = tokens,
                IsTransducer = true,
                Encoder = encoder,
                Decoder = decoder,
                Joiner = joiner,
            };
        }

        if (!hasParaformer)
        {
            throw new InvalidOperationException("Sherpa-ONNX 模型缺少 encoder/decoder 文件。");
        }

        return new SherpaModelPaths
        {
            Tokens = tokens,
            IsTransducer = false,
            ParaformerEncoder = paraEncoder!,
            ParaformerDecoder = paraDecoder!,
        };
    }

    private static string NormalizeEngine(string? engine)
    {
        if (string.Equals(engine, "Vosk", StringComparison.OrdinalIgnoreCase))
        {
            return "Vosk";
        }

        if (string.Equals(engine, "Paraformer-ZH-Streaming", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(engine, "paraformer-zh-streaming", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(engine, "Paraformer", StringComparison.OrdinalIgnoreCase))
        {
            return "Sherpa-ONNX";
        }

        if (string.Equals(engine, "Sherpa-ONNX", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(engine, "Sherpa", StringComparison.OrdinalIgnoreCase))
        {
            return "Sherpa-ONNX";
        }

        return "Windows";
    }

    public static string? GetBundledSherpaModelDirectory()
    {
        return TryFindSherpaModelDirectory(BundledSherpaRoot);
    }

    public static string? ResolveSherpaModelDirectory(string? configuredPath)
    {
        var configured = (configuredPath ?? string.Empty).Trim();
        if (!string.IsNullOrWhiteSpace(configured) && Directory.Exists(configured))
        {
            return configured;
        }

        return GetBundledSherpaModelDirectory();
    }

    private static string? TryFindSherpaModelDirectory(string? root)
    {
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
        {
            return null;
        }

        if (Directory.EnumerateFiles(root, "tokens.txt", SearchOption.TopDirectoryOnly).Any() &&
            Directory.EnumerateFiles(root, "*.onnx", SearchOption.TopDirectoryOnly).Any())
        {
            return root;
        }

        foreach (var dir in Directory.EnumerateDirectories(root, "*", SearchOption.AllDirectories))
        {
            if (Directory.EnumerateFiles(dir, "tokens.txt", SearchOption.TopDirectoryOnly).Any() &&
                Directory.EnumerateFiles(dir, "*.onnx", SearchOption.TopDirectoryOnly).Any())
            {
                return dir;
            }
        }

        return null;
    }

    private static string NormalizeSherpaProvider(string? provider)
    {
        var p = (provider ?? string.Empty).Trim().ToLowerInvariant();
        return p switch
        {
            "cuda" => "cuda",
            "dml" => "dml",
            _ => "cpu",
        };
    }

    private static string NormalizeSherpaDecoding(string? method)
    {
        var m = (method ?? string.Empty).Trim().ToLowerInvariant();
        return m == "modified_beam_search" ? "modified_beam_search" : "greedy_search";
    }

    private static int ResolveWaveInputDeviceNumber(string? configuredId)
    {
        var value = (configuredId ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(value) || string.Equals(value, "default", StringComparison.OrdinalIgnoreCase))
        {
            return -1;
        }

        if (int.TryParse(value, out var number) && number >= 0 && number < WaveIn.DeviceCount)
        {
            return number;
        }

        RuntimeLogService.Warn($"Configured microphone device is invalid or unavailable: '{value}'. Falling back to system default.");
        return -1;
    }

    private static RecognizerInfo? SelectWindowsRecognizerInfo()
    {
        try
        {
            var installed = SpeechRecognitionEngine.InstalledRecognizers().ToList();
            if (installed.Count == 0)
            {
                return null;
            }

            var culture = CultureInfo.CurrentUICulture;
            return installed.FirstOrDefault(x => string.Equals(x.Culture.Name, culture.Name, StringComparison.OrdinalIgnoreCase))
                ?? installed.FirstOrDefault(x => string.Equals(x.Culture.TwoLetterISOLanguageName, culture.TwoLetterISOLanguageName, StringComparison.OrdinalIgnoreCase))
                ?? installed[0];
        }
        catch
        {
            return null;
        }
    }

    private sealed class PcmBufferedReadStream : Stream
    {
        private readonly Queue<byte> _queue = new();
        private readonly object _sync = new();
        private readonly AutoResetEvent _dataSignal = new(false);
        private bool _isCompleted;
        private bool _isDisposed;

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
            // No-op.
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            while (true)
            {
                lock (_sync)
                {
                    if (_isDisposed)
                    {
                        return 0;
                    }

                    var available = Math.Min(count, _queue.Count);
                    if (available > 0)
                    {
                        for (var i = 0; i < available; i++)
                        {
                            buffer[offset + i] = _queue.Dequeue();
                        }

                        return available;
                    }

                    if (_isCompleted)
                    {
                        return 0;
                    }
                }

                _dataSignal.WaitOne(120);
            }
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (buffer is null || count <= 0)
            {
                return;
            }

            lock (_sync)
            {
                if (_isDisposed || _isCompleted)
                {
                    return;
                }

                for (var i = 0; i < count; i++)
                {
                    _queue.Enqueue(buffer[offset + i]);
                }
            }

            _dataSignal.Set();
        }

        public void Complete()
        {
            lock (_sync)
            {
                if (_isDisposed || _isCompleted)
                {
                    return;
                }

                _isCompleted = true;
            }

            _dataSignal.Set();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                lock (_sync)
                {
                    _isDisposed = true;
                    _isCompleted = true;
                    _queue.Clear();
                }

                _dataSignal.Set();
                _dataSignal.Dispose();
            }

            base.Dispose(disposing);
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
    }

    private sealed class SherpaModelPaths
    {
        public string Tokens { get; set; } = string.Empty;
        public bool IsTransducer { get; set; }
        public string Encoder { get; set; } = string.Empty;
        public string Decoder { get; set; } = string.Empty;
        public string Joiner { get; set; } = string.Empty;
        public string ParaformerEncoder { get; set; } = string.Empty;
        public string ParaformerDecoder { get; set; } = string.Empty;
    }

    private static string ShortTextForLog(string text)
    {
        var normalized = text.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return normalized.Length <= 120 ? normalized : normalized[..120] + "...";
    }

    private static string DescribeWaveInputDevice(int deviceNumber)
    {
        try
        {
            var count = WaveIn.DeviceCount;
            if (count <= 0)
            {
                return "no-input-device";
            }

            if (deviceNumber >= 0 && deviceNumber < count)
            {
                var caps = WaveIn.GetCapabilities(deviceNumber);
                return $"#{deviceNumber} {caps.ProductName}";
            }

            return $"default(WAVE_MAPPER), available={count}";
        }
        catch
        {
            return deviceNumber >= 0 ? $"#{deviceNumber}" : "default(WAVE_MAPPER)";
        }
    }
}
