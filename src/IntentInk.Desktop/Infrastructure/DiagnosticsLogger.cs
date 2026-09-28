using System.Collections.Concurrent;
using System.IO;

namespace IntentInk.Desktop.Infrastructure;

public enum LogLevel
{
    Debug,
    Info,
    Success,
    Warning,
    Error
}

/// <summary>
/// Global diagnostics and real-time activity logger.
/// Outputs to console, single persistent log file (logs/diagnostics.log), in-memory buffer, and live UI subscribers.
/// </summary>
public static class DiagnosticsLogger
{
    public static readonly string WorkspaceLogDir = ResolveLogDir();
    public static readonly string PrimaryLogPath = Path.Combine(WorkspaceLogDir, "diagnostics.log");

    private static string ResolveLogDir()
    {
        try
        {
            var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (dir != null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "IntentInk.sln")))
                {
                    return Path.Combine(dir.FullName, "logs");
                }
                dir = dir.Parent;
            }
        }
        catch { }

        return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
    }

    private static readonly object FileLock = new();

    private const int MaxInMemoryLines = 500;
    private static readonly ConcurrentQueue<string> InMemoryLogs = new();

    public static event Action<string, LogLevel>? LogEmitted;

    static DiagnosticsLogger()
    {
        try
        {
            Directory.CreateDirectory(WorkspaceLogDir);
            Console.WriteLine($"[DiagnosticsLogger] Logging to: {PrimaryLogPath}");
        }
        catch { }
    }

    public static IReadOnlyList<string> GetRecentLogs() => InMemoryLogs.ToArray();

    public static void Log(string category, string message, LogLevel level = LogLevel.Info, long? elapsedMs = null)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
        var elapsedStr = elapsedMs.HasValue ? $" ({elapsedMs.Value}ms)" : "";
        var levelPrefix = level switch
        {
            LogLevel.Success => "[OK]",
            LogLevel.Warning => "[WARN]",
            LogLevel.Error   => "[ERROR]",
            LogLevel.Debug   => "[DEBUG]",
            _                => "[INFO]"
        };

        var line = $"[{timestamp}] {levelPrefix} [{category}] {message}{elapsedStr}";

        // 1. Console Output
        try
        {
            var prevColor = Console.ForegroundColor;
            Console.ForegroundColor = level switch
            {
                LogLevel.Success => ConsoleColor.Green,
                LogLevel.Warning => ConsoleColor.Yellow,
                LogLevel.Error   => ConsoleColor.Red,
                LogLevel.Debug   => ConsoleColor.DarkGray,
                _                => ConsoleColor.Cyan
            };
            Console.WriteLine(line);
            Console.ForegroundColor = prevColor;
        }
        catch { }

        // 2. In-Memory Rolling Buffer
        InMemoryLogs.Enqueue(line);
        while (InMemoryLogs.Count > MaxInMemoryLines)
        {
            InMemoryLogs.TryDequeue(out _);
        }

        // 3. Fire Real-time Event for UI Viewers
        try
        {
            LogEmitted?.Invoke(line, level);
        }
        catch { }

        // 4. Append to Persistent Disk File (logs/diagnostics.log)
        try
        {
            lock (FileLock)
            {
                var entry = line + Environment.NewLine;
                Directory.CreateDirectory(WorkspaceLogDir);
                File.AppendAllText(PrimaryLogPath, entry);
            }
        }
        catch { }
    }

    public static void LogInfo(string category, string message) =>
        Log(category, message, LogLevel.Info);

    public static void LogSuccess(string category, string message, long? elapsedMs = null) =>
        Log(category, message, LogLevel.Success, elapsedMs);

    public static void LogWarning(string category, string message) =>
        Log(category, message, LogLevel.Warning);

    public static void LogError(string category, Exception ex) =>
        LogError(category, ex.Message, ex);

    public static void LogError(string category, string message, Exception? ex = null)
    {
        var fullMessage = ex != null ? $"{message} | Exception: {ex.GetType().Name}: {ex.Message}" : message;
        Log(category, fullMessage, LogLevel.Error);
    }

    public static void LogAi(string message, LogLevel level = LogLevel.Info, long? elapsedMs = null) =>
        Log("AI", message, level, elapsedMs);

    public static void LogInference(string model, int inputLength, int outputLength, long elapsedMs, bool accepted)
    {
        var level = accepted ? LogLevel.Success : LogLevel.Info;
        var status = accepted ? "Accepted suggestion" : "No changes / Rejected";
        Log("AI", $"Model='{model}' | In: {inputLength} chars, Out: {outputLength} chars | {status}", level, elapsedMs);
    }
}
