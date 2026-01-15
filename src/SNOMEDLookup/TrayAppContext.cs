using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.IO;

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
        _hotKey.HotKeyPressed += async (_, __) => await LookupClipboardAsync();
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
        menu.Items.Add("Lookup clipboard", null, async (_, __) => await LookupClipboardAsync());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Settings...", null, (_, __) => ShowSettings());
        menu.Items.Add("View logs...", null, (_, __) => ViewLogs());
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

    private async Task LookupClipboardAsync()
    {
        var mouse = System.Windows.Forms.Control.MousePosition;
        PopupWindow? loadingWindow = null;

        try
        {
            string? raw = await ClipboardSelectionReader.ReadClipboardAsync();
            Log.Info($"Clipboard text: '{raw ?? ""}'");

            var conceptId = ClipboardSelectionReader.ExtractFirstSnomedId(raw);
            if (string.IsNullOrWhiteSpace(conceptId))
            {
                PopupWindow.ShowAt(mouse.X, mouse.Y, 
                    "No SNOMED CT ID Found", 
                    "Copy a SNOMED CT concept ID to clipboard first, then press the hotkey.");
                return;
            }

            // Show loading popup immediately
            loadingWindow = PopupWindow.ShowAt(mouse.X, mouse.Y, 
                "Loading...", 
                $"Looking up concept {conceptId}...", 
                isLoading: true);

            Log.Info($"Lookup conceptId={conceptId}");
            var res = await _client.LookupAsync(conceptId);

            var title = res.Pt ?? res.Fsn ?? conceptId;
            var subtitle = $"{res.ConceptId} • {res.ActiveText} • {res.Branch}";
            
            // Update the loading window with results
            loadingWindow.UpdateContent(title, subtitle);
        }
        catch (RateLimitException ex)
        {
            Log.Error($"Rate limited: {ex.Message}");
            if (loadingWindow != null)
            {
                loadingWindow.UpdateContent("Rate Limit Reached", 
                    "Too many requests. Please wait a moment and try again.");
            }
            else
            {
                PopupWindow.ShowAt(mouse.X, mouse.Y,
                    "Rate Limit Reached",
                    "Too many requests. Please wait a moment and try again.");
            }
        }
        catch (ConceptNotFoundException ex)
        {
            Log.Error($"Concept not found: {ex.Message}");
            if (loadingWindow != null)
            {
                loadingWindow.UpdateContent("Concept Not Found",
                    "This concept ID was not found in the terminology server.");
            }
            else
            {
                PopupWindow.ShowAt(mouse.X, mouse.Y,
                    "Concept Not Found",
                    "This concept ID was not found in the terminology server.");
            }
        }
        catch (ApiException ex)
        {
            Log.Error($"API error: {ex.Message}");
            if (loadingWindow != null)
            {
                loadingWindow.UpdateContent("Lookup Error", ex.Message);
            }
            else
            {
                PopupWindow.ShowAt(mouse.X, mouse.Y, "Lookup Error", ex.Message);
            }
        }
        catch (Exception ex)
        {
            Log.Error($"LookupClipboard failed: {ex.GetType().Name}: {ex.Message}");
            if (loadingWindow != null)
            {
                loadingWindow.UpdateContent("Lookup Failed",
                    "An unexpected error occurred. Check logs for details.");
            }
            else
            {
                PopupWindow.ShowAt(mouse.X, mouse.Y,
                    "Lookup Failed",
                    "An unexpected error occurred. Check logs for details.");
            }
        }
    }

    private void ViewLogs()
    {
        try
        {
            var logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                                       "AEHRC", "SNOMED Lookup", "logs", "app.log");
            if (File.Exists(logPath))
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = logPath,
                    UseShellExecute = true
                });
            }
        }
        catch { }
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
