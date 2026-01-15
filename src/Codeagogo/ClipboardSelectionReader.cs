// Copyright 2026 CSIRO. Licensed under the Apache License, Version 2.0.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Specialized;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media.Imaging;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;

namespace Codeagogo;

/// <summary>
/// Provides methods for reading selected text from applications and extracting SNOMED CT concept IDs.
/// </summary>
/// <remarks>
/// This class implements two strategies for obtaining text:
/// <list type="number">
/// <item><description>Selection reading via simulated Ctrl+C - captures currently selected text from any application</description></item>
/// <item><description>Clipboard reading - reads existing clipboard text as a fallback</description></item>
/// </list>
/// The selection reading preserves and restores the original clipboard contents.
/// </remarks>
public static class ClipboardSelectionReader
{
    private const int MaxInputLength = 10_000;

    private static readonly Regex SnomedIdRegex = new(@"\b\d{6,18}\b", RegexOptions.Compiled);

    /// <summary>
    /// Regex pattern that matches a 6-18 digit SNOMED code optionally followed by a pipe-delimited term.
    /// Group 1: the numeric code. Group 2 (optional): the existing term text.
    /// </summary>
    private static readonly Regex SnomedConceptIdRegex = new(
        @"\b(\d{6,18})(?:\s*\|\s*([^|]+?)\s*\|)?",
        RegexOptions.Compiled);

    /// <summary>
    /// Regex pattern that matches alphanumeric codes (e.g., LOINC "8867-4", ICD-10 "E11.9")
    /// with optional pipe-delimited term. Lookaheads require at least one digit AND at least
    /// one letter or hyphen, preventing matches on pure words, pure numbers, and digit-dot
    /// patterns like "0..0".
    /// Group 1: the alphanumeric code. Group 2 (optional): the existing term text.
    /// </summary>
    private static readonly Regex AlphanumericCodeRegex = new(
        @"\b((?=[A-Za-z0-9.\-]*\d)(?=[A-Za-z0-9.\-]*[A-Za-z\-])[A-Za-z0-9][A-Za-z0-9.\-]{1,17})(?:\s*\|\s*([^|]+?)\s*\|)?",
        RegexOptions.Compiled);

    /// <summary>
    /// Extracts all concept ID matches (with positions) from the given text.
    /// Uses a two-pass approach: first matches SNOMED numeric codes, then alphanumeric
    /// codes (LOINC, ICD-10, etc.) while avoiding overlaps.
    /// Each match includes the code, its position in the text, the total match length
    /// (including any pipe-delimited term), and whether it passes SCTID validation.
    /// </summary>
    public static List<ConceptMatch> ExtractAllConceptIds(string? text)
    {
        var allMatches = new List<ConceptMatch>();
        if (string.IsNullOrWhiteSpace(text)) return allMatches;

        if (text.Length > MaxInputLength)
            text = text[..MaxInputLength];

        // Pass 1: SNOMED numeric codes (6-18 digits) with optional pipe-delimited term
        var matchedRanges = new List<(int Start, int End)>();
        foreach (Match m in SnomedConceptIdRegex.Matches(text))
        {
            var conceptId = m.Groups[1].Value;
            var existingTerm = m.Groups[2].Success ? m.Groups[2].Value.Trim() : null;
            var isSCTID = SCTIDValidator.IsValidSCTID(conceptId);

            allMatches.Add(new ConceptMatch(conceptId, m.Index, m.Length, existingTerm, isSCTID));
            matchedRanges.Add((m.Index, m.Index + m.Length));
        }

        // Pass 2: Alphanumeric codes (skip ranges already matched by pass 1)
        foreach (Match m in AlphanumericCodeRegex.Matches(text))
        {
            bool overlaps = matchedRanges.Exists(r =>
                m.Index < r.End && m.Index + m.Length > r.Start);
            if (overlaps) continue;

            var conceptId = m.Groups[1].Value;
            var existingTerm = m.Groups[2].Success ? m.Groups[2].Value.Trim() : null;

            allMatches.Add(new ConceptMatch(conceptId, m.Index, m.Length, existingTerm, IsSCTID: false));
        }

        // Sort by position in the original text
        allMatches.Sort((a, b) => a.StartIndex.CompareTo(b.StartIndex));
        return allMatches;
    }

    #region Win32 APIs

    private const ushort VK_CONTROL = 0x11;
    private const ushort VK_SHIFT = 0x10;
    private const ushort VK_MENU = 0x12; // Alt key
    private const ushort VK_LWIN = 0x5B;
    private const ushort VK_RWIN = 0x5C;
    private const ushort VK_C = 0x43;
    private const uint INPUT_KEYBOARD = 1;
    private const uint KEYEVENTF_KEYUP = 0x0002;
    private const uint WM_COPY = 0x0301;

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint type;
        public INPUTUNION u;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct INPUTUNION
    {
        [FieldOffset(0)] public KEYBDINPUT ki;
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

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [DllImport("user32.dll")]
    private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("user32.dll")]
    private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

