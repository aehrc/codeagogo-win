using System;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Threading;

namespace SNOMEDLookup;

public partial class PopupWindow : Window
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(4) };
    private bool _isLoading;

    public PopupWindow(string title, string subtitle, bool isLoading = false)
    {
        InitializeComponent();
        TitleText.Text = title;
        SubtitleText.Text = subtitle;
        _isLoading = isLoading;

        // Allow closing with Escape key
        KeyDown += PopupWindow_KeyDown;

        if (!isLoading)
        {
            _timer.Tick += (_, __) =>
            {
                _timer.Stop();
                Close();
            };
        }
    }

    private void PopupWindow_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            _timer.Stop();
            Close();
        }
    }

    public static PopupWindow ShowAt(int screenX, int screenY, string title, string subtitle, bool isLoading = false)
    {
        PopupWindow? window = null;

        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            window = new PopupWindow(title, subtitle, isLoading);

            // Calculate desired position
            double desiredLeft = screenX + 12;
            double desiredTop = screenY + 12;

            // Get the screen bounds where the cursor is
            var screen = Screen.FromPoint(new System.Drawing.Point(screenX, screenY));
            double screenLeft = screen.WorkingArea.Left;
            double screenTop = screen.WorkingArea.Top;
            double screenRight = screen.WorkingArea.Right;
            double screenBottom = screen.WorkingArea.Bottom;

            // Ensure window is measured so we know its size
            window.Measure(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity));
            double windowWidth = window.DesiredSize.Width;
            double windowHeight = window.DesiredSize.Height;

            // Adjust horizontal position if it would go off-screen
            if (desiredLeft + windowWidth > screenRight)
            {
                desiredLeft = screenRight - windowWidth - 12;
            }
            if (desiredLeft < screenLeft)
            {
                desiredLeft = screenLeft + 12;
            }

            // Adjust vertical position if it would go off-screen
            if (desiredTop + windowHeight > screenBottom)
            {
                desiredTop = screenBottom - windowHeight - 12;
            }
            if (desiredTop < screenTop)
            {
                desiredTop = screenTop + 12;
            }

            window.Left = desiredLeft;
            window.Top = desiredTop;

            window.Show();

            if (!isLoading)
            {
                window._timer.Start();
            }
        });

        return window!;
    }

    public void UpdateContent(string title, string subtitle)
    {
        Dispatcher.Invoke(() =>
        {
            TitleText.Text = title;
            SubtitleText.Text = subtitle;
            _isLoading = false;
            _timer.Start();
        });
    }
}