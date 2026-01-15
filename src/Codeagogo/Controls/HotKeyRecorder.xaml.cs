// Copyright 2026 CSIRO. Licensed under the Apache License, Version 2.0.
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using System.Windows;
using System.Windows.Input;

namespace Codeagogo.Controls;

/// <summary>
/// A control that records hotkey combinations (modifiers + key).
/// </summary>
public partial class HotKeyRecorder : System.Windows.Controls.UserControl
{
    private bool _isRecording;

    public HotKeyRecorder()
    {
        InitializeComponent();
        UpdateDisplay();
    }

    // ── Dependency Properties ────────────────────────────────────────

    public static readonly DependencyProperty ModifiersProperty =
        DependencyProperty.Register(
            nameof(Modifiers),
            typeof(uint),
            typeof(HotKeyRecorder),
            new PropertyMetadata(0u, OnHotkeyChanged));

    public static readonly DependencyProperty VirtualKeyProperty =
        DependencyProperty.Register(
            nameof(VirtualKey),
            typeof(uint),
            typeof(HotKeyRecorder),
            new PropertyMetadata(0u, OnHotkeyChanged));

    public uint Modifiers
    {
        get => (uint)GetValue(ModifiersProperty);
        set => SetValue(ModifiersProperty, value);
    }

    public uint VirtualKey
    {
        get => (uint)GetValue(VirtualKeyProperty);
        set => SetValue(VirtualKeyProperty, value);
    }

    private static void OnHotkeyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is HotKeyRecorder recorder)
            recorder.UpdateDisplay();
    }

    // ── Recording Logic ──────────────────────────────────────────────

    private void RecordButton_Click(object sender, RoutedEventArgs e)
    {
        _isRecording = !_isRecording;

        if (_isRecording)
        {
            RecordButton.Content = "Press key...";
            HotkeyText.Text = "Press a key combination...";
            Focus();
        }
        else
        {
            RecordButton.Content = "Record";
            UpdateDisplay();
        }
    }

    protected override void OnPreviewKeyDown(System.Windows.Input.KeyEventArgs e)
    {
        if (!_isRecording)
        {
            base.OnPreviewKeyDown(e);
            return;
        }

        // Ignore modifier-only keypresses
        if (e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl ||
            e.Key == Key.LeftShift || e.Key == Key.RightShift ||
            e.Key == Key.LeftAlt || e.Key == Key.RightAlt ||
            e.Key == Key.LWin || e.Key == Key.RWin ||
            e.Key == Key.System)
        {
            e.Handled = true;
            return;
        }

        // Extract modifiers
        uint mods = 0;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            mods |= 0x0002; // MOD_CONTROL
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
            mods |= 0x0004; // MOD_SHIFT
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt))
            mods |= 0x0001; // MOD_ALT
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Windows))
            mods |= 0x0008; // MOD_WIN

        // Convert WPF Key to virtual key code
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        var vk = (uint)KeyInterop.VirtualKeyFromKey(key);

        // Update properties
        Modifiers = mods;
        VirtualKey = vk;

        // End recording
        _isRecording = false;
        RecordButton.Content = "Record";
        UpdateDisplay();

        e.Handled = true;
    }

    protected override void OnLostFocus(RoutedEventArgs e)
    {
        base.OnLostFocus(e);

        if (_isRecording)
        {
            _isRecording = false;
            RecordButton.Content = "Record";
            UpdateDisplay();
        }
    }

    // ── Display Formatting ───────────────────────────────────────────

    private void UpdateDisplay()
    {
        HotkeyText.Text = FormatHotkey(Modifiers, VirtualKey);
    }

    /// <summary>
    /// Formats a hotkey as a human-readable string like "Ctrl+Shift+L".
    /// </summary>
    public static string FormatHotkey(uint modifiers, uint virtualKey)
    {
        if (virtualKey == 0)
            return "None";

        var sb = new StringBuilder();

        // MOD_CONTROL = 0x0002
        if ((modifiers & 0x0002) != 0)
            sb.Append("Ctrl+");

        // MOD_ALT = 0x0001
        if ((modifiers & 0x0001) != 0)
            sb.Append("Alt+");

        // MOD_SHIFT = 0x0004
        if ((modifiers & 0x0004) != 0)
            sb.Append("Shift+");

        // MOD_WIN = 0x0008
        if ((modifiers & 0x0008) != 0)
            sb.Append("Win+");

        // Convert virtual key to name
        var keyName = GetKeyName(virtualKey);
        sb.Append(keyName);

        return sb.ToString();
    }

    private static string GetKeyName(uint vk)
    {
        // Common keys
        return vk switch
        {
            >= 0x30 and <= 0x39 => ((char)vk).ToString(), // 0-9
            >= 0x41 and <= 0x5A => ((char)vk).ToString(), // A-Z
            >= 0x70 and <= 0x87 => $"F{vk - 0x6F}",       // F1-F24
            0x08 => "Backspace",
            0x09 => "Tab",
            0x0D => "Enter",
            0x1B => "Escape",
            0x20 => "Space",
            0x21 => "PageUp",
            0x22 => "PageDown",
            0x23 => "End",
            0x24 => "Home",
            0x25 => "Left",
            0x26 => "Up",
            0x27 => "Right",
            0x28 => "Down",
            0x2D => "Insert",
            0x2E => "Delete",
            0xBA => ";",
            0xBB => "=",
            0xBC => ",",
            0xBD => "-",
            0xBE => ".",
            0xBF => "/",
            0xC0 => "`",
            0xDB => "[",
            0xDC => "\\",
            0xDD => "]",
            0xDE => "'",
            _ => $"0x{vk:X2}"
        };
    }
}
