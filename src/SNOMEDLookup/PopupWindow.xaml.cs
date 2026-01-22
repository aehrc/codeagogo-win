using System;
using System.Drawing;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace SNOMEDLookup;

public partial class PopupWindow : Window
{
    private readonly DispatcherTimer _errorTimer = new() { Interval = TimeSpan.FromSeconds(6) };
    private ConceptResult? _currentResult;
    private bool _isLoading;
    private bool _allowDeactivateClose = true;
    private bool _isClosing;

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
            CopyButtonsPanel.Visibility = Visibility.Visible;

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

            EditionText.Text = result.Edition ?? EditionNames.GetEditionNameOrModuleId(result.ModuleId);

            // Enable/disable copy buttons based on data availability
            CopyFsnBtn.IsEnabled = !string.IsNullOrEmpty(result.Fsn);
            CopyPtBtn.IsEnabled = !string.IsNullOrEmpty(result.Pt);
            CopyIdFsnBtn.IsEnabled = !string.IsNullOrEmpty(result.Fsn);
            CopyIdPtBtn.IsEnabled = !string.IsNullOrEmpty(result.Pt);

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
    /// Creates and shows a popup in loading state, positioned near the selected text or mouse cursor.
    /// </summary>
    /// <param name="screenX">Mouse X position (fallback if selection bounds unavailable).</param>
    /// <param name="screenY">Mouse Y position (fallback if selection bounds unavailable).</param>
    /// <param name="conceptId">The concept ID being looked up.</param>
    /// <param name="selectionBounds">Optional bounding rectangle of the selected text.</param>
    public static PopupWindow ShowLoadingAt(int screenX, int screenY, string conceptId, Rectangle? selectionBounds = null)
    {
        PopupWindow? window = null;

        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            window = new PopupWindow();
            PositionWindow(window, screenX, screenY, selectionBounds);
            window.ShowLoading(conceptId);
            window.Show();
        });

        return window!;
    }

    /// <summary>
    /// Creates and shows a popup with an error, positioned near the selected text or mouse cursor.
    /// </summary>
    /// <param name="screenX">Mouse X position (fallback if selection bounds unavailable).</param>
    /// <param name="screenY">Mouse Y position (fallback if selection bounds unavailable).</param>
    /// <param name="title">Error title.</param>
    /// <param name="message">Error message.</param>
    /// <param name="selectionBounds">Optional bounding rectangle of the selected text.</param>
    public static PopupWindow ShowErrorAt(int screenX, int screenY, string title, string message, Rectangle? selectionBounds = null)
    {
        PopupWindow? window = null;

        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            window = new PopupWindow();
            PositionWindow(window, screenX, screenY, selectionBounds);
            window.ShowError(title, message);
            window.Show();
        });

        return window!;
    }

    /// <summary>
    /// Legacy factory method for backward compatibility.
    /// </summary>
    public static PopupWindow ShowAt(int screenX, int screenY, string title, string subtitle, bool isLoading = false, Rectangle? selectionBounds = null)
    {
        PopupWindow? window = null;

        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            window = new PopupWindow();
            PositionWindow(window, screenX, screenY, selectionBounds);

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

    /// <summary>
    /// Positions the window intelligently based on selection bounds or mouse position.
    /// Priority: 1) Near selected text 2) Near mouse cursor 3) Clamped to screen bounds
    /// </summary>
    private static void PositionWindow(PopupWindow window, int screenX, int screenY, Rectangle? selectionBounds = null)
    {
        // Ensure window is measured so we know its size
        window.Measure(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity));
        double windowWidth = window.DesiredSize.Width;
        double windowHeight = window.DesiredSize.Height;

        // Get the screen bounds where the cursor is (or selection is)
        var referencePoint = selectionBounds.HasValue
            ? new System.Drawing.Point(selectionBounds.Value.X, selectionBounds.Value.Y)
            : new System.Drawing.Point(screenX, screenY);
        var screen = Screen.FromPoint(referencePoint);
        var screenBounds = new Rectangle(
            screen.WorkingArea.Left,
            screen.WorkingArea.Top,
            screen.WorkingArea.Width,
            screen.WorkingArea.Height
        );

        // Calculate optimal position using the helper
        var mousePosition = new System.Drawing.Point(screenX, screenY);
        var position = SelectionPositionHelper.CalculatePopupPosition(
            selectionBounds,
            mousePosition,
            windowWidth,
            windowHeight,
            screenBounds
        );

        window.Left = position.X;
        window.Top = position.Y;
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
