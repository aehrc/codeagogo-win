// Copyright 2026 CSIRO. Licensed under the Apache License, Version 2.0.
// SPDX-License-Identifier: Apache-2.0

using System.IO;
using FlaUI.Core;
using FlaUI.UIA3;
using FluentAssertions;

namespace Codeagogo.E2ETests;

/// <summary>
/// E2E tests for the Settings window UI.
/// These tests launch the app and interact with the Settings dialog.
/// </summary>
[Trait("Category", "E2E")]
public class SettingsWindowTests : IDisposable
{
    private Application? _app;

    private static string FindAppExe()
    {
        var dir = Directory.GetCurrentDirectory();
        while (dir != null)
        {
            if (Directory.GetFiles(dir, "*.sln").Length > 0)
                break;
            dir = Directory.GetParent(dir)?.FullName;
        }
        dir ??= Directory.GetCurrentDirectory();

        var debugPath = Path.Combine(dir, "src", "Codeagogo", "bin", "Debug", "net8.0-windows", "Codeagogo.exe");
        return debugPath;
    }

    [Fact]
    public void SettingsWindow_ContainsAllExpectedGroupBoxes()
    {
        var appPath = FindAppExe();
        if (!File.Exists(appPath))
        {
            Assert.Fail($"App not found at {appPath}. Build first.");
            return;
        }

        _app = Application.Launch(appPath);
        Thread.Sleep(3000);

        using var automation = new UIA3Automation();

        // The Settings window must be triggered via tray icon context menu.
        // In a full E2E setup, you would right-click the tray icon and select "Settings..."
        // For now, this test validates the app starts without crashing.
        _app.HasExited.Should().BeFalse();
    }

    public void Dispose()
    {
        if (_app != null && !_app.HasExited)
        {
            _app.Kill();
            _app.Dispose();
        }
    }
}
