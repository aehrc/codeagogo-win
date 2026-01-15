using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;

namespace SNOMEDLookup;

public static class ClipboardSelectionReader
{
    private static readonly Regex SnomedIdRegex = new(@"\b\d{6,18}\b", RegexOptions.Compiled);

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
                Log.Debug($"Clipboard content: '{text}'");
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