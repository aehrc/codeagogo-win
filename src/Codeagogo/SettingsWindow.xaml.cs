// Copyright 2026 CSIRO. Licensed under the Apache License, Version 2.0.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using WpfControls = System.Windows.Controls;

namespace Codeagogo;

public partial class SettingsWindow : Window
{
    private readonly Settings _settings;
    private CodeSystemSettings _codeSystemSettings;
    private readonly ObservableCollection<CodeSystemItem> _codeSystemItems = new();
    private const string DefaultFhirUrl = "https://tx.ontoserver.csiro.au/fhir/";

    /// <summary>
    /// Event raised when settings are saved with changed FHIR URL.
    /// </summary>
    public event Action<string>? FhirUrlChanged;

    public SettingsWindow()
    {
        InitializeComponent();
        _settings = Settings.Load();
        _codeSystemSettings = CodeSystemSettings.Load();
        LoadSettings();
        LoadCodeSystems();
    }

    private void LoadSettings()
    {
        // Load hotkey settings into recorders
        LookupHotkeyRecorder.Modifiers = _settings.LookupHotKeyModifiers;
        LookupHotkeyRecorder.VirtualKey = _settings.LookupHotKeyVirtualKey;

        SearchHotkeyRecorder.Modifiers = _settings.SearchHotKeyModifiers;
        SearchHotkeyRecorder.VirtualKey = _settings.SearchHotKeyVirtualKey;

        ReplaceHotkeyRecorder.Modifiers = _settings.ReplaceHotKeyModifiers;
        ReplaceHotkeyRecorder.VirtualKey = _settings.ReplaceHotKeyVirtualKey;

        EclFormatHotkeyRecorder.Modifiers = _settings.EclFormatHotKeyModifiers;
        EclFormatHotkeyRecorder.VirtualKey = _settings.EclFormatHotKeyVirtualKey;

        ShrimpHotkeyRecorder.Modifiers = _settings.ShrimpHotKeyModifiers;
        ShrimpHotkeyRecorder.VirtualKey = _settings.ShrimpHotKeyVirtualKey;

        EvaluateHotkeyRecorder.Modifiers = _settings.EvaluateHotKeyModifiers;
        EvaluateHotkeyRecorder.VirtualKey = _settings.EvaluateHotKeyVirtualKey;

        // Load insert format
        SelectComboByTag(InsertFormatCombo, _settings.DefaultInsertFormat.ToString());

        // Load replace settings
        SelectComboByTag(ReplaceTermFormatCombo, _settings.ReplaceTermFormat.ToString());
        PrefixInactiveCheckBox.IsChecked = _settings.PrefixInactive;

        // Load FHIR settings
        FhirBaseUrlTextBox.Text = _settings.FhirBaseUrl;

        // Load startup setting
        StartWithWindowsCheckBox.IsChecked = StartupManager.IsEnabled;

        // Load logging settings
        DebugLoggingCheckBox.IsChecked = _settings.DebugLogging;

        // Load privacy settings
        InstallIdTextBox.Text = _settings.InstallId ?? string.Empty;
    }

    private static void SelectComboByTag(WpfControls.ComboBox combo, string tag)
    {
        foreach (WpfControls.ComboBoxItem item in combo.Items)
        {
            if (item.Tag?.ToString() == tag)
            {
                combo.SelectedItem = item;
                return;
            }
        }
        if (combo.Items.Count > 0)
            combo.SelectedIndex = 0;
    }

