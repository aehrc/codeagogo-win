// Copyright 2026 CSIRO. Licensed under the Apache License, Version 2.0.
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.InteropServices;

namespace Codeagogo;

/// <summary>
/// Helper class for selecting text after insertion using SendInput.
/// This is a best-effort feature that may not work in all applications.
/// </summary>
public static class TextSelectionHelper
{
    private const int INPUT_KEYBOARD = 1;
    private const uint KEYEVENTF_KEYUP = 0x0002;
    private const uint KEYEVENTF_EXTENDEDKEY = 0x0001;

    // Virtual key codes
    private const ushort VK_SHIFT = 0x10;
    private const ushort VK_LEFT = 0x25;

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct HARDWAREINPUT
    {
        public uint uMsg;
        public ushort wParamL;
        public ushort wParamH;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public MOUSEINPUT mi;
        [FieldOffset(0)] public KEYBDINPUT ki;
        [FieldOffset(0)] public HARDWAREINPUT hi;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public int type;
        public InputUnion u;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    /// <summary>
    /// Attempts to select the text that was just inserted by sending Shift+Left arrow keys.
    /// This is a best-effort feature that may not work in all applications.
    /// </summary>
    /// <param name="length">The number of characters to select.</param>
    /// <returns>True if the selection was attempted, false if an error occurred.</returns>
    public static bool SelectInsertedText(int length)
    {
        if (length <= 0)
            return false;

        try
        {
            // Build array of inputs: press Shift, then Left arrow (length times), then release Shift
            // Each key press/release is a separate input
            var inputs = new INPUT[2 + (length * 2)];

            // Press Shift
            inputs[0] = CreateKeyInput(VK_SHIFT, false);

            // Press and release Left arrow for each character
            for (int i = 0; i < length; i++)
            {
                inputs[1 + (i * 2)] = CreateKeyInput(VK_LEFT, false, true);
                inputs[1 + (i * 2) + 1] = CreateKeyInput(VK_LEFT, true, true);
            }

            // Release Shift
            inputs[inputs.Length - 1] = CreateKeyInput(VK_SHIFT, true);

            uint sent = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT)));
            Log.Debug($"TextSelectionHelper: sent {sent}/{inputs.Length} inputs for {length} chars");

            return sent == inputs.Length;
        }
        catch (Exception ex)
        {
            Log.Error($"TextSelectionHelper failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Sends a single Shift+Left arrow key combination.
    /// </summary>
    public static bool SendShiftLeft()
    {
        try
        {
            var inputs = new INPUT[]
            {
                CreateKeyInput(VK_SHIFT, false),
                CreateKeyInput(VK_LEFT, false, true),
                CreateKeyInput(VK_LEFT, true, true),
                CreateKeyInput(VK_SHIFT, true)
            };

            uint sent = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT)));
            return sent == inputs.Length;
        }
        catch (Exception ex)
        {
            Log.Error($"SendShiftLeft failed: {ex.Message}");
            return false;
        }
    }

    private static INPUT CreateKeyInput(ushort vk, bool keyUp, bool extended = false)
    {
        uint flags = 0;
        if (keyUp) flags |= KEYEVENTF_KEYUP;
        if (extended) flags |= KEYEVENTF_EXTENDEDKEY;

        return new INPUT
        {
            type = INPUT_KEYBOARD,
            u = new InputUnion
            {
                ki = new KEYBDINPUT
                {
                    wVk = vk,
                    wScan = 0,
                    dwFlags = flags,
                    time = 0,
                    dwExtraInfo = IntPtr.Zero
                }
            }
        };
    }
}
