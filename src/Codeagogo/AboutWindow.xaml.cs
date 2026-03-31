// Copyright 2026 CSIRO. Licensed under the Apache License, Version 2.0.
// SPDX-License-Identifier: Apache-2.0

using System.Windows;

namespace Codeagogo;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();

        var version = typeof(AboutWindow).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
        VersionText.Text = $"Version {version}";
    }

    private void GitHub_Click(object sender, RoutedEventArgs e)
    {
        OpenUrl("https://github.com/aehrc/codeagogo-win");
    }

    private void Issues_Click(object sender, RoutedEventArgs e)
    {
        OpenUrl("https://github.com/aehrc/codeagogo-win/issues");
    }

    private void License_Click(object sender, RoutedEventArgs e)
    {
        OpenUrl("https://github.com/aehrc/codeagogo-win/blob/main/LICENSE");
    }

    private void OK_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private static void OpenUrl(string url)
    {
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = url,
            UseShellExecute = true
        });
    }
}
