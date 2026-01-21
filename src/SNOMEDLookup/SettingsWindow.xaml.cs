using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;

namespace SNOMEDLookup;

public partial class SettingsWindow : Window
{
    private readonly Settings _settings;
    private uint _modifiers;
    private uint _virtualKey;

    /// <summary>
    /// Event raised when settings are saved with changed FHIR URL.
    /// </summary>
    public event Action<string>? FhirUrlChanged;

    public SettingsWindow()
    {
        InitializeComponent();
        _settings = Settings.Load();

        // Load hotkey settings
        _modifiers = _settings.HotKeyModifiers;
        _virtualKey = _settings.HotKeyVirtualKey;
        UpdateHotkeyDisplay();

        // Load FHIR settings
        FhirBaseUrlTextBox.Text = _settings.FhirBaseUrl;

        // Load logging settings
        DebugLoggingCheckBox.IsChecked = _settings.DebugLoggingEnabled;
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
        // Validate FHIR URL
        var fhirUrl = FhirBaseUrlTextBox.Text.Trim();
        if (!string.IsNullOrEmpty(fhirUrl) && !Uri.TryCreate(fhirUrl, UriKind.Absolute, out _))
        {
            System.Windows.MessageBox.Show("Please enter a valid FHIR endpoint URL.", "Invalid URL",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            return;
        }

        // Save hotkey settings
        _settings.HotKeyModifiers = _modifiers;
        _settings.HotKeyVirtualKey = _virtualKey;

        // Save FHIR settings
        var oldFhirUrl = _settings.FhirBaseUrl;
        _settings.FhirBaseUrl = string.IsNullOrEmpty(fhirUrl)
            ? "https://tx.ontoserver.csiro.au/fhir"
            : fhirUrl;

        // Save logging settings
        _settings.DebugLoggingEnabled = DebugLoggingCheckBox.IsChecked ?? false;

        // Apply logging setting immediately
        Log.DebugEnabled = _settings.DebugLoggingEnabled;

        _settings.Save();

        // Notify if FHIR URL changed
        if (_settings.FhirBaseUrl != oldFhirUrl)
        {
            FhirUrlChanged?.Invoke(_settings.FhirBaseUrl);
        }

        Log.Info($"Settings saved: Hotkey=0x{_settings.HotKeyModifiers:X}+0x{_settings.HotKeyVirtualKey:X}, DebugEnabled={_settings.DebugLoggingEnabled}, FhirUrl={_settings.FhirBaseUrl}");

        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void ExportDiagnostics_Click(object sender, RoutedEventArgs e)
    {
        var saveDialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Export Diagnostics",
            Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
            FileName = $"snomed-lookup-diagnostics-{DateTime.Now:yyyyMMdd-HHmmss}.txt",
            DefaultExt = ".txt"
        };

        if (saveDialog.ShowDialog() == true)
        {
            try
            {
                var diagnostics = GenerateDiagnostics();
                File.WriteAllText(saveDialog.FileName, diagnostics);

                System.Windows.MessageBox.Show($"Diagnostics exported to:\n{saveDialog.FileName}",
                    "Export Complete", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Log.Error($"Failed to export diagnostics: {ex.Message}");
                System.Windows.MessageBox.Show($"Failed to export diagnostics:\n{ex.Message}",
                    "Export Failed", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }
    }

    private string GenerateDiagnostics()
    {
        var sb = new System.Text.StringBuilder();

        sb.AppendLine("=== SNOMED Lookup Diagnostics ===");
        sb.AppendLine($"Generated: {DateTime.Now:O}");
        sb.AppendLine();

        sb.AppendLine("=== System Information ===");
        sb.AppendLine($"OS Version: {Environment.OSVersion}");
        sb.AppendLine($".NET Version: {Environment.Version}");
        sb.AppendLine($"Machine Name: {Environment.MachineName}");
        sb.AppendLine();

        sb.AppendLine("=== Application Settings ===");
        sb.AppendLine($"FHIR Base URL: {_settings.FhirBaseUrl}");
        sb.AppendLine($"Debug Logging: {_settings.DebugLoggingEnabled}");
        sb.AppendLine($"Hotkey: {HotkeyTextBox.Text}");
        sb.AppendLine();

        sb.AppendLine("=== Recent Logs ===");
        sb.AppendLine(Log.GetRecentLogs(500));

        return sb.ToString();
    }
}
