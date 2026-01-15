// Copyright 2026 CSIRO. Licensed under the Apache License, Version 2.0.
// SPDX-License-Identifier: Apache-2.0

using System.Windows;

namespace Codeagogo;

public partial class ProgressHUD : Window
{
    private static ProgressHUD? _instance;

    public ProgressHUD()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Shows the progress HUD near the current cursor position with the given message.
    /// Uses a singleton pattern - calling Show again updates the existing HUD.
    /// </summary>
    public static void Show(string message)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            if (_instance == null || !_instance.IsVisible)
            {
                _instance = new ProgressHUD();
            }

            _instance.MessageText.Text = message;

            // Position near cursor
            var mouse = System.Windows.Forms.Control.MousePosition;
            _instance.Left = mouse.X + 15;
            _instance.Top = mouse.Y + 15;

            // Ensure on screen
            var screen = System.Windows.Forms.Screen.FromPoint(
                new System.Drawing.Point(mouse.X, mouse.Y));
            var workArea = screen.WorkingArea;

            if (_instance.Left + _instance.ActualWidth > workArea.Right)
                _instance.Left = workArea.Right - _instance.ActualWidth - 10;
            if (_instance.Top + _instance.ActualHeight > workArea.Bottom)
                _instance.Top = workArea.Bottom - _instance.ActualHeight - 10;

            _instance.Show();
        });
    }

    /// <summary>
    /// Hides and disposes the progress HUD singleton.
    /// </summary>
    public static new void Hide()
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            if (_instance != null)
            {
                try { _instance.Close(); } catch { }
                _instance = null;
            }
        });
    }
}