    [DllImport("user32.dll")]
    private static extern IntPtr SetFocus(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool AllowSetForegroundWindow(int dwProcessId);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    [DllImport("user32.dll")]
    private static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);

    [DllImport("user32.dll")]
    private static extern int GetClassName(IntPtr hWnd, System.Text.StringBuilder lpClassName, int nMaxCount);

    private const int ASFW_ANY = -1;
    private const uint GW_CHILD = 5;
    private const uint WM_KEYDOWN = 0x0100;
    private const uint WM_KEYUP = 0x0101;

    #endregion

    /// <summary>
    /// Waits for all modifier keys (Ctrl, Shift, Alt, Win) to be released.
    /// This is necessary because when a hotkey is triggered, those keys are still held down.
    /// </summary>
    private static void WaitForModifiersReleased(int timeoutMs = 1000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);

        while (DateTime.UtcNow < deadline)
        {
            bool anyHeld = IsKeyHeld(VK_CONTROL) || IsKeyHeld(VK_SHIFT) ||
                          IsKeyHeld(VK_MENU) || IsKeyHeld(VK_LWIN) || IsKeyHeld(VK_RWIN);

            if (!anyHeld)
            {
                Log.Debug("All modifier keys released");
                return;
            }

            System.Threading.Thread.Sleep(10);
        }

