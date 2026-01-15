// Copyright 2026 CSIRO. Licensed under the Apache License, Version 2.0.
// SPDX-License-Identifier: Apache-2.0

using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;

namespace Codeagogo;

/// <summary>
/// ECL evaluation results window. Shows matching concepts from a FHIR server
/// with semantic tags, Shrimp links, and diagram buttons.
/// </summary>
public partial class EvaluateWindow : Window
{
    private readonly EvaluateViewModel _vm;
    private readonly OntoserverClient _client;

    public EvaluateWindow(OntoserverClient client, string expression)
    {
        InitializeComponent();

        _client = client;
        _vm = new EvaluateViewModel(client) { Expression = expression };
        DataContext = _vm;

        _vm.PropertyChanged += OnViewModelChanged;
    }

    /// <summary>
    /// Starts the ECL evaluation.
    /// </summary>
    public async Task EvaluateAsync(int resultLimit = 50)
    {
        await _vm.EvaluateAsync(resultLimit);
    }

    /// <summary>
    /// Sets concept validation warnings on the view model.
    /// </summary>
    public void SetWarnings(List<string> warnings)
    {
        _vm.Warnings = warnings;
    }

    /// <summary>
    /// Positions the window near the specified screen coordinates.
    /// </summary>
    public void PositionNearCursor(int x, int y)
    {
        Left = x;
        Top = y;

        // Adjust to keep on screen
        var screen = System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point(x, y));
        var workArea = screen.WorkingArea;

        if (Left + ActualWidth > workArea.Right)
            Left = workArea.Right - Width;
        if (Top + ActualHeight > workArea.Bottom)
            Top = workArea.Bottom - Height;
        if (Left < workArea.Left)
            Left = workArea.Left;
        if (Top < workArea.Top)
            Top = workArea.Top;
    }

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(EvaluateViewModel.IsEvaluating):
                UpdateVisualState();
                break;
            case nameof(EvaluateViewModel.Result):
                UpdateResults();
                break;
            case nameof(EvaluateViewModel.ErrorMessage):
                UpdateVisualState();
                break;
            case nameof(EvaluateViewModel.Warnings):
                UpdateWarnings();
                break;
            case nameof(EvaluateViewModel.ShowFsn):
                UpdateDisplayMode();
                break;
        }
    }

    private void UpdateVisualState()
    {
        LoadingPanel.Visibility = _vm.IsEvaluating ? Visibility.Visible : Visibility.Collapsed;
        ErrorPanel.Visibility = !_vm.IsEvaluating && _vm.ErrorMessage != null ? Visibility.Visible : Visibility.Collapsed;
        ResultsList.Visibility = !_vm.IsEvaluating && _vm.Result != null ? Visibility.Visible : Visibility.Collapsed;

        if (_vm.ErrorMessage != null)
        {
            ErrorText.Text = _vm.ErrorMessage;
        }
    }

    private void UpdateResults()
    {
        if (_vm.Result == null) return;

        var result = _vm.Result;
        if (result.Total > result.Concepts.Count)
            TotalText.Text = $"{result.Concepts.Count} of {result.Total} concepts";
        else
            TotalText.Text = $"{result.Total} concepts";

        ResultsList.ItemsSource = result.Concepts;
        UpdateVisualState();
    }

    private void UpdateWarnings()
    {
        WarningsBanner.Visibility = _vm.Warnings.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void UpdateDisplayMode()
    {
        // Switch the Display column binding between Display and Fsn
        if (ResultsList.Columns.Count >= 2 && ResultsList.Columns[1] is DataGridTextColumn displayCol)
        {
            displayCol.Binding = new System.Windows.Data.Binding(_vm.ShowFsn ? "Fsn" : "Display");
            displayCol.Header = _vm.ShowFsn ? "FSN" : "Display";
        }

        // Refresh
        if (_vm.Result != null)
        {
            ResultsList.ItemsSource = null;
            ResultsList.ItemsSource = _vm.Result.Concepts;
        }
    }

    private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
        }
    }

    private void ConceptCode_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Hyperlink link && link.DataContext is EvaluationConcept concept)
        {
            OpenInShrimp(concept.Code);
        }
    }

    private void DiagramButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button btn && btn.DataContext is EvaluationConcept concept)
        {
            OpenDiagram(concept.Code, concept.Display);
        }
    }

    private void ContextShrimp_Click(object sender, RoutedEventArgs e)
    {
        if (ResultsList.SelectedItem is EvaluationConcept concept)
            OpenInShrimp(concept.Code);
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
                FileName = url,
                UseShellExecute = true
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
