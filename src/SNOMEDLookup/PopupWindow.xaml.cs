using System;
using System.Windows;
using System.Windows.Threading;

namespace SNOMEDLookup;

public partial class PopupWindow : Window
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(4) };

    public PopupWindow(string title, string subtitle)
    {
        InitializeComponent();
        TitleText.Text = title;
        SubtitleText.Text = subtitle;

        _timer.Tick += (_, __) =>
        {
            _timer.Stop();
            Close();
        };
    }

    public static void ShowAt(int screenX, int screenY, string title, string subtitle)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            var w = new PopupWindow(title, subtitle);

            w.Left = screenX + 12;
            w.Top = screenY + 12;

            w.Show();
            w._timer.Start();
        });
    }
}
