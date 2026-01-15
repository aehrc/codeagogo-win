// Copyright 2026 CSIRO. Licensed under the Apache License, Version 2.0.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace Codeagogo;

public partial class SearchWindow : Window
{
    private readonly SearchViewModel _vm;
    private readonly IntPtr _previousWindow;

    /// <summary>
    /// The formatted text to insert, or null if cancelled.
    /// </summary>
    public string? InsertText { get; private set; }

    public SearchWindow(OntoserverClient client, InsertFormat defaultFormat, IntPtr previousWindow)
    {
        InitializeComponent();

        _previousWindow = previousWindow;
        _vm = new SearchViewModel(client, defaultFormat);

        ResultsList.ItemsSource = _vm.Results;
        ResultsList.SelectionChanged += (_, _) =>
        {
            InsertButton.IsEnabled = ResultsList.SelectedItem != null;
        };

        InitializeCodeSystemCombo();
        InitializeEditionComboDefault();
        InitializeFormatCombo(defaultFormat);

        Loaded += async (_, _) =>
        {
            SearchTextBox.Focus();
            await LoadEditionsAsync(client);
        };
    }

    private void InitializeCodeSystemCombo()
    {
        CodeSystemCombo.Items.Add(new ComboBoxItem { Content = "SNOMED CT", Tag = "http://snomed.info/sct" });
        CodeSystemCombo.Items.Add(new ComboBoxItem { Content = "LOINC", Tag = "http://loinc.org" });
        CodeSystemCombo.Items.Add(new ComboBoxItem { Content = "ICD-10", Tag = "http://hl7.org/fhir/sid/icd-10" });
        CodeSystemCombo.Items.Add(new ComboBoxItem { Content = "ICD-10-CM", Tag = "http://hl7.org/fhir/sid/icd-10-cm" });
        CodeSystemCombo.Items.Add(new ComboBoxItem { Content = "RxNorm", Tag = "http://www.nlm.nih.gov/research/umls/rxnorm" });
        CodeSystemCombo.SelectedIndex = 0;
    }

    /// <summary>
    /// Initializes the edition combo with just "All editions" as a placeholder
    /// until the server responds with available editions.
    /// </summary>
    private void InitializeEditionComboDefault()
    {
        EditionCombo.Items.Clear();
        EditionCombo.Items.Add(new ComboBoxItem { Content = "All editions", Tag = (string?)null });
        EditionCombo.Items.Add(new ComboBoxItem { Content = "Loading editions...", Tag = (string?)null, IsEnabled = false });
        EditionCombo.SelectedIndex = 0;
    }

    /// <summary>
    /// Loads available SNOMED CT editions from the server and populates the edition combo.
    /// Falls back to just "All editions" if the server call fails.
    /// </summary>
    private async Task LoadEditionsAsync(OntoserverClient client)
    {
        try
        {
            var editions = await client.GetAvailableEditionsAsync();

            // Remember current selection tag so we can restore it
            var previousTag = (EditionCombo.SelectedItem as ComboBoxItem)?.Tag as string;

            EditionCombo.Items.Clear();
            EditionCombo.Items.Add(new ComboBoxItem { Content = "All editions", Tag = (string?)null });

            foreach (var edition in editions)
            {
                EditionCombo.Items.Add(new ComboBoxItem { Content = edition.Title, Tag = edition.EditionUri });
            }

            // Restore previous selection if it still exists, otherwise select "All editions"
            var restored = false;
            if (previousTag != null)
            {
                for (int i = 0; i < EditionCombo.Items.Count; i++)
                {
                    if (EditionCombo.Items[i] is ComboBoxItem item && item.Tag as string == previousTag)
                    {
                        EditionCombo.SelectedIndex = i;
                        restored = true;
                        break;
                    }
                }
            }

            if (!restored)
            {
                EditionCombo.SelectedIndex = 0;
            }
        }
        catch (Exception ex)
        {
            Log.Error($"Failed to load editions: {ex.Message}");

            // Fall back to just "All editions"
            EditionCombo.Items.Clear();
            EditionCombo.Items.Add(new ComboBoxItem { Content = "All editions", Tag = (string?)null });
            EditionCombo.SelectedIndex = 0;
        }
    }