    private static string? GetSelectedTag(WpfControls.ComboBox combo)
    {
        return (combo.SelectedItem as WpfControls.ComboBoxItem)?.Tag?.ToString();
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
        _settings.LookupHotKeyModifiers = LookupHotkeyRecorder.Modifiers;
        _settings.LookupHotKeyVirtualKey = LookupHotkeyRecorder.VirtualKey;

        _settings.SearchHotKeyModifiers = SearchHotkeyRecorder.Modifiers;
        _settings.SearchHotKeyVirtualKey = SearchHotkeyRecorder.VirtualKey;

        _settings.ReplaceHotKeyModifiers = ReplaceHotkeyRecorder.Modifiers;
        _settings.ReplaceHotKeyVirtualKey = ReplaceHotkeyRecorder.VirtualKey;

        _settings.EclFormatHotKeyModifiers = EclFormatHotkeyRecorder.Modifiers;
        _settings.EclFormatHotKeyVirtualKey = EclFormatHotkeyRecorder.VirtualKey;

        _settings.ShrimpHotKeyModifiers = ShrimpHotkeyRecorder.Modifiers;
        _settings.ShrimpHotKeyVirtualKey = ShrimpHotkeyRecorder.VirtualKey;

        _settings.EvaluateHotKeyModifiers = EvaluateHotkeyRecorder.Modifiers;
        _settings.EvaluateHotKeyVirtualKey = EvaluateHotkeyRecorder.VirtualKey;

        // Save insert format
        var insertFormat = GetSelectedTag(InsertFormatCombo);
        if (Enum.TryParse<InsertFormat>(insertFormat, out var insertVal))
            _settings.DefaultInsertFormat = insertVal;

        // Save replace settings
        var termFormat = GetSelectedTag(ReplaceTermFormatCombo);
        if (Enum.TryParse<TermFormat>(termFormat, out var termVal))
            _settings.ReplaceTermFormat = termVal;
        _settings.PrefixInactive = PrefixInactiveCheckBox.IsChecked ?? true;

        // Save FHIR settings
        var oldFhirUrl = _settings.FhirBaseUrl;
        _settings.FhirBaseUrl = string.IsNullOrEmpty(fhirUrl) ? DefaultFhirUrl : fhirUrl;

        // Save startup setting
        var startWithWindows = StartWithWindowsCheckBox.IsChecked ?? false;
        _settings.StartWithWindows = startWithWindows;
        try { StartupManager.SetEnabled(startWithWindows); }
        catch (Exception ex) { Log.Error($"Failed to set startup: {ex.Message}"); }

        // Save logging settings
        _settings.DebugLogging = DebugLoggingCheckBox.IsChecked ?? false;

        // Apply logging setting immediately
        Log.DebugEnabled = _settings.DebugLogging;

        _settings.Save();

        // Save code system settings
        _codeSystemSettings.Systems = _codeSystemItems
            .Select(i => new ConfiguredCodeSystem(i.Uri, i.Title, i.Enabled))
            .ToList();
        _codeSystemSettings.Save();

        // Notify if FHIR URL changed
        if (_settings.FhirBaseUrl != oldFhirUrl)
        {
            FhirUrlChanged?.Invoke(_settings.FhirBaseUrl);
        }

        Log.Info($"Settings saved: DebugEnabled={_settings.DebugLogging}, FhirUrl={_settings.FhirBaseUrl}");

        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void FhirBaseUrlTextBox_TextChanged(object sender, WpfControls.TextChangedEventArgs e)
    {
        if (HttpWarningText == null) return;

        var url = FhirBaseUrlTextBox.Text.Trim();
        HttpWarningText.Visibility = url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void ResetFhirUrl_Click(object sender, RoutedEventArgs e)
    {
        FhirBaseUrlTextBox.Text = DefaultFhirUrl;
    }

    private void ResetInstallId_Click(object sender, RoutedEventArgs e)
    {
        _settings.ResetInstallId();
        _settings.Save();
        InstallIdTextBox.Text = _settings.InstallId ?? string.Empty;
        Log.Info("Install ID reset");
    }

    private void ShowWelcome_Click(object sender, RoutedEventArgs e)
    {
        var welcome = new WelcomeWindow();
        welcome.ShowDialog();
    }

    private void ExportDiagnostics_Click(object sender, RoutedEventArgs e)
    {
        var saveDialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Export Diagnostics",
            Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
            FileName = $"codeagogo-diagnostics-{DateTime.Now:yyyyMMdd-HHmmss}.txt",
            DefaultExt = ".txt"
        };

        var privacyResult = System.Windows.MessageBox.Show(
            "The diagnostics file will contain system information, application settings, and recent logs. " +
            "Do not share this file publicly if it may contain sensitive information.",
            "Privacy Notice",
            System.Windows.MessageBoxButton.OKCancel,
            System.Windows.MessageBoxImage.Information);

        if (privacyResult != System.Windows.MessageBoxResult.OK)
        {
            return;
        }

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

    private void LoadCodeSystems()
    {
        _codeSystemItems.Clear();
        foreach (var system in _codeSystemSettings.Systems)
        {
            _codeSystemItems.Add(new CodeSystemItem
            {
                Enabled = system.Enabled,
                Title = system.Title,
                Uri = system.Uri
            });
        }
        CodeSystemsListView.ItemsSource = _codeSystemItems;
    }

    private async void RefreshCodeSystems_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var fhirUrl = FhirBaseUrlTextBox.Text.Trim();
            if (string.IsNullOrEmpty(fhirUrl))
                fhirUrl = DefaultFhirUrl;

            var client = new OntoserverClient(baseUrl: fhirUrl, installId: _settings.InstallId);
            var available = await client.GetAvailableCodeSystemsAsync();

            if (available.Count == 0)
            {
                System.Windows.MessageBox.Show(
                    "No code systems were found on the server.",
                    "Add from Server",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
                return;
            }

            // Filter out systems already configured
            var existingUris = new HashSet<string>(_codeSystemItems.Select(i => i.Uri), StringComparer.OrdinalIgnoreCase);
            var newSystems = available.Where(a => !existingUris.Contains(a.Uri)).ToList();

            if (newSystems.Count == 0)
            {
                System.Windows.MessageBox.Show(
                    "All available code systems from the server are already configured.",
                    "Add from Server",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
                return;
            }

            // Build a message listing available systems for the user to confirm
            var message = "The following code systems were found on the server. Click OK to add them:\n\n";
            foreach (var sys in newSystems)
            {
                var versionInfo = string.IsNullOrEmpty(sys.Version) ? "" : $" (v{sys.Version})";
                message += $"  - {sys.Title}{versionInfo}\n    {sys.Uri}\n";
            }

            var result = System.Windows.MessageBox.Show(
                message,
                "Add from Server",
                System.Windows.MessageBoxButton.OKCancel,
                System.Windows.MessageBoxImage.Question);

            if (result == System.Windows.MessageBoxResult.OK)
            {
                foreach (var sys in newSystems)
                {
                    _codeSystemItems.Add(new CodeSystemItem
                    {
                        Enabled = false,
                        Title = sys.Title,
                        Uri = sys.Uri
                    });
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error($"Failed to fetch code systems from server: {ex.Message}");
            System.Windows.MessageBox.Show(
                $"Failed to fetch code systems from server:\n{ex.Message}",
                "Error",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }
    }

    private void ResetCodeSystems_Click(object sender, RoutedEventArgs e)
    {
        _codeSystemSettings = new CodeSystemSettings();
        LoadCodeSystems();
    }

    private string GenerateDiagnostics()
    {
        var sb = new System.Text.StringBuilder();

        sb.AppendLine("=== Codeagogo Diagnostics ===");
        sb.AppendLine($"Generated: {DateTime.Now:O}");
        sb.AppendLine();

        sb.AppendLine("=== System Information ===");
        sb.AppendLine($"OS Version: {Environment.OSVersion}");
        sb.AppendLine($".NET Version: {Environment.Version}");
        sb.AppendLine("Machine Name: (redacted for privacy)");
        sb.AppendLine();

        sb.AppendLine("=== Application Settings ===");
        sb.AppendLine($"FHIR Base URL: {_settings.FhirBaseUrl}");
        sb.AppendLine($"Debug Logging: {_settings.DebugLogging}");
        sb.AppendLine($"Lookup Hotkey: {Controls.HotKeyRecorder.FormatHotkey(_settings.LookupHotKeyModifiers, _settings.LookupHotKeyVirtualKey)}");
        sb.AppendLine($"Search Hotkey: {Controls.HotKeyRecorder.FormatHotkey(_settings.SearchHotKeyModifiers, _settings.SearchHotKeyVirtualKey)}");
        sb.AppendLine($"Replace Hotkey: {Controls.HotKeyRecorder.FormatHotkey(_settings.ReplaceHotKeyModifiers, _settings.ReplaceHotKeyVirtualKey)}");
        sb.AppendLine($"ECL Format Hotkey: {Controls.HotKeyRecorder.FormatHotkey(_settings.EclFormatHotKeyModifiers, _settings.EclFormatHotKeyVirtualKey)}");
        sb.AppendLine($"Shrimp Hotkey: {Controls.HotKeyRecorder.FormatHotkey(_settings.ShrimpHotKeyModifiers, _settings.ShrimpHotKeyVirtualKey)}");
        sb.AppendLine($"Insert Format: {_settings.DefaultInsertFormat}");
        sb.AppendLine($"Replace Term Format: {_settings.ReplaceTermFormat}");
        sb.AppendLine($"Prefix Inactive: {_settings.PrefixInactive}");
        sb.AppendLine();

        sb.AppendLine("=== Recent Logs ===");
        sb.AppendLine(Log.GetRecentLogs(500));

        return sb.ToString();
    }
}

/// <summary>
/// Bindable helper class for code system items in the ListView.
/// </summary>
public sealed class CodeSystemItem : INotifyPropertyChanged
{
    private bool _enabled;
    private string _title = string.Empty;
    private string _uri = string.Empty;

    public bool Enabled
    {
        get => _enabled;
        set
        {
            if (_enabled != value)
            {
                _enabled = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Enabled)));
            }
        }
    }

    public string Title
    {
        get => _title;
        set
        {
            if (_title != value)
            {
                _title = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Title)));
            }
        }
    }

    public string Uri
    {
        get => _uri;
        set
        {
            if (_uri != value)
            {
                _uri = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Uri)));
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
