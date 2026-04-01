// Copyright 2026 CSIRO. Licensed under the Apache License, Version 2.0.
// SPDX-License-Identifier: Apache-2.0

using System.Drawing;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Forms;

namespace Codeagogo;

/// <summary>
/// Main application context that manages the system tray icon, hotkey registration,
/// and coordinates SNOMED CT concept lookups.
/// </summary>
/// <remarks>
/// This class serves as the central coordinator for the application:
/// - Creates and manages the NotifyIcon in the system tray
/// - Registers the global hotkey via HotKeyManager
/// - Handles lookup requests by coordinating between ClipboardSelectionReader and FhirClient
/// - Manages the popup window lifecycle for displaying results
/// </remarks>
public sealed class TrayAppContext : IDisposable
{
    private const int MaxEclLength = 50_000;

    private readonly NotifyIcon _notify;
    private readonly HotKeyManager _hotKey;
    private readonly OntoserverClient _client;
    private readonly Lazy<ECLBridge> _eclBridge = new(() => new ECLBridge());
    private int _lookupHotKeyId;
    private int _searchHotKeyId;
    private int _replaceHotKeyId;
    private int _eclFormatHotKeyId;
    private int _shrimpHotKeyId;
    private int _evaluateHotKeyId;
    private PopupWindow? _currentPopup;
    private SearchWindow? _currentSearchWindow;
    private EvaluateWindow? _currentEvaluateWindow;
    private ECLWorkbenchWindow? _currentWorkbench;
    private ECLReferenceWindow? _currentReferenceWindow;

    /// <summary>
    /// Initializes the tray application context with system tray icon and hotkey handler.
    /// </summary>
    public TrayAppContext()
    {
        var settings = Settings.Load();

        _client = new OntoserverClient(baseUrl: settings.FhirBaseUrl, installId: settings.InstallId);

        // Apply debug logging setting
        Log.DebugEnabled = settings.DebugLogging;

        _notify = new NotifyIcon
        {
            Text = "Codeagogo",
            Icon = LoadAppIcon(),
            Visible = true,
            ContextMenuStrip = BuildMenu()
        };

        _hotKey = new HotKeyManager();
    }

    /// <summary>
    /// Starts the application by registering the global hotkey.
    /// </summary>
    public void Start()
    {
        var s = Settings.Load();
        _lookupHotKeyId = _hotKey.Register(s.LookupHotKeyModifiers, s.LookupHotKeyVirtualKey, async () => await LookupSelectionAsync());
        Log.Info($"Registered lookup hotkey id={_lookupHotKeyId} modifiers=0x{s.LookupHotKeyModifiers:X} vk=0x{s.LookupHotKeyVirtualKey:X}");

        try
        {
            _searchHotKeyId = _hotKey.Register(s.SearchHotKeyModifiers, s.SearchHotKeyVirtualKey, () => ShowSearchPanelAsync());
            Log.Info($"Registered search hotkey id={_searchHotKeyId} modifiers=0x{s.SearchHotKeyModifiers:X} vk=0x{s.SearchHotKeyVirtualKey:X}");
        }
        catch (Exception ex)
        {
            Log.Error($"Failed to register search hotkey: {ex.Message}");
        }

        try
        {
            _replaceHotKeyId = _hotKey.Register(s.ReplaceHotKeyModifiers, s.ReplaceHotKeyVirtualKey, async () => await ReplaceSelectionAsync());
            Log.Info($"Registered replace hotkey id={_replaceHotKeyId} modifiers=0x{s.ReplaceHotKeyModifiers:X} vk=0x{s.ReplaceHotKeyVirtualKey:X}");
        }
        catch (Exception ex)
        {
            Log.Error($"Failed to register replace hotkey: {ex.Message}");
        }

        try
        {
            _eclFormatHotKeyId = _hotKey.Register(s.EclFormatHotKeyModifiers, s.EclFormatHotKeyVirtualKey, async () => await FormatECLSelectionAsync());
            Log.Info($"Registered ECL format hotkey id={_eclFormatHotKeyId} modifiers=0x{s.EclFormatHotKeyModifiers:X} vk=0x{s.EclFormatHotKeyVirtualKey:X}");
        }
        catch (Exception ex)
        {
            Log.Error($"Failed to register ECL format hotkey: {ex.Message}");
        }

        try
        {
            _shrimpHotKeyId = _hotKey.Register(s.ShrimpHotKeyModifiers, s.ShrimpHotKeyVirtualKey, async () => await OpenInShrimpAsync());
            Log.Info($"Registered Shrimp hotkey id={_shrimpHotKeyId} modifiers=0x{s.ShrimpHotKeyModifiers:X} vk=0x{s.ShrimpHotKeyVirtualKey:X}");
        }
        catch (Exception ex)
        {
            Log.Error($"Failed to register Shrimp hotkey: {ex.Message}");
        }

        try
        {
            _evaluateHotKeyId = _hotKey.Register(s.EvaluateHotKeyModifiers, s.EvaluateHotKeyVirtualKey, async () => await EvaluateEclSelectionAsync());
            Log.Info($"Registered evaluate hotkey id={_evaluateHotKeyId} modifiers=0x{s.EvaluateHotKeyModifiers:X} vk=0x{s.EvaluateHotKeyVirtualKey:X}");
        }
        catch (Exception ex)
        {
            Log.Error($"Failed to register evaluate hotkey: {ex.Message}");
        }

        Log.Info($"Using FHIR endpoint: {s.FhirBaseUrl}");
    }

