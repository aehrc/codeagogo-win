// Copyright 2026 CSIRO. Licensed under the Apache License, Version 2.0.
// SPDX-License-Identifier: Apache-2.0

using System.ComponentModel;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using Microsoft.Web.WebView2.Core;

namespace Codeagogo;

/// <summary>
/// ECL Workbench window with a Monaco-based ECL editor (top) and
/// evaluation results DataGrid (bottom) in a resizable split layout.
/// </summary>
public partial class ECLWorkbenchWindow : Window
{
    private readonly ECLWorkbenchViewModel _vm;
    private readonly OntoserverClient _client;
    private bool _editorReady;
    private bool _isClosingForReal;

    /// <summary>
    /// The ECL expression to load when the editor initializes.
    /// Set before Show() so it's available even if WebView2 fails.
    /// </summary>
    public string InitialExpression
    {
        get => _vm.CurrentExpression;
        set => _vm.CurrentExpression = value;
    }

    /// <summary>
    /// Raised when WebView2 is unavailable and the caller should fall back
    /// to the static evaluate panel.
    /// </summary>
    public event Action<string>? WebView2Unavailable;

    public ECLWorkbenchWindow(OntoserverClient client)
    {
        InitializeComponent();

        _client = client;
        _vm = new ECLWorkbenchViewModel(client);
        DataContext = _vm;

        _vm.PropertyChanged += OnViewModelChanged;

        RestoreWindowGeometry();
        Loaded += async (_, _) => await InitializeEditorAsync();
        SizeChanged += (_, _) => SaveWindowGeometry();
        LocationChanged += (_, _) => SaveWindowGeometry();
    }

    private void RestoreWindowGeometry()
    {
        var s = Settings.Load();
        Width = s.WorkbenchWidth;
        Height = s.WorkbenchHeight;

        if (s.WorkbenchLeft >= 0 && s.WorkbenchTop >= 0)
        {
            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = s.WorkbenchLeft;
            Top = s.WorkbenchTop;

            // Clamp to screen bounds
            var screen = System.Windows.Forms.Screen.FromPoint(
                new System.Drawing.Point((int)Left, (int)Top));
            var wa = screen.WorkingArea;
            if (Left + Width > wa.Right) Left = wa.Right - Width;
            if (Top + Height > wa.Bottom) Top = wa.Bottom - Height;
            if (Left < wa.Left) Left = wa.Left;
            if (Top < wa.Top) Top = wa.Top;
        }

        // Restore split ratio
        if (s.WorkbenchSplitRatio is > 0.1 and < 0.9)
        {
            var grid = (System.Windows.Controls.Grid)Content;
            grid.RowDefinitions[0].Height = new GridLength(s.WorkbenchSplitRatio, GridUnitType.Star);
            grid.RowDefinitions[2].Height = new GridLength(1.0 - s.WorkbenchSplitRatio, GridUnitType.Star);
        }
    }

    private void SaveWindowGeometry()
    {
        if (WindowState != WindowState.Normal) return;

        var s = Settings.Load();
        s.WorkbenchWidth = Width;
        s.WorkbenchHeight = Height;
        s.WorkbenchLeft = Left;
        s.WorkbenchTop = Top;

        // Save split ratio
        var grid = (System.Windows.Controls.Grid)Content;
        var editorHeight = grid.RowDefinitions[0].ActualHeight;
        var totalHeight = editorHeight + grid.RowDefinitions[2].ActualHeight;
        if (totalHeight > 0)
            s.WorkbenchSplitRatio = editorHeight / totalHeight;

        s.Save();
    }

    /// <summary>
    /// Sets the editor content and optionally triggers evaluation.
    /// </summary>
    public async Task SetEditorValueAsync(string value, bool evaluate = false)
    {
        if (_editorReady && EditorWebView.CoreWebView2 != null)
        {
            var escaped = value.Replace("\\", "\\\\").Replace("'", "\\'")
                .Replace("\n", "\\n").Replace("\r", "\\r");
            await EditorWebView.CoreWebView2.ExecuteScriptAsync(
                $"document.querySelector('ecl-editor').value = '{escaped}'");
        }

        if (evaluate && !string.IsNullOrWhiteSpace(value))
        {
            var settings = Settings.Load();
            await _vm.EvaluateAsync(value, settings.EvaluateResultLimit);
        }
    }

