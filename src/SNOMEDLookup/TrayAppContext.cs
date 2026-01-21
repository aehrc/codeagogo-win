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
    private readonly FhirClient _client;

    public TrayAppContext()
    {
        var settings = Settings.Load();

        // Initialize FHIR client with saved URL
        _client = new FhirClient(settings.FhirBaseUrl);

        // Apply debug logging setting
        Log.DebugEnabled = settings.DebugLoggingEnabled;

        _notify = new NotifyIcon
        {
            Text = "SNOMED Lookup",
            Icon = SystemIcons.Information,
            Visible = true,
            ContextMenuStrip = BuildMenu()
        };

        _hotKey = new HotKeyManager();
        _hotKey.HotKeyPressed += async (_, _) => await LookupSelectionAsync();
    }

    public void Start()
    {
        var s = Settings.Load();
        _hotKey.Register(s.HotKeyModifiers, s.HotKeyVirtualKey);
        Log.Info($"Registered hotkey modifiers=0x{s.HotKeyModifiers:X} vk=0x{s.HotKeyVirtualKey:X}");
        Log.Info($"Using FHIR endpoint: {s.FhirBaseUrl}");
    }

    private ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Lookup selection", null, async (_, _) => await LookupSelectionAsync());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Settings...", null, (_, _) => ShowSettings());
        menu.Items.Add("View logs...", null, (_, _) => ViewLogs());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Quit", null, (_, _) => Quit());
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

            // Handle FHIR URL changes
            win.FhirUrlChanged += url =>
            {
                _client.SetBaseUrl(url);
                Log.Info($"FHIR endpoint changed to: {url}");
            };

            win.ShowDialog();

            // Re-register hotkey in case it changed
            var s = Settings.Load();
            _hotKey.Register(s.HotKeyModifiers, s.HotKeyVirtualKey);
            Log.Info($"Re-registered hotkey modifiers=0x{s.HotKeyModifiers:X} vk=0x{s.HotKeyVirtualKey:X}");
        });
    }

    private async Task LookupSelectionAsync()
    {
        var mouse = System.Windows.Forms.Control.MousePosition;
        PopupWindow? popup = null;

        try
        {
            // Try selection reading first (simulates Ctrl+C)
            string? text = await ClipboardSelectionReader.ReadSelectionByCopyingAsync();
            Log.Debug($"Selection text: '{Log.Snippet(text, 50)}'");

            // Fallback to clipboard if selection was empty
            if (string.IsNullOrWhiteSpace(text))
            {
                Log.Debug("Selection empty, falling back to clipboard");
                text = await ClipboardSelectionReader.ReadClipboardAsync();
                Log.Info($"Clipboard text: '{Log.Snippet(text, 50)}'");
            }

            // Extract SNOMED CT concept ID
            var conceptId = ClipboardSelectionReader.ExtractFirstSnomedId(text);
            if (string.IsNullOrWhiteSpace(conceptId))
            {
                PopupWindow.ShowErrorAt(mouse.X, mouse.Y,
                    "No SNOMED CT ID Found",
                    "Select or copy a SNOMED CT concept ID first, then press the hotkey.");
                return;
            }

            // Show loading popup
            popup = PopupWindow.ShowLoadingAt(mouse.X, mouse.Y, conceptId);

            Log.Info($"Looking up conceptId={conceptId}");
            var result = await _client.LookupAsync(conceptId);

            // Show result
            popup.ShowResult(result);
            Log.Info($"Found: {result.Pt ?? result.Fsn ?? conceptId} ({result.Edition})");
        }
        catch (RateLimitException ex)
        {
            Log.Error($"Rate limited: {ex.Message}");
            ShowError(popup, mouse, "Rate Limit Reached",
                "Too many requests. Please wait a moment and try again.");
        }
        catch (ConceptNotFoundException ex)
        {
            Log.Error($"Concept not found: {ex.Message}");
            ShowError(popup, mouse, "Concept Not Found",
                "This concept ID was not found in any SNOMED CT edition.");
        }
        catch (ApiException ex)
        {
            Log.Error($"API error: {ex.Message}");
            ShowError(popup, mouse, "Lookup Error", ex.Message);
        }
        catch (Exception ex)
        {
            Log.Error($"LookupSelection failed: {ex.GetType().Name}: {ex.Message}");
            ShowError(popup, mouse, "Lookup Failed",
                "An unexpected error occurred. Check logs for details.");
        }
    }

    private static void ShowError(PopupWindow? existingPopup, System.Drawing.Point mouse, string title, string message)
    {
        if (existingPopup != null)
        {
            existingPopup.ShowError(title, message);
        }
        else
        {
            PopupWindow.ShowErrorAt(mouse.X, mouse.Y, title, message);
        }
    }

    private void ViewLogs()
    {
        try
        {
            var logPath = Log.GetLogPath();
            if (File.Exists(logPath))
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = logPath,
                    UseShellExecute = true
                });
            }
            else
            {
                System.Windows.MessageBox.Show("No log file exists yet.", "View Logs",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            Log.Error($"Failed to open logs: {ex.Message}");
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
