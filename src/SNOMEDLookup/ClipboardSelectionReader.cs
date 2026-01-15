using System;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;

namespace SNOMEDLookup;

public static class ClipboardSelectionReader
{
    private static readonly Regex SnomedIdRegex = new(@"\b\d{6,18}\b", RegexOptions.Compiled);

    public static async Task<string?> ReadSelectionByCopyingAsync(int delayMs = 120)
    {
        // Clipboard APIs require STA
        if (Application.Current.Dispatcher.CheckAccess())
            return await ReadOnUiThreadAsync(delayMs);

        return await Application.Current.Dispatcher.InvokeAsync(() => ReadOnUiThreadAsync(delayMs)).Task.Unwrap();
    }

    private static async Task<string?> ReadOnUiThreadAsync(int delayMs)
    {
        string? original = null;
        try
        {
            original = Clipboard.ContainsText() ? Clipboard.GetText() : null;
        }
        catch { }

        try
        {
            try { Clipboard.Clear(); } catch { }

            SendCtrlC();
            await Task.Delay(delayMs);

            return Clipboard.ContainsText() ? Clipboard.GetText() : null;
        }
        finally
        {
            try
            {
                if (original != null)
                    Clipboard.SetText(original);
            }
            catch { }
        }
    }

    public static string? ExtractFirstSnomedId(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var m = SnomedIdRegex.Match(text);
        return m.Success ? m.Value : null;
    }

    private static void SendCtrlC()
    {
        INPUT[] inputs = new INPUT[]
        {
            INPUT.KeyDown(VK_CONTROL),
            INPUT.KeyDown(VK_C),
            INPUT.KeyUp(VK_C),
            INPUT.KeyUp(VK_CONTROL),
        };
        SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
    }

    private const ushort VK_CONTROL = 0x11;
    private const ushort VK_C = 0x43;

    [DllImport("user32.dll")]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint type;
        public InputUnion u;

        public static INPUT KeyDown(ushort vk) => new()
        {
            type = 1,
            u = new InputUnion { ki = new KEYBDINPUT { wVk = vk, dwFlags = 0 } }
        };

        public static INPUT KeyUp(ushort vk) => new()
        {
            type = 1,
            u = new InputUnion { ki = new KEYBDINPUT { wVk = vk, dwFlags = 2 } }
        };
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)]
        public KEYBDINPUT ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }
}
