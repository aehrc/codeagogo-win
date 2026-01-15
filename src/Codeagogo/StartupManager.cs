// Copyright 2026 CSIRO. Licensed under the Apache License, Version 2.0.
// SPDX-License-Identifier: Apache-2.0

using System.IO;

namespace Codeagogo;

/// <summary>
/// Manages adding/removing Codeagogo from Windows startup via the Startup folder.
/// Uses shell:startup (Environment.SpecialFolder.Startup) which is universally supported
/// and visible in Task Manager's Startup tab.
/// </summary>
public static class StartupManager
{
    private const string ShortcutName = "Codeagogo.lnk";

    /// <summary>
    /// Whether a startup shortcut currently exists.
    /// </summary>
    public static bool IsEnabled => File.Exists(GetShortcutPath());

    /// <summary>
    /// Enables or disables startup by creating/removing a shortcut in the Startup folder.
    /// </summary>
    public static void SetEnabled(bool enabled)
    {
        var shortcutPath = GetShortcutPath();

        if (enabled)
        {
            CreateShortcut(shortcutPath);
            Log.Info("StartupManager: added to Windows startup");
        }
        else
        {
            if (File.Exists(shortcutPath))
            {
                File.Delete(shortcutPath);
                Log.Info("StartupManager: removed from Windows startup");
            }
        }
    }

    private static string GetShortcutPath()
    {
        var startupFolder = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
        return Path.Combine(startupFolder, ShortcutName);
    }

    private static void CreateShortcut(string shortcutPath)
    {
        var exePath = Environment.ProcessPath ?? System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
        if (exePath == null)
        {
            Log.Error("StartupManager: could not determine executable path");
            return;
        }

        // Use Windows Script Host COM object to create a .lnk shortcut
        var shellType = Type.GetTypeFromProgID("WScript.Shell");
        if (shellType == null)
        {
            Log.Error("StartupManager: WScript.Shell not available");
            return;
        }

        dynamic shell = Activator.CreateInstance(shellType)!;
        try
        {
            var shortcut = shell.CreateShortcut(shortcutPath);
            shortcut.TargetPath = exePath;
            shortcut.WorkingDirectory = Path.GetDirectoryName(exePath);
            shortcut.Description = "Codeagogo - Clinical terminology lookup";
            shortcut.Save();
        }
        finally
        {
            System.Runtime.InteropServices.Marshal.ReleaseComObject(shell);
        }
    }
}
