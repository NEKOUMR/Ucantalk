using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Windowing;
using System.Text;
using Windows.Graphics;
using WinRT.Interop;

namespace VRC_cantalkcn
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application
    {
        private static readonly object LogLock = new();
        private Window? window;
        public static Window MainWindow { get; private set; } = null!;

        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
            UnhandledException += App_UnhandledException;

            Log("App constructor start.");
            try
            {
                this.InitializeComponent();
                Log("InitializeComponent success.");
            }
            catch (Exception ex)
            {
                Log($"InitializeComponent failed: {ex}");
                throw;
            }
        }

        /// <summary>
        /// Invoked when the application is launched normally by the end user.  Other entry points
        /// will be used such as when the application is launched to open a specific file.
        /// </summary>
        /// <param name="e">Details about the launch request and process.</param>
        protected override void OnLaunched(LaunchActivatedEventArgs e)
        {
            Log($"OnLaunched start. Args: '{e.Arguments}'");
            try
            {
                window ??= new Window();
                MainWindow = window;
                window.Title = "Ucantalk";

                if (window.Content is not Frame rootFrame)
                {
                    rootFrame = new Frame();
                    rootFrame.NavigationFailed += OnNavigationFailed;
                    window.Content = rootFrame;
                }

                var navOk = rootFrame.Navigate(typeof(MainPage), e.Arguments);
                Log($"Navigate MainPage result: {navOk}");
                if (!navOk)
                {
                    ShowStartupErrorContent("MainPage navigation returned false.");
                }
                window.Activate();
                EnsureWindowVisible(window);
                Log("Window activated.");
            }
            catch (Exception ex)
            {
                Log($"OnLaunched failed: {ex}");
                ShowStartupErrorWindow(ex);
            }
        }

        /// <summary>
        /// Invoked when Navigation to a certain page fails
        /// </summary>
        /// <param name="sender">The Frame which failed navigation</param>
        /// <param name="e">Details about the navigation failure</param>
        void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
        {
            var ex = new Exception("Failed to load Page " + e.SourcePageType.FullName, e.Exception);
            Log($"Navigation failed: {ex}");
            ShowStartupErrorContent(ex.ToString());
        }

        private static void EnsureWindowVisible(Window targetWindow)
        {
            try
            {
                var hwnd = WindowNative.GetWindowHandle(targetWindow);
                var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
                var appWindow = AppWindow.GetFromWindowId(windowId);

                if (appWindow.Presenter is OverlappedPresenter presenter)
                {
                    presenter.Restore();
                }

                // Force into visible bounds to avoid off-screen/hidden restore states.
                appWindow.MoveAndResize(new RectInt32(80, 60, 1280, 820));
            }
            catch
            {
                // Ignore if windowing APIs are unavailable on this environment.
            }
        }

        private void ShowStartupErrorWindow(Exception ex)
        {
            try
            {
                window ??= new Window();
                MainWindow = window;
                ShowStartupErrorContent(ex.ToString());
                window.Activate();
            }
            catch
            {
                // Last resort: no UI fallback available.
            }
        }

        private void ShowStartupErrorContent(string detail)
        {
            if (window is null)
            {
                return;
            }

            var panel = new StackPanel
            {
                Spacing = 8,
                Margin = new Thickness(16)
            };

            panel.Children.Add(new TextBlock
            {
                Text = "应用启动失败，请把日志文件发给开发者。",
                TextWrapping = TextWrapping.Wrap
            });

            panel.Children.Add(new TextBlock
            {
                Text = $"日志路径: {GetLogFilePath()}",
                TextWrapping = TextWrapping.Wrap
            });

            panel.Children.Add(new TextBox
            {
                IsReadOnly = true,
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                MinHeight = 260,
                Text = detail
            });

            window.Content = new ScrollViewer
            {
                Content = panel
            };
        }

        private void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            Log($"UI unhandled exception: {e.Exception}");
        }

        private void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            Log($"Task unobserved exception: {e.Exception}");
        }

        private void CurrentDomain_UnhandledException(object sender, System.UnhandledExceptionEventArgs e)
        {
            Log($"Domain unhandled exception: {e.ExceptionObject}");
        }

        private static void Log(string message)
        {
            try
            {
                var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}{Environment.NewLine}";
                lock (LogLock)
                {
                    File.AppendAllText(GetLogFilePath(), line, Encoding.UTF8);
                }
            }
            catch
            {
                // Ignore logging errors.
            }
        }

        private static string GetLogFilePath()
        {
            var rootPath = Path.Combine(AppContext.BaseDirectory, "logs");
            try
            {
                Directory.CreateDirectory(rootPath);
                return Path.Combine(rootPath, "startup.log");
            }
            catch
            {
                // Fallback path if app root is not writable.
                var fallback = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "Ucantalk",
                    "logs");
                Directory.CreateDirectory(fallback);
                return Path.Combine(fallback, "startup.log");
            }
        }
    }
}