    private ContextMenuStrip BuildMenu()
    {
        var s = Settings.Load();
        var menu = new ContextMenuStrip { ShowImageMargin = false, ShowCheckMargin = false };
        menu.Renderer = new ShortcutMenuRenderer();

        AddMenuItem(menu, "Lookup selection", s.LookupHotKeyModifiers, s.LookupHotKeyVirtualKey, async (_, _) => await LookupSelectionAsync());
        AddMenuItem(menu, "Search concepts", s.SearchHotKeyModifiers, s.SearchHotKeyVirtualKey, (_, _) => ShowSearchPanelAsync());
        AddMenuItem(menu, "Replace selection", s.ReplaceHotKeyModifiers, s.ReplaceHotKeyVirtualKey, async (_, _) => await ReplaceSelectionAsync());
        menu.Items.Add(new ToolStripSeparator());
        AddMenuItem(menu, "Format ECL", s.EclFormatHotKeyModifiers, s.EclFormatHotKeyVirtualKey, async (_, _) => await FormatECLSelectionAsync());
        AddMenuItem(menu, "Evaluate ECL", s.EvaluateHotKeyModifiers, s.EvaluateHotKeyVirtualKey, async (_, _) => await EvaluateEclSelectionAsync());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("ECL Reference...", null, (_, _) => ShowECLReferencePanel());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Settings...", null, (_, _) => ShowSettings());
        menu.Items.Add("Check for updates", null, async (_, _) => await CheckForUpdatesManualAsync());
        menu.Items.Add("View logs...", null, (_, _) => ViewLogs());
        AddMenuItem(menu, "Open in Shrimp", s.ShrimpHotKeyModifiers, s.ShrimpHotKeyVirtualKey, async (_, _) => await OpenInShrimpAsync());
        menu.Items.Add("About Codeagogo", null, (_, _) => ShowAbout());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Quit", null, (_, _) => Quit());
        return menu;
    }

    /// <summary>
    /// Adds a menu item with a right-aligned shortcut key display string.
    /// </summary>
    private static void AddMenuItem(ContextMenuStrip menu, string text, uint modifiers, uint virtualKey, EventHandler handler)
    {
        var item = new ToolStripMenuItem(text);
        item.ShortcutKeyDisplayString = FormatHotkey(modifiers, virtualKey);
        item.Click += handler;
        menu.Items.Add(item);
    }

