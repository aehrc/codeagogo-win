using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SNOMEDLookup;

public static class Log
{
    private static readonly object _lock = new();

    /// <summary>
    /// Controls whether Debug() calls actually write to the log.
    /// </summary>
    public static bool DebugEnabled { get; set; } = false;

    private static string LogPath
        => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "AEHRC", "SNOMED Lookup", "logs", "app.log");

    public static void Debug(string msg)
    {
        if (!DebugEnabled) return;
        Write("DEBUG", msg);
    }

    public static void Info(string msg) => Write("INFO", msg);
    public static void Error(string msg) => Write("ERROR", msg);

    private static void Write(string level, string msg)
    {
        var line = $"{DateTimeOffset.Now:O} [{level}] {msg}";
        lock (_lock)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
            File.AppendAllText(LogPath, line + Environment.NewLine);
        }
    }

    /// <summary>
    /// Truncates a string to a maximum length for logging, adding ellipsis if truncated.
    /// </summary>
    public static string Snippet(string? text, int limit = 100)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        if (text.Length <= limit) return text;
        return text[..limit] + "...";
    }

    /// <summary>
    /// Gets the most recent log lines for diagnostic export.
    /// </summary>
    public static string GetRecentLogs(int lines = 500)
    {
        try
        {
            lock (_lock)
            {
                if (!File.Exists(LogPath))
                    return "No logs available.";

                var allLines = File.ReadAllLines(LogPath);
                var recentLines = allLines.Length <= lines
                    ? allLines
                    : allLines.Skip(allLines.Length - lines).ToArray();

                return string.Join(Environment.NewLine, recentLines);
            }
        }
        catch (Exception ex)
        {
            return $"Failed to read logs: {ex.Message}";
        }
    }

    /// <summary>
    /// Gets the log file path for external access.
    /// </summary>
    public static string GetLogPath() => LogPath;
}
