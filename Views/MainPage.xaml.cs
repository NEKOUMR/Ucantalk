using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Threading;
using VRC_cantalkcn.Models;
using VRC_cantalkcn.Services;
using Windows.ApplicationModel.DataTransfer;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.System;
using Windows.UI.Core;
using Windows.UI;
using WinRT.Interop;

namespace VRC_cantalkcn.Views;

public sealed partial class MainPage : Page
{
    private readonly ConfigService _configService = new();
    private readonly TtsService _ttsService = new();
    private readonly AudioRouterService _audioRouterService = new();
    private readonly OscChatService _oscChatService = new();
    private readonly TranslatorService _translatorService = new();
    private readonly SpeechInputService _speechInputService = new();
    private readonly GptSovitsService _gptSovitsService = new();
    private readonly QrCodeService _qrCodeService = new();
    private readonly MobileControlService _mobileControlService = new();
    private readonly GlobalHotkeyService _focusHotkeyService = new();
    private readonly GlobalHotkeyService _speechHotkeyService = new();
    private readonly GlobalHotkeyService _sendHotkeyService = new();
    private readonly Microsoft.UI.Dispatching.DispatcherQueueTimer _playerTimer;
    private readonly Microsoft.UI.Dispatching.DispatcherQueueTimer _appearanceApplyTimer;
    private readonly Microsoft.UI.Dispatching.DispatcherQueueTimer _windowMoveIdleTimer;

    private AppConfig _config = new();
    private AppWindow? _appWindow;
    private bool _isLoaded;
    private bool _isSending;
    private bool _isSpeechRunning;
    private bool _isPlayerSeeking;
    private bool _isProfileUpdating;
    private bool _isSpeechPressActive;
    private int _activeProfileIndex = -1;
    private bool _isUpdatingTranslationTargets;
    private bool _isUpdatingAutoSendSwitch;
    private bool _isApplyingLocalization;
    private bool _isApplyingAppearanceUi;
    private bool _windowChromeApplied;
    private bool _isWindowMoveOptimizationActive;
    private bool _windowMoveUiWorkPending;
    private bool _hasBackgroundImageLoaded;
    private CancellationTokenSource? _playerRouteCts;
    private bool _isStartingGptApi;
    private readonly Queue<string> _speechAutoSendQueue = new();
    private readonly SemaphoreSlim _speechAutoSendGate = new(1, 1);
    private const int MaxRecentSpeechHistory = 10;
    private readonly ObservableCollection<RecentSpeechHistoryEntry> _recentSpeechHistory = new();
    private readonly ObservableCollection<QuickPhraseEntry> _quickPhrases = new();
    private DateTime _lastGptApiStartAttemptUtc = DateTime.MinValue;
    private DateTime _lastWindowPositionChangedUtc = DateTime.MinValue;
    private readonly SemaphoreSlim _speechStateGate = new(1, 1);
    private const int MaxLogCharsInUi = 220_000;
    private readonly Dictionary<TextBlock, string> _textSources = new();
    private readonly Dictionary<Button, string> _buttonContentSources = new();
    private readonly Dictionary<CheckBox, string> _checkBoxContentSources = new();
    private readonly Dictionary<NavigationViewItem, string> _navItemContentSources = new();
    private readonly Dictionary<ToggleSwitch, string> _toggleHeaderSources = new();
    private readonly Dictionary<TextBox, string> _textBoxHeaderSources = new();
    private readonly Dictionary<TextBox, string> _textBoxPlaceholderSources = new();
    private readonly Dictionary<ComboBox, string> _comboHeaderSources = new();
    private readonly Dictionary<ComboBoxItem, string> _comboItemContentSources = new();
    private const string DefaultAccentColorHex = "#8A8A8A";
    private const string DefaultBackgroundColorHex = "#1F1F1F";
    private const string DefaultLightBackgroundColorHex = "#F3F3F3";
    private const double DefaultBackgroundFrost = 35;
    private const double DefaultBackgroundBrightness = 0;
    private static readonly string AppDataRootPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Ucantalk");

    public MainPage()
    {
        InitializeComponent();
        RecentSpeechHistoryListView.ItemsSource = _recentSpeechHistory;
        QuickPhraseListView.ItemsSource = _quickPhrases;

        _speechInputService.TextRecognized += SpeechInputService_TextRecognized;
        RuntimeLogService.LogAdded += RuntimeLogService_LogAdded;

        _playerTimer = DispatcherQueue.CreateTimer();
        _playerTimer.Interval = TimeSpan.FromMilliseconds(500);
        _playerTimer.Tick += PlayerTimer_Tick;
        _appearanceApplyTimer = DispatcherQueue.CreateTimer();
        _appearanceApplyTimer.Interval = TimeSpan.FromMilliseconds(280);
        _appearanceApplyTimer.Tick += AppearanceApplyTimer_Tick;
        _windowMoveIdleTimer = DispatcherQueue.CreateTimer();
        _windowMoveIdleTimer.Interval = TimeSpan.FromMilliseconds(200);
        _windowMoveIdleTimer.IsRepeating = true;
        _windowMoveIdleTimer.Tick += WindowMoveIdleTimer_Tick;
        _focusHotkeyService.HotkeyPressed += FocusHotkeyService_HotkeyPressed;
        _speechHotkeyService.HotkeyPressed += SpeechHotkeyService_HotkeyPressed;
        _sendHotkeyService.HotkeyPressed += SendHotkeyService_HotkeyPressed;

        RootNav.SelectedItem = NavHome;
        ShowPanel("home");
        RuntimeLogService.Info("MainPage initialized.");
    }

    private async void Page_Loaded(object sender, RoutedEventArgs e)
    {
        if (_isLoaded)
        {
            return;
        }
        try
        {
            RuntimeLogService.Info("Page_Loaded start.");
            InitializePlayer();
            _config = _configService.Load();
            ApplyConfigToUi();
            SetupCustomTitleBar();
            ApplyLocalization();
            await RefreshAudioDevicesAsync();
            await RestartMobileControlServerAsync(showStatus: false);
            await UpdateWebUrlAndQrAsync();
            RegisterHotkeys();
            _ = EnsureGptApiRunningAsync(showStatus: false);
            _ = PreloadSpeechEngineAsync("startup");
            LoadLogsToUi();

            _isLoaded = true;
            SetStatus(L("就绪"));
            RuntimeLogService.Info("Page_Loaded completed.");
        }
        catch (Exception ex)
        {
            RuntimeLogService.Error("Page_Loaded failed.", ex);
            SetStatus(LF("启动失败: {0}", ex.Message));
        }
    }

