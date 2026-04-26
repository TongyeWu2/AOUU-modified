using System;
using System.Runtime.InteropServices;

namespace AOUU.Services;

public sealed class GlobalInputHookService : IDisposable
{
    private const int WhKeyboardLl = 13;
    private const int WhMouseLl = 14;
    private const int WmKeyDown = 0x0100;
    private const int WmSysKeyDown = 0x0104;
    private const int WmLButtonDown = 0x0201;
    private const int WmRButtonDown = 0x0204;
    private const int WmMButtonDown = 0x0207;
    private const int WmXButtonDown = 0x020B;

    private readonly HookProc _keyboardHookProc;
    private readonly HookProc _mouseHookProc;

    private IntPtr _keyboardHookHandle = IntPtr.Zero;
    private IntPtr _mouseHookHandle = IntPtr.Zero;
    private bool _disposed;

    public GlobalInputHookService()
    {
        _keyboardHookProc = KeyboardHookCallback;
        _mouseHookProc = MouseHookCallback;
    }

    public event Action<int>? KeyboardPressed;

    public event Action<int>? MousePressed;

    public bool IsInstalled => _keyboardHookHandle != IntPtr.Zero && _mouseHookHandle != IntPtr.Zero;

    public void Install()
    {
        ThrowIfDisposed();

        if (IsInstalled)
        {
            return;
        }

        _keyboardHookHandle = SetWindowsHookEx(WhKeyboardLl, _keyboardHookProc, IntPtr.Zero, 0);
        _mouseHookHandle = SetWindowsHookEx(WhMouseLl, _mouseHookProc, IntPtr.Zero, 0);

        if (_keyboardHookHandle == IntPtr.Zero || _mouseHookHandle == IntPtr.Zero)
        {
            Remove();
            throw new InvalidOperationException("Failed to install the global input hook.");
        }
    }

    public void Remove()
    {
        if (_keyboardHookHandle != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_keyboardHookHandle);
            _keyboardHookHandle = IntPtr.Zero;
        }

        if (_mouseHookHandle != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_mouseHookHandle);
            _mouseHookHandle = IntPtr.Zero;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Remove();
        _disposed = true;
    }

    private IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && (wParam == (IntPtr)WmKeyDown || wParam == (IntPtr)WmSysKeyDown))
        {
            var keyCode = Marshal.ReadInt32(lParam);
            KeyboardPressed?.Invoke(keyCode);
        }

        return CallNextHookEx(_keyboardHookHandle, nCode, wParam, lParam);
    }

    private IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var keyCode = wParam switch
            {
                (IntPtr)WmLButtonDown => 0x01,
                (IntPtr)WmRButtonDown => 0x02,
                (IntPtr)WmMButtonDown => 0x04,
                (IntPtr)WmXButtonDown => ResolveXButtonKeyCode(lParam),
                _ => 0
            };

            if (keyCode != 0)
            {
                MousePressed?.Invoke(keyCode);
            }
        }

        return CallNextHookEx(_mouseHookHandle, nCode, wParam, lParam);
    }

    private static int ResolveXButtonKeyCode(IntPtr lParam)
    {
        var mouseStruct = Marshal.PtrToStructure<MsLlHookStruct>(lParam);
        var highWord = (mouseStruct.MouseData >> 16) & 0xffff;
        return highWord == 1 ? 0x05 : 0x06;
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(GlobalInputHookService));
        }
    }

    private delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct PointStruct
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MsLlHookStruct
    {
        public PointStruct Point;
        public int MouseData;
        public int Flags;
        public int Time;
        public IntPtr DwExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
}
