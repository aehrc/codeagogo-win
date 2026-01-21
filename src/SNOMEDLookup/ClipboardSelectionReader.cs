using System;
using System.Collections.Specialized;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;

namespace SNOMEDLookup;

public static class ClipboardSelectionReader
{
    private static readonly Regex SnomedIdRegex = new(@"\b\d{6,18}\b", RegexOptions.Compiled);

    #region Win32 keybd_event

    private const byte VK_CONTROL = 0x11;
    private const byte VK_C = 0x43;
    private const uint KEYEVENTF_KEYUP = 0x0002;

    [DllImport("user32.dll")]
    private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

    #endregion

    /// <summary>
    /// Reads the current selection by simulating Ctrl+C.
    /// Saves and restores the original clipboard content.
    /// </summary>
    public static async Task<string?> ReadSelectionByCopyingAsync()
    {
        try
        {
            Log.Debug("Reading selection by copying...");

            // Snapshot original clipboard (must be on STA thread)
            var snapshot = await System.Windows.Application.Current.Dispatcher.InvokeAsync(SnapshotClipboard);

            // Clear clipboard
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                try { System.Windows.Clipboard.Clear(); } catch { }
            });

            // Small delay before sending keys
            await Task.Delay(30);

            // Send Ctrl+C (can be called from any thread)
            SendCtrlC();

            // Wait for copy to complete - apps need time to respond
            await Task.Delay(150);

            // Read copied text (must be on STA thread)
            string? copiedText = await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                try
                {
                    if (System.Windows.Clipboard.ContainsText())
                    {
                        var text = System.Windows.Clipboard.GetText();
                        Log.Debug($"Copied selection: '{Log.Snippet(text, 50)}'");
                        return text;
                    }
                    Log.Debug("No text was copied (selection may be empty)");
                    return null;
                }
                catch (Exception ex)
                {
                    Log.Debug($"Failed to read copied text: {ex.Message}");
                    return null;
                }
            });

            // Restore original clipboard
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() => RestoreClipboard(snapshot));

            return copiedText;
        }
        catch (Exception ex)
        {
            Log.Error($"ReadSelectionByCopying failed: {ex.Message}");
            return null;
        }
    }

    private static void SendCtrlC()
    {
        // Use keybd_event which is more reliable for simulating clipboard operations
        // Ctrl down
        keybd_event(VK_CONTROL, 0, 0, UIntPtr.Zero);
        // C down
        keybd_event(VK_C, 0, 0, UIntPtr.Zero);
        // C up
        keybd_event(VK_C, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        // Ctrl up
        keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);

        Log.Debug("Sent Ctrl+C via keybd_event");
    }

    #region Clipboard Snapshot/Restore

    private sealed class ClipboardSnapshot
    {
        public string? Text { get; set; }
        public BitmapSource? Image { get; set; }
        public StringCollection? FileDropList { get; set; }
        public bool HasData { get; set; }
    }

    private static ClipboardSnapshot SnapshotClipboard()
    {
        var snapshot = new ClipboardSnapshot();

        try
        {
            if (System.Windows.Clipboard.ContainsText())
            {
                snapshot.Text = System.Windows.Clipboard.GetText();
                snapshot.HasData = true;
            }

            if (System.Windows.Clipboard.ContainsImage())
            {
                snapshot.Image = System.Windows.Clipboard.GetImage();
                snapshot.HasData = true;
            }

            if (System.Windows.Clipboard.ContainsFileDropList())
            {
                snapshot.FileDropList = System.Windows.Clipboard.GetFileDropList();
                snapshot.HasData = true;
            }
        }
        catch (Exception ex)
        {
            Log.Debug($"Failed to snapshot clipboard: {ex.Message}");
        }

        return snapshot;
    }

    private static void RestoreClipboard(ClipboardSnapshot snapshot)
    {
        try
        {
            if (!snapshot.HasData)
            {
                System.Windows.Clipboard.Clear();
                return;
            }

            var dataObject = new System.Windows.DataObject();

            if (snapshot.Text != null)
            {
                dataObject.SetText(snapshot.Text);
            }

            if (snapshot.Image != null)
            {
                dataObject.SetImage(snapshot.Image);
            }

            if (snapshot.FileDropList != null && snapshot.FileDropList.Count > 0)
            {
                dataObject.SetFileDropList(snapshot.FileDropList);
            }

            System.Windows.Clipboard.SetDataObject(dataObject, true);
            Log.Debug("Clipboard restored");
        }
        catch (Exception ex)
        {
            Log.Debug($"Failed to restore clipboard: {ex.Message}");
        }
    }

    #endregion

    public static async Task<string?> ReadClipboardAsync()
    {
        // Clipboard APIs require STA
        if (System.Windows.Application.Current.Dispatcher.CheckAccess())
            return ReadFromClipboard();

        return await System.Windows.Application.Current.Dispatcher.InvokeAsync(ReadFromClipboard);
    }

    private static string? ReadFromClipboard()
    {
        try
        {
            if (System.Windows.Clipboard.ContainsText())
            {
                var text = System.Windows.Clipboard.GetText();
                Log.Debug($"Clipboard content: '{Log.Snippet(text, 50)}'");
                return text;
            }

            Log.Debug("Clipboard is empty or doesn't contain text");
            return null;
        }
        catch (Exception ex)
        {
            Log.Error($"Failed to read clipboard: {ex.Message}");
            return null;
        }
    }

    public static string? ExtractFirstSnomedId(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var m = SnomedIdRegex.Match(text);
        return m.Success ? m.Value : null;
    }
}