using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace SNOMEDLookup;

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
    public event EventHandler<HotKeyEventArgs>? HotKeyPressed;

    private readonly MessageWindow _window;
    private int _currentId = 1;

    public HotKeyManager()
    {
        _window = new MessageWindow(this);
    }

    public void Register(uint modifiers, uint virtualKey)
    {
        UnregisterAll();
        int id = _currentId++;

        if (!RegisterHotKey(_window.Handle, id, modifiers, virtualKey))
        {
            int err = Marshal.GetLastWin32Error();
            throw new InvalidOperationException($"RegisterHotKey failed (err={err}). Hotkey may be in use.");
        }

        _window.RegisteredIds.Add(id);
    }

    private void UnregisterAll()
    {
        foreach (var id in _window.RegisteredIds)
        {
            UnregisterHotKey(_window.Handle, id);
        }
        _window.RegisteredIds.Clear();
    }

    internal void OnHotKey(IntPtr foregroundWindow) => HotKeyPressed?.Invoke(this, new HotKeyEventArgs(foregroundWindow));

    public void Dispose()
    {
        UnregisterAll();
        _window.Dispose();
    }

    private sealed class MessageWindow : NativeWindow, IDisposable
    {
        private readonly HotKeyManager _owner;
        public System.Collections.Generic.List<int> RegisteredIds { get; } = new();

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
                // Capture foreground window IMMEDIATELY before any other processing
                var foregroundWindow = GetForegroundWindow();
                _owner.OnHotKey(foregroundWindow);
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
