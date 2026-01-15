// Copyright 2026 CSIRO. Licensed under the Apache License, Version 2.0.
// SPDX-License-Identifier: Apache-2.0

using System.IO;

namespace Codeagogo;

public static class Log
{
    private static readonly object _lock = new();

    /// <summary>
    /// Maximum log file size before rotation (10 MB).
    /// </summary>
    private const long MaxLogFileSize = 10 * 1024 * 1024;

    /// <summary>
    /// Maximum number of rotated backup log files to keep.
    /// </summary>
    private const int MaxBackupFiles = 3;

    /// <summary>
    /// Maximum bytes to read when the log file is very large (5 MB).
    /// </summary>
    private const long MaxReadSize = 5 * 1024 * 1024;

    /// <summary>
    /// Controls whether Debug() calls actually write to the log.
    /// </summary>
    public static bool DebugEnabled { get; set; } = false;

    private static string LogPath
        => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "AEHRC", "Codeagogo", "logs", "app.log");

    public static void Debug(string msg)
    {
        if (!DebugEnabled) return;
        Write("DEBUG", msg);
    }

    public static void Info(string msg) => Write("INFO", msg);
    public static void Error(string msg) => Write("ERROR", msg);

    private static void Write(string level, string msg)
    {
        try
        {
            var line = $"{DateTimeOffset.Now:O} [{level}] {msg}";
            lock (_lock)
            {
                var dir = Path.GetDirectoryName(LogPath)!;
                Directory.CreateDirectory(dir);

                RotateIfNeeded();

                using var stream = new FileStream(LogPath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
                using var writer = new StreamWriter(stream);
                writer.WriteLine(line);
            }
        }
        catch
        {
            // Swallow log write failures to prevent cascading errors
        }
    }

    /// <summary>
    /// Rotates log files if the current log exceeds <see cref="MaxLogFileSize"/>.
    /// Renames app.log -> app.log.1 -> app.log.2 -> app.log.3, deleting the oldest.
    /// </summary>
    private static void RotateIfNeeded()
    {
        try
        {
            var logPath = LogPath;
            if (!File.Exists(logPath))
            {
                return;
            }

            var fileInfo = new FileInfo(logPath);
            if (fileInfo.Length < MaxLogFileSize)
            {
                return;
            }

            // Delete the oldest backup if it exists
            var oldest = $"{logPath}.{MaxBackupFiles}";
            if (File.Exists(oldest))
            {
                File.Delete(oldest);
            }

            // Shift existing backups up by one
            for (var i = MaxBackupFiles - 1; i >= 1; i--)
            {
                var source = $"{logPath}.{i}";
                var dest = $"{logPath}.{i + 1}";
                if (File.Exists(source))
                {
                    File.Move(source, dest);
                }
            }

            // Rotate current log to .1
            File.Move(logPath, $"{logPath}.1");
        }
        catch
        {
            // Skip rotation if files are locked or another error occurs
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
                {
                    return "No logs available.";
                }

                using var stream = new FileStream(LogPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

                // If the file is larger than 5 MB, only read the tail to avoid OutOfMemoryException
                if (stream.Length > MaxReadSize)
                {
                    stream.Seek(-MaxReadSize, SeekOrigin.End);
                    // Skip the first partial line after seeking
                    using var reader = new StreamReader(stream);
                    reader.ReadLine();
                    var tail = reader.ReadToEnd();
                    var tailLines = tail.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
                    var recentTailLines = tailLines.Length <= lines
                        ? tailLines
                        : tailLines.Skip(tailLines.Length - lines).ToArray();
                    return string.Join(Environment.NewLine, recentTailLines);
                }

                using var fullReader = new StreamReader(stream);
                var allLines = fullReader.ReadToEnd().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
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
