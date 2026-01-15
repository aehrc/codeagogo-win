// Copyright 2026 CSIRO. Licensed under the Apache License, Version 2.0.
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Windows;

namespace Codeagogo;

/// <summary>
/// Welcome window shown on first launch to introduce users to Codeagogo.
/// </summary>
public partial class WelcomeWindow : Window
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WelcomeWindow"/> class.
    /// </summary>
    public WelcomeWindow()
    {
        InitializeComponent();
    }

    private void StarOnGitHub_Click(object sender, RoutedEventArgs e)
    {
        var url = "https://github.com/aehrc/codeagogo";
        if (IsAllowedUrl(url))
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
    }

    private void JoinMailingList_Click(object sender, RoutedEventArgs e)
    {
        var url = "https://lists.csiro.au/mailman3/lists/codeagogo.lists.csiro.au/";
        if (IsAllowedUrl(url))
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
    }

    private void ReportIssue_Click(object sender, RoutedEventArgs e)
    {
        var url = "https://github.com/aehrc/codeagogo/issues";
        if (IsAllowedUrl(url))
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
    }

    private void GetStarted_Click(object sender, RoutedEventArgs e)
    {
        // Apply startup preference from welcome screen
        var startWithWindows = StartWithWindowsCheckBox.IsChecked ?? true;
        var settings = Settings.Load();
        settings.StartWithWindows = startWithWindows;
        settings.Save();

        try { StartupManager.SetEnabled(startWithWindows); }
        catch (Exception ex) { Log.Error($"Failed to set startup: {ex.Message}"); }

        Close();
    }

    /// <summary>
    /// Validates that a URL uses http or https scheme to prevent shell-execute injection.
    /// </summary>
    private static bool IsAllowedUrl(string url)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }
}