    private void InitializeFormatCombo(InsertFormat defaultFormat)
    {
        FormatCombo.Items.Add(new ComboBoxItem { Content = "ID only", Tag = InsertFormat.IdOnly });
        FormatCombo.Items.Add(new ComboBoxItem { Content = "PT only", Tag = InsertFormat.PtOnly });
        FormatCombo.Items.Add(new ComboBoxItem { Content = "FSN only", Tag = InsertFormat.FsnOnly });
        FormatCombo.Items.Add(new ComboBoxItem { Content = "ID|PT|", Tag = InsertFormat.IdPipePT });
        FormatCombo.Items.Add(new ComboBoxItem { Content = "ID|FSN|", Tag = InsertFormat.IdPipeFSN });

        // Select the default format
        for (int i = 0; i < FormatCombo.Items.Count; i++)
        {
            if (FormatCombo.Items[i] is ComboBoxItem item && item.Tag is InsertFormat fmt && fmt == defaultFormat)
            {
                FormatCombo.SelectedIndex = i;
                break;
            }
        }
    }

    private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        _vm.SearchDebounced(SearchTextBox.Text);
    }

    private void CodeSystemCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CodeSystemCombo.SelectedItem is ComboBoxItem item && item.Tag is string system)
        {
            _vm.SelectedCodeSystem = system;

            // Show/hide edition combo (only for SNOMED CT)
            var isSnomedCt = system.Contains("snomed.info");
            EditionLabel.Visibility = isSnomedCt ? Visibility.Visible : Visibility.Collapsed;
            EditionCombo.Visibility = isSnomedCt ? Visibility.Visible : Visibility.Collapsed;

            if (!isSnomedCt)
                _vm.SelectedEditionUri = null;

            // Re-trigger search with new system
            _vm.SearchDebounced(SearchTextBox.Text);
        }
    }

    private void EditionCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (EditionCombo.SelectedItem is ComboBoxItem item)
        {
            _vm.SelectedEditionUri = item.Tag as string;
            _vm.SearchDebounced(SearchTextBox.Text);
        }
    }

    private void FormatCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (FormatCombo.SelectedItem is ComboBoxItem item && item.Tag is InsertFormat fmt)
        {
            _vm.SelectedFormat = fmt;
        }
    }

    private void Insert_Click(object sender, RoutedEventArgs e)
    {
        PerformInsert();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        InsertText = null;
        _vm.Dispose();
        Close();
    }

    private void ResultsList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ResultsList.SelectedItem != null)
        {
            PerformInsert();
        }
    }

    private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            InsertText = null;
            _vm.Dispose();
            Close();
        }
        else if (e.Key == Key.Enter && ResultsList.SelectedItem != null)
        {
            PerformInsert();
        }
    }

    private void PerformInsert()
    {
        if (ResultsList.SelectedItem is SearchResultItem item)
        {
            InsertText = _vm.FormatForInsertion(item);
            _vm.Dispose();
            Close();
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _vm.Dispose();

        // Restore focus to previous window
        if (_previousWindow != IntPtr.Zero)
        {
            SetForegroundWindow(_previousWindow);
        }

        base.OnClosed(e);
    }

    /// <summary>
    /// Positions the window near the specified screen coordinates.
    /// </summary>
    public void PositionNearCursor(int x, int y)
    {
        Left = x + 15;
        Top = y + 15;

        // Ensure window stays on screen
        var screen = System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point(x, y));
        var workArea = screen.WorkingArea;

        if (Left + Width > workArea.Right)
            Left = workArea.Right - Width;
        if (Top + Height > workArea.Bottom)
            Top = workArea.Bottom - Height;
        if (Left < workArea.Left)
            Left = workArea.Left;
        if (Top < workArea.Top)
            Top = workArea.Top;
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);
}

/// <summary>
/// Converts null/empty strings to Collapsed visibility.
/// </summary>
public sealed class NullToCollapsedConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string s && !string.IsNullOrEmpty(s))
            return Visibility.Visible;
        return Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
