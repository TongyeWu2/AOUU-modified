using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace AOUU.Services;

public sealed class GlobalInputHookService : IDisposable
{
    private const int WhKeyboardLl = 13;
    private const int WhMouseLl = 14;
    private const int WmKeyDown = 0x0100;
    private const int WmKeyUp = 0x0101;
    private const int WmSysKeyDown = 0x0104;
    private const int WmSysKeyUp = 0x0105;
    private const int WmLButtonDown = 0x0201;
    private const int WmRButtonDown = 0x0204;
    private const int WmMButtonDown = 0x0207;
    private const int WmXButtonDown = 0x020B;
    private const int LlkhfExtended = 0x01;
    private const int VkShift = 0x10;
    private const int VkControl = 0x11;
    private const int VkMenu = 0x12;
    private const int VkLShift = 0xA0;
    private const int VkRShift = 0xA1;
    private const int VkLControl = 0xA2;
    private const int VkRControl = 0xA3;
    private const int VkLMenu = 0xA4;
    private const int VkRMenu = 0xA5;
    private const int LeftShiftScanCode = 0x2A;
    private const int RightShiftScanCode = 0x36;

    private static readonly object PressedKeyboardKeysLock = new();
    private static readonly HashSet<int> PressedKeyboardKeys = [];

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

    public static HashSet<int> GetPressedKeyboardKeys()
    {
        lock (PressedKeyboardKeysLock)
        {
            return PressedKeyboardKeys.ToHashSet();
        }
    }

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

        lock (PressedKeyboardKeysLock)
        {
            PressedKeyboardKeys.Clear();
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
        if (nCode >= 0)
        {
            var keyboardStruct = Marshal.PtrToStructure<KbLlHookStruct>(lParam);
            var keyCode = NormalizeKeyboardHookKeyCode(keyboardStruct);

            if (wParam == (IntPtr)WmKeyDown || wParam == (IntPtr)WmSysKeyDown)
            {
                var isNewPress = false;
                lock (PressedKeyboardKeysLock)
                {
                    isNewPress = PressedKeyboardKeys.Add(keyCode);
                }

                InputDebugLogger.LogHookKeyboardEvent(keyCode, isDown: true, isNewPress);
                if (isNewPress)
                {
                    KeyboardPressed?.Invoke(keyCode);
                }
            }
            else if (wParam == (IntPtr)WmKeyUp || wParam == (IntPtr)WmSysKeyUp)
            {
                var wasPressed = false;
                lock (PressedKeyboardKeysLock)
                {
                    wasPressed = PressedKeyboardKeys.Remove(keyCode);
                }

                InputDebugLogger.LogHookKeyboardEvent(keyCode, isDown: false, wasPressed);
            }
        }

        return CallNextHookEx(_keyboardHookHandle, nCode, wParam, lParam);
    }

    private static int NormalizeKeyboardHookKeyCode(KbLlHookStruct keyboardStruct)
    {
        return keyboardStruct.VkCode switch
        {
            VkControl => (keyboardStruct.Flags & LlkhfExtended) != 0 ? VkRControl : VkLControl,
            VkMenu => (keyboardStruct.Flags & LlkhfExtended) != 0 ? VkRMenu : VkLMenu,
            VkShift => keyboardStruct.ScanCode == RightShiftScanCode ? VkRShift :
                       keyboardStruct.ScanCode == LeftShiftScanCode ? VkLShift :
                       VkShift,
            _ => keyboardStruct.VkCode
        };
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

    [StructLayout(LayoutKind.Sequential)]
    private struct KbLlHookStruct
    {
        public int VkCode;
        public int ScanCode;
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