        Log.Debug("Timeout waiting for modifier keys to be released");
    }

    private static bool IsKeyHeld(int vKey)
    {
        // High-order bit set means key is currently down
        return (GetAsyncKeyState(vKey) & 0x8000) != 0;
    }

    /// <summary>
    /// Attempts to read selected text via UI Automation TextPattern.
    /// This is fast and non-invasive (no clipboard disruption), but only works
    /// for apps that implement UIA TextPattern (Notepad, Word, Terminal, WPF/WinForms).
    /// Does NOT work for browsers (Chrome, Edge, Firefox) or Electron apps.
    /// </summary>
    /// <returns>The selected text, or null if UIA is unavailable or no text is selected.</returns>
    /// <summary>
    /// Attempts to read selected text via UI Automation (COM UIA3 via FlaUI).
    /// Uses the COM-based UIA3 interface which is safer than the managed
    /// System.Windows.Automation wrapper (which causes AccessViolationException
    /// in RawTextRange_GetText for cross-process text ranges).
    /// </summary>
    private static string? TryReadSelectionViaUIA()
    {
        try
        {
            // Run on thread pool with timeout — FocusedElement can hang on some apps
            var task = Task.Run(() =>
            {
                using var automation = new UIA3Automation();
                var focused = automation.FocusedElement();
                if (focused == null) return null;

                var textPattern = focused.Patterns.Text.PatternOrDefault;
                if (textPattern == null) return null;

                var selections = textPattern.GetSelection();
                if (selections.Length > 0)
                {
                    var text = selections[0].GetText(-1);
                    if (!string.IsNullOrEmpty(text))
                    {
                        Log.Debug($"UIA3 TextPattern: got selection '{Log.Snippet(text, 50)}'");
                        return text;
                    }
                }

                return null;
            });

            // 500ms timeout — if UIA hangs, fall through to Ctrl+C
            if (task.Wait(500))
                return task.Result;

            Log.Debug("UIA3 TextPattern: timed out after 500ms");
            return null;
        }
        catch (Exception ex)
        {
            Log.Debug($"UIA3 TextPattern failed: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Reads the current selection, trying UI Automation first (fast, no clipboard disruption),
    /// then falling back to simulated Ctrl+C (works universally including browsers).
    /// Saves and restores the original clipboard content when using Ctrl+C fallback.
    /// </summary>
    /// <param name="targetWindow">The window handle where text is selected (captured at hotkey press time).</param>
    public static async Task<string?> ReadSelectionByCopyingAsync(IntPtr targetWindow)
    {
        try
        {
            Log.Debug($"Reading selection from window {targetWindow}...");

            // Strategy 1: Try UIA3 TextPattern via FlaUI (COM-based, no native crash risk)
            var uiaText = TryReadSelectionViaUIA();
            if (!string.IsNullOrEmpty(uiaText))
            {
                Log.Debug("Selection captured via UIA3 TextPattern");
                return uiaText;
            }

            Log.Debug("UIA3 unavailable, falling back to Ctrl+C simulation...");

            // Snapshot clipboard before clearing (so we can restore it)
            ClipboardSnapshot? snapshot = null;
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                try { snapshot = SnapshotClipboard(); Log.Debug("Clipboard snapshot taken"); }
                catch (Exception ex) { Log.Debug($"Failed to snapshot clipboard: {ex.Message}"); }
            });

            // Clear clipboard so we can tell if copy worked
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                try { System.Windows.Clipboard.Clear(); Log.Debug("Clipboard cleared"); }
                catch (Exception ex) { Log.Debug($"Failed to clear clipboard: {ex.Message}"); }
            });

            // Small delay after clearing
            await Task.Delay(50);

            // Send copy command to target window
            bool copySent = TrySendCopyToWindow(targetWindow);
            if (!copySent)
            {
                Log.Debug("Failed to send copy command to target window");
                // Restore clipboard
                if (snapshot != null)
                {
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() => RestoreClipboard(snapshot));
                }
                return null;
            }

            // Poll clipboard for up to 500ms - some apps are slow to respond to Ctrl+C
            string? copiedText = null;
            for (int attempt = 0; attempt < 5; attempt++)
            {
                await Task.Delay(100);
                copiedText = await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    try
                    {
                        if (System.Windows.Clipboard.ContainsText())
                        {
                            var text = System.Windows.Clipboard.GetText();
                            Log.Debug($"Copied selection (attempt {attempt + 1}): '{Log.Snippet(text, 50)}'");
                            return text;
                        }
                        return null;
                    }
                    catch (Exception ex)
                    {
                        Log.Debug($"Failed to read copied text: {ex.Message}");
                        return null;
                    }
                });
                if (copiedText != null) break;
            }

            if (copiedText == null)
                Log.Debug("No text was copied after 5 attempts (selection may be empty or copy failed)");

            // Restore original clipboard content
            if (snapshot != null)
            {
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() => RestoreClipboard(snapshot));
            }

            return copiedText;
        }
        catch (Exception ex)
        {
            Log.Error($"ReadSelectionByCopying failed: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Attempts to copy the current selection from the target window using multiple strategies.
    /// </summary>
    /// <returns>True if a copy command was successfully sent.</returns>
    private static bool TrySendCopyToWindow(IntPtr hWnd)
    {
        if (hWnd == IntPtr.Zero)
        {
            Log.Debug("No target window provided");
            return false;
        }

        // Log window info for debugging
        var className = new System.Text.StringBuilder(256);
        GetClassName(hWnd, className, 256);
        Log.Debug($"Target window: {hWnd}, class: {className}");

        // Check current foreground window
        var currentFg = GetForegroundWindow();
        Log.Debug($"Current foreground window: {currentFg}");

        // CRITICAL: Wait for the user to release the hotkey modifier keys
        // Otherwise we'll send Ctrl+Shift+C instead of Ctrl+C
        WaitForModifiersReleased();

        // Flush any lingering modifier key-down state from the input queue
        // by sending explicit key-up events. GetAsyncKeyState may report released
        // while the input queue still has stale key-down events.
        FlushModifierKeys();

        // Check foreground again after waiting
        var fgAfterWait = GetForegroundWindow();
        Log.Debug($"Foreground after modifier wait: {fgAfterWait}");

        uint currentThreadId = GetCurrentThreadId();
        uint targetThreadId = GetWindowThreadProcessId(hWnd, out uint processId);
        bool attached = false;

        try
        {
            // Allow any process to set foreground window (required for background apps)
            AllowSetForegroundWindow(ASFW_ANY);

            // Attach our thread's input to the target thread first
            if (currentThreadId != targetThreadId)
            {
                attached = AttachThreadInput(currentThreadId, targetThreadId, true);
                Log.Debug($"Attached thread input: {attached} (current={currentThreadId}, target={targetThreadId})");
            }

            // Try to restore focus to target window
            bool fgSet = SetForegroundWindow(hWnd);
            var focusResult = SetFocus(hWnd);
            Log.Debug($"SetForegroundWindow: {fgSet}, SetFocus result: {focusResult}");

            // Give Windows time to process focus change
            System.Threading.Thread.Sleep(50);

            // Check if we actually got foreground
            var fgNow = GetForegroundWindow();
            Log.Debug($"Foreground after SetForegroundWindow: {fgNow}, target was: {hWnd}");

            // Strategy 1: keybd_event - older API that sometimes bypasses UIPI
            Log.Debug("Strategy 1: keybd_event Ctrl+C");
            SendCtrlCViaKeyboardEvent();
            System.Threading.Thread.Sleep(100);

            // Strategy 2: SendInput to the input queue
            Log.Debug("Strategy 2: SendInput Ctrl+C");
            SendCtrlCViaInput();
            System.Threading.Thread.Sleep(50);

            // Strategy 3: WM_COPY message to the window
            Log.Debug("Strategy 3: Sending WM_COPY");
            SendMessage(hWnd, WM_COPY, IntPtr.Zero, IntPtr.Zero);

            return true;
        }
        catch (Exception ex)
        {
            Log.Error($"TrySendCopyToWindow failed: {ex.Message}");
            return false;
        }
        finally
        {
            // Always detach if we attached
            if (attached && currentThreadId != targetThreadId)
            {
                AttachThreadInput(currentThreadId, targetThreadId, false);
                Log.Debug("Detached thread input");
            }
        }
    }

    /// <summary>
    /// Sends Ctrl+C keystrokes using SendInput API.
    /// </summary>
    private static void SendCtrlCViaInput()
    {
        var inputs = new INPUT[4];

        // Ctrl down
        inputs[0].type = INPUT_KEYBOARD;
        inputs[0].u.ki.wVk = VK_CONTROL;

        // C down
        inputs[1].type = INPUT_KEYBOARD;
        inputs[1].u.ki.wVk = VK_C;

        // C up
        inputs[2].type = INPUT_KEYBOARD;
        inputs[2].u.ki.wVk = VK_C;
        inputs[2].u.ki.dwFlags = KEYEVENTF_KEYUP;

        // Ctrl up
        inputs[3].type = INPUT_KEYBOARD;
        inputs[3].u.ki.wVk = VK_CONTROL;
        inputs[3].u.ki.dwFlags = KEYEVENTF_KEYUP;

        uint sent = SendInput(4, inputs, Marshal.SizeOf<INPUT>());
        Log.Debug($"SendInput sent {sent} of 4 inputs");
    }

    /// <summary>
    /// Sends key-up events for all modifier keys to flush any lingering state from the
    /// keyboard input queue. This ensures the subsequent Ctrl+C is not contaminated by
    /// Shift/Alt/Win from the hotkey that triggered this operation.
    /// </summary>
    private static void FlushModifierKeys()
    {
        const uint KUP = KEYEVENTF_KEYUP;
        keybd_event((byte)VK_SHIFT, 0, KUP, UIntPtr.Zero);
        keybd_event((byte)VK_CONTROL, 0, KUP, UIntPtr.Zero);
        keybd_event((byte)VK_MENU, 0, KUP, UIntPtr.Zero);
        keybd_event((byte)VK_LWIN, 0, KUP, UIntPtr.Zero);
        keybd_event((byte)VK_RWIN, 0, KUP, UIntPtr.Zero);
        System.Threading.Thread.Sleep(30);
        Log.Debug("Flushed modifier keys");
    }

    /// <summary>
    /// Sends Ctrl+C keystrokes using the older keybd_event API.
    /// This sometimes works when SendInput fails due to UIPI restrictions.
    /// </summary>
    private static void SendCtrlCViaKeyboardEvent()
    {
        const byte VK_CTRL = 0x11;
        const byte VK_C_KEY = 0x43;

        // Ctrl down
        keybd_event(VK_CTRL, 0, 0, UIntPtr.Zero);
        // C down
        keybd_event(VK_C_KEY, 0, 0, UIntPtr.Zero);
        // C up
        keybd_event(VK_C_KEY, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        // Ctrl up
        keybd_event(VK_CTRL, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);

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

    /// <summary>
    /// Reads text from the clipboard asynchronously.
    /// </summary>
    /// <returns>The clipboard text, or null if clipboard is empty or doesn't contain text.</returns>
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

    /// <summary>
    /// Extracts the first SNOMED CT concept ID from the given text.
    /// </summary>
    /// <param name="text">The text to search for concept IDs.</param>
    /// <returns>The first 6-18 digit number found, or null if none found.</returns>
    public static string? ExtractFirstSnomedId(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        if (text.Length > MaxInputLength)
            text = text[..MaxInputLength];

        var m = SnomedIdRegex.Match(text);
        return m.Success ? m.Value : null;
    }
}

/// <summary>
/// Represents a concept ID match found in text, including its position and metadata.
/// </summary>
/// <param name="ConceptId">The numeric concept code (6-18 digits)</param>
/// <param name="StartIndex">The start index of the match in the source text</param>
/// <param name="Length">The total length of the match (including any pipe-delimited term)</param>
/// <param name="ExistingTerm">The existing pipe-delimited term text, if present (e.g., from "12345 |some term|")</param>
/// <param name="IsSCTID">Whether the concept ID passes SNOMED CT Identifier (Verhoeff) validation</param>
public sealed record ConceptMatch(
    string ConceptId,
    int StartIndex,
    int Length,
    string? ExistingTerm,
    bool IsSCTID
);