    private async void Page_Unloaded(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_isLoaded)
            {
                SyncConfigFromUi();
                _configService.Save(_config);
                RuntimeLogService.Info("Config auto-saved on unload.");
            }
        }
        catch (Exception ex)
        {
            RuntimeLogService.Warn($"Auto-save on unload failed: {ex.Message}");
        }

        await _speechInputService.StopAsync();
        _speechInputService.Dispose();
        _focusHotkeyService.Dispose();
        _speechHotkeyService.Dispose();
        _sendHotkeyService.Dispose();
        RuntimeLogService.LogAdded -= RuntimeLogService_LogAdded;

        _playerTimer.Stop();
        _appearanceApplyTimer.Stop();
        _windowMoveIdleTimer.Stop();
        _playerRouteCts?.Cancel();
        _playerRouteCts?.Dispose();
        _playerRouteCts = null;
        await _mobileControlService.StopAsync();
        _mobileControlService.Dispose();
        if (_appWindow is not null)
        {
            _appWindow.Changed -= AppWindow_Changed;
            _appWindow = null;
        }
        PlayerElement.MediaPlayer?.Pause();
        PlayerElement.MediaPlayer?.Dispose();
    }

    private void Page_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (!_isLoaded || _isApplyingAppearanceUi)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(_config.BackgroundImagePath) && _config.BackgroundBlur > 0.1)
        {
            ScheduleAppearanceApply();
        }
    }

    private void AppearanceApplyTimer_Tick(Microsoft.UI.Dispatching.DispatcherQueueTimer sender, object args)
    {
        sender.Stop();
        ApplyAppearanceFromUiAndPersist();
    }

    private void RootNav_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        var tag = args.SelectedItemContainer?.Tag?.ToString() ?? "home";
        ShowPanel(tag);
    }

    private void ShowPanel(string tag)
    {
        HomePanel.Visibility = tag == "home" ? Visibility.Visible : Visibility.Collapsed;
        SettingsPanel.Visibility = tag == "settings" ? Visibility.Visible : Visibility.Collapsed;
        VoicePanel.Visibility = tag == "voice" ? Visibility.Visible : Visibility.Collapsed;
        ExtensionsPanel.Visibility = tag == "plugins" ? Visibility.Visible : Visibility.Collapsed;
        TranslationPanel.Visibility = tag == "translation" ? Visibility.Visible : Visibility.Collapsed;
        SpeechPanel.Visibility = tag == "speech" ? Visibility.Visible : Visibility.Collapsed;
        PlayerPanel.Visibility = tag == "player" ? Visibility.Visible : Visibility.Collapsed;
        LogsPanel.Visibility = tag == "logs" ? Visibility.Visible : Visibility.Collapsed;
        AboutPanel.Visibility = tag == "about" ? Visibility.Visible : Visibility.Collapsed;

        if (tag == "logs")
        {
            LoadLogsToUi();
        }

        if (tag == "about")
        {
            UpdateAboutInfo();
        }

        // Newly shown panels may only get realized after first navigation.
        // Re-apply localization so late-realized controls are translated too.
        ApplyLocalization();
    }

    private void AutoSendSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingAutoSendSwitch)
        {
            return;
        }

        var isOn = sender is ToggleSwitch sw && sw.IsOn;
        _isUpdatingAutoSendSwitch = true;
        AutoSendSwitch.IsOn = isOn;
        SpeechAutoSendSwitch.IsOn = isOn;
        _isUpdatingAutoSendSwitch = false;
    }

    private void FocusHotkeyService_HotkeyPressed(object? sender, EventArgs e)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            App.MainWindow?.Activate();
            InputTextBox.Focus(FocusState.Programmatic);
        });
    }

    private void SpeechHotkeyService_HotkeyPressed(object? sender, EventArgs e)
    {
        DispatcherQueue.TryEnqueue(() => _ = ToggleSpeechInputFromHotkeyAsync());
    }

    private void SendHotkeyService_HotkeyPressed(object? sender, EventArgs e)
    {
        DispatcherQueue.TryEnqueue(() => _ = SendCurrentTextAsync());
    }

    private void HotkeyCaptureBox_GotFocus(object sender, RoutedEventArgs e)
    {
        SetStatus(L("请按下要设置的热键"));
    }

    private void HotkeyCaptureBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (sender is not TextBox box)
        {
            return;
        }

        if (e.Key is VirtualKey.Back or VirtualKey.Delete)
        {
            box.Text = string.Empty;
            SyncConfigFromUi();
            _configService.Save(_config);
            RegisterHotkeys();
            SetStatus(L("热键已清空"));
            e.Handled = true;
            return;
        }

        var hotkey = BuildHotkeyText(e);
        if (string.IsNullOrWhiteSpace(hotkey))
        {
            SetStatus(L("该按键暂不支持，请换一个键。"));
            e.Handled = true;
            return;
        }

        box.Text = hotkey;
        SyncConfigFromUi();
        _configService.Save(_config);
        RegisterHotkeys();
        SetStatus(LF("热键已设置: {0}", hotkey));
        e.Handled = true;
    }

    private static string? BuildHotkeyText(KeyRoutedEventArgs e)
    {
        var key = e.Key;
        if (IsModifierKey(key))
        {
            return null;
        }

        var parts = new List<string>(4);
        if (IsModifierPressed(VirtualKey.Control))
        {
            parts.Add("ctrl");
        }

        if (IsModifierPressed(VirtualKey.Menu))
        {
            parts.Add("alt");
        }

        if (IsModifierPressed(VirtualKey.Shift))
        {
            parts.Add("shift");
        }

        if (IsModifierPressed(VirtualKey.LeftWindows) || IsModifierPressed(VirtualKey.RightWindows))
        {
            parts.Add("win");
        }

        var token = ToHotkeyToken(key);
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        parts.Add(token);
        return string.Join("+", parts);
    }

    private static bool IsModifierPressed(VirtualKey key)
    {
        var state = InputKeyboardSource.GetKeyStateForCurrentThread(key);
        return (state & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down;
    }

    private static bool IsModifierKey(VirtualKey key)
    {
        return key is VirtualKey.Control
            or VirtualKey.LeftControl
            or VirtualKey.RightControl
            or VirtualKey.Shift
            or VirtualKey.LeftShift
            or VirtualKey.RightShift
            or VirtualKey.Menu
            or VirtualKey.LeftMenu
            or VirtualKey.RightMenu
            or VirtualKey.LeftWindows
            or VirtualKey.RightWindows;
    }

    private static string? ToHotkeyToken(VirtualKey key)
    {
        if (key >= VirtualKey.Number0 && key <= VirtualKey.Number9)
        {
            return ((int)key - (int)VirtualKey.Number0).ToString(CultureInfo.InvariantCulture);
        }

        if (key >= VirtualKey.NumberPad0 && key <= VirtualKey.NumberPad9)
        {
            return ((int)key - (int)VirtualKey.NumberPad0).ToString(CultureInfo.InvariantCulture);
        }

        if (key >= VirtualKey.A && key <= VirtualKey.Z)
        {
            return key.ToString().ToLowerInvariant();
        }

        if (key >= VirtualKey.F1 && key <= VirtualKey.F24)
        {
            return $"f{(int)key - (int)VirtualKey.F1 + 1}";
        }

        return key switch
        {
            VirtualKey.Space => "space",
            VirtualKey.Enter => "enter",
            VirtualKey.Tab => "tab",
            VirtualKey.Escape => "esc",
            VirtualKey.Up => "up",
            VirtualKey.Down => "down",
            VirtualKey.Left => "left",
            VirtualKey.Right => "right",
            _ => null,
        };
    }

    private void RegisterHotkeys()
    {
        var window = App.MainWindow;
        if (window is null)
        {
            return;
        }

        var hwnd = WindowNative.GetWindowHandle(window);
        RegisterHotkey(_focusHotkeyService, hwnd, _config.Hotkey, "全局唤醒热键");
        RegisterHotkey(_speechHotkeyService, hwnd, _config.SpeechHotkey, "语音识别热键");
        RegisterHotkey(_sendHotkeyService, hwnd, _config.SendHotkey, "发送热键");
    }

    private void RegisterHotkey(GlobalHotkeyService service, IntPtr hwnd, string hotkey, string label)
    {
        try
        {
            service.Register(hwnd, hotkey);
        }
        catch (Exception ex)
        {
            if (!string.IsNullOrWhiteSpace(hotkey))
            {
                SetStatus($"{L(label)} {LF("热键注册失败: {0}", ex.Message)}");
            }
        }
    }

    private void SetupCustomTitleBar()
    {
        if (_windowChromeApplied)
        {
            return;
        }

        try
        {
            var win = App.MainWindow;
            if (win is null)
            {
                return;
            }

            win.ExtendsContentIntoTitleBar = true;
            win.SetTitleBar(TitleBarDragRegion);
            var hwnd = WindowNative.GetWindowHandle(win);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
            _appWindow = AppWindow.GetFromWindowId(windowId);
            _appWindow.Changed -= AppWindow_Changed;
            _appWindow.Changed += AppWindow_Changed;
            _windowChromeApplied = true;
        }
        catch (Exception ex)
        {
            RuntimeLogService.Warn($"Setup custom title bar failed: {ex.Message}");
        }
    }

    private void AppWindow_Changed(AppWindow sender, AppWindowChangedEventArgs args)
    {
        if (!(args.DidPositionChange || args.DidSizeChange))
        {
            return;
        }

        if (!ShouldUseWindowMoveOptimization())
        {
            return;
        }

        _lastWindowPositionChangedUtc = DateTime.UtcNow;
        if (_windowMoveUiWorkPending)
        {
            return;
        }

        _windowMoveUiWorkPending = true;

        DispatcherQueue.TryEnqueue(() =>
        {
            _windowMoveUiWorkPending = false;
            ApplyWindowMoveOptimization(enable: true);
            if (!_windowMoveIdleTimer.IsRunning)
            {
                _windowMoveIdleTimer.Start();
            }
        });
    }

    private void WindowMoveIdleTimer_Tick(Microsoft.UI.Dispatching.DispatcherQueueTimer sender, object args)
    {
        if ((DateTime.UtcNow - _lastWindowPositionChangedUtc).TotalMilliseconds < 180)
        {
            return;
        }

        sender.Stop();
        ApplyWindowMoveOptimization(enable: false);
    }

    private bool ShouldUseWindowMoveOptimization()
    {
        return _hasBackgroundImageLoaded && _config.BackgroundBlur > 0.1;
    }

    private ElementTheme GetConfiguredElementTheme()
    {
        return _config.ThemeMode switch
        {
            "dark" => ElementTheme.Dark,
            "light" => ElementTheme.Light,
            _ => ElementTheme.Default,
        };
    }

    private void ApplyWindowMoveOptimization(bool enable)
    {
        if (!enable && !_isWindowMoveOptimizationActive)
        {
            return;
        }

        if (enable && !ShouldUseWindowMoveOptimization())
        {
            return;
        }

        if (_isWindowMoveOptimizationActive == enable)
        {
            return;
        }

        _isWindowMoveOptimizationActive = enable;
        var elementTheme = GetConfiguredElementTheme();

        if (enable)
        {
            ApplyBackgroundFrostForWindowMove(elementTheme);
        }
        else
        {
            ApplyBackgroundFrost(_config.BackgroundBlur, elementTheme);
        }
    }

    private void ApplyBackgroundFrostForWindowMove(ElementTheme theme)
    {
        var normalized = Math.Clamp(_config.BackgroundBlur, 0, 100) / 100.0;
        if (normalized <= 0.001)
        {
            BackgroundFrostRect.Fill = null;
            BackgroundFrostRect.Opacity = 0;
            return;
        }

        var strength = Math.Pow(normalized, 1.2);
        var isLight = theme == ElementTheme.Light ||
            (theme == ElementTheme.Default && ActualTheme == ElementTheme.Light);

        var overlayColor = isLight
            ? Color.FromArgb(0xFF, 0xF3, 0xF3, 0xF3)
            : Color.FromArgb(0xFF, 0x1B, 0x1C, 0x1D);

        BackgroundFrostRect.Fill = new SolidColorBrush(overlayColor);
        BackgroundFrostRect.Opacity = isLight
            ? (0.08 + strength * 0.20)
            : (0.10 + strength * 0.24);
    }

    private async Task UpdateWebUrlAndQrAsync()
    {
        var host = ResolveMobileHostIp();
        var url = $"http://{host}:5000";
        WebUrlText.Text = url;
        QrImage.Source = await _qrCodeService.GenerateAsync(url);
    }

    private async Task RestartMobileControlServerAsync(bool showStatus)
    {
        try
        {
            var host = ResolveMobileHostIp();
            await _mobileControlService.StartAsync(
                host,
                5000,
                BuildMobileControlSnapshot,
                OnMobileControlSubmit);
            RuntimeLogService.Info($"Mobile control ready at http://{host}:5000");
        }
        catch (Exception ex)
        {
            RuntimeLogService.Error("Mobile control server start failed.", ex);
            if (showStatus)
            {
                SetStatus(LF("手机控制端启动失败: {0}", ex.Message));
            }
        }
    }

    private MobileControlSnapshot BuildMobileControlSnapshot()
    {
        var inputLanguage = _config.TextLanguage;
        if (string.IsNullOrWhiteSpace(inputLanguage))
        {
            inputLanguage = "zh";
        }

        var forcedLang = _config.Translation.MainTarget ?? string.Empty;
        var profileIndex = Math.Clamp(_config.CurrentProfile, 0, Math.Max(_config.Profiles.Count - 1, 0));

        return new MobileControlSnapshot
        {
            UiLanguage = _config.UiLanguage,
            CurrentProfileIndex = profileIndex,
            InputLanguage = inputLanguage,
            TtsForcedLanguage = forcedLang,
            GptSpeed = _config.GptSpeed,
            VolumePercent = _config.VolumePercent,
            Profiles = _config.Profiles
                .Select((p, i) => new MobileOptionItem { Index = i, Name = p.Name })
                .ToList(),
            InputLanguageOptions = new List<MobileOptionItem>
            {
                new() { Value = "zh", Label = "中文" },
                new() { Value = "en", Label = "English" },
                new() { Value = "ja", Label = "日本語" },
                new() { Value = "ko", Label = "한국어" },
            },
            TtsForcedLanguageOptions = new List<MobileOptionItem>
            {
                new() { Value = "", Label = L("不指定（默认读原文）") },
                new() { Value = "zh", Label = L("中文") },
                new() { Value = "en", Label = L("英语") },
                new() { Value = "ja", Label = L("日语") },
                new() { Value = "ko", Label = L("韩语") },
            },
        };
    }

    private void OnMobileControlSubmit(MobileControlSubmitRequest request)
    {
        DispatcherQueue.TryEnqueue(async () =>
        {
            try
            {
                if (_config.Profiles.Count > 0)
                {
                    var idx = Math.Clamp(request.ProfileIndex, 0, _config.Profiles.Count - 1);
                    if (ProfileCombo.SelectedIndex != idx)
                    {
                        ProfileCombo.SelectedIndex = idx;
                    }
                }

                SetComboValue(TextLangCombo, request.InputLanguage, "zh");
                SetMainTargetComboValue(request.TtsForcedLanguage);
                GptSpeedSlider.Value = Math.Clamp(request.GptSpeed, 0.5, 2.0);
                VolumeSlider.Value = Math.Clamp(request.VolumePercent, 0, 200);

                if (string.IsNullOrWhiteSpace(request.Text))
                {
                    RuntimeLogService.Info("Mobile control submit ignored: empty text.");
                    return;
                }

                InputTextBox.Text = request.Text.Trim();
                RuntimeLogService.Info(
                    $"Mobile control submit: profile={ProfileCombo.SelectedIndex}, lang={request.InputLanguage}, forced={request.TtsForcedLanguage}, speed={request.GptSpeed:0.0}, vol={request.VolumePercent:0}");
                await SendCurrentTextAsync();
            }
            catch (Exception ex)
            {
                RuntimeLogService.Error("Mobile control submit failed.", ex);
            }
        });
    }

    private string ResolveMobileHostIp()
    {
        var host = (_config.HostIp ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(host) ||
            string.Equals(host, "127.0.0.1", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase))
        {
            return GetLocalLanIpOrLoopback();
        }

        return host;
    }

    private static string GetLocalLanIpOrLoopback()
    {
        try
        {
            var candidates = new List<IPAddress>();
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus != OperationalStatus.Up)
                {
                    continue;
                }

                if (nic.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel)
                {
                    continue;
                }

                var props = nic.GetIPProperties();
                foreach (var unicast in props.UnicastAddresses)
                {
                    var addr = unicast.Address;
                    if (addr.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
                    {
                        continue;
                    }

                    if (IPAddress.IsLoopback(addr))
                    {
                        continue;
                    }

                    var bytes = addr.GetAddressBytes();
                    if (bytes[0] == 169 && bytes[1] == 254)
                    {
                        // Skip link-local APIPA.
                        continue;
                    }

                    candidates.Add(addr);
                }
            }

            var selected = candidates
                .OrderBy(GetLanIpPriority)
                .ThenBy(x => x.ToString(), StringComparer.Ordinal)
                .FirstOrDefault();

            if (selected is not null)
            {
                return selected.ToString();
            }
        }
        catch
        {
            // Ignore and use fallback.
        }

        return "127.0.0.1";
    }

    private static int GetLanIpPriority(IPAddress ip)
    {
        var b = ip.GetAddressBytes();
        if (b[0] == 192 && b[1] == 168)
        {
            return 0;
        }

        if (b[0] == 10)
        {
            return 1;
        }

        if (b[0] == 172 && b[1] >= 16 && b[1] <= 31)
        {
            return 2;
        }

        return 3;
    }

    private async void SendButton_Click(object sender, RoutedEventArgs e)
    {
        await SendCurrentTextAsync();
    }

    private void RecentSpeechHistorySwitch_Toggled(object sender, RoutedEventArgs e)
    {
        UpdateRecentSpeechHistoryUiState();
    }

    private async void RecentSpeechHistoryListView_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is not RecentSpeechHistoryEntry entry)
        {
            return;
        }

        var replayText = !string.IsNullOrWhiteSpace(entry.ReplayText)
            ? entry.ReplayText
            : !string.IsNullOrWhiteSpace(entry.ChatText)
                ? entry.ChatText
                : !string.IsNullOrWhiteSpace(entry.SpokenText)
                    ? entry.SpokenText
                    : entry.Text;

        if (string.IsNullOrWhiteSpace(replayText))
        {
            return;
        }

        InputTextBox.Text = replayText;
        await SendCurrentTextAsync();
    }

    private async void QuickPhraseListView_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is not QuickPhraseEntry entry || string.IsNullOrWhiteSpace(entry.Text))
        {
            return;
        }

        InputTextBox.Text = entry.Text;
        await SendCurrentTextAsync();
    }

    private void QuickPhraseAddButton_Click(object sender, RoutedEventArgs e)
    {
        var text = QuickPhraseInputBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            SetStatus(L("快捷卡片内容不能为空。"));
            return;
        }

        if (_quickPhrases.Any(x => string.Equals(x.Text, text, StringComparison.OrdinalIgnoreCase)))
        {
            SetStatus(LF("已存在同名快捷卡片: {0}", text));
            return;
        }

        _quickPhrases.Insert(0, new QuickPhraseEntry
        {
            Text = text
        });
        while (_quickPhrases.Count > 20)
        {
            _quickPhrases.RemoveAt(_quickPhrases.Count - 1);
        }

        PersistQuickPhrasesToConfig();
        QuickPhraseInputBox.Text = string.Empty;
        SetStatus(LF("快捷卡片已添加: {0}", text));
    }

    private void QuickPhraseDeleteButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: QuickPhraseEntry entry })
        {
            return;
        }

        for (var i = 0; i < _quickPhrases.Count; i++)
        {
            if (string.Equals(_quickPhrases[i].Id, entry.Id, StringComparison.Ordinal))
            {
                var text = _quickPhrases[i].Text;
                _quickPhrases.RemoveAt(i);
                PersistQuickPhrasesToConfig();
                SetStatus(LF("快捷卡片已删除: {0}", text));
                break;
            }
        }
    }

    private void PersistQuickPhrasesToConfig()
    {
        _config.QuickPhrases = _quickPhrases
            .Select(x => x.Text?.Trim() ?? string.Empty)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(20)
            .ToList();
        _configService.Save(_config);
    }

    private void AddRecentSpeechHistory(string replayText, string? chatText, string? spokenText)
    {
        if (!RecentSpeechHistorySwitch.IsOn)
        {
            return;
        }

        var entry = RecentSpeechHistoryEntry.Create(replayText, chatText, spokenText);
        if (string.IsNullOrWhiteSpace(entry.Text))
        {
            return;
        }

        var normalizedKey = !string.IsNullOrWhiteSpace(entry.ReplayText) ? entry.ReplayText : entry.Text;

        for (var i = 0; i < _recentSpeechHistory.Count; i++)
        {
            var existing = _recentSpeechHistory[i];
            var existingKey = !string.IsNullOrWhiteSpace(existing.ReplayText) ? existing.ReplayText : existing.Text;
            if (string.Equals(existingKey, normalizedKey, StringComparison.OrdinalIgnoreCase))
            {
                _recentSpeechHistory.RemoveAt(i);
                break;
            }
        }

        _recentSpeechHistory.Insert(0, entry);
        while (_recentSpeechHistory.Count > MaxRecentSpeechHistory)
        {
            _recentSpeechHistory.RemoveAt(_recentSpeechHistory.Count - 1);
        }

        PersistRecentSpeechHistoryToConfig();
        UpdateRecentSpeechHistoryUiState();

        DispatcherQueue.TryEnqueue(() =>
        {
            if (_recentSpeechHistory.Count == 0)
            {
                return;
            }

            try
            {
                RecentSpeechHistoryListView.UpdateLayout();
                RecentSpeechHistoryListView.ScrollIntoView(_recentSpeechHistory[0]);
            }
            catch (Exception ex)
            {
                RuntimeLogService.Warn($"Scroll recent speech history failed: {ex.Message}");
            }
        });
    }

    private void PersistRecentSpeechHistoryToConfig()
    {
        _config.RecentSpeechHistoryEntries = _recentSpeechHistory
            .Take(MaxRecentSpeechHistory)
            .Select(x =>
            {
                x.Normalize();
                return x.Clone();
            })
            .ToList();
        _config.RecentSpeechHistory = _config.RecentSpeechHistoryEntries
            .Select(x => !string.IsNullOrWhiteSpace(x.ReplayText) ? x.ReplayText : x.Text)
            .ToList();
        _configService.Save(_config);
    }

    private void UpdateRecentSpeechHistoryUiState()
    {
        var enabled = RecentSpeechHistorySwitch.IsOn;
        RecentSpeechHistoryPanel.Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;
    }

    private async Task SendCurrentTextAsync()
    {
        if (_isSending)
        {
            RuntimeLogService.Info("Send skipped because previous send is still in progress.");
            return;
        }

        var rawText = InputTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(rawText))
        {
            return;
        }

        _isSending = true;
        SendButton.IsEnabled = false;
        var speechCaptureSuppressed = false;

        string? generatedFile = null;

        try
        {
            SyncConfigFromUi();
            _configService.Save(_config);
            InputTextBox.Text = string.Empty;
            RuntimeLogService.Info($"Send start. Engine={_config.TtsEngine}, ProfileIndex={_config.CurrentProfile}");

            if (!_config.EnableTextOutput && !_config.EnableTts)
            {
                throw new InvalidOperationException(L("至少启用一种输出方式：文字或语音。"));
            }

            if (_config.EnableTts &&
                string.Equals(_config.TtsEngine, "GPT-SoVITS", StringComparison.OrdinalIgnoreCase))
            {
                await EnsureGptApiRunningAsync(showStatus: true);
                if (!await IsGptApiReachableAsync(_config.GptApiUrl, _config.Proxy))
                {
                    throw new InvalidOperationException(L("GPT-SoVITS API 未就绪，请检查 api.bat 和 API 地址。"));
                }

                var refAudioPath = _config.RefAudioPath?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(refAudioPath))
                {
                    throw new InvalidOperationException(L("GPT-SoVITS 需要参考音频(ref_audio_path)。请在语音引擎页选择“音频语音(参考音频路径)”。"));
                }

                if (!File.Exists(refAudioPath))
                {
                    throw new InvalidOperationException(LF("参考音频文件不存在: {0}", refAudioPath));
                }
            }

            SetStatus(L("翻译中..."));
            var translation = await _translatorService.TranslateAsync(rawText, _config.Translation, _config.Proxy);

            if (_config.EnableTextOutput && !_config.ForceSync)
            {
                TrySendOscChatbox(translation.DisplayText);
            }

            var forcedTargetForTts = _config.Translation.Enabled && translation.HasMainTargetTranslation
                ? translation.MainTargetLanguage
                : string.Empty;
            var effectiveTtsLanguage = ResolveEffectiveTtsLanguage(
                _config.TextLanguage,
                forcedTargetForTts);
            RuntimeLogService.Info(
                $"TTS language resolved. ui={_config.TextLanguage}, forcedTarget={_config.Translation.MainTarget}, appliedForced={forcedTargetForTts}, effective={effectiveTtsLanguage}");

            if (!_config.EnableTts)
            {
                if (_config.EnableTextOutput && _config.ForceSync)
                {
                    TrySendOscChatbox(translation.DisplayText);
                }

                AddRecentSpeechHistory(
                    rawText,
                    _config.EnableTextOutput ? translation.DisplayText : string.Empty,
                    string.Empty);
                SetStatus(L("已发送翻译文本（TTS已关闭）。"));
                return;
            }

            SetStatus(L("语音合成中..."));
            generatedFile = await _ttsService.GenerateSpeechFileAsync(new TtsRequest
            {
                Engine = _config.TtsEngine,
                Text = translation.TtsText,
                TextLanguage = effectiveTtsLanguage,
                PromptLanguage = _config.PromptLanguage,
                PromptText = _config.PromptText,
                RefAudioPath = _config.RefAudioPath ?? string.Empty,
                GptApiUrl = _config.GptApiUrl,
                GptSpeed = _config.GptSpeed,
                EdgeRate = _config.EdgeRate,
                EdgePitch = _config.EdgePitch,
                EdgeVoice = _config.EdgeVoice,
                FishApiKey = _config.FishApiKey,
                FishReferenceId = _config.FishReferenceId,
                CleanPunctuation = _config.CleanPunctuation,
            }, _config.Proxy);

            if (_config.EnableTextOutput && _config.ForceSync)
            {
                TrySendOscChatbox(translation.DisplayText);
            }

            AddRecentSpeechHistory(
                rawText,
                _config.EnableTextOutput ? translation.DisplayText : string.Empty,
                translation.TtsText);

            speechCaptureSuppressed = TrySuppressSpeechCaptureForPlayback();
            SetStatus(L("播放中..."));
            var volume = (float)Math.Clamp(_config.VolumePercent / 100.0, 0.0, 3.0);
            try
            {
                await _audioRouterService.PlayToDevicesAsync(
                    generatedFile,
                    _config.MonitorDeviceId,
                    _config.VrcDeviceId,
                    volume);
            }
            catch (Exception playEx)
            {
                // Device routing can fail when saved device IDs are stale or a driver is busy.
                // Retry once on the default output device to keep the send flow usable.
                await _audioRouterService.PlayToDevicesAsync(
                    generatedFile,
                    null,
                    null,
                    volume);

                SetStatus(LF("播放设备异常，已回退默认设备: {0}", playEx.Message));
            }

            SetStatus(_config.EnableTextOutput ? L("就绪") : L("已播放语音（未输出文字）。"));
        }
        catch (Exception ex)
        {
            RuntimeLogService.Error("SendCurrentTextAsync failed.", ex);
            SetStatus(LF("错误: {0}", ex.Message));
        }
        finally
        {
            if (!string.IsNullOrWhiteSpace(generatedFile) && File.Exists(generatedFile))
            {
                try
                {
                    File.Delete(generatedFile);
                }
                catch
                {
                    // Ignore cleanup failures.
                }
            }

            ResumeSpeechCaptureAfterPlayback(speechCaptureSuppressed);
            SendButton.IsEnabled = true;
            _isSending = false;
        }
    }

    private bool TrySuppressSpeechCaptureForPlayback()
    {
        if (_config.SpeechInput.CaptureWhileTtsPlaying)
        {
            return false;
        }

        if (!_isSpeechRunning)
        {
            return false;
        }

        try
        {
            _speechInputService.SetCaptureSuppressed(true, "tts-playback");
            return true;
        }
        catch (Exception ex)
        {
            RuntimeLogService.Warn($"Suppress speech capture failed: {ex.Message}");
            return false;
        }
    }

    private void ResumeSpeechCaptureAfterPlayback(bool previouslySuppressed)
    {
        if (!previouslySuppressed)
        {
            return;
        }

        try
        {
            _speechInputService.SetCaptureSuppressed(false, "tts-playback");
        }
        catch (Exception ex)
        {
            RuntimeLogService.Warn($"Resume speech capture failed: {ex.Message}");
        }
    }

    private void TrySendOscChatbox(string text)
    {
        try
        {
            _oscChatService.SendChatboxInput(text);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[osc] send failed: {ex}");
            RuntimeLogService.Error("OSC chatbox send failed.", ex);
            SetStatus(LF("聊天框发送失败: {0}", ex.Message));
        }
    }

    private async Task EnsureGptApiRunningAsync(bool showStatus)
    {
        if (!string.Equals(_config.TtsEngine, "GPT-SoVITS", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (await IsGptApiReachableAsync(_config.GptApiUrl, _config.Proxy))
        {
            return;
        }

        var batPath = _config.BatPath?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(batPath) || !File.Exists(batPath))
        {
            if (showStatus)
            {
                SetStatus(L("未找到 GPT-SoVITS 启动脚本，请在设置页配置 api.bat。"));
            }

            RuntimeLogService.Warn($"api.bat not found: '{batPath}'");

            return;
        }

        if (_isStartingGptApi)
        {
            return;
        }

        if ((DateTime.UtcNow - _lastGptApiStartAttemptUtc).TotalSeconds < 5)
        {
            return;
        }

        _isStartingGptApi = true;
        _lastGptApiStartAttemptUtc = DateTime.UtcNow;

        try
        {
            if (showStatus)
            {
                SetStatus(L("正在启动 GPT-SoVITS API..."));
            }

            RuntimeLogService.Info($"Starting GPT-SoVITS API via: {batPath}");

            var psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c \"\"{batPath}\"\"",
                WorkingDirectory = Path.GetDirectoryName(batPath) ?? AppContext.BaseDirectory,
                UseShellExecute = true,
                CreateNoWindow = false,
            };

            Process.Start(psi);

            var start = DateTime.UtcNow;
            while ((DateTime.UtcNow - start).TotalSeconds < 45)
            {
                await Task.Delay(800);
                if (await IsGptApiReachableAsync(_config.GptApiUrl, _config.Proxy))
                {
                    if (showStatus)
                    {
                        SetStatus(L("GPT-SoVITS API 已启动。"));
                    }

                    RuntimeLogService.Info("GPT-SoVITS API is reachable.");

                    return;
                }
            }

            if (showStatus)
            {
                SetStatus(L("GPT-SoVITS API 启动超时，请检查 api.bat 控制台输出。"));
            }

            RuntimeLogService.Warn("GPT-SoVITS API start timeout.");
        }
        catch (Exception ex)
        {
            RuntimeLogService.Error("Failed to start GPT-SoVITS API.", ex);
            if (showStatus)
            {
                SetStatus(LF("启动 GPT-SoVITS 失败: {0}", ex.Message));
            }
        }
        finally
        {
            _isStartingGptApi = false;
        }
    }

    private static async Task<bool> IsGptApiReachableAsync(string apiUrl, string? proxy)
    {
        if (string.IsNullOrWhiteSpace(apiUrl) || !Uri.TryCreate(apiUrl, UriKind.Absolute, out var uri))
        {
            return false;
        }

        HttpMessageHandler handler;
        if (string.IsNullOrWhiteSpace(proxy))
        {
            handler = new HttpClientHandler();
        }
        else
        {
            handler = new HttpClientHandler
            {
                Proxy = new WebProxy(proxy),
                UseProxy = true,
            };
        }

        using var client = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(1.5),
        };

        try
        {
            using var resp = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead);
            _ = resp.StatusCode;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private async void VoiceToggleButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.Equals(GetCurrentSpeechTriggerMode(), "ptt", StringComparison.OrdinalIgnoreCase))
        {
            // PTT mode is handled by pointer press/release events.
            return;
        }

        try
        {
            if (_isSpeechRunning)
            {
                await StopSpeechInputAsync();
            }
            else
            {
                await StartSpeechInputAsync();
            }
        }
        catch (Exception ex)
        {
            RuntimeLogService.Error("Voice toggle click failed.", ex);
            SetStatus(LF("语音输入错误: {0}", ex.Message));
        }
    }

    private async void VoiceToggleButton_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (!string.Equals(GetCurrentSpeechTriggerMode(), "ptt", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (_isSpeechPressActive)
        {
            return;
        }

        _isSpeechPressActive = true;
        if (sender is UIElement element)
        {
            element.CapturePointer(e.Pointer);
        }

        try
        {
            await StartSpeechInputAsync();
        }
        catch (Exception ex)
        {
            _isSpeechPressActive = false;
            RuntimeLogService.Error("Voice PTT press failed.", ex);
            SetStatus(LF("语音输入错误: {0}", ex.Message));
        }
    }

    private async void VoiceToggleButton_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        await StopSpeechInputForPttAsync(sender);
    }

    private async void VoiceToggleButton_PointerCanceled(object sender, PointerRoutedEventArgs e)
    {
        await StopSpeechInputForPttAsync(sender);
    }

    private async void VoiceToggleButton_PointerCaptureLost(object sender, PointerRoutedEventArgs e)
    {
        await StopSpeechInputForPttAsync(sender);
    }

    private async Task StopSpeechInputForPttAsync(object sender)
    {
        if (!string.Equals(GetCurrentSpeechTriggerMode(), "ptt", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (!_isSpeechPressActive)
        {
            return;
        }

        _isSpeechPressActive = false;
        if (sender is UIElement element)
        {
            element.ReleasePointerCaptures();
        }

        try
        {
            await StopSpeechInputAsync();
        }
        catch (Exception ex)
        {
            RuntimeLogService.Error("Voice PTT release failed.", ex);
            SetStatus(LF("语音输入错误: {0}", ex.Message));
        }
    }

    private async Task StartSpeechInputAsync()
    {
        await _speechStateGate.WaitAsync();
        try
        {
            if (_isSpeechRunning)
            {
                return;
            }

            SyncConfigFromUi();
            _configService.Save(_config);
            await _speechInputService.StartAsync(_config.SpeechInput);
            _isSpeechRunning = true;
            if (_config.SpeechInput.CueEnabled)
            {
                AudioCueService.PlayStart();
            }

            var mode = GetCurrentSpeechTriggerMode();
            SetStatus(mode == "ptt" ? L("按住说话中...") : L("语音输入中..."));
        }
        finally
        {
            _speechStateGate.Release();
            UpdateVoiceToggleButtonContent();
        }
    }

    private async Task StopSpeechInputAsync()
    {
        await _speechStateGate.WaitAsync();
        try
        {
            if (!_isSpeechRunning)
            {
                return;
            }

            await _speechInputService.StopAsync();
            _isSpeechRunning = false;
            lock (_speechAutoSendQueue)
            {
                _speechAutoSendQueue.Clear();
            }
            if (_config.SpeechInput.CueEnabled)
            {
                AudioCueService.PlayStop();
            }
            SetStatus(L("语音输入已停止"));
        }
        finally
        {
            _speechStateGate.Release();
            UpdateVoiceToggleButtonContent();
        }
    }

    private async Task ToggleSpeechInputFromHotkeyAsync()
    {
        try
        {
            if (_isSpeechRunning)
            {
                await StopSpeechInputAsync();
            }
            else
            {
                await StartSpeechInputAsync();
            }
        }
        catch (Exception ex)
        {
            RuntimeLogService.Error("Speech hotkey toggle failed.", ex);
            SetStatus(LF("语音输入错误: {0}", ex.Message));
        }
    }

    private async void RefreshAudioBtn_Click(object sender, RoutedEventArgs e)
    {
        await RefreshAudioDevicesAsync();
    }

    private void UiLanguageCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isApplyingLocalization)
        {
            return;
        }

        _config.UiLanguage = GetComboTagValue(UiLanguageCombo, "zh");
        ApplyLocalization();
        _ = RefreshAudioDevicesAsync();

        if (_isLoaded)
        {
            _configService.Save(_config);
            SetStatus(L("界面语言已切换"));
        }
    }

    private void SaveConfigButton_Click(object sender, RoutedEventArgs e)
    {
        SyncConfigFromUi();
        ApplyAppearanceFromConfig();
        _configService.Save(_config);
        RegisterHotkeys();
        _ = RestartMobileControlServerAsync(showStatus: false);
        _ = UpdateWebUrlAndQrAsync();
        _ = PreloadSpeechEngineAsync("save-config");
        SetStatus(L("配置已保存"));
    }

    private void SaveTranslationButton_Click(object sender, RoutedEventArgs e)
    {
        SyncConfigFromUi();
        _configService.Save(_config);
        SetStatus(L("翻译设置已保存并生效"));
    }

    private void TranslationTargetCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingTranslationTargets)
        {
            return;
        }

        if (sender is not CheckBox changed)
        {
            return;
        }

        var selected = GetSelectedTranslationTargetsFromUi();
        if (selected.Count > 3 && changed.IsChecked.GetValueOrDefault())
        {
            _isUpdatingTranslationTargets = true;
            changed.IsChecked = false;
            _isUpdatingTranslationTargets = false;
            SetStatus(L("辅助显示语言最多选择 3 个。"));
        }

        UpdateTranslationTargetAvailability();
    }

    private void PresetZhipuButton_Click(object sender, RoutedEventArgs e)
    {
        ApplyTranslationPreset("https://open.bigmodel.cn/api/paas/v4/", "glm-4-flash", keepApiKey: true);
    }

    private void PresetDeepSeekButton_Click(object sender, RoutedEventArgs e)
    {
        ApplyTranslationPreset("https://api.deepseek.com", "deepseek-chat", keepApiKey: true);
    }

    private void PresetKimiButton_Click(object sender, RoutedEventArgs e)
    {
        ApplyTranslationPreset("https://api.moonshot.cn/v1", "moonshot-v1-8k", keepApiKey: true);
    }

    private void PresetSiliconButton_Click(object sender, RoutedEventArgs e)
    {
        ApplyTranslationPreset("https://api.siliconflow.cn/v1", "Qwen/Qwen2.5-7B-Instruct", keepApiKey: true);
    }

    private void PresetOllamaButton_Click(object sender, RoutedEventArgs e)
    {
        ApplyTranslationPreset("http://localhost:11434/v1", "llama3", keepApiKey: false, keyValue: "ollama");
    }

    private void ApplyTranslationPreset(string api, string model, bool keepApiKey, string keyValue = "")
    {
        var currentPrompt = UniPromptBox.Text?.Trim() ?? string.Empty;

        UniApiBox.Text = api;
        UniModelBox.Text = model;
        if (string.IsNullOrWhiteSpace(currentPrompt) ||
            string.Equals(currentPrompt, TranslationConfig.LegacyUniversalPrompt, StringComparison.Ordinal) ||
            string.Equals(currentPrompt, TranslationConfig.StrictUniversalPromptV1, StringComparison.Ordinal) ||
            string.Equals(currentPrompt, TranslationConfig.DefaultUniversalPrompt, StringComparison.Ordinal))
        {
            UniPromptBox.Text = TranslationConfig.DefaultUniversalPrompt;
        }

        if (!keepApiKey)
        {
            UniKeyBox.Text = keyValue;
        }

        SetStatus(LF("已应用预设: {0}", model));
    }

    private async void BrowseRefAudioButton_Click(object sender, RoutedEventArgs e)
    {
        var file = await PickSingleFileAsync(new[] { ".wav", ".mp3", ".flac" });
        if (file is not null)
        {
            RefAudioPathBox.Text = file.Path;
        }
    }

    private async void BrowseVoskModelButton_Click(object sender, RoutedEventArgs e)
    {
        var folder = await PickFolderAsync();
        if (folder is not null)
        {
            VoskModelPathBox.Text = folder.Path;
        }
    }

    private async void BrowseSherpaModelButton_Click(object sender, RoutedEventArgs e)
    {
        var folder = await PickFolderAsync();
        if (folder is not null)
        {
            SherpaModelPathBox.Text = folder.Path;
        }
    }

    private void SpeechEngineCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateSpeechEngineUiState();
        _ = PreloadSpeechEngineFromUiAsync();
    }

    private void SpeechTriggerModeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateSpeechTriggerModeUiState();
        UpdateVoiceToggleButtonContent();
    }

    private void UpdateSpeechEngineUiState()
    {
        var engine = GetComboValue(SpeechEngineCombo, "Sherpa-ONNX");
        var isVosk = string.Equals(engine, "Vosk", StringComparison.OrdinalIgnoreCase);
        var isSherpa = string.Equals(engine, "Sherpa-ONNX", StringComparison.OrdinalIgnoreCase);
        var isOnnxEngine = isSherpa;

        VoskModelPathBox.IsEnabled = isVosk;
        BrowseVoskModelButton.IsEnabled = isVosk;
        VoskModelPathBox.Opacity = isVosk ? 1.0 : 0.70;

        SherpaModelRow.Visibility = isSherpa ? Visibility.Visible : Visibility.Collapsed;
        SherpaProviderCombo.Visibility = isOnnxEngine ? Visibility.Visible : Visibility.Collapsed;
        SherpaModelPathBox.IsEnabled = isSherpa;
        BrowseSherpaModelButton.IsEnabled = isSherpa;
        SherpaProviderCombo.IsEnabled = isOnnxEngine;

        UpdateSpeechTriggerModeUiState();
        UpdateVoiceToggleButtonContent();
    }

    private void UpdateSpeechTriggerModeUiState()
    {
        var engine = GetComboValue(SpeechEngineCombo, "Sherpa-ONNX");
        var supportsContinuous = SupportsContinuousTriggerMode(engine);

        if (SpeechTriggerContinuousItem is not null)
        {
            SpeechTriggerContinuousItem.IsEnabled = supportsContinuous;
        }

        var mode = GetCurrentSpeechTriggerMode();
        if (!supportsContinuous && string.Equals(mode, "continuous", StringComparison.OrdinalIgnoreCase))
        {
            SetComboTagValue(SpeechTriggerModeCombo, "toggle", "continuous");
        }
    }

    private static bool SupportsContinuousTriggerMode(string engine)
    {
        return string.Equals(engine, "Windows", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(engine, "Vosk", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(engine, "Sherpa-ONNX", StringComparison.OrdinalIgnoreCase);
    }

    private async Task PreloadSpeechEngineFromUiAsync()
    {
        if (!_isLoaded)
        {
            return;
        }

        var preheatConfig = new SpeechInputConfig
        {
            Engine = GetComboValue(SpeechEngineCombo, "Sherpa-ONNX"),
            TriggerMode = _config.SpeechInput.TriggerMode,
            MicrophoneDeviceId = SpeechMicCombo.SelectedValue?.ToString() ?? _config.SpeechInput.MicrophoneDeviceId,
            VoskModelPath = VoskModelPathBox.Text.Trim(),
            SherpaModelPath = NormalizeSherpaModelPathForSave(SherpaModelPathBox.Text),
            SherpaProvider = GetComboValue(SherpaProviderCombo, "cpu"),
            SherpaNumThreads = _config.SpeechInput.SherpaNumThreads,
            SherpaDecodingMethod = _config.SpeechInput.SherpaDecodingMethod,
            AutoSend = AutoSendSwitch.IsOn,
            CaptureWhileTtsPlaying = CaptureDuringTtsSwitch.IsOn,
        };

        await PreloadSpeechEngineAsync("engine-selection", preheatConfig);
    }

    private async Task PreloadSpeechEngineAsync(string reason, SpeechInputConfig? config = null)
    {
        try
        {
            await _speechInputService.PreloadAsync(config ?? _config.SpeechInput);
            RuntimeLogService.Info($"Speech engine preloaded ({reason}).");
        }
        catch (Exception ex)
        {
            RuntimeLogService.Warn($"Speech preload skipped ({reason}): {ex.Message}");
        }
    }

    private string GetCurrentSpeechTriggerMode()
    {
        return NormalizeSpeechTriggerMode(GetComboTagValue(SpeechTriggerModeCombo, "continuous"));
    }

    private static string NormalizeSpeechTriggerMode(string? mode)
    {
        var m = (mode ?? string.Empty).Trim().ToLowerInvariant();
        return m switch
        {
            "ptt" => "ptt",
            "toggle" => "toggle",
            "continuous" => "continuous",
            _ => "continuous",
        };
    }

    private void UpdateVoiceToggleButtonContent()
    {
        var mode = GetCurrentSpeechTriggerMode();
        if (_isSpeechRunning)
        {
            VoiceToggleButton.Content = string.Equals(mode, "ptt", StringComparison.OrdinalIgnoreCase)
                ? L("松开结束说话")
                : L("停止语音输入");
            return;
        }

        VoiceToggleButton.Content = mode switch
        {
            "ptt" => L("按住说话 (长按)"),
            "continuous" => L("开始连续识别"),
            _ => L("点击说话 (单击)")
        };
    }

    private async void BrowsePlayerFileButton_Click(object sender, RoutedEventArgs e)
    {
        var file = await PickSingleFileAsync(new[] { ".wav", ".mp3", ".flac", ".m4a" });
        if (file is not null)
        {
            LoadPlayerFile(file.Path);
        }
    }

    private async void BrowseGptModelButton_Click(object sender, RoutedEventArgs e)
    {
        var file = await PickSingleFileAsync(new[] { ".ckpt", ".bin", ".pt" });
        if (file is not null)
        {
            GptModelPathBox.Text = file.Path;
        }
    }

    private async void BrowseSovitsModelButton_Click(object sender, RoutedEventArgs e)
    {
        var file = await PickSingleFileAsync(new[] { ".pth", ".pt" });
        if (file is not null)
        {
            SovitsModelPathBox.Text = file.Path;
        }
    }

    private async void BrowseBatButton_Click(object sender, RoutedEventArgs e)
    {
        var file = await PickSingleFileAsync(new[] { ".bat", ".cmd" });
        if (file is not null)
        {
            BatPathBox.Text = file.Path;
        }
    }

    private async void BrowseBackgroundButton_Click(object sender, RoutedEventArgs e)
    {
        var file = await PickSingleFileAsync(new[] { ".png", ".jpg", ".jpeg", ".bmp", ".webp" });
        if (file is not null)
        {
            BackgroundImagePathBox.Text = file.Path;
            ScheduleAppearanceApply(immediate: true);
        }
    }

    private void ClearBackgroundButton_Click(object sender, RoutedEventArgs e)
    {
        BackgroundImagePathBox.Text = string.Empty;
        ScheduleAppearanceApply(immediate: true);
        SetStatus(L("背景图已清除。"));
    }

    private void AppearanceControl_Changed(object sender, RoutedEventArgs e)
    {
        ScheduleAppearanceApply();
    }

    private void AppearanceSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (Math.Abs(e.NewValue - e.OldValue) < 0.0001)
        {
            return;
        }

        ScheduleAppearanceApply();
    }

    private void AccentColorPicker_ColorChanged(ColorPicker sender, ColorChangedEventArgs args)
    {
        if (_isApplyingAppearanceUi)
        {
            return;
        }

        AccentColorBox.Text = ToHex(args.NewColor);
        ScheduleAppearanceApply(immediate: true);
    }

    private void BackgroundColorPicker_ColorChanged(ColorPicker sender, ColorChangedEventArgs args)
    {
        if (_isApplyingAppearanceUi)
        {
            return;
        }

        BackgroundColorBox.Text = ToHex(args.NewColor);
        ScheduleAppearanceApply(immediate: true);
    }

    private void ResetAppearanceButton_Click(object sender, RoutedEventArgs e)
    {
        _config.ThemeMode = "system";
        _config.AccentColorHex = DefaultAccentColorHex;
        _config.BackgroundColorHex = DefaultBackgroundColorHex;
        _config.BackgroundImagePath = string.Empty;
        _config.BackgroundBlur = DefaultBackgroundFrost;
        _config.BackgroundBrightness = DefaultBackgroundBrightness;

        ApplyAppearanceFromConfig();
        _configService.Save(_config);
        SetStatus(L("外观已恢复默认。"));
    }

    private void ProfileCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isProfileUpdating)
        {
            return;
        }

        if (_activeProfileIndex >= 0 && _activeProfileIndex < _config.Profiles.Count)
        {
            SyncProfileFromUi(_activeProfileIndex);
        }

        var idx = ProfileCombo.SelectedIndex;
        if (idx < 0 || idx >= _config.Profiles.Count)
        {
            return;
        }

        _config.CurrentProfile = idx;
        _activeProfileIndex = idx;
        LoadProfileToUi(_config.Profiles[idx]);
        _configService.Save(_config);
        _ = ApplySelectedProfileModelAsync();
    }

    private void NewProfileButton_Click(object sender, RoutedEventArgs e)
    {
        var baseProfile = (_activeProfileIndex >= 0 && _activeProfileIndex < _config.Profiles.Count)
            ? _config.Profiles[_activeProfileIndex]
            : new VoiceProfile();

        if (_activeProfileIndex >= 0 && _activeProfileIndex < _config.Profiles.Count)
        {
            SyncProfileFromUi(_activeProfileIndex);
        }

        var profile = new VoiceProfile
        {
            Name = GenerateUniqueProfileName(),
            GptModelPath = baseProfile.GptModelPath,
            SovitsModelPath = baseProfile.SovitsModelPath,
            RefAudioPath = baseProfile.RefAudioPath,
            PromptText = baseProfile.PromptText,
            PromptLanguage = string.IsNullOrWhiteSpace(baseProfile.PromptLanguage)
                ? GetComboValue(PromptLangCombo, "zh")
                : baseProfile.PromptLanguage,
        };
        _config.Profiles.Add(profile);
        _config.CurrentProfile = _config.Profiles.Count - 1;
        RebindProfilesAndSelect(_config.CurrentProfile);
        _configService.Save(_config);
        RuntimeLogService.Info($"New profile created: {profile.Name}");
    }

    private void RenameProfileButton_Click(object sender, RoutedEventArgs e)
    {
        RenameCurrentProfile();
    }

    private void DeleteProfileButton_Click(object sender, RoutedEventArgs e)
    {
        if (_config.Profiles.Count <= 1)
        {
            SetStatus(L("至少保留一个角色档案。"));
            return;
        }

        var idx = ProfileCombo.SelectedIndex;
        if (idx < 0 || idx >= _config.Profiles.Count)
        {
            return;
        }

        if (_activeProfileIndex >= 0 && _activeProfileIndex < _config.Profiles.Count)
        {
            SyncProfileFromUi(_activeProfileIndex);
        }

        _config.Profiles.RemoveAt(idx);
        _config.CurrentProfile = Math.Clamp(idx, 0, _config.Profiles.Count - 1);
        RebindProfilesAndSelect(_config.CurrentProfile);
        _configService.Save(_config);
    }

    private async void SaveProfileButton_Click(object sender, RoutedEventArgs e)
    {
        SyncConfigFromUi();
        _configService.Save(_config);
        await ApplySelectedProfileModelAsync();
        SetStatus(L("档案已保存。"));
    }

    private void RenameCurrentProfile()
    {
        var idx = ProfileCombo.SelectedIndex;
        if (idx < 0 || idx >= _config.Profiles.Count)
        {
            return;
        }

        var newName = ProfileNameBox.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(newName))
        {
            SetStatus(L("档案名称不能为空。"));
            return;
        }

        var duplicate = _config.Profiles
            .Where((_, i) => i != idx)
            .Any(p => string.Equals(p.Name, newName, StringComparison.OrdinalIgnoreCase));
        if (duplicate)
        {
            SetStatus(LF("已存在同名档案: {0}", newName));
            return;
        }

        _config.Profiles[idx].Name = newName;
        RebindProfilesAndSelect(idx);
        _configService.Save(_config);
        RuntimeLogService.Info($"Profile renamed. Index={idx}, Name={newName}");
        SetStatus(LF("档案已重命名为: {0}", newName));
    }

    private void OpenPluginFolderButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var pluginDir = Path.Combine(AppContext.BaseDirectory, "plugins");
            Directory.CreateDirectory(pluginDir);
            Process.Start(new ProcessStartInfo("explorer.exe", pluginDir) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            SetStatus(LF("打开插件目录失败: {0}", ex.Message));
        }
    }

    private void ReloadPluginsButton_Click(object sender, RoutedEventArgs e)
    {
        SetStatus(L("插件重载: 当前版本待实现动态插件执行。"));
    }

    private void RuntimeLogService_LogAdded(string line)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            if (LogsTextBox is null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(LogsTextBox.Text))
            {
                LogsTextBox.Text = line;
                return;
            }

            var appended = line + Environment.NewLine + LogsTextBox.Text;
            if (appended.Length > MaxLogCharsInUi)
            {
                appended = appended[..MaxLogCharsInUi];
            }

            LogsTextBox.Text = appended;
        });
    }

    private void LoadLogsToUi()
    {
        if (LogsTextBox is null)
        {
            return;
        }

        var content = RuntimeLogService.ReadAllSafe();
        LogsTextBox.Text = GetLogsForUi(content);
    }

    private void RefreshLogsButton_Click(object sender, RoutedEventArgs e)
    {
        LoadLogsToUi();
        SetStatus(L("日志已刷新。"));
    }

    private void ClearLogsButton_Click(object sender, RoutedEventArgs e)
    {
        RuntimeLogService.Clear();
        LoadLogsToUi();
        SetStatus(L("日志已清空。"));
    }

    private void OpenLogsFolderButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Directory.CreateDirectory(RuntimeLogService.LogDirectoryPath);
            Process.Start(new ProcessStartInfo("explorer.exe", RuntimeLogService.LogDirectoryPath)
            {
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            RuntimeLogService.Error("Open logs folder failed.", ex);
            SetStatus(LF("打开日志目录失败: {0}", ex.Message));
        }
    }

    private void CopyLogsButton_Click(object sender, RoutedEventArgs e)
    {
        var content = GetLogsForUi(RuntimeLogService.ReadAllSafe());
        var dataPackage = new DataPackage();
        dataPackage.SetText(content);
        Clipboard.SetContent(dataPackage);
        SetStatus(L("日志已复制到剪贴板。"));
    }

    private void OpenAppDataFolderButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Directory.CreateDirectory(AppDataRootPath);
            Process.Start(new ProcessStartInfo("explorer.exe", AppDataRootPath)
            {
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            RuntimeLogService.Error("Open app data folder failed.", ex);
            SetStatus(LF("打开数据目录失败: {0}", ex.Message));
        }
    }

    private void CopyAboutInfoButton_Click(object sender, RoutedEventArgs e)
    {
        UpdateAboutInfo();
        var info = string.Join(Environment.NewLine, new[]
        {
            "Ucantalk",
            "Author: NEKO_UMR",
            $"Version: {AboutVersionText.Text}",
            $"Install Path: {AboutInstallPathText.Text}",
            $"Data Path: {AboutDataPathText.Text}",
        });

        var dataPackage = new DataPackage();
        dataPackage.SetText(info);
        Clipboard.SetContent(dataPackage);
        SetStatus(L("版本信息已复制到剪贴板。"));
    }

    private void PlayerPlayButton_Click(object sender, RoutedEventArgs e)
    {
        var player = PlayerElement.MediaPlayer;
        if (player is null)
        {
            return;
        }

        if (player.Source is null)
        {
            if (!string.IsNullOrWhiteSpace(PlayerFileBox.Text) && File.Exists(PlayerFileBox.Text))
            {
                LoadPlayerFile(PlayerFileBox.Text);
            }

            return;
        }

        if (player.PlaybackSession.PlaybackState == MediaPlaybackState.Playing)
        {
            player.Pause();
        }
        else
        {
            player.Play();
        }
    }

    private void PlayerStopButton_Click(object sender, RoutedEventArgs e)
    {
        _playerRouteCts?.Cancel();

        var player = PlayerElement.MediaPlayer;
        if (player is null)
        {
            return;
        }

        player.Pause();
        player.PlaybackSession.Position = TimeSpan.Zero;
        PlayerPositionSlider.Value = 0;
    }

    private async void PlayerRoutePlayButton_Click(object sender, RoutedEventArgs e)
    {
        if (_playerRouteCts is not null)
        {
            _playerRouteCts.Cancel();
            return;
        }

        var path = PlayerFileBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            SetStatus(L("请选择有效的音频文件。"));
            return;
        }

        var monitorId = PlayerMonitorDeviceCombo.SelectedValue?.ToString() ?? string.Empty;
        var vrcId = PlayerVrcDeviceCombo.SelectedValue?.ToString() ?? string.Empty;
        var volume = (float)Math.Clamp(PlayerVolumeSlider.Value / 100.0, 0.0, 3.0);

        _playerRouteCts = new CancellationTokenSource();
        PlayerRoutePlayButton.Content = L("停止路由播放");
        SetStatus(L("路由播放中..."));

        try
        {
            await _audioRouterService.PlayToDevicesAsync(path, monitorId, vrcId, volume, _playerRouteCts.Token);
            SetStatus(L("路由播放完成。"));
        }
        catch (OperationCanceledException)
        {
            SetStatus(L("路由播放已停止。"));
        }
        catch (Exception ex)
        {
            RuntimeLogService.Error("Player route playback failed.", ex);
            SetStatus(LF("路由播放失败: {0}", ex.Message));
        }
        finally
        {
            _playerRouteCts?.Dispose();
            _playerRouteCts = null;
            PlayerRoutePlayButton.Content = L("路由播放");
        }
    }

    private void PlayerVolumeSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        UpdatePlayerVolumeUi();
    }

    private void UpdatePlayerVolumeUi()
    {
        PlayerVolumeLabel.Text = $"{PlayerVolumeSlider.Value:0}%";
        var player = PlayerElement.MediaPlayer;
        if (player is not null)
        {
            player.Volume = Math.Clamp(PlayerVolumeSlider.Value / 100.0, 0.0, 1.0);
        }
    }

    private void PlayerPositionSlider_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        _isPlayerSeeking = true;
    }

    private void PlayerPositionSlider_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        var player = PlayerElement.MediaPlayer;
        if (player is not null && player.PlaybackSession.NaturalDuration > TimeSpan.Zero)
        {
            player.PlaybackSession.Position = TimeSpan.FromSeconds(PlayerPositionSlider.Value);
        }

        _isPlayerSeeking = false;
    }

    private void InputTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter)
        {
            e.Handled = true;
            if (sender is TextBox box)
            {
                // Some IME/input paths can still inject a trailing newline; strip it before sending.
                box.Text = box.Text.TrimEnd('\r', '\n');
            }
            _ = SendCurrentTextAsync();
        }
    }

    private void Slider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        UpdateSliderLabels();
    }

    private void SpeechInputService_TextRecognized(object? sender, string text)
    {
        DispatcherQueue.TryEnqueue(async () =>
        {
            RuntimeLogService.Info($"Speech recognized in UI. auto_send={_config.SpeechInput.AutoSend}, text={ShortTextForLog(text)}");
            SpeechPreviewText.Text = $"{L("语音识别输出: -").Replace("-", string.Empty)}{text}";

            if (_config.SpeechInput.AutoSend)
            {
                EnqueueSpeechAutoSend(text);
                await ProcessSpeechAutoSendQueueAsync();
            }
            else
            {
                if (string.IsNullOrWhiteSpace(InputTextBox.Text))
                {
                    InputTextBox.Text = text;
                }
                else
                {
                    InputTextBox.Text += $" {text}";
                }
            }
        });
    }

    private void EnqueueSpeechAutoSend(string text)
    {
        lock (_speechAutoSendQueue)
        {
            _speechAutoSendQueue.Enqueue(text);
        }
    }

    private bool TryDequeueSpeechAutoSend(out string text)
    {
        lock (_speechAutoSendQueue)
        {
            if (_speechAutoSendQueue.Count > 0)
            {
                text = _speechAutoSendQueue.Dequeue();
                return true;
            }
        }

        text = string.Empty;
        return false;
    }

    private async Task ProcessSpeechAutoSendQueueAsync()
    {
        if (!await _speechAutoSendGate.WaitAsync(0))
        {
            return;
        }

        try
        {
            while (TryDequeueSpeechAutoSend(out var next))
            {
                while (_isSending)
                {
                    await Task.Delay(60);
                }

                InputTextBox.Text = next;
                if (_config.SpeechInput.CueEnabled)
                {
                    AudioCueService.PlaySend();
                }
                RuntimeLogService.Info($"Speech auto-send dequeued: {ShortTextForLog(next)}");
                await SendCurrentTextAsync();
            }
        }
        finally
        {
            _speechAutoSendGate.Release();
        }
    }

    private static string ShortTextForLog(string text)
    {
        var normalized = text.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return normalized.Length <= 120 ? normalized : normalized[..120] + "...";
    }

    private void InitializePlayer()
    {
        var player = new MediaPlayer();
        player.Volume = Math.Clamp(_config.PlayerVolumePercent / 100.0, 0.0, 1.0);
        player.MediaOpened += Player_MediaOpened;
        player.MediaEnded += Player_MediaEnded;
        player.PlaybackSession.PlaybackStateChanged += PlaybackSession_PlaybackStateChanged;
        PlayerElement.SetMediaPlayer(player);
        _playerTimer.Start();
    }

    private void PlaybackSession_PlaybackStateChanged(MediaPlaybackSession sender, object args)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            PlayerPlayButton.Content = sender.PlaybackState == MediaPlaybackState.Playing
                ? L("暂停")
                : L("播放 / 暂停");
        });
    }

    private void Player_MediaOpened(MediaPlayer sender, object args)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            var total = sender.PlaybackSession.NaturalDuration;
            PlayerPositionSlider.Maximum = Math.Max(1, total.TotalSeconds);
            PlayerTimeText.Text = $"00:00 / {FormatTime(total)}";
        });
    }

    private void Player_MediaEnded(MediaPlayer sender, object args)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            PlayerPositionSlider.Value = 0;
            PlayerTimeText.Text = $"00:00 / {FormatTime(sender.PlaybackSession.NaturalDuration)}";
        });
    }

    private void PlayerTimer_Tick(Microsoft.UI.Dispatching.DispatcherQueueTimer sender, object args)
    {
        var player = PlayerElement.MediaPlayer;
        if (player is null || player.Source is null)
        {
            return;
        }

        var session = player.PlaybackSession;
        if (!_isPlayerSeeking)
        {
            PlayerPositionSlider.Maximum = Math.Max(1, session.NaturalDuration.TotalSeconds);
            PlayerPositionSlider.Value = Math.Min(PlayerPositionSlider.Maximum, session.Position.TotalSeconds);
        }

        PlayerTimeText.Text = $"{FormatTime(session.Position)} / {FormatTime(session.NaturalDuration)}";
    }

    private async Task RefreshAudioDevicesAsync()
    {
        var currentMonitor = MonitorDeviceCombo.SelectedValue?.ToString() ?? _config.MonitorDeviceId;
        var currentVrc = VrcDeviceCombo.SelectedValue?.ToString() ?? _config.VrcDeviceId;
        var currentPlayerMonitor = PlayerMonitorDeviceCombo.SelectedValue?.ToString() ?? _config.PlayerMonitorDeviceId;
        var currentPlayerVrc = PlayerVrcDeviceCombo.SelectedValue?.ToString() ?? _config.PlayerVrcDeviceId;
        var currentSpeechMic = SpeechMicCombo.SelectedValue?.ToString() ?? _config.SpeechInput.MicrophoneDeviceId;

        var outputDevices = await Task.Run(_audioRouterService.GetOutputDevices);
        var inputDevices = await Task.Run(_audioRouterService.GetInputDevices);
        var speechMicDevices = new List<AudioDeviceInfo>
        {
            new() { Id = "default", Name = L("跟随系统默认") }
        };
        speechMicDevices.AddRange(inputDevices);

        MonitorDeviceCombo.ItemsSource = outputDevices;
        VrcDeviceCombo.ItemsSource = outputDevices;
        PlayerMonitorDeviceCombo.ItemsSource = outputDevices;
        PlayerVrcDeviceCombo.ItemsSource = outputDevices;
        SpeechMicCombo.ItemsSource = speechMicDevices;

        SetComboSelectedValue(MonitorDeviceCombo, currentMonitor);
        SetComboSelectedValue(VrcDeviceCombo, currentVrc);
        SetComboSelectedValue(PlayerMonitorDeviceCombo, currentPlayerMonitor);
        SetComboSelectedValue(PlayerVrcDeviceCombo, currentPlayerVrc);
        SetComboSelectedValue(SpeechMicCombo, currentSpeechMic);
        if (SpeechMicCombo.SelectedIndex < 0)
        {
            SetComboSelectedValue(SpeechMicCombo, "default");
        }
    }

    private void LoadPlayerFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return;
        }

        PlayerFileBox.Text = filePath;
        PlayerElement.MediaPlayer!.Source = MediaSource.CreateFromUri(new Uri(filePath));
        PlayerElement.MediaPlayer.Play();
    }

    private void EnsureProfiles()
    {
        _config.Profiles ??= new List<VoiceProfile>();
        if (_config.Profiles.Count == 0)
        {
            _config.Profiles.Add(new VoiceProfile { Name = "Default" });
        }

        _config.CurrentProfile = Math.Clamp(_config.CurrentProfile, 0, _config.Profiles.Count - 1);
    }

    private string GenerateUniqueProfileName()
    {
        var idx = _config.Profiles.Count + 1;
        while (true)
        {
            var name = $"Profile {idx}";
            var exists = _config.Profiles.Any(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
            if (!exists)
            {
                return name;
            }

            idx++;
        }
    }

    private void RebindProfilesAndSelect(int index)
    {
        EnsureProfiles();
        _isProfileUpdating = true;
        ProfileCombo.ItemsSource = _config.Profiles.Select(p => p.Name).ToList();
        ProfileCombo.SelectedIndex = Math.Clamp(index, 0, _config.Profiles.Count - 1);
        _isProfileUpdating = false;

        _activeProfileIndex = ProfileCombo.SelectedIndex;
        if (_activeProfileIndex >= 0 && _activeProfileIndex < _config.Profiles.Count)
        {
            LoadProfileToUi(_config.Profiles[_activeProfileIndex]);
        }
    }

    private void LoadProfileToUi(VoiceProfile profile)
    {
        ProfileNameBox.Text = profile.Name;
        GptModelPathBox.Text = profile.GptModelPath;
        SovitsModelPathBox.Text = profile.SovitsModelPath;
        RefAudioPathBox.Text = profile.RefAudioPath;
        PromptTextBox.Text = profile.PromptText;
        SetComboValue(PromptLangCombo, profile.PromptLanguage, "zh");
        ApplyProfileToConfig(profile);
    }

    private void SyncProfileFromUi(int index)
    {
        if (index < 0 || index >= _config.Profiles.Count)
        {
            return;
        }

        var profile = _config.Profiles[index];
        profile.GptModelPath = GptModelPathBox.Text.Trim();
        profile.SovitsModelPath = SovitsModelPathBox.Text.Trim();
        profile.RefAudioPath = RefAudioPathBox.Text.Trim();
        profile.PromptText = PromptTextBox.Text.Trim();
        profile.PromptLanguage = GetComboValue(PromptLangCombo, "zh");

        ApplyProfileToConfig(profile);
    }

    private void ApplyProfileToConfig(VoiceProfile profile)
    {
        _config.GptModelPath = profile.GptModelPath;
        _config.SovitsModelPath = profile.SovitsModelPath;
        _config.RefAudioPath = profile.RefAudioPath;
        _config.PromptText = profile.PromptText;
        _config.PromptLanguage = profile.PromptLanguage;
    }

    private async Task ApplySelectedProfileModelAsync()
    {
        try
        {
            await _gptSovitsService.SetModelAsync(
                _config.GptApiUrl,
                _config.GptModelPath,
                _config.SovitsModelPath,
                _config.Proxy);

            SetStatus(L("GPT-SoVITS 模型已切换。"));
        }
        catch (Exception ex)
        {
            SetStatus(LF("模型切换失败: {0}", ex.Message));
        }
    }

    private void ApplyConfigToUi()
    {
        EnsureProfiles();
        InputTextBox.Text = string.Empty;

        SetComboValue(EngineCombo, _config.TtsEngine, "Edge");
        SetComboValue(TextLangCombo, _config.TextLanguage, "zh");
        SetComboValue(PromptLangCombo, _config.PromptLanguage, "zh");
        SetComboValue(EdgeVoiceCombo, _config.EdgeVoice, "zh-CN-XiaoxiaoNeural");

        GptApiUrlBox.Text = _config.GptApiUrl;
        FishKeyBox.Text = _config.FishApiKey;
        FishRefIdBox.Text = _config.FishReferenceId;

        HostIpBox.Text = _config.HostIp;
        HotkeyBox.Text = _config.Hotkey;
        SpeechHotkeyBox.Text = _config.SpeechHotkey;
        SendHotkeyBox.Text = _config.SendHotkey;
        ProxyBox.Text = _config.Proxy;
        BatPathBox.Text = _config.BatPath;
        SetComboTagValue(UiLanguageCombo, _config.UiLanguage, "zh");
        _isApplyingAppearanceUi = true;
        SetComboTagValue(ThemeModeCombo, _config.ThemeMode, "system");
        AccentColorBox.Text = _config.AccentColorHex;
        BackgroundColorBox.Text = _config.BackgroundColorHex;
        BackgroundImagePathBox.Text = _config.BackgroundImagePath;
        BackgroundBlurSlider.Value = _config.BackgroundBlur;
        BackgroundBrightnessSlider.Value = _config.BackgroundBrightness;
        if (TryParseHexColor(_config.AccentColorHex, out var accentColor))
        {
            AccentColorPicker.Color = accentColor;
        }

        if (TryParseHexColor(_config.BackgroundColorHex, out var backgroundColor))
        {
            BackgroundColorPicker.Color = backgroundColor;
        }
        _isApplyingAppearanceUi = false;
        ApplyAppearanceFromConfig();
        _ = UpdateWebUrlAndQrAsync();

        RebindProfilesAndSelect(_config.CurrentProfile);

        EdgeRateSlider.Value = _config.EdgeRate;
        EdgePitchSlider.Value = _config.EdgePitch;
        VolumeSlider.Value = _config.VolumePercent;
        GptSpeedSlider.Value = _config.GptSpeed;
        EnableTextOutputSwitch.IsOn = _config.EnableTextOutput;
        EnableTtsSwitch.IsOn = _config.EnableTts;
        ForceSyncSwitch.IsOn = _config.ForceSync;
        CleanPuncSwitch.IsOn = _config.CleanPunctuation;
        RecentSpeechHistorySwitch.IsOn = _config.EnableRecentSpeechHistory;
        _quickPhrases.Clear();
        foreach (var phrase in (_config.QuickPhrases ?? new List<string>()).Take(20))
        {
            _quickPhrases.Add(new QuickPhraseEntry
            {
                Text = phrase
            });
        }
        _recentSpeechHistory.Clear();
        var historyEntries = _config.RecentSpeechHistoryEntries?.Count > 0
            ? _config.RecentSpeechHistoryEntries
            : (_config.RecentSpeechHistory ?? new List<string>())
                .Select(x => RecentSpeechHistoryEntry.Create(x, x, x))
                .ToList();
        foreach (var entry in historyEntries.Take(MaxRecentSpeechHistory))
        {
            entry.Normalize();
            _recentSpeechHistory.Add(entry.Clone());
        }
        UpdateRecentSpeechHistoryUiState();

        TranslationEnabledSwitch.IsOn = _config.Translation.Enabled;
        var engine = string.Equals(_config.Translation.Engine, "MyMemory", StringComparison.OrdinalIgnoreCase)
            ? "Universal"
            : _config.Translation.Engine;
        SetComboValue(TranslationEngineCombo, engine, "Universal");
        SetMainTargetComboValue(_config.Translation.MainTarget);
        SetSelectedTranslationTargetsToUi(_config.Translation.Targets);
        UniApiBox.Text = _config.Translation.UniversalApi;
        UniKeyBox.Text = _config.Translation.UniversalKey;
        UniModelBox.Text = _config.Translation.UniversalModel;
        UniPromptBox.Text = _config.Translation.UniversalPrompt;
        DeepLKeyBox.Text = _config.Translation.DeepLKey;

        SetComboValue(SpeechEngineCombo, _config.SpeechInput.Engine, "Sherpa-ONNX");
        SetComboTagValue(SpeechTriggerModeCombo, _config.SpeechInput.TriggerMode, "continuous");
        UpdateSpeechEngineUiState();
        VoskModelPathBox.Text = _config.SpeechInput.VoskModelPath;
        SherpaModelPathBox.Text = GetDisplaySherpaModelPath();
        SetComboValue(SherpaProviderCombo, _config.SpeechInput.SherpaProvider, "cpu");
        _isUpdatingAutoSendSwitch = true;
        AutoSendSwitch.IsOn = _config.SpeechInput.AutoSend;
        SpeechAutoSendSwitch.IsOn = _config.SpeechInput.AutoSend;
        _isUpdatingAutoSendSwitch = false;
        SpeechCueSwitch.IsOn = _config.SpeechInput.CueEnabled;
        CaptureDuringTtsSwitch.IsOn = _config.SpeechInput.CaptureWhileTtsPlaying;
        PlayerVolumeSlider.Value = _config.PlayerVolumePercent;
        UpdatePlayerVolumeUi();

        UpdateSliderLabels();
        UpdateVoiceToggleButtonContent();
    }

    private void SyncConfigFromUi()
    {
        EnsureProfiles();
        _config.TtsEngine = GetComboValue(EngineCombo, "Edge");
        _config.TextLanguage = GetComboValue(TextLangCombo, "zh");
        _config.EdgeVoice = GetComboValue(EdgeVoiceCombo, "zh-CN-XiaoxiaoNeural");

        _config.GptApiUrl = GptApiUrlBox.Text.Trim();
        _config.FishApiKey = FishKeyBox.Text.Trim();
        _config.FishReferenceId = FishRefIdBox.Text.Trim();

        _config.HostIp = HostIpBox.Text.Trim();
        _config.Hotkey = HotkeyBox.Text.Trim();
        _config.SpeechHotkey = SpeechHotkeyBox.Text.Trim();
        _config.SendHotkey = SendHotkeyBox.Text.Trim();
        _config.Proxy = ProxyBox.Text.Trim();
        _config.BatPath = BatPathBox.Text.Trim();
        _config.UiLanguage = GetComboTagValue(UiLanguageCombo, "zh");
        _config.ThemeMode = GetComboTagValue(ThemeModeCombo, "system");
        _config.AccentColorHex = AccentColorBox.Text.Trim();
        _config.BackgroundColorHex = BackgroundColorBox.Text.Trim();
        _config.BackgroundImagePath = BackgroundImagePathBox.Text.Trim();
        _config.BackgroundBlur = BackgroundBlurSlider.Value;
        _config.BackgroundBrightness = BackgroundBrightnessSlider.Value;

        _config.EdgeRate = EdgeRateSlider.Value;
        _config.EdgePitch = EdgePitchSlider.Value;
        _config.VolumePercent = VolumeSlider.Value;
        _config.GptSpeed = GptSpeedSlider.Value;
        _config.EnableTextOutput = EnableTextOutputSwitch.IsOn;
        _config.EnableTts = EnableTtsSwitch.IsOn;
        _config.ForceSync = ForceSyncSwitch.IsOn;
        _config.CleanPunctuation = CleanPuncSwitch.IsOn;
        _config.EnableRecentSpeechHistory = RecentSpeechHistorySwitch.IsOn;
        _config.QuickPhrases = _quickPhrases
            .Select(x => x.Text?.Trim() ?? string.Empty)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(20)
            .ToList();
        _config.RecentSpeechHistoryEntries = _recentSpeechHistory
            .Take(MaxRecentSpeechHistory)
            .Select(x =>
            {
                x.Normalize();
                return x.Clone();
            })
            .ToList();
        _config.RecentSpeechHistory = _config.RecentSpeechHistoryEntries
            .Select(x => !string.IsNullOrWhiteSpace(x.ReplayText) ? x.ReplayText : x.Text)
            .ToList();

        _config.MonitorDeviceId = MonitorDeviceCombo.SelectedValue?.ToString() ?? string.Empty;
        _config.VrcDeviceId = VrcDeviceCombo.SelectedValue?.ToString() ?? string.Empty;
        _config.PlayerMonitorDeviceId = PlayerMonitorDeviceCombo.SelectedValue?.ToString() ?? string.Empty;
        _config.PlayerVrcDeviceId = PlayerVrcDeviceCombo.SelectedValue?.ToString() ?? string.Empty;
        _config.PlayerVolumePercent = PlayerVolumeSlider.Value;
        _config.SpeechInput.MicrophoneDeviceId = SpeechMicCombo.SelectedValue?.ToString() ?? "default";

        _config.Translation.Enabled = TranslationEnabledSwitch.IsOn;
        _config.Translation.Engine = GetComboValue(TranslationEngineCombo, "Universal");
        _config.Translation.MainTarget = GetMainTargetComboValue();
        _config.Translation.Targets = GetSelectedTranslationTargetsFromUi();
        _config.Translation.UniversalApi = UniApiBox.Text.Trim();
        _config.Translation.UniversalKey = UniKeyBox.Text.Trim();
        _config.Translation.UniversalModel = UniModelBox.Text.Trim();
        _config.Translation.UniversalPrompt = UniPromptBox.Text.Trim();
        _config.Translation.DeepLKey = DeepLKeyBox.Text.Trim();

        _config.SpeechInput.Engine = GetComboValue(SpeechEngineCombo, "Sherpa-ONNX");
        _config.SpeechInput.TriggerMode = GetCurrentSpeechTriggerMode();
        _config.SpeechInput.VoskModelPath = VoskModelPathBox.Text.Trim();
        _config.SpeechInput.SherpaModelPath = NormalizeSherpaModelPathForSave(SherpaModelPathBox.Text);
        _config.SpeechInput.SherpaProvider = GetComboValue(SherpaProviderCombo, "cpu");
        _config.SpeechInput.AutoSend = AutoSendSwitch.IsOn;
        _config.SpeechInput.CueEnabled = SpeechCueSwitch.IsOn;
        _config.SpeechInput.CaptureWhileTtsPlaying = CaptureDuringTtsSwitch.IsOn;

        _config.CurrentProfile = Math.Clamp(ProfileCombo.SelectedIndex, 0, _config.Profiles.Count - 1);
        SyncProfileFromUi(_config.CurrentProfile);
    }

    private void ScheduleAppearanceApply(bool immediate = false)
    {
        if (_isApplyingAppearanceUi || !_isLoaded)
        {
            return;
        }

        if (immediate)
        {
            _appearanceApplyTimer.Stop();
            ApplyAppearanceFromUiAndPersist();
            return;
        }

        _appearanceApplyTimer.Stop();
        _appearanceApplyTimer.Start();
    }

    private void ApplyAppearanceFromUiAndPersist()
    {
        if (_isApplyingAppearanceUi)
        {
            return;
        }

        SyncAppearanceConfigFromUi();
        ApplyAppearanceFromConfig();
        _configService.Save(_config);
    }

    private void SyncAppearanceConfigFromUi()
    {
        _config.ThemeMode = GetComboTagValue(ThemeModeCombo, "system");
        _config.AccentColorHex = AccentColorBox.Text.Trim();
        _config.BackgroundColorHex = BackgroundColorBox.Text.Trim();
        _config.BackgroundImagePath = BackgroundImagePathBox.Text.Trim();
        _config.BackgroundBlur = BackgroundBlurSlider.Value;
        _config.BackgroundBrightness = BackgroundBrightnessSlider.Value;
    }

    private string GetDisplaySherpaModelPath()
    {
        return SpeechInputService.ResolveSherpaModelDirectory(_config.SpeechInput.SherpaModelPath) ?? string.Empty;
    }

    private static string NormalizeSherpaModelPathForSave(string? text)
    {
        var path = (text ?? string.Empty).Trim();
        var bundled = SpeechInputService.GetBundledSherpaModelDirectory();
        if (!string.IsNullOrWhiteSpace(bundled) &&
            string.Equals(path, bundled, StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        return path;
    }

    private void ApplyAppearanceFromConfig()
    {
        _config.ThemeMode = NormalizeThemeMode(_config.ThemeMode);
        _config.BackgroundBlur = Math.Clamp(_config.BackgroundBlur, 0, 100);
        _config.BackgroundBrightness = Math.Clamp(_config.BackgroundBrightness, -100, 100);

        var elementTheme = GetConfiguredElementTheme();
        RequestedTheme = elementTheme;
        RootNav.RequestedTheme = elementTheme;
        if (App.MainWindow?.Content is FrameworkElement root)
        {
            root.RequestedTheme = elementTheme;
        }
        ApplyWindowTitleBarTheme(elementTheme);

        var accentColor = ParseHexColorOrDefault(_config.AccentColorHex, DefaultAccentColorHex);
        _config.AccentColorHex = ToHex(accentColor);
        ApplyAccentColor(accentColor);
        ApplyAccentToUiElements(accentColor);

        var effectiveBackgroundHex = GetEffectiveBackgroundColorHex(_config.BackgroundColorHex, elementTheme);
        var backgroundColor = ParseHexColorOrDefault(effectiveBackgroundHex, DefaultBackgroundColorHex);
        _config.BackgroundColorHex = ToHex(backgroundColor);

        var imagePath = (_config.BackgroundImagePath ?? string.Empty).Trim();
        _config.BackgroundImagePath = imagePath;
        BackgroundBaseRect.Fill = new SolidColorBrush(backgroundColor);
        ApplyBackgroundToneOverlay(_config.BackgroundBrightness);

        if (!string.IsNullOrWhiteSpace(imagePath) && File.Exists(imagePath))
        {
            try
            {
                var image = CreateBackgroundBitmap(imagePath);
                BackgroundImageBrush.ImageSource = image;
                BackgroundImageRect.Visibility = Visibility.Visible;
                _hasBackgroundImageLoaded = true;
            }
            catch
            {
                BackgroundImageBrush.ImageSource = null;
                BackgroundImageRect.Visibility = Visibility.Collapsed;
                _hasBackgroundImageLoaded = false;
            }
        }
        else
        {
            BackgroundImageBrush.ImageSource = null;
            BackgroundImageRect.Visibility = Visibility.Collapsed;
            _hasBackgroundImageLoaded = false;
        }

        if (_isWindowMoveOptimizationActive && !ShouldUseWindowMoveOptimization())
        {
            _isWindowMoveOptimizationActive = false;
            _windowMoveIdleTimer.Stop();
        }

        if (_isWindowMoveOptimizationActive)
        {
            ApplyBackgroundFrostForWindowMove(elementTheme);
        }
        else
        {
            ApplyBackgroundFrost(_config.BackgroundBlur, elementTheme);
        }

        _isApplyingAppearanceUi = true;
        AccentColorBox.Text = _config.AccentColorHex;
        BackgroundColorBox.Text = _config.BackgroundColorHex;
        BackgroundImagePathBox.Text = imagePath;
        BackgroundBlurSlider.Value = _config.BackgroundBlur;
        BackgroundBrightnessSlider.Value = _config.BackgroundBrightness;
        if (TryParseHexColor(_config.AccentColorHex, out var accentPickerColor))
        {
            AccentColorPicker.Color = accentPickerColor;
        }

        if (TryParseHexColor(_config.BackgroundColorHex, out var backgroundPickerColor))
        {
            BackgroundColorPicker.Color = backgroundPickerColor;
        }
        _isApplyingAppearanceUi = false;
    }

    private void ApplyWindowTitleBarTheme(ElementTheme elementTheme)
    {
        var isLight = elementTheme == ElementTheme.Light ||
            (elementTheme == ElementTheme.Default && ActualTheme == ElementTheme.Light);

        TitleBarHost.Background = new SolidColorBrush(
            isLight
                ? Color.FromArgb(0xD8, 0xF8, 0xF8, 0xF8)
                : Color.FromArgb(0x90, 0x14, 0x14, 0x14));
        TitleBarText.Foreground = new SolidColorBrush(
            isLight
                ? Color.FromArgb(0xFF, 0x16, 0x16, 0x16)
                : Color.FromArgb(0xFF, 0xF5, 0xF5, 0xF5));

        try
        {
            var hwnd = WindowNative.GetWindowHandle(App.MainWindow);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
            var appWindow = AppWindow.GetFromWindowId(windowId);
            var titleBar = appWindow.TitleBar;

            if (AppWindowTitleBar.IsCustomizationSupported())
            {
                var transparent = Color.FromArgb(0x00, 0x00, 0x00, 0x00);
                var hover = isLight
                    ? Color.FromArgb(0x24, 0x00, 0x00, 0x00)
                    : Color.FromArgb(0x30, 0xFF, 0xFF, 0xFF);
                var pressed = isLight
                    ? Color.FromArgb(0x3A, 0x00, 0x00, 0x00)
                    : Color.FromArgb(0x48, 0xFF, 0xFF, 0xFF);
                var fg = isLight
                    ? Color.FromArgb(0xFF, 0x10, 0x10, 0x10)
                    : Color.FromArgb(0xFF, 0xF2, 0xF2, 0xF2);

                titleBar.BackgroundColor = transparent;
                titleBar.ForegroundColor = fg;
                titleBar.InactiveBackgroundColor = transparent;
                titleBar.InactiveForegroundColor = fg;
                titleBar.ButtonBackgroundColor = transparent;
                titleBar.ButtonForegroundColor = fg;
                titleBar.ButtonInactiveBackgroundColor = transparent;
                titleBar.ButtonInactiveForegroundColor = fg;
                titleBar.ButtonHoverBackgroundColor = hover;
                titleBar.ButtonHoverForegroundColor = fg;
                titleBar.ButtonPressedBackgroundColor = pressed;
                titleBar.ButtonPressedForegroundColor = fg;
            }
        }
        catch
        {
            // Ignore if title bar APIs are unavailable.
        }
    }

    private static BitmapImage CreateBackgroundBitmap(string imagePath)
    {
        return new BitmapImage(new Uri(imagePath, UriKind.Absolute));
    }

    private void ApplyBackgroundFrost(double amount, ElementTheme theme)
    {
        var normalized = Math.Clamp(amount, 0, 100) / 100.0;
        if (normalized <= 0.001)
        {
            BackgroundFrostRect.Fill = null;
            BackgroundFrostRect.Opacity = 0;
            return;
        }
        // Bias lower values for finer control and avoid "brightness-only" look.
        var strength = Math.Pow(normalized, 1.45);

        var isLight = theme == ElementTheme.Light ||
            (theme == ElementTheme.Default && ActualTheme == ElementTheme.Light);
        var tint = isLight
            ? Color.FromArgb(0xFF, 0xF8, 0xF8, 0xF8)
            : Color.FromArgb(0xFF, 0x18, 0x1A, 0x1C);
        var fallback = isLight
            ? Color.FromArgb(0xFF, 0xEE, 0xEE, 0xEE)
            : Color.FromArgb(0xFF, 0x1A, 0x1A, 0x1A);

        // If no custom background image is loaded, Acrylic offers little visual gain
        // but adds significant composition cost while moving/resizing the window.
        if (!_hasBackgroundImageLoaded)
        {
            var overlay = isLight
                ? Color.FromArgb(0xFF, 0xF1, 0xF1, 0xF1)
                : Color.FromArgb(0xFF, 0x1D, 0x1E, 0x1F);
            BackgroundFrostRect.Fill = new SolidColorBrush(overlay);
            BackgroundFrostRect.Opacity = isLight
                ? (0.08 + strength * 0.22)
                : (0.10 + strength * 0.26);
            return;
        }

        BackgroundFrostRect.Fill = new AcrylicBrush
        {
            TintColor = tint,
            TintOpacity = isLight ? (0.03 + strength * 0.14) : (0.04 + strength * 0.16),
            TintLuminosityOpacity = isLight ? (0.74 - strength * 0.20) : (0.42 - strength * 0.18),
            FallbackColor = fallback,
        };
        BackgroundFrostRect.Opacity = 0.08 + strength * 0.36;
    }

    private void ApplyBackgroundToneOverlay(double brightness)
    {
        var b = Math.Clamp(brightness, -100, 100);
        if (Math.Abs(b) < 0.1)
        {
            BackgroundToneOverlayRect.Fill = null;
            BackgroundToneOverlayRect.Opacity = 0;
            return;
        }

        if (b > 0)
        {
            BackgroundToneOverlayRect.Fill = new SolidColorBrush(Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF));
            BackgroundToneOverlayRect.Opacity = Math.Clamp(b / 100.0 * 0.45, 0, 0.45);
        }
        else
        {
            BackgroundToneOverlayRect.Fill = new SolidColorBrush(Color.FromArgb(0xFF, 0x00, 0x00, 0x00));
            BackgroundToneOverlayRect.Opacity = Math.Clamp((-b) / 100.0 * 0.65, 0, 0.65);
        }
    }

    private static string GetEffectiveBackgroundColorHex(string? configured, ElementTheme theme)
    {
        var raw = (configured ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(raw))
        {
            return theme == ElementTheme.Light ? DefaultLightBackgroundColorHex : DefaultBackgroundColorHex;
        }

        if (raw.Equals(DefaultBackgroundColorHex, StringComparison.OrdinalIgnoreCase) && theme == ElementTheme.Light)
        {
            return DefaultLightBackgroundColorHex;
        }

        if (raw.Equals(DefaultLightBackgroundColorHex, StringComparison.OrdinalIgnoreCase) && theme == ElementTheme.Dark)
        {
            return DefaultBackgroundColorHex;
        }

        return raw;
    }

    private static string NormalizeThemeMode(string? mode)
    {
        var value = (mode ?? string.Empty).Trim().ToLowerInvariant();
        return value switch
        {
            "dark" => "dark",
            "light" => "light",
            _ => "system",
        };
    }

    private static Color ParseHexColorOrDefault(string? input, string fallback)
    {
        if (TryParseHexColor(input, out var color))
        {
            return color;
        }

        if (TryParseHexColor(fallback, out color))
        {
            return color;
        }

        return Color.FromArgb(0xFF, 0x4C, 0xC2, 0xFF);
    }

    private static bool TryParseHexColor(string? input, out Color color)
    {
        color = Color.FromArgb(0x00, 0x00, 0x00, 0x00);
        var text = (input ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        if (text.StartsWith("#", StringComparison.Ordinal))
        {
            text = text[1..];
        }

        if (text.Length != 6 && text.Length != 8)
        {
            return false;
        }

        if (!uint.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value))
        {
            return false;
        }

        if (text.Length == 6)
        {
            color = Color.FromArgb(
                0xFF,
                (byte)((value >> 16) & 0xFF),
                (byte)((value >> 8) & 0xFF),
                (byte)(value & 0xFF));
        }
        else
        {
            color = Color.FromArgb(
                (byte)((value >> 24) & 0xFF),
                (byte)((value >> 16) & 0xFF),
                (byte)((value >> 8) & 0xFF),
                (byte)(value & 0xFF));
        }

        return true;
    }

    private static string ToHex(Color color)
    {
        return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
    }

    private static void ApplyAccentColor(Color accent)
    {
        if (Application.Current?.Resources is not ResourceDictionary resources)
        {
            return;
        }

        var dark1 = ChangeLuminosity(accent, -0.10);
        var dark2 = ChangeLuminosity(accent, -0.20);
        var dark3 = ChangeLuminosity(accent, -0.30);
        var light1 = ChangeLuminosity(accent, 0.12);
        var light2 = ChangeLuminosity(accent, 0.24);
        var light3 = ChangeLuminosity(accent, 0.36);

        resources["SystemAccentColor"] = accent;
        resources["SystemAccentColorDark1"] = dark1;
        resources["SystemAccentColorDark2"] = dark2;
        resources["SystemAccentColorDark3"] = dark3;
        resources["SystemAccentColorLight1"] = light1;
        resources["SystemAccentColorLight2"] = light2;
        resources["SystemAccentColorLight3"] = light3;

        resources["Primary"] = accent;
        resources["PrimaryBrush"] = new SolidColorBrush(accent);
    }

    private void ApplyAccentToUiElements(Color accent)
    {
        var accentBrush = new SolidColorBrush(accent);
        var soft = new SolidColorBrush(Color.FromArgb(0x40, accent.R, accent.G, accent.B));
        var textOnAccent = new SolidColorBrush(Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF));

        RootNav.Resources["NavigationViewSelectionIndicatorForeground"] = accentBrush;
        RootNav.Resources["NavigationViewItemForegroundSelected"] = accentBrush;
        RootNav.Resources["NavigationViewItemBackgroundSelected"] = soft;

        StatusText.Foreground = accentBrush;
        WebUrlText.Foreground = accentBrush;

        SendButton.Background = accentBrush;
        SendButton.Foreground = textOnAccent;
    }

    private static Color ChangeLuminosity(Color source, double delta)
    {
        static byte Shift(byte c, double d)
        {
            var v = d >= 0
                ? c + ((255 - c) * d)
                : c * (1 + d);
            return (byte)Math.Clamp((int)Math.Round(v), 0, 255);
        }

        return Color.FromArgb(
            source.A,
            Shift(source.R, delta),
            Shift(source.G, delta),
            Shift(source.B, delta));
    }

    private void UpdateSliderLabels()
    {
        // WinUI can fire ValueChanged during InitializeComponent before all named
        // TextBlocks are wired, so guard against early calls.
        if (EdgeRateLabel is null || EdgePitchLabel is null || VolumeLabel is null || GptSpeedLabel is null)
        {
            return;
        }

        EdgeRateLabel.Text = $"{EdgeRateSlider.Value:+0;-0;0}%";
        EdgePitchLabel.Text = $"{EdgePitchSlider.Value:+0;-0;0}Hz";
        VolumeLabel.Text = $"{VolumeSlider.Value:0}%";
        GptSpeedLabel.Text = $"{GptSpeedSlider.Value:0.0}x";
    }

    private static string GetComboValue(ComboBox comboBox, string fallback)
    {
        return comboBox.SelectedItem?.ToString() ?? fallback;
    }

    private static void SetComboValue(ComboBox comboBox, string? value, string fallback)
    {
        value ??= fallback;
        foreach (var item in comboBox.Items)
        {
            if (string.Equals(item?.ToString(), value, StringComparison.OrdinalIgnoreCase))
            {
                comboBox.SelectedItem = item;
                return;
            }
        }

        foreach (var item in comboBox.Items)
        {
            if (string.Equals(item?.ToString(), fallback, StringComparison.OrdinalIgnoreCase))
            {
                comboBox.SelectedItem = item;
                return;
            }
        }

        if (comboBox.Items.Count > 0)
        {
            comboBox.SelectedIndex = 0;
        }
    }

    private static void SetComboSelectedValue(ComboBox comboBox, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            comboBox.SelectedIndex = -1;
            return;
        }

        comboBox.SelectedValue = value;
    }

    private string GetMainTargetComboValue()
    {
        if (MainTargetCombo.SelectedItem is ComboBoxItem item)
        {
            return item.Tag?.ToString() ?? string.Empty;
        }

        return string.Empty;
    }

    private void SetMainTargetComboValue(string? value)
    {
        value ??= string.Empty;
        foreach (var item in MainTargetCombo.Items)
        {
            if (item is ComboBoxItem comboItem &&
                string.Equals(comboItem.Tag?.ToString() ?? string.Empty, value, StringComparison.OrdinalIgnoreCase))
            {
                MainTargetCombo.SelectedItem = comboItem;
                return;
            }
        }

        MainTargetCombo.SelectedIndex = 0;
    }

    private void SetSelectedTranslationTargetsToUi(IEnumerable<string>? targets)
    {
        var selected = new HashSet<string>((targets ?? Enumerable.Empty<string>())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim()), StringComparer.OrdinalIgnoreCase);

        _isUpdatingTranslationTargets = true;
        foreach (var (code, checkBox) in GetTranslationTargetCheckBoxes())
        {
            checkBox.IsChecked = selected.Contains(code);
        }

        UpdateTranslationTargetAvailability();
        _isUpdatingTranslationTargets = false;
    }

    private void UpdateTranslationTargetAvailability()
    {
        var selectedCount = GetSelectedTranslationTargetsFromUi().Count;
        var locked = selectedCount >= 3;

        foreach (var (_, checkBox) in GetTranslationTargetCheckBoxes())
        {
            if (checkBox.IsChecked.GetValueOrDefault())
            {
                checkBox.IsEnabled = true;
            }
            else
            {
                checkBox.IsEnabled = !locked;
            }
        }
    }

    private List<string> GetSelectedTranslationTargetsFromUi()
    {
        return GetTranslationTargetCheckBoxes()
            .Where(x => x.CheckBox.IsChecked.GetValueOrDefault())
            .Select(x => x.Code)
            .Take(3)
            .ToList();
    }

    private IEnumerable<(string Code, CheckBox CheckBox)> GetTranslationTargetCheckBoxes()
    {
        yield return ("zh", TargetZhCheckBox);
        yield return ("en", TargetEnCheckBox);
        yield return ("ja", TargetJaCheckBox);
        yield return ("ko", TargetKoCheckBox);
        yield return ("ru", TargetRuCheckBox);
        yield return ("fr", TargetFrCheckBox);
        yield return ("de", TargetDeCheckBox);
        yield return ("es", TargetEsCheckBox);
        yield return ("th", TargetThCheckBox);
        yield return ("vi", TargetViCheckBox);
        yield return ("id", TargetIdCheckBox);
        yield return ("ar", TargetArCheckBox);
    }

    private string L(string sourceText)
    {
        return UiLocalizationService.Translate(_config.UiLanguage, sourceText);
    }

    private string LF(string sourceFormat, params object[] args)
    {
        return string.Format(L(sourceFormat), args);
    }

    private static string GetComboTagValue(ComboBox comboBox, string fallback)
    {
        if (comboBox.SelectedItem is ComboBoxItem item)
        {
            return item.Tag?.ToString() ?? fallback;
        }

        return fallback;
    }

    private static void SetComboTagValue(ComboBox comboBox, string? value, string fallback)
    {
        var expected = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        foreach (var item in comboBox.Items)
        {
            if (item is ComboBoxItem comboItem &&
                string.Equals(comboItem.Tag?.ToString() ?? string.Empty, expected, StringComparison.OrdinalIgnoreCase))
            {
                comboBox.SelectedItem = comboItem;
                return;
            }
        }

        foreach (var item in comboBox.Items)
        {
            if (item is ComboBoxItem comboItem &&
                string.Equals(comboItem.Tag?.ToString() ?? string.Empty, fallback, StringComparison.OrdinalIgnoreCase))
            {
                comboBox.SelectedItem = comboItem;
                return;
            }
        }

        if (comboBox.Items.Count > 0)
        {
            comboBox.SelectedIndex = 0;
        }
    }

    private void CaptureLocalizationSources()
    {
        foreach (var node in EnumerateVisualTree(this))
        {
            switch (node)
            {
                case TextBlock textBlock when !string.IsNullOrWhiteSpace(textBlock.Text):
                    _textSources.TryAdd(textBlock, textBlock.Text);
                    break;
                case Button button when button.Content is string buttonText && !string.IsNullOrWhiteSpace(buttonText):
                    _buttonContentSources.TryAdd(button, buttonText);
                    break;
                case CheckBox checkBox when checkBox.Content is string checkText && !string.IsNullOrWhiteSpace(checkText):
                    _checkBoxContentSources.TryAdd(checkBox, checkText);
                    break;
                case NavigationViewItem navItem when navItem.Content is string navText && !string.IsNullOrWhiteSpace(navText):
                    _navItemContentSources.TryAdd(navItem, navText);
                    break;
                case ToggleSwitch toggleSwitch when toggleSwitch.Header is string headerText && !string.IsNullOrWhiteSpace(headerText):
                    _toggleHeaderSources.TryAdd(toggleSwitch, headerText);
                    break;
                case TextBox textBox:
                    if (textBox.Header is string tbHeader && !string.IsNullOrWhiteSpace(tbHeader))
                    {
                        _textBoxHeaderSources.TryAdd(textBox, tbHeader);
                    }

                    if (!string.IsNullOrWhiteSpace(textBox.PlaceholderText))
                    {
                        _textBoxPlaceholderSources.TryAdd(textBox, textBox.PlaceholderText);
                    }
                    break;
                case ComboBox comboBox:
                    if (comboBox.Header is string cbHeader && !string.IsNullOrWhiteSpace(cbHeader))
                    {
                        _comboHeaderSources.TryAdd(comboBox, cbHeader);
                    }
                    break;
                case ComboBoxItem comboBoxItem when comboBoxItem.Content is string comboItemText && !string.IsNullOrWhiteSpace(comboItemText):
                    _comboItemContentSources.TryAdd(comboBoxItem, comboItemText);
                    break;
            }
        }
    }

    private void ApplyLocalization()
    {
        if (_isApplyingLocalization)
        {
            return;
        }

        _isApplyingLocalization = true;
        _config.UiLanguage = UiLocalizationService.NormalizeLanguage(_config.UiLanguage);
        SetComboTagValue(UiLanguageCombo, _config.UiLanguage, "zh");

        CaptureLocalizationSources();

        foreach (var (tb, source) in _textSources)
        {
            tb.Text = L(source);
        }

        foreach (var (btn, source) in _buttonContentSources)
        {
            btn.Content = L(source);
        }

        foreach (var (cb, source) in _checkBoxContentSources)
        {
            cb.Content = L(source);
        }

        foreach (var (nav, source) in _navItemContentSources)
        {
            nav.Content = L(source);
        }

        foreach (var (sw, source) in _toggleHeaderSources)
        {
            sw.Header = L(source);
        }

        foreach (var (tb, source) in _textBoxHeaderSources)
        {
            tb.Header = L(source);
        }

        foreach (var (tb, source) in _textBoxPlaceholderSources)
        {
            tb.PlaceholderText = L(source);
        }

        foreach (var (cb, source) in _comboHeaderSources)
        {
            cb.Header = L(source);
        }

        foreach (var (item, source) in _comboItemContentSources)
        {
            item.Content = L(source);
        }

        UpdateVoiceToggleButtonContent();
        var isPlaying = PlayerElement.MediaPlayer?.PlaybackSession.PlaybackState == MediaPlaybackState.Playing;
        PlayerPlayButton.Content = isPlaying ? L("暂停") : L("播放 / 暂停");
        UpdateAboutInfo();

        _isApplyingLocalization = false;
    }

    private void UpdateAboutInfo()
    {
        if (AboutVersionText is null || AboutInstallPathText is null || AboutDataPathText is null)
        {
            return;
        }

        var assembly = typeof(MainPage).Assembly;
        var fileVersion = assembly
            .GetCustomAttribute<AssemblyFileVersionAttribute>()?
            .Version;
        var assemblyVersion = assembly.GetName().Version?.ToString();
        AboutVersionText.Text = string.IsNullOrWhiteSpace(fileVersion) ? (assemblyVersion ?? "-") : fileVersion;
        AboutInstallPathText.Text = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
        AboutDataPathText.Text = AppDataRootPath;
    }

    private static string GetLogsForUi(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return string.Empty;
        }

        var lines = content
            .Replace("\r\n", "\n")
            .Replace('\r', '\n')
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .ToArray();
        Array.Reverse(lines);

        var reversed = string.Join(Environment.NewLine, lines);
        if (reversed.Length > MaxLogCharsInUi)
        {
            reversed = reversed[..MaxLogCharsInUi];
        }

        return reversed;
    }

    private static IEnumerable<DependencyObject> EnumerateVisualTree(DependencyObject root)
    {
        yield return root;
        var childCount = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < childCount; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            foreach (var nested in EnumerateVisualTree(child))
            {
                yield return nested;
            }
        }
    }

    private async Task<StorageFile?> PickSingleFileAsync(IEnumerable<string> fileTypes)
    {
        var picker = new FileOpenPicker();
        foreach (var type in fileTypes)
        {
            picker.FileTypeFilter.Add(type);
        }

        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(App.MainWindow));
        return await picker.PickSingleFileAsync();
    }

    private async Task<StorageFolder?> PickFolderAsync()
    {
        var picker = new FolderPicker();
        picker.FileTypeFilter.Add("*");
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(App.MainWindow));
        return await picker.PickSingleFolderAsync();
    }

    private static string FormatTime(TimeSpan ts)
    {
        if (ts.TotalHours >= 1)
        {
            return ts.ToString(@"hh\:mm\:ss");
        }

        return ts.ToString(@"mm\:ss");
    }

    private static string ResolveEffectiveTtsLanguage(string? uiInputLanguage, string? forcedTarget)
    {
        var forced = NormalizeSpeechLanguageCode(forcedTarget);
        if (!string.IsNullOrWhiteSpace(forced))
        {
            return forced;
        }

        var ui = NormalizeSpeechLanguageCode(uiInputLanguage);
        return string.IsNullOrWhiteSpace(ui) ? "zh" : ui;
    }

    private static string NormalizeSpeechLanguageCode(string? value)
    {
        var v = (value ?? string.Empty).Trim().ToLowerInvariant();
        return v switch
        {
            "zh" or "zh-cn" => "zh",
            "en" or "en-us" => "en",
            "ja" or "ja-jp" => "ja",
            "ko" or "ko-kr" => "ko",
            _ => string.Empty,
        };
    }

    private void SetStatus(string text)
    {
        StatusText.Text = text;
    }
}
