using System;
using System.Diagnostics;
using System.IO;

namespace BlackStarHydra360.Logging;

public enum LogLevel { Debug, Info, Warning, Error, Critical }

public sealed class LogEntry
{
    public DateTime Timestamp { get; init; } = DateTime.Now;
    public LogLevel Level { get; init; }
    public string Message { get; init; } = string.Empty;
    public Exception? Exception { get; init; }

    public override string ToString()
    {
        string tag = Level switch
        {
            LogLevel.Debug => "DBG",
            LogLevel.Info => "INF",
            LogLevel.Warning => "WRN",
            LogLevel.Error => "ERR",
            LogLevel.Critical => "CRT",
            _ => "???"
        };
        string line = $"[{Timestamp:HH:mm:ss.fff}] [{tag}] {Message}";
        if (Exception != null)
            line += $"\n    → {Exception.GetType().Name}: {Exception.Message}";
        return line;
    }
}

/// <summary>
/// Thread-safe application logger. Writes to file and broadcasts to UI subscribers.
/// </summary>
public static class AppLogger
{
    private static readonly object _lock = new();
    private static string? _logDir;
    private static string? _logFile;
    private static volatile bool _fileOk = true;
    private static long _fileSize;
    private const long MaxLogSize = 5 * 1024 * 1024;

    public static event Action<LogEntry>? LogEmitted;

    static AppLogger()
    {
        try
        {
            _logDir = Path.Combine(Path.GetTempPath(), "BlackStarHydra", "logs");
            Directory.CreateDirectory(_logDir);
            _logFile = Path.Combine(_logDir, $"hydra_{DateTime.Now:yyyyMMdd}.log");
        }
        catch (Exception ex)
        {
            _fileOk = false;
            Debug.WriteLine($"[Hydra] Logger init failed: {ex.Message}");
        }
    }

    public static void Log(LogLevel level, string message, Exception? ex = null)
    {
        var entry = new LogEntry { Level = level, Message = message, Exception = ex };
        LogEmitted?.Invoke(entry);

        lock (_lock)
        {
            string line = entry.ToString();
            try
            {
                if (_fileOk && _logFile != null)
                {
                    _fileSize += line.Length + Environment.NewLine.Length;
                    if (_fileSize > MaxLogSize)
                    {
                        _logFile = Path.Combine(_logDir!, $"hydra_{DateTime.Now:yyyyMMdd_HHmmss}.log");
                        _fileSize = 0;
                    }
                    File.AppendAllText(_logFile, line + Environment.NewLine);
                }
            }
            catch
            {
                _fileOk = false;
            }
            Debug.WriteLine(line);
        }
    }

    public static void Info(string msg) => Log(LogLevel.Info, msg);
    public static void Warning(string msg, Exception? ex = null) => Log(LogLevel.Warning, msg, ex);
    public static void Error(string msg, Exception? ex = null) => Log(LogLevel.Error, msg, ex);
    public static void Critical(string msg, Exception? ex = null) => Log(LogLevel.Critical, msg, ex);
}
