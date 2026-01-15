// Copyright 2026 CSIRO. Licensed under the Apache License, Version 2.0.
// SPDX-License-Identifier: Apache-2.0

using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Codeagogo;

public partial class PopupWindow : Window
{
    private readonly DispatcherTimer _errorTimer = new() { Interval = TimeSpan.FromSeconds(6) };
    private ConceptResult? _currentResult;
    private bool _isLoading;
    private bool _allowDeactivateClose = true;
    private bool _isClosing;

    /// <summary>
    /// Shared OntoserverClient for diagram and other operations.
    /// Set by TrayAppContext to reuse the warm cache.
    /// </summary>
    public OntoserverClient? Client { get; set; }

    public PopupWindow()
    {
        InitializeComponent();
        KeyDown += PopupWindow_KeyDown;
        Closing += (_, _) => _isClosing = true;

        _errorTimer.Tick += (_, _) =>
        {
            _errorTimer.Stop();
            SafeClose();
        };
    }

    private void SafeClose()
    {
        if (!_isClosing)
        {
            _isClosing = true;
            Close();
        }
    }

    private void PopupWindow_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            _errorTimer.Stop();
            SafeClose();
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        _errorTimer.Stop();
        SafeClose();
    }

    private void Window_Deactivated(object sender, EventArgs e)
    {
        // Close when clicking outside, but not during loading or if already closing
        if (_allowDeactivateClose && !_isLoading && !_isClosing)
        {
            _errorTimer.Stop();
            SafeClose();
        }
    }

    /// <summary>
    /// Shows the popup in loading state.
    /// </summary>
    public void ShowLoading(string conceptId)
    {
        Dispatcher.Invoke(() =>
        {
            _isLoading = true;
            CloseButton.Visibility = Visibility.Collapsed;
            HideAllPanels();
            LoadingPanel.Visibility = Visibility.Visible;
            LoadingText.Text = $"Looking up {conceptId}...";
        });
    }

    /// <summary>
    /// Shows the popup with a concept result.
    /// </summary>
    public void ShowResult(ConceptResult result)
    {
        Dispatcher.Invoke(() =>
        {
            _isLoading = false;
            _currentResult = result;

            CloseButton.Visibility = Visibility.Visible;
            HideAllPanels();
            ResultPanel.Visibility = Visibility.Visible;

            ConceptIdText.Text = result.ConceptId;
            FsnText.Text = result.Fsn ?? "-";
            PtText.Text = result.Pt ?? "-";

            // Status with color coding
            StatusText.Text = result.ActiveText;
            StatusText.Foreground = result.Active switch
            {
                true => new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x4E, 0xC9, 0x4E)), // Green
                false => new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xFF, 0x6B, 0x6B)), // Red
                _ => new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xAA, 0xAA, 0xAA)) // Gray
            };

            EditionText.Text = result.Branch;

            // Show inactive warning banner for inactive concepts
            InactiveWarningPanel.Visibility = result.Active == false
                ? Visibility.Visible
                : Visibility.Collapsed;

            // Show the appropriate copy button panel based on code system
            if (result.IsSnomedCT)
            {
                CopyButtonsPanel.Visibility = Visibility.Visible;

                // Enable/disable SNOMED copy buttons based on data availability
                CopyFsnBtn.IsEnabled = !string.IsNullOrEmpty(result.Fsn);
                CopyPtBtn.IsEnabled = !string.IsNullOrEmpty(result.Pt);
                CopyIdFsnBtn.IsEnabled = !string.IsNullOrEmpty(result.Fsn);
                CopyIdPtBtn.IsEnabled = !string.IsNullOrEmpty(result.Pt);
            }
            else
            {
                NonSnomedCopyButtonsPanel.Visibility = Visibility.Visible;

                // Enable/disable non-SNOMED copy buttons based on data availability
                CopyDisplayBtn.IsEnabled = !string.IsNullOrEmpty(result.Pt);
                CopyCodeDisplayBtn.IsEnabled = !string.IsNullOrEmpty(result.Pt);
            }

            // Results stay until dismissed - no auto-close
        });
    }

    /// <summary>
    /// Shows the popup with an error message.
    /// </summary>
    public void ShowError(string title, string message)
    {
        Dispatcher.Invoke(() =>
        {
            _isLoading = false;
            _currentResult = null;

            CloseButton.Visibility = Visibility.Visible;
            HideAllPanels();
            ErrorPanel.Visibility = Visibility.Visible;

            ErrorTitle.Text = title;
            ErrorMessage.Text = message;

            // Errors auto-close after 6 seconds, but can still be dismissed manually
            _errorTimer.Start();
        });
    }

    private void HideAllPanels()
    {
        LoadingPanel.Visibility = Visibility.Collapsed;
        ErrorPanel.Visibility = Visibility.Collapsed;
        ResultPanel.Visibility = Visibility.Collapsed;
        CopyButtonsPanel.Visibility = Visibility.Collapsed;
        NonSnomedCopyButtonsPanel.Visibility = Visibility.Collapsed;
        SimplePanel.Visibility = Visibility.Collapsed;
    }

    #region Copy Button Handlers

    private void CopyId_Click(object sender, RoutedEventArgs e)
    {
        if (_currentResult == null) return;
        CopyToClipboard(_currentResult.ConceptId);
    }

    private void CopyFsn_Click(object sender, RoutedEventArgs e)
    {
        if (_currentResult?.Fsn == null) return;
        CopyToClipboard(_currentResult.Fsn);
    }

    private void CopyPt_Click(object sender, RoutedEventArgs e)
    {
        if (_currentResult?.Pt == null) return;
        CopyToClipboard(_currentResult.Pt);
    }

    private void CopyIdFsn_Click(object sender, RoutedEventArgs e)
    {
        if (_currentResult?.Fsn == null) return;
        CopyToClipboard($"{_currentResult.ConceptId} | {_currentResult.Fsn} | ");
    }

    private void CopyIdPt_Click(object sender, RoutedEventArgs e)
    {
        if (_currentResult?.Pt == null) return;
        CopyToClipboard($"{_currentResult.ConceptId} | {_currentResult.Pt} | ");
    }

    private void CopyCode_Click(object sender, RoutedEventArgs e)
    {
        if (_currentResult == null) return;
        CopyToClipboard(_currentResult.ConceptId);
    }

    private void CopyDisplay_Click(object sender, RoutedEventArgs e)
    {
        if (_currentResult?.Pt == null) return;
        CopyToClipboard(_currentResult.Pt);
    }

    private void CopyCodeDisplay_Click(object sender, RoutedEventArgs e)
    {
        if (_currentResult?.Pt == null) return;
        CopyToClipboard($"{_currentResult.ConceptId} | {_currentResult.Pt} | ");
    }

    private void Browser_Click(object sender, RoutedEventArgs e)
    {
        if (_currentResult == null) return;

        try
        {
            var url = ShrimpUrlBuilder.BuildUrl(_currentResult, Settings.Load().FhirBaseUrl);
            if (url != null && IsAllowedUrl(url))
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
                Log.Info($"Opened Shrimp browser for {_currentResult.ConceptId}");
            }
            else if (url != null)
            {
                Log.Error($"Blocked non-HTTP URL: {url}");
            }
        }
        catch (Exception ex)
        {
            Log.Error($"Failed to open Shrimp browser: {ex.Message}");
        }
    }

    /// <summary>
    /// Validates that a URL uses http or https scheme to prevent shell-execute injection.
    /// </summary>
    private static bool IsAllowedUrl(string url)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }

    private void Diagram_Click(object sender, RoutedEventArgs e)
    {
        if (_currentResult == null) return;

        try
        {
            var client = Client ?? new OntoserverClient(baseUrl: Settings.Load().FhirBaseUrl);
            var window = new Visualization.VisualizationWindow(
                client, _currentResult.ConceptId, _currentResult.Pt);
            window.Show();
            window.Activate();
            Log.Info($"Opened diagram for {_currentResult.ConceptId}");
        }
        catch (Exception ex)
        {
            Log.Error($"Failed to open diagram: {ex.Message}");
        }
    }

    private void CopyToClipboard(string text)
    {
        try
        {
            // Temporarily prevent deactivate-close while clipboard operation happens
            _allowDeactivateClose = false;
            System.Windows.Clipboard.SetText(text);
            Log.Debug($"Copied to clipboard: {Log.Snippet(text, 50)}");

            // Re-enable after a brief delay
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                _allowDeactivateClose = true;
            };
            timer.Start();
        }
        catch (Exception ex)
        {
            Log.Error($"Failed to copy to clipboard: {ex.Message}");
            _allowDeactivateClose = true;
        }
    }

    #endregion

    #region Factory Methods

    /// <summary>
    /// Creates and shows a popup in loading state at the specified position.
    /// </summary>
    public static PopupWindow ShowLoadingAt(int screenX, int screenY, string conceptId)
    {
        PopupWindow? window = null;

        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            window = new PopupWindow();
            PositionWindow(window, screenX, screenY);
            window.ShowLoading(conceptId);
            window.Show();
        });

        return window!;
    }

    /// <summary>
    /// Creates and shows a popup with an error at the specified position.
    /// </summary>
    public static PopupWindow ShowErrorAt(int screenX, int screenY, string title, string message)
    {
        PopupWindow? window = null;

        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            window = new PopupWindow();
            PositionWindow(window, screenX, screenY);
            window.ShowError(title, message);
            window.Show();
        });

        return window!;
    }

    /// <summary>
    /// Legacy factory method for backward compatibility.
    /// </summary>
    public static PopupWindow ShowAt(int screenX, int screenY, string title, string subtitle, bool isLoading = false)
    {
        PopupWindow? window = null;

        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            window = new PopupWindow();
            PositionWindow(window, screenX, screenY);

            // Use simple panel for legacy display
            window.HideAllPanels();
            window.SimplePanel.Visibility = Visibility.Visible;
            window.TitleText.Text = title;
            window.SubtitleText.Text = subtitle;

            window.Show();

            if (!isLoading)
            {
                window._errorTimer.Start();
            }
        });

        return window!;
    }

    private static void PositionWindow(PopupWindow window, int screenX, int screenY)
    {
        // Get the screen bounds where the cursor is
        var screen = Screen.FromPoint(new System.Drawing.Point(screenX, screenY));
        double screenLeft = screen.WorkingArea.Left;
        double screenTop = screen.WorkingArea.Top;
        double screenRight = screen.WorkingArea.Right;
        double screenBottom = screen.WorkingArea.Bottom;

        // Use the explicit window dimensions from XAML (more reliable than DesiredSize)
        double windowWidth = window.Width;
        double windowHeight = window.Height;

        // If dimensions are NaN (auto), fall back to measurement
        if (double.IsNaN(windowWidth) || double.IsNaN(windowHeight))
        {
            window.Measure(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity));
            windowWidth = double.IsNaN(windowWidth) ? window.DesiredSize.Width : windowWidth;
            windowHeight = double.IsNaN(windowHeight) ? window.DesiredSize.Height : windowHeight;
        }

        // Calculate desired position (offset from cursor)
        double desiredLeft = screenX + 12;
        double desiredTop = screenY + 12;

        // Adjust horizontal position if it would go off-screen on the right
        if (desiredLeft + windowWidth > screenRight)
        {
            // Try positioning to the left of the cursor
            desiredLeft = screenX - windowWidth - 12;
        }
        // If still off-screen on the left, clamp to screen edge
        if (desiredLeft < screenLeft)
        {
            desiredLeft = screenLeft + 8;
        }

        // Adjust vertical position if it would go off-screen on the bottom
        if (desiredTop + windowHeight > screenBottom)
        {
            // Try positioning above the cursor
            desiredTop = screenY - windowHeight - 12;
        }
        // If still off-screen on the top, clamp to screen edge
        if (desiredTop < screenTop)
        {
            desiredTop = screenTop + 8;
        }

        window.Left = desiredLeft;
        window.Top = desiredTop;
    }

    #endregion

    /// <summary>
    /// Legacy method for updating content.
    /// </summary>
    public void UpdateContent(string title, string subtitle)
    {
        Dispatcher.Invoke(() =>
        {
            _isLoading = false;
            CloseButton.Visibility = Visibility.Visible;
            HideAllPanels();
            SimplePanel.Visibility = Visibility.Visible;
            TitleText.Text = title;
            SubtitleText.Text = subtitle;
            _errorTimer.Start();
        });
    }
}
