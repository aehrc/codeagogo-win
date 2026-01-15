using System.Windows;

namespace SNOMEDLookup;

public partial class SettingsWindow : Window
{
    private readonly Settings _settings;

    public SettingsWindow()
    {
        InitializeComponent();
        _settings = Settings.Load();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        _settings.Save();
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();
}
