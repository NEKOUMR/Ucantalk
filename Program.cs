using System.Runtime.InteropServices;
using System.Text;
using Microsoft.UI.Xaml;

namespace VRC_cantalkcn;

public static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        Log($"Program.Main start. Args: {string.Join(' ', args)}");

        try
        {
            WinRT.ComWrappersSupport.InitializeComWrappers();
            Log("InitializeComWrappers success.");

            Application.Start(p =>
            {
                var context = new Microsoft.UI.Dispatching.DispatcherQueueSynchronizationContext(
                    Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread());
                SynchronizationContext.SetSynchronizationContext(context);
                _ = p;
                Log("Creating App instance.");
                try
                {
                    _ = new App();
                    Log("App instance created.");
                }
                catch (Exception ex)
                {
                    Log($"App ctor failed: {ex}");
                    ShowFatalMessage(ex);
                }
            });
        }
        catch (Exception ex)
        {
            Log($"Program.Main fatal: {ex}");
            ShowFatalMessage(ex);
        }
    }

    private static void ShowFatalMessage(Exception ex)
    {
        var text =
            "应用启动失败。\n\n" +
            $"日志: {GetLogFilePath()}\n\n" +
            ex;
        _ = MessageBoxW(IntPtr.Zero, text, "Ucantalk - Startup Error", 0x00000010);
    }

    private static void Log(string message)
    {
        var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}{Environment.NewLine}";
        foreach (var file in GetLogFilePaths())
        {
            try
            {
                var dir = Path.GetDirectoryName(file);
                if (!string.IsNullOrWhiteSpace(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                File.AppendAllText(file, line, Encoding.UTF8);
            }
            catch
            {
                // Ignore logging failure on this target.
            }
        }
    }

    private static string GetLogFilePath()
    {
        // Keep message box display stable by showing app-root target first.
        return Path.Combine(AppContext.BaseDirectory, "logs", "bootstrap.log");
    }

    private static IEnumerable<string> GetLogFilePaths()
    {
        yield return Path.Combine(AppContext.BaseDirectory, "logs", "bootstrap.log");
        yield return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Ucantalk",
            "logs",
            "bootstrap.log");
        yield return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "VRC_cantalkcn",
            "logs",
            "bootstrap.log");
        yield return Path.Combine(Path.GetTempPath(), "Ucantalk_bootstrap.log");
        yield return Path.Combine(Path.GetTempPath(), "VRC_cantalkcn_bootstrap.log");
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBoxW(IntPtr hWnd, string text, string caption, uint type);
}