    /// <summary>
    /// Formats Win32 hotkey modifiers and virtual key code into a readable string (e.g., "Ctrl+Shift+L").
    /// </summary>
    private static string FormatHotkey(uint modifiers, uint virtualKey)
    {
        var parts = new List<string>();
        if ((modifiers & 0x0002) != 0) parts.Add("Ctrl");
        if ((modifiers & 0x0001) != 0) parts.Add("Alt");
        if ((modifiers & 0x0004) != 0) parts.Add("Shift");
        if ((modifiers & 0x0008) != 0) parts.Add("Win");

        // Convert virtual key code to readable name
        var keyName = virtualKey switch
        {
            >= 0x30 and <= 0x39 => ((char)virtualKey).ToString(), // 0-9
            >= 0x41 and <= 0x5A => ((char)virtualKey).ToString(), // A-Z
            >= 0x70 and <= 0x87 => $"F{virtualKey - 0x6F}",      // F1-F24
            _ => $"0x{virtualKey:X2}"
        };

        parts.Add(keyName);
        return string.Join("+", parts);
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

            // Unregister old hotkeys and register new ones
            _hotKey.Unregister(_lookupHotKeyId);
            _hotKey.Unregister(_searchHotKeyId);
            _hotKey.Unregister(_replaceHotKeyId);
            _hotKey.Unregister(_eclFormatHotKeyId);
            _hotKey.Unregister(_shrimpHotKeyId);
            _hotKey.Unregister(_evaluateHotKeyId);

            var s = Settings.Load();
            _lookupHotKeyId = _hotKey.Register(s.LookupHotKeyModifiers, s.LookupHotKeyVirtualKey, async () => await LookupSelectionAsync());
            Log.Info($"Re-registered lookup hotkey id={_lookupHotKeyId}");

            try
            {
                _searchHotKeyId = _hotKey.Register(s.SearchHotKeyModifiers, s.SearchHotKeyVirtualKey, () => ShowSearchPanelAsync());
                Log.Info($"Re-registered search hotkey id={_searchHotKeyId}");
            }
            catch (Exception ex)
            {
                Log.Error($"Failed to re-register search hotkey: {ex.Message}");
            }

            try
            {
                _replaceHotKeyId = _hotKey.Register(s.ReplaceHotKeyModifiers, s.ReplaceHotKeyVirtualKey, async () => await ReplaceSelectionAsync());
                Log.Info($"Re-registered replace hotkey id={_replaceHotKeyId}");
            }
            catch (Exception ex)
            {
                Log.Error($"Failed to re-register replace hotkey: {ex.Message}");
            }

            try
            {
                _eclFormatHotKeyId = _hotKey.Register(s.EclFormatHotKeyModifiers, s.EclFormatHotKeyVirtualKey, async () => await FormatECLSelectionAsync());
                Log.Info($"Re-registered ECL format hotkey id={_eclFormatHotKeyId}");
            }
            catch (Exception ex)
            {
                Log.Error($"Failed to re-register ECL format hotkey: {ex.Message}");
            }

            try
            {
                _shrimpHotKeyId = _hotKey.Register(s.ShrimpHotKeyModifiers, s.ShrimpHotKeyVirtualKey, async () => await OpenInShrimpAsync());
                Log.Info($"Re-registered Shrimp hotkey id={_shrimpHotKeyId}");
            }
            catch (Exception ex)
            {
                Log.Error($"Failed to re-register Shrimp hotkey: {ex.Message}");
            }

            try
            {
                _evaluateHotKeyId = _hotKey.Register(s.EvaluateHotKeyModifiers, s.EvaluateHotKeyVirtualKey, async () => await EvaluateEclSelectionAsync());
                Log.Info($"Re-registered evaluate hotkey id={_evaluateHotKeyId}");
            }
            catch (Exception ex)
            {
                Log.Error($"Failed to re-register evaluate hotkey: {ex.Message}");
            }

            // Rebuild menu to reflect updated hotkey shortcuts
            _notify.ContextMenuStrip = BuildMenu();
        });
    }

    private void ShowAbout()
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            var win = new AboutWindow
            {
                Owner = null,
                WindowStartupLocation = WindowStartupLocation.CenterScreen
            };
            win.ShowDialog();
        });
    }

    private void ShowSearchPanelAsync()
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            // If search window is already open, bring it to focus
            if (_currentSearchWindow != null && _currentSearchWindow.IsVisible)
            {
                _currentSearchWindow.Activate();
                return;
            }

            var mouse = System.Windows.Forms.Control.MousePosition;
            var previousWindow = GetForegroundWindow();
            var settings = Settings.Load();

            var searchWindow = new SearchWindow(_client, settings.DefaultInsertFormat, previousWindow);
            searchWindow.PositionNearCursor(mouse.X, mouse.Y);
            _currentSearchWindow = searchWindow;

            searchWindow.Closed += async (_, _) =>
            {
                _currentSearchWindow = null;

                // If the user chose to insert, paste the text
                if (!string.IsNullOrEmpty(searchWindow.InsertText))
                {
                    await Task.Delay(100); // Brief delay for focus to restore
                    try
                    {
                        await SetClipboardWithRetryAsync(searchWindow.InsertText);
                        System.Windows.Forms.SendKeys.SendWait("^v");
                        Log.Info($"Inserted: {Log.Snippet(searchWindow.InsertText, 50)}");

                        // Select inserted text for easy undo (Mac parity)
                        if (searchWindow.InsertText.Length <= 1000)
                        {
                            await Task.Delay(100);
                            TextSelectionHelper.SelectInsertedText(searchWindow.InsertText.Length);
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"Failed to paste search result: {ex.Message}");
                    }
                }
            };

            searchWindow.Show();
            Log.Info("Search panel opened");
        });
    }

    private async Task LookupSelectionAsync()
    {
        Log.Info("Lookup: hotkey triggered");
        var mouse = System.Windows.Forms.Control.MousePosition;
        PopupWindow? popup = null;

        // Close any existing popup before creating a new one
        CloseCurrentPopup();

        try
        {
            // Capture the foreground window before doing anything
            var targetWindow = GetForegroundWindow();
            Log.Info($"Lookup: target window={targetWindow}");

            // Try selection reading first (simulates Ctrl+C to the target window)
            string? text = await ClipboardSelectionReader.ReadSelectionByCopyingAsync(targetWindow);
            Log.Info($"Lookup: selection read: '{Log.Snippet(text, 80)}'");

            // If no text was captured, the user may not have anything selected.
            // Don't fall back to the clipboard — that would give stale results from
            // a previous copy and confuse the user.
            if (string.IsNullOrWhiteSpace(text))
            {
                Log.Info("Lookup: no selection captured, checking if user pre-copied to clipboard");
                // Only use clipboard if the user explicitly copied something recently
                // (i.e., clipboard already had content before we tried our Ctrl+C).
                // ReadSelectionByCopyingAsync restores the clipboard, so reading it now
                // gives the pre-existing content — which is what the user expects if they
                // manually pressed Ctrl+C before the hotkey.
                text = await ClipboardSelectionReader.ReadClipboardAsync();
                Log.Info($"Lookup: clipboard text: '{Log.Snippet(text, 80)}'");
            }

            // Extract concept ID
            var conceptId = ClipboardSelectionReader.ExtractFirstSnomedId(text);
            if (string.IsNullOrWhiteSpace(conceptId))
            {
                Log.Info($"Lookup: no concept ID found in text: '{Log.Snippet(text, 80)}'");
                PopupWindow.ShowErrorAt(mouse.X, mouse.Y,
                    "No Concept ID Found",
                    "Select or copy a concept ID first, then press the hotkey.");
                return;
            }

            // Show loading popup and track it
            popup = PopupWindow.ShowLoadingAt(mouse.X, mouse.Y, conceptId);
            popup.Client = _client;
            _currentPopup = popup;

            // Check if it's a valid SCTID
            bool isSCTID = SCTIDValidator.IsValidSCTID(conceptId);

            if (isSCTID)
            {
                Log.Info($"Looking up SNOMED CT conceptId={conceptId}");
                var result = await _client.LookupAsync(conceptId);
                popup.ShowResult(result);
                Log.Info($"Found: {result.Pt ?? result.Fsn ?? conceptId} ({result.Branch})");
            }
            else
            {
                // Not a valid SCTID - try configured code systems
                Log.Info($"Not a valid SCTID, trying configured systems for code={conceptId}");
                var csSettings = CodeSystemSettings.Load();
                var enabledSystems = csSettings.EnabledSystemUris.ToList();

                if (enabledSystems.Count == 0)
                {
                    popup.ShowError("No Code Systems", "No code systems are enabled. Configure code systems in Settings.");
                    return;
                }

                var result = await _client.LookupInConfiguredSystemsAsync(conceptId, enabledSystems);
                if (result != null)
                {
                    popup.ShowResult(result);
                    Log.Info($"Found in {result.SystemName}: {result.Pt ?? conceptId}");
                }
                else
                {
                    popup.ShowError("Not Found", $"Code {conceptId} not found in any enabled code system.");
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error($"LookupSelection failed: {ex.GetType().Name}: {ex.Message}");
            ShowError(popup, mouse, "Lookup Failed", ex.Message);
        }
    }

    private async Task ReplaceSelectionAsync()
    {
        var targetWindow = GetForegroundWindow();
        bool showedHud = false;

        try
        {
            // Read selection
            string? text = await ClipboardSelectionReader.ReadSelectionByCopyingAsync(targetWindow);
            Log.Info($"Replace: selection read: '{Log.Snippet(text, 80)}'");

            if (string.IsNullOrWhiteSpace(text))
            {
                Log.Info("Replace: selection empty, falling back to clipboard");
                text = await ClipboardSelectionReader.ReadClipboardAsync();
                Log.Info($"Replace: clipboard text: '{Log.Snippet(text, 80)}'");
            }

            if (string.IsNullOrWhiteSpace(text))
            {
                Log.Info("Replace: no text available");
                return;
            }

            // Give clipboard time to be released by the target app after Ctrl+C
            await Task.Delay(300);

            // Extract all concept IDs with positions
            var matches = ClipboardSelectionReader.ExtractAllConceptIds(text);
            if (matches.Count == 0)
            {
                Log.Info("Replace: no concept IDs found in selection");
                return;
            }

            Log.Info($"Replace: found {matches.Count} concept IDs");

            // Show progress HUD if more than 3 concepts
            if (matches.Count > 3)
            {
                ProgressHUD.Show($"Looking up {matches.Count} concepts...");
                showedHud = true;
            }

            var settings = Settings.Load();

            // Separate SCTIDs from non-SCTID codes
            var sctidCodes = matches.Where(m => m.IsSCTID).Select(m => m.ConceptId).Distinct().ToList();
            var nonSctidCodes = matches.Where(m => !m.IsSCTID).Select(m => m.ConceptId).Distinct().ToList();

            // Batch lookup SCTIDs
            var batchResult = sctidCodes.Count > 0
                ? await _client.BatchLookupAsync(sctidCodes)
                : new BatchLookupResult(new(), new(), new());

            // Individual lookup non-SCTIDs via configured systems
            var nonSctidPt = new Dictionary<string, string>();
            var nonSctidFsn = new Dictionary<string, string>();
            var nonSctidActive = new Dictionary<string, bool>();

            foreach (var code in nonSctidCodes)
            {
                try
                {
                    var result = await _client.LookupInConfiguredSystemsAsync(code,
                        new[] { "http://snomed.info/sct", "http://loinc.org", "http://hl7.org/fhir/sid/icd-10" });
                    if (result != null)
                    {
                        if (result.Pt != null) nonSctidPt[code] = result.Pt;
                        if (result.Fsn != null) nonSctidFsn[code] = result.Fsn;
                        nonSctidActive[code] = result.Active ?? true;
                    }
                }
                catch (Exception ex)
                {
                    Log.Debug($"Replace: non-SCTID lookup failed for {code}: {ex.Message}");
                }
            }

            // Merge results
            var allPt = new Dictionary<string, string>(batchResult.PtByCode);
            var allFsn = new Dictionary<string, string>(batchResult.FsnByCode);
            var allActive = new Dictionary<string, bool>(batchResult.ActiveByCode);
            foreach (var kv in nonSctidPt) allPt[kv.Key] = kv.Value;
            foreach (var kv in nonSctidFsn) allFsn[kv.Key] = kv.Value;
            foreach (var kv in nonSctidActive) allActive[kv.Key] = kv.Value;

            // Smart toggle: check if all codes already have the correct terms
            bool allHaveCorrectTerms = matches.All(m =>
            {
                if (m.ExistingTerm == null) return false;
                var expectedTerm = GetTermForFormat(m.ConceptId, settings.ReplaceTermFormat, allPt, allFsn, allActive, settings.PrefixInactive);
                return expectedTerm != null && m.ExistingTerm.Trim() == expectedTerm.Trim();
            });

            // Build replacement string in reverse order to preserve indices
            var result2 = new StringBuilder(text);
            foreach (var match in matches.OrderByDescending(m => m.StartIndex))
            {
                string? term = GetTermForFormat(match.ConceptId, settings.ReplaceTermFormat, allPt, allFsn, allActive, settings.PrefixInactive);
                if (term == null)
                {
                    Log.Debug($"Replace: no term found for {match.ConceptId}, skipping");
                    continue;
                }

                string replacement;
                if (allHaveCorrectTerms)
                {
                    // Remove mode: strip terms, leave bare codes
                    replacement = match.ConceptId;
                }
                else
                {
                    // Add mode: annotate with terms
                    replacement = $"{match.ConceptId} |{term}|";
                }

                result2.Remove(match.StartIndex, match.Length);
                result2.Insert(match.StartIndex, replacement);
            }

            var replacedText = result2.ToString();

            // Copy result to clipboard (with retry for clipboard lock)
            await SetClipboardWithRetryAsync(replacedText);

            // Restore focus and paste
            SetForegroundWindow(targetWindow);
            await Task.Delay(150);
            System.Windows.Forms.SendKeys.SendWait("^v");

            // Select replaced text for easy undo (Mac parity, up to 1000 chars)
            if (replacedText.Length <= 1000)
            {
                await Task.Delay(100);
                TextSelectionHelper.SelectInsertedText(replacedText.Length);
            }

            var mode = allHaveCorrectTerms ? "removed" : "added";
            Log.Info($"Replace: {mode} terms for {matches.Count} concepts");
        }
        catch (Exception ex)
        {
            Log.Error($"ReplaceSelection failed: {ex.Message}");
        }
        finally
        {
            if (showedHud)
                ProgressHUD.Hide();
        }
    }

    /// <summary>
    /// Reads the current selection, parses it as ECL, and toggles between formatted and minified.
    /// </summary>
    private async Task FormatECLSelectionAsync()
    {
        var targetWindow = GetForegroundWindow();
        Log.Info("ECL format: hotkey triggered");

        try
        {
            string? text = await ClipboardSelectionReader.ReadSelectionByCopyingAsync(targetWindow);
            Log.Info($"ECL format: selection read: '{Log.Snippet(text, 80)}'");

            if (string.IsNullOrWhiteSpace(text))
            {
                Log.Info("ECL format: selection empty, falling back to clipboard");
                text = await ClipboardSelectionReader.ReadClipboardAsync();
                Log.Info($"ECL format: clipboard text: '{Log.Snippet(text, 80)}'");
            }

            if (string.IsNullOrWhiteSpace(text))
            {
                Log.Info("ECL format: no text to format");
                System.Media.SystemSounds.Beep.Play();
                return;
            }

            if (text.Length > MaxEclLength)
            {
                Log.Info($"ECL format: input too long ({text.Length} chars, max {MaxEclLength})");
                System.Media.SystemSounds.Beep.Play();
                return;
            }

            var toggled = _eclBridge.Value.ToggleECLFormat(text.Trim());
            if (toggled == null)
            {
                // Get structured parse errors for logging
                var parseResult = _eclBridge.Value.ParseECL(text.Trim());
                var errorMsg = parseResult.Errors.Count > 0
                    ? parseResult.Errors[0].Message
                    : "Invalid ECL expression";
                Log.Info($"ECL format: parse error - {errorMsg}");
                System.Media.SystemSounds.Beep.Play();
                return;
            }

            var result = toggled;

            await SetClipboardWithRetryAsync(result);

            SetForegroundWindow(targetWindow);
            await Task.Delay(150);
            System.Windows.Forms.SendKeys.SendWait("^v");

            // Select formatted text for easy undo (Mac parity, up to 1000 chars)
            if (result.Length <= 1000)
            {
                await Task.Delay(100);
                TextSelectionHelper.SelectInsertedText(result.Length);
            }

            Log.Info($"ECL format: toggled successfully ({(text.Contains('\n') ? "minified" : "formatted")})");
        }
        catch (Exception ex)
        {
            Log.Error($"FormatECLSelection failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Opens the selected concept in the Shrimp terminology browser.
    /// </summary>
    private async Task OpenInShrimpAsync()
    {
        var mouse = System.Windows.Forms.Control.MousePosition;
        var targetWindow = GetForegroundWindow();

        try
        {
            var text = await ClipboardSelectionReader.ReadSelectionByCopyingAsync(targetWindow);
            if (string.IsNullOrWhiteSpace(text))
            {
                PopupWindow.ShowErrorAt(mouse.X, mouse.Y, "No Selection", "Please select a SNOMED CT concept ID first.");
                return;
            }

            var conceptId = ClipboardSelectionReader.ExtractFirstSnomedId(text);
            if (string.IsNullOrWhiteSpace(conceptId))
            {
                PopupWindow.ShowErrorAt(mouse.X, mouse.Y, "No SCTID Found", "No valid SNOMED CT concept ID found in selection.");
                return;
            }

            Log.Info($"Opening in Shrimp: {conceptId}");

            // First lookup the concept to get the module/edition info
            var result = await _client.LookupAsync(conceptId);
            if (result == null)
            {
                // If lookup fails, open with just the concept ID (International edition fallback)
                Log.Info($"Could not lookup concept {conceptId}, using International edition fallback");
                var fallbackUrl = ShrimpUrlBuilder.BuildUrl(
                    conceptId: conceptId,
                    system: "http://snomed.info/sct",
                    fhirEndpoint: Settings.Load().FhirBaseUrl);

                if (fallbackUrl != null && IsAllowedUrl(fallbackUrl))
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = fallbackUrl,
                        UseShellExecute = true
                    });
                }
                else if (fallbackUrl != null)
                {
                    Log.Error($"Blocked non-HTTP URL: {fallbackUrl}");
                }
                return;
            }

            // Build URL with full module info
            var url = ShrimpUrlBuilder.BuildUrl(result, Settings.Load().FhirBaseUrl);
            if (url == null)
            {
                PopupWindow.ShowErrorAt(mouse.X, mouse.Y, "URL Error", "Could not build Shrimp URL for this concept.");
                return;
            }

            if (!IsAllowedUrl(url))
            {
                Log.Error($"Blocked non-HTTP URL: {url}");
                return;
            }

            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });

            Log.Info($"Opened Shrimp browser for {conceptId}");
        }
        catch (Exception ex)
        {
            Log.Error($"OpenInShrimp failed: {ex.Message}");
            PopupWindow.ShowErrorAt(mouse.X, mouse.Y, "Error", $"Failed to open browser: {ex.Message}");
        }
    }

    /// <summary>
    /// Opens the ECL Workbench with the selected text (or empty for drafting).
    /// </summary>
    private async Task EvaluateEclSelectionAsync()
    {
        var targetWindow = GetForegroundWindow();
        Log.Info("ECL workbench: hotkey triggered");

        try
        {
            // Try to read selection — empty is OK (opens blank workbench)
            var text = await ClipboardSelectionReader.ReadSelectionByCopyingAsync(targetWindow);
            if (string.IsNullOrWhiteSpace(text))
                text = await ClipboardSelectionReader.ReadClipboardAsync();

            var trimmed = text?.Trim() ?? "";

            await System.Windows.Application.Current.Dispatcher.InvokeAsync(async () =>
            {
                if (_currentWorkbench != null)
                {
                    // Reuse existing workbench — bring to front
                    _currentWorkbench.Show();
                    _currentWorkbench.Activate();

                    // If text is selected, replace editor content and evaluate
                    if (!string.IsNullOrEmpty(trimmed))
                        await _currentWorkbench.SetEditorValueAsync(trimmed, evaluate: true);
                }
                else
                {
                    // Create new workbench with the expression pre-loaded
                    var window = new ECLWorkbenchWindow(_client) { InitialExpression = trimmed };
                    _currentWorkbench = window;
                    window.IsVisibleChanged += (_, _) =>
                    {
                        if (!window.IsVisible)
                            Log.Info("ECL workbench: hidden");
                    };
                    // Fall back to static evaluate panel if WebView2 is unavailable
                    window.WebView2Unavailable += expr =>
                    {
                        _currentWorkbench = null;
                        OpenStaticEvaluatePanel(expr);
                    };
                    window.Show();

                    // Set content after editor initializes (small delay for WebView2)
                    if (!string.IsNullOrEmpty(trimmed))
                    {
                        await Task.Delay(1500); // Wait for editor to initialize
                        await window.SetEditorValueAsync(trimmed, evaluate: true);
                    }
                }
            });

            Log.Info($"ECL workbench: opened with '{Log.Snippet(trimmed, 60)}'");
        }
        catch (Exception ex)
        {
            Log.Error($"EvaluateEclSelection failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Shows the ECL Reference panel window.
    /// </summary>
    /// <summary>
    /// Opens the static EvaluateWindow as a fallback when WebView2 is unavailable.
    /// </summary>
    private void OpenStaticEvaluatePanel(string expression)
    {
        // Use BeginInvoke to defer — this is called from a Loaded event handler
        System.Windows.Application.Current.Dispatcher.BeginInvoke(async () =>
        {
            try
            {
                if (_currentEvaluateWindow != null && _currentEvaluateWindow.IsVisible)
                    _currentEvaluateWindow.Close();

                if (string.IsNullOrWhiteSpace(expression))
                {
                    Log.Info("ECL workbench: no expression for static fallback");
                    return;
                }

                var settings = Settings.Load();
                var window = new EvaluateWindow(_client, expression);
                window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                _currentEvaluateWindow = window;
                window.Closed += (_, _) => _currentEvaluateWindow = null;
                window.Show();
                window.Activate();

                await window.EvaluateAsync(settings.EvaluateResultLimit);
                Log.Info("ECL workbench: fell back to static evaluate panel");
            }
            catch (Exception ex)
            {
                Log.Error($"Static evaluate fallback failed: {ex.Message}");
            }
        });
    }

    private void ShowECLReferencePanel()
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            // Bring to front if already open
            if (_currentReferenceWindow != null && _currentReferenceWindow.IsVisible)
            {
                _currentReferenceWindow.Activate();
                return;
            }

            var articles = _eclBridge.Value.GetArticles();
            var window = new ECLReferenceWindow(articles);
            _currentReferenceWindow = window;
            window.Closed += (_, _) => _currentReferenceWindow = null;
            window.Show();
            Log.Info($"ECL Reference panel opened ({articles.Count} articles)");
        });
    }

    /// <summary>
    /// Builds warning messages for inactive or unknown concepts in an ECL expression.
    /// </summary>
    public static List<string> BuildConceptWarnings(List<string> conceptIds, BatchLookupResult batch)
    {
        var warnings = new List<string>();
        foreach (var id in conceptIds)
        {
            if (!batch.PtByCode.ContainsKey(id))
            {
                warnings.Add($"{id} not found on server");
            }
            else if (batch.ActiveByCode.TryGetValue(id, out var active) && !active)
            {
                warnings.Add($"{id} is inactive");
            }
        }
        return warnings;
    }

    /// <summary>
    /// Gets the term to use for replacement based on the configured format.
    /// </summary>
    private static string? GetTermForFormat(
        string code,
        TermFormat format,
        Dictionary<string, string> ptByCode,
        Dictionary<string, string> fsnByCode,
        Dictionary<string, bool> activeByCode,
        bool prefixInactive)
    {
        string? term = format switch
        {
            TermFormat.FSN => fsnByCode.GetValueOrDefault(code) ?? ptByCode.GetValueOrDefault(code),
            TermFormat.PT => ptByCode.GetValueOrDefault(code) ?? fsnByCode.GetValueOrDefault(code),
            _ => ptByCode.GetValueOrDefault(code)
        };

        if (term == null) return null;

        // Prefix with INACTIVE if concept is inactive and setting is enabled
        if (prefixInactive && activeByCode.TryGetValue(code, out var active) && !active)
        {
            term = $"INACTIVE {term}";
        }

        return term;
    }

    private async Task LookupClipboardAsync()
    {
        var mouse = System.Windows.Forms.Control.MousePosition;
        PopupWindow? popup = null;

        CloseCurrentPopup();

        try
        {
            // Read directly from clipboard (menu item use case)
            string? text = await ClipboardSelectionReader.ReadClipboardAsync();
            Log.Debug($"Clipboard text: '{Log.Snippet(text, 50)}'");

            var conceptId = ClipboardSelectionReader.ExtractFirstSnomedId(text);
            if (string.IsNullOrWhiteSpace(conceptId))
            {
                PopupWindow.ShowErrorAt(mouse.X, mouse.Y,
                    "No SNOMED CT ID Found",
                    "Copy a SNOMED CT concept ID to clipboard first.");
                return;
            }

            popup = PopupWindow.ShowLoadingAt(mouse.X, mouse.Y, conceptId);
            popup.Client = _client;
            _currentPopup = popup;

            var result = await _client.LookupAsync(conceptId);
            popup.ShowResult(result);
            Log.Info($"Found: {result.Pt ?? result.Fsn ?? conceptId} ({result.Branch})");
        }
        catch (Exception ex)
        {
            Log.Error($"LookupClipboard failed: {ex.Message}");
            ShowError(popup, mouse, "Lookup Failed", ex.Message);
        }
    }

    /// <summary>
    /// Sets clipboard text using Win32 APIs directly, bypassing WPF's OLE clipboard.
    /// Retries if the clipboard is locked by another process.
    /// </summary>
    private static async Task SetClipboardWithRetryAsync(string text, int maxRetries = 20)
    {
        for (int i = 0; i < maxRetries; i++)
        {
            if (SetClipboardTextWin32(text))
            {
                Log.Debug($"Clipboard set via Win32 on attempt {i + 1}");
                return;
            }

            var err = System.Runtime.InteropServices.Marshal.GetLastWin32Error();
            Log.Debug($"Win32 OpenClipboard failed, error={err}, retry {i + 1}/{maxRetries}");
            await Task.Delay(100 + i * 50);
        }

        throw new Exception("Failed to open clipboard after multiple retries");
    }

    private const uint CF_UNICODETEXT = 13;
    private const uint GMEM_MOVEABLE = 0x0002;

    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    private static extern bool OpenClipboard(IntPtr hWndNewOwner);

    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    private static extern bool CloseClipboard();

    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    private static extern bool EmptyClipboard();

    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);

    [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalAlloc(uint uFlags, UIntPtr dwBytes);

    [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalLock(IntPtr hMem);

    [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalUnlock(IntPtr hMem);

    /// <summary>
    /// Sets clipboard text using raw Win32 APIs (bypasses OLE/COM clipboard).
    /// </summary>
    private static bool SetClipboardTextWin32(string text)
    {
        if (!OpenClipboard(IntPtr.Zero))
            return false;

        try
        {
            EmptyClipboard();

            var str = text + "\0";
            var bytes = System.Text.Encoding.Unicode.GetBytes(str);
            var hGlobal = GlobalAlloc(GMEM_MOVEABLE, (UIntPtr)bytes.Length);
            if (hGlobal == IntPtr.Zero)
            {
                Log.Error("GlobalAlloc failed");
                return false;
            }

            var ptr = GlobalLock(hGlobal);
            if (ptr == IntPtr.Zero)
            {
                Log.Error("GlobalLock failed");
                return false;
            }

            System.Runtime.InteropServices.Marshal.Copy(bytes, 0, ptr, bytes.Length);
            GlobalUnlock(hGlobal);

            var result = SetClipboardData(CF_UNICODETEXT, hGlobal);
            return result != IntPtr.Zero;
        }
        finally
        {
            CloseClipboard();
        }
    }

    private void CloseCurrentPopup()
    {
        if (_currentPopup != null)
        {
            try
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    try { _currentPopup.Close(); } catch { }
                });
            }
            catch { }
            _currentPopup = null;
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

    /// <summary>
    /// Manually triggered update check from the tray menu. Shows feedback to the user.
    /// </summary>
    private async Task CheckForUpdatesManualAsync()
    {
        try
        {
            var mgr = new Velopack.UpdateManager(new Velopack.Sources.GithubSource("https://github.com/aehrc/codeagogo-win", null, false));
            var update = await mgr.CheckForUpdatesAsync();

            if (update == null)
            {
                _notify.BalloonTipTitle = "No Updates";
                _notify.BalloonTipText = "You are running the latest version of Codeagogo.";
                _notify.BalloonTipIcon = ToolTipIcon.Info;
                _notify.ShowBalloonTip(5_000);
                return;
            }

            _notify.BalloonTipTitle = "Downloading Update...";
            _notify.BalloonTipText = $"Downloading Codeagogo {update.TargetFullRelease.Version}";
            _notify.BalloonTipIcon = ToolTipIcon.Info;
            _notify.ShowBalloonTip(3_000);

            await mgr.DownloadUpdatesAsync(update);

            NotifyUpdateReady(update.TargetFullRelease.Version.ToString(), () =>
            {
                mgr.ApplyUpdatesAndRestart(update);
            });
        }
        catch (Exception ex)
        {
            Log.Error($"Manual update check failed: {ex.Message}");
            _notify.BalloonTipTitle = "Update Check Failed";
            _notify.BalloonTipText = "Could not check for updates. Check your internet connection.";
            _notify.BalloonTipIcon = ToolTipIcon.Warning;
            _notify.ShowBalloonTip(5_000);
        }
    }

    /// <summary>
    /// Notifies the user that an update has been downloaded and is ready to install.
    /// Shows a balloon tip and adds a menu item to restart and apply the update.
    /// </summary>
    public void NotifyUpdateReady(string version, Action applyUpdate)
    {
        _notify.Text = $"Codeagogo — update {version} ready";
        _notify.BalloonTipTitle = "Update Available";
        _notify.BalloonTipText = $"Codeagogo {version} has been downloaded. Click here or use the tray menu to restart and update.";
        _notify.BalloonTipIcon = ToolTipIcon.Info;
        _notify.BalloonTipClicked += (_, _) => applyUpdate();
        _notify.ShowBalloonTip(10_000);

        // Add "Restart to Update" menu item at the top
        var updateItem = new ToolStripMenuItem($"Restart to Update ({version})")
        {
            Font = new System.Drawing.Font(_notify.ContextMenuStrip!.Font, System.Drawing.FontStyle.Bold)
        };
        updateItem.Click += (_, _) => applyUpdate();
        _notify.ContextMenuStrip.Items.Insert(0, updateItem);
        _notify.ContextMenuStrip.Items.Insert(1, new ToolStripSeparator());

        Log.Info($"Update {version} ready — user notified");
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

    /// <summary>
    /// Validates that a URL uses http or https scheme to prevent shell-execute injection.
    /// </summary>
    private static bool IsAllowedUrl(string url)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }

    /// <summary>
    /// Loads the application icon from embedded resources, falling back to a system icon.
    /// </summary>
    private static Icon LoadAppIcon()
    {
        try
        {
            var uri = new Uri("pack://application:,,,/Assets/app.ico", UriKind.Absolute);
            var stream = System.Windows.Application.GetResourceStream(uri)?.Stream;
            if (stream != null)
                return new Icon(stream, 32, 32);
        }
        catch (Exception ex)
        {
            Log.Debug($"Failed to load app icon: {ex.Message}");
        }

        return SystemIcons.Information;
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);
}

/// <summary>
/// Custom menu renderer that draws shortcut key text in italic grey.
/// </summary>
internal sealed class ShortcutMenuRenderer : ToolStripProfessionalRenderer
{
    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        if (e.Item is ToolStripMenuItem menuItem && !string.IsNullOrEmpty(menuItem.ShortcutKeyDisplayString)
            && e.Text == menuItem.ShortcutKeyDisplayString)
        {
            using var font = new System.Drawing.Font(e.TextFont, System.Drawing.FontStyle.Italic);
            e.TextColor = System.Drawing.Color.Gray;
            e.TextFont = font;
        }

        base.OnRenderItemText(e);
    }
}
