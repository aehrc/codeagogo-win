using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;

namespace SNOMEDLookup;

public sealed class TrayAppContext : IDisposable
{
    private readonly NotifyIcon _notify;
    private readonly HotKeyManager _hotKey;
    private readonly SnowstormClient _client;

    public TrayAppContext()
    {
        _client = new SnowstormClient();

        _notify = new NotifyIcon
        {
            Text = "SNOMED Lookup",
            Icon = SystemIcons.Information,
            Visible = true,
            ContextMenuStrip = BuildMenu()
        };

        _hotKey = new HotKeyManager();
        _hotKey.HotKeyPressed += async (_, __) => await LookupSelectionAsync();
    }

    public void Start()
    {
        var s = Settings.Load();
        _hotKey.Register(s.HotKeyModifiers, s.HotKeyVirtualKey);
        Log.Info($"Registered hotkey modifiers=0x{s.HotKeyModifiers:X} vk=0x{s.HotKeyVirtualKey:X}");
    }

    private ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Lookup selection", null, async (_, __) => await LookupSelectionAsync());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Settings...", null, (_, __) => ShowSettings());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Quit", null, (_, __) => Quit());
        return menu;
    }

    private void ShowSettings()
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            var win = new SettingsWindow
            {
                Owner = null,
                WindowStartupLocation = WindowStartupLocation.CenterScreen
            };
            win.ShowDialog();

            var s = Settings.Load();
            _hotKey.Register(s.HotKeyModifiers, s.HotKeyVirtualKey);
            Log.Info($"Re-registered hotkey modifiers=0x{s.HotKeyModifiers:X} vk=0x{s.HotKeyVirtualKey:X}");
        });
    }

    private async Task LookupSelectionAsync()
    {
        try
        {
            var mouse = System.Windows.Forms.Control.MousePosition;

            string? raw = await ClipboardSelectionReader.ReadSelectionByCopyingAsync();
            Log.Info($"Selection raw='{raw ?? ""}'");

            var conceptId = ClipboardSelectionReader.ExtractFirstSnomedId(raw);
            if (string.IsNullOrWhiteSpace(conceptId))
            {
                PopupWindow.ShowAt(mouse.X, mouse.Y, "Not a SNOMED CT concept ID", "Select a numeric conceptId and try again.");
                return;
            }

            Log.Info($"Lookup conceptId={conceptId}");
            var res = await _client.LookupAsync(conceptId);

            var title = res.Pt ?? res.Fsn ?? conceptId;
            var subtitle = $"{res.ConceptId} • {res.ActiveText} • {res.Branch}";
            PopupWindow.ShowAt(mouse.X, mouse.Y, title, subtitle);
        }
        catch (Exception ex)
        {
            Log.Error($"LookupSelection failed: {ex.GetType().Name}: {ex.Message}");
            PopupWindow.ShowAt(System.Windows.Forms.Control.MousePosition.X,
                               System.Windows.Forms.Control.MousePosition.Y,
                               "Lookup failed",
                               ex.Message);
        }
    }

    private void Quit()
    {
        _notify.Visible = false;
        _notify.Dispose();
        System.Windows.Application.Current.Shutdown();
    }

    public void Dispose()
    {
        _hotKey.Dispose();
        _notify.Dispose();
    }
}
