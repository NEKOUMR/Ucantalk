using System.Text;

namespace VRC_cantalkcn.Services;

public static class RuntimeLogService
{
    private const string AppDataFolderName = "Ucantalk";
    private const string LegacyAppDataFolderName = "VRC_cantalkcn";
    private static readonly object SyncRoot = new();
    private static readonly string LogDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        AppDataFolderName,
        "logs");
    private static readonly string LogFile = Path.Combine(LogDirectory, "runtime.log");
    private static readonly string LegacyLogFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        LegacyAppDataFolderName,
        "logs",
        "runtime.log");
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    public static event Action<string>? LogAdded;

    public static string LogDirectoryPath => LogDirectory;
    public static string LogFilePath => LogFile;

    public static void Info(string message) => Write("INFO", message);
    public static void Warn(string message) => Write("WARN", message);

    public static void Error(string message, Exception? exception = null)
    {
        var text = exception is null
            ? message
            : $"{message} | {exception.GetType().Name}: {exception.Message}{Environment.NewLine}{exception.StackTrace}";
        Write("ERROR", text);
    }

    public static string ReadAllSafe()
    {
        try
        {
            lock (SyncRoot)
            {
                EnsureLogDirectory();
                if (!File.Exists(LogFile))
                {
                    if (File.Exists(LegacyLogFile))
                    {
                        return File.ReadAllText(LegacyLogFile, Utf8NoBom);
                    }

                    return string.Empty;
                }

                return File.ReadAllText(LogFile, Utf8NoBom);
            }
        }
        catch
        {
            return string.Empty;
        }
    }

    public static void Clear()
    {
        try
        {
            lock (SyncRoot)
            {
                EnsureLogDirectory();
                File.WriteAllText(LogFile, string.Empty, Utf8NoBom);
            }
        }
        catch
        {
            // Ignore.
        }
    }

    private static void Write(string level, string message)
    {
        var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{level}] {message}";

        try
        {
            lock (SyncRoot)
            {
                EnsureLogDirectory();
                File.AppendAllText(LogFile, line + Environment.NewLine, Utf8NoBom);
            }
        }
        catch
        {
            // Ignore.
        }

        try
        {
            LogAdded?.Invoke(line);
        }
        catch
        {
            // Ignore.
        }
    }

    private static void EnsureLogDirectory()
    {
        Directory.CreateDirectory(LogDirectory);
    }
}