    private async Task InitializeEditorAsync()
    {
        try
        {
            // Extract ecl-editor resource to temp
            var resourceDir = ECLEditorResourceManager.GetResourceDirectory();

            await EditorWebView.EnsureCoreWebView2Async();

            // Map virtual host to the resource directory
            EditorWebView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                ECLEditorHtmlBuilder.VirtualHost,
                resourceDir,
                CoreWebView2HostResourceAccessKind.Allow);

            // Security: disable unnecessary features
            EditorWebView.CoreWebView2.Settings.AreDefaultScriptDialogsEnabled = false;
            EditorWebView.CoreWebView2.Settings.IsGeneralAutofillEnabled = false;
            EditorWebView.CoreWebView2.Settings.IsPasswordAutosaveEnabled = false;

            // Handle JS-to-C# messages
            EditorWebView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;

            // Build and load editor HTML
            var settings = Settings.Load();
            var html = ECLEditorHtmlBuilder.Build(
                value: _vm.CurrentExpression,
                fhirServerUrl: settings.FhirBaseUrl,
                darkTheme: true);

            EditorWebView.CoreWebView2.NavigateToString(html);
            _editorReady = true;

            StatusText.Text = "Ready — Ctrl+Enter to evaluate";
            Log.Info("ECL Workbench: editor initialized");
        }
        catch (Exception ex)
        {
            Log.Error($"ECL Workbench: failed to initialize editor: {ex.Message}");

            // Fall back to static evaluate panel
            Log.Info("ECL Workbench: WebView2 unavailable, falling back to static evaluate panel");
            var expression = _vm.CurrentExpression;
            _isClosingForReal = true;
            Close();
            WebView2Unavailable?.Invoke(expression);
        }
    }

    private void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            var json = e.WebMessageAsJson;
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var eventType = root.GetProperty("event").GetString();
            var value = root.GetProperty("value").GetString() ?? "";

            switch (eventType)
            {
                case "change":
                    // Debounced content change — trigger evaluation
                    _ = HandleChangeAsync(value);
                    break;
                case "evaluate":
                    // Explicit Ctrl+Enter — immediate evaluation
                    _ = HandleEvaluateAsync(value);
                    break;
            }
        }
        catch (Exception ex)
        {
            Log.Debug($"ECL Workbench: message parse error: {ex.Message}");
        }
    }

    private async Task HandleChangeAsync(string value)
    {
        var settings = Settings.Load();
        await _vm.EvaluateAsync(value, settings.EvaluateResultLimit);
    }

    private async Task HandleEvaluateAsync(string value)
    {
        var settings = Settings.Load();
        await _vm.EvaluateAsync(value, settings.EvaluateResultLimit);
    }

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(ECLWorkbenchViewModel.IsEvaluating):
            case nameof(ECLWorkbenchViewModel.Result):
            case nameof(ECLWorkbenchViewModel.ErrorMessage):
                UpdateResultsView();
                break;
            case nameof(ECLWorkbenchViewModel.ShowFsn):
                RefreshResultsList();
                break;
        }
    }

    private void UpdateResultsView()
    {
        LoadingPanel.Visibility = _vm.IsEvaluating ? Visibility.Visible : Visibility.Collapsed;
        ErrorPanel.Visibility = !_vm.IsEvaluating && _vm.ErrorMessage != null ? Visibility.Visible : Visibility.Collapsed;
        ResultsList.Visibility = !_vm.IsEvaluating && _vm.Result != null ? Visibility.Visible : Visibility.Collapsed;

        if (_vm.ErrorMessage != null)
        {
            ErrorText.Text = _vm.ErrorMessage;
            ResultsHeaderText.Text = "Error";
        }

        if (_vm.Result != null)
        {
            var r = _vm.Result;
            ResultsHeaderText.Text = r.Total > r.Concepts.Count
                ? $"{r.Concepts.Count} of {r.Total} concepts"
                : $"{r.Total} concepts";

            ResultsList.ItemsSource = r.Concepts;
            StatusText.Text = $"Evaluated — {r.Total} matching concepts";
        }

        if (!_vm.IsEvaluating && _vm.Result == null && _vm.ErrorMessage == null)
        {
            ResultsHeaderText.Text = "";
            ResultsList.ItemsSource = null;
        }
    }

    private void RefreshResultsList()
    {
        if (_vm.Result != null)
        {
            ResultsList.ItemsSource = null;
            ResultsList.ItemsSource = _vm.Result.Concepts;
        }
    }

    // ── Event Handlers ──────────────────────────────────────────────

    private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Escape)
        {
            Hide();
            e.Handled = true;
        }
    }

    private void Window_Closing(object sender, CancelEventArgs e)
    {
        if (_isClosingForReal) return; // Allow real close (e.g., WebView2 fallback)

        // Hide instead of close to preserve editor content
        e.Cancel = true;
        Hide();
    }

    private void ConceptCode_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Hyperlink link && link.DataContext is EvaluationConcept concept)
            OpenInShrimp(concept.Code);
    }

    private void DiagramButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button btn && btn.DataContext is EvaluationConcept concept)
            OpenDiagram(concept.Code, concept.Display);
    }

    private void ContextShrimp_Click(object sender, RoutedEventArgs e)
    {
        if (ResultsList.SelectedItem is EvaluationConcept concept)
            OpenInShrimp(concept.Code);
    }

    private void ContextDiagram_Click(object sender, RoutedEventArgs e)
    {
        if (ResultsList.SelectedItem is EvaluationConcept concept)
            OpenDiagram(concept.Code, concept.Display);
    }

    private void ContextCopyCode_Click(object sender, RoutedEventArgs e)
    {
        if (ResultsList.SelectedItem is EvaluationConcept concept)
            System.Windows.Clipboard.SetText(concept.Code);
    }

    private void ContextCopyDisplay_Click(object sender, RoutedEventArgs e)
    {
        if (ResultsList.SelectedItem is EvaluationConcept concept)
            System.Windows.Clipboard.SetText(concept.Display);
    }

    private void OpenInShrimp(string conceptId)
    {
        var url = ShrimpUrlBuilder.BuildUrl(conceptId: conceptId, system: "http://snomed.info/sct",
            fhirEndpoint: Settings.Load().FhirBaseUrl);
        if (url != null && Uri.TryCreate(url, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = url, UseShellExecute = true
            });
        }
    }

    private void OpenDiagram(string conceptId, string? preferredTerm)
    {
        try
        {
            var window = new Visualization.VisualizationWindow(_client, conceptId, preferredTerm);
            window.Show();
            window.Activate();
        }
        catch (Exception ex)
        {
            Log.Error($"Failed to open diagram: {ex.Message}");
        }
    }
}
