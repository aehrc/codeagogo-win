// Copyright 2026 CSIRO. Licensed under the Apache License, Version 2.0.
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Codeagogo;

/// <summary>
/// Event args that include the foreground window handle captured at hotkey press time.
/// </summary>
public sealed class HotKeyEventArgs : EventArgs
{
    /// <summary>
    /// The window that had focus when the hotkey was pressed.
    /// </summary>
    public IntPtr ForegroundWindow { get; }

    public HotKeyEventArgs(IntPtr foregroundWindow)
    {
        ForegroundWindow = foregroundWindow;
    }
}

public sealed class HotKeyManager : IDisposable
{
    private readonly MessageWindow _window;
    private readonly Dictionary<int, Action> _callbacks = new();
    private int _currentId = 1;

    public HotKeyManager()
    {
        _window = new MessageWindow(this);
    }

    /// <summary>
    /// Registers a global hotkey with a callback action.
    /// </summary>
    /// <param name="modifiers">Modifier keys (MOD_ALT, MOD_CONTROL, MOD_SHIFT, MOD_WIN)</param>
    /// <param name="virtualKey">Virtual key code</param>
    /// <param name="callback">Action to invoke when hotkey is pressed</param>
    /// <returns>Hotkey ID for later unregistration</returns>
    public int Register(uint modifiers, uint virtualKey, Action callback)
    {
        int id = _currentId++;

        if (!RegisterHotKey(_window.Handle, id, modifiers, virtualKey))
        {
            int err = Marshal.GetLastWin32Error();
            throw new InvalidOperationException($"RegisterHotKey failed (err={err}). Hotkey may be in use.");
        }

        _callbacks[id] = callback;
        return id;
    }

    /// <summary>
    /// Unregisters a specific hotkey by its ID.
    /// </summary>
    /// <param name="id">The hotkey ID returned from Register</param>
    public void Unregister(int id)
    {
        if (_callbacks.ContainsKey(id))
        {
            UnregisterHotKey(_window.Handle, id);
            _callbacks.Remove(id);
        }
    }

    private void UnregisterAll()
    {
        foreach (var id in _callbacks.Keys.ToList())
        {
            UnregisterHotKey(_window.Handle, id);
        }
        _callbacks.Clear();
    }

    internal void OnHotKey(int id)
    {
        if (_callbacks.TryGetValue(id, out var callback))
        {
            callback?.Invoke();
        }
    }

    public void Dispose()
    {
        UnregisterAll();
        _window.Dispose();
    }

    private sealed class MessageWindow : NativeWindow, IDisposable
    {
        private readonly HotKeyManager _owner;

        public MessageWindow(HotKeyManager owner)
        {
            _owner = owner;
            CreateHandle(new CreateParams());
        }

        protected override void WndProc(ref Message m)
        {
            const int WM_HOTKEY = 0x0312;
            if (m.Msg == WM_HOTKEY)
            {
                int id = m.WParam.ToInt32();
                _owner.OnHotKey(id);
            }
            base.WndProc(ref m);
        }

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        public void Dispose() => DestroyHandle();
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
