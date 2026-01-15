using System;
using System.IO;

namespace SNOMEDLookup;

public static class Log
{
    private static readonly object _lock = new();

    private static string LogPath
        => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "AEHRC", "SNOMED Lookup", "logs", "app.log");

    public static void Debug(string msg) => Write("DEBUG", msg);
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
}
