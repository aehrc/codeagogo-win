using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace SNOMEDLookup;

public sealed class HotKeyManager : IDisposable
{
    public event EventHandler? HotKeyPressed;

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

    internal void OnHotKey() => HotKeyPressed?.Invoke(this, EventArgs.Empty);

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
                _owner.OnHotKey();
            }
            base.WndProc(ref m);
        }

        public void Dispose() => DestroyHandle();
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
