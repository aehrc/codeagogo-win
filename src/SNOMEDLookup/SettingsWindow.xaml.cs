using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;

namespace SNOMEDLookup;

public partial class SettingsWindow : Window
{
    private readonly Settings _settings;
    private uint _modifiers;
    private uint _virtualKey;

    public SettingsWindow()
    {
        InitializeComponent();
        _settings = Settings.Load();
        _modifiers = _settings.HotKeyModifiers;
        _virtualKey = _settings.HotKeyVirtualKey;
        UpdateHotkeyDisplay();
    }

    private void HotkeyTextBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        e.Handled = true;

        // Ignore modifier keys by themselves
        if (e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl ||
            e.Key == Key.LeftShift || e.Key == Key.RightShift ||
            e.Key == Key.LeftAlt || e.Key == Key.RightAlt ||
            e.Key == Key.LWin || e.Key == Key.RWin)
        {
            return;
        }

        // Get modifiers
        _modifiers = 0;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            _modifiers |= 0x0002; // MOD_CONTROL
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
            _modifiers |= 0x0004; // MOD_SHIFT
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt))
            _modifiers |= 0x0001; // MOD_ALT
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Windows))
            _modifiers |= 0x0008; // MOD_WIN

        // Require at least one modifier
        if (_modifiers == 0)
        {
            HotkeyTextBox.Text = "Please use at least one modifier key (Ctrl, Shift, Alt, or Win)";
            return;
        }

        // Get the virtual key code
        _virtualKey = (uint)KeyInterop.VirtualKeyFromKey(e.Key);

        UpdateHotkeyDisplay();
    }

    private void UpdateHotkeyDisplay()
    {
        var parts = new List<string>();

        if ((_modifiers & 0x0002) != 0) parts.Add("Ctrl");
        if ((_modifiers & 0x0004) != 0) parts.Add("Shift");
        if ((_modifiers & 0x0001) != 0) parts.Add("Alt");
        if ((_modifiers & 0x0008) != 0) parts.Add("Win");

        // Convert virtual key code back to a readable key name
        var key = KeyInterop.KeyFromVirtualKey((int)_virtualKey);
        parts.Add(key.ToString());

        HotkeyTextBox.Text = string.Join("+", parts);
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        _settings.HotKeyModifiers = _modifiers;
        _settings.HotKeyVirtualKey = _virtualKey;
        _settings.Save();
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();
}
