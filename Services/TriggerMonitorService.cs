using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace AOUU.Services;

public sealed class TriggerMonitorService : IDisposable
{
    private const int MaxVirtualKey = 254;
    private const int GamepadCodeBase = 0x1000;
    private const int GamepadA = GamepadCodeBase + 1;
    private const int GamepadB = GamepadCodeBase + 2;
    private const int GamepadX = GamepadCodeBase + 3;
    private const int GamepadY = GamepadCodeBase + 4;
    private const int GamepadLeftShoulder = GamepadCodeBase + 5;
    private const int GamepadRightShoulder = GamepadCodeBase + 6;
    private const int GamepadBack = GamepadCodeBase + 7;
    private const int GamepadStart = GamepadCodeBase + 8;
    private const int GamepadLeftThumb = GamepadCodeBase + 9;
    private const int GamepadRightThumb = GamepadCodeBase + 10;
    private const int GamepadDPadUp = GamepadCodeBase + 11;
    private const int GamepadDPadDown = GamepadCodeBase + 12;
    private const int GamepadDPadLeft = GamepadCodeBase + 13;
    private const int GamepadDPadRight = GamepadCodeBase + 14;
    private const int GamepadLeftTrigger = GamepadCodeBase + 15;
    private const int GamepadRightTrigger = GamepadCodeBase + 16;
    private const byte TriggerThreshold = 30;

    private const ushort XInputGamepadDPadUp = 0x0001;
    private const ushort XInputGamepadDPadDown = 0x0002;
    private const ushort XInputGamepadDPadLeft = 0x0004;
    private const ushort XInputGamepadDPadRight = 0x0008;
    private const ushort XInputGamepadStart = 0x0010;
    private const ushort XInputGamepadBack = 0x0020;
    private const ushort XInputGamepadLeftThumb = 0x0040;
    private const ushort XInputGamepadRightThumb = 0x0080;
    private const ushort XInputGamepadLeftShoulder = 0x0100;
    private const ushort XInputGamepadRightShoulder = 0x0200;
    private const ushort XInputGamepadA = 0x1000;
    private const ushort XInputGamepadB = 0x2000;
    private const ushort XInputGamepadX = 0x4000;
    private const ushort XInputGamepadY = 0x8000;

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    [DllImport("xinput1_4.dll", EntryPoint = "XInputGetState")]
    private static extern int XInputGetState(int dwUserIndex, out XInputState state);

    private readonly System.Windows.Forms.Timer _timer;
    private bool _wasPressed;

    public TriggerMonitorService()
    {
        _timer = new System.Windows.Forms.Timer();
        _timer.Interval = 20;
        _timer.Tick += Timer_Tick;
    }

    public event EventHandler? Triggered;

    public int TriggerKey { get; set; } = 0x77;

    public bool Enabled
    {
        get => _timer.Enabled;
        set
        {
            if (value)
            {
                _timer.Start();
            }
            else
            {
                _timer.Stop();
                _wasPressed = false;
            }
        }
    }

    public void Dispose()
    {
        _timer.Dispose();
    }

    public static bool TryGetPressedKey(out int keyCode)
    {
        foreach (var candidate in EnumeratePressedKeyboardAndMouseKeys())
        {
            keyCode = candidate;
            return true;
        }

        foreach (var candidate in GetPressedGamepadKeys())
        {
            keyCode = candidate;
            return true;
        }

        keyCode = 0;
        return false;
    }

    public static HashSet<int> GetPressedKeys()
    {
        var pressedKeys = new HashSet<int>();

        foreach (var candidate in EnumeratePressedKeyboardAndMouseKeys())
        {
            pressedKeys.Add(candidate);
        }

        pressedKeys.UnionWith(GetPressedGamepadKeys());
        return pressedKeys;
    }

    public static HashSet<int> GetPressedGamepadKeys()
    {
        var pressedKeys = new HashSet<int>();

        foreach (var state in EnumerateConnectedGamepadStates())
        {
            AddPressedGamepadKeys(state.Gamepad, pressedKeys);
        }

        return pressedKeys;
    }

    public static string GetKeyName(int keyCode)
    {
        if (TryGetGamepadKeyName(keyCode, out var gamepadName))
        {
            return gamepadName;
        }

        return keyCode switch
        {
            0x01 => "鼠标左键",
            0x02 => "鼠标右键",
            0x04 => "鼠标中键",
            0x05 => "鼠标侧键1",
            0x06 => "鼠标侧键2",
            0x08 => "Backspace",
            0x09 => "Tab",
            0x0D => "Enter",
            0x10 => "Shift",
            0x11 => "Ctrl",
            0x12 => "Alt",
            0x1B => "Esc",
            0x20 => "Space",
            0x25 => "方向键左",
            0x26 => "方向键上",
            0x27 => "方向键右",
            0x28 => "方向键下",
            0xA0 => "Left Shift",
            0xA1 => "Right Shift",
            0xA2 => "Left Ctrl",
            0xA3 => "Right Ctrl",
            0xA4 => "Left Alt",
            0xA5 => "Right Alt",
            >= 0x70 and <= 0x7B => $"F{keyCode - 0x6F}",
            >= 0x30 and <= 0x39 => ((char)keyCode).ToString(),
            >= 0x41 and <= 0x5A => ((char)keyCode).ToString(),
            _ => $"未知按键 {keyCode}"
        };
    }

    public static bool IsSupportedHotkey(int keyCode)
    {
        if (TryGetGamepadKeyName(keyCode, out _))
        {
            return true;
        }

        if (keyCode <= 0 || keyCode > MaxVirtualKey || keyCode == 0x01)
        {
            return false;
        }

        return !GetKeyName(keyCode).StartsWith("未知按键 ", StringComparison.Ordinal);
    }

    public static bool IsSameHotkey(int configuredKeyCode, int pressedKeyCode)
    {
        if (configuredKeyCode == pressedKeyCode)
        {
            return true;
        }

        return configuredKeyCode switch
        {
            0x10 => pressedKeyCode is 0xA0 or 0xA1,
            0x11 => pressedKeyCode is 0xA2 or 0xA3,
            0x12 => pressedKeyCode is 0xA4 or 0xA5,
            _ => false
        };
    }

    private void Timer_Tick(object? sender, EventArgs e)
    {
        var isPressed = IsHotkeyPressed(TriggerKey);
        if (isPressed && !_wasPressed)
        {
            Triggered?.Invoke(this, EventArgs.Empty);
        }

        _wasPressed = isPressed;
    }

    private static IEnumerable<int> EnumeratePressedKeyboardAndMouseKeys()
    {
        for (var candidate = 1; candidate <= MaxVirtualKey; candidate++)
        {
            if ((GetAsyncKeyState(candidate) & 0x8000) != 0)
            {
                yield return candidate;
            }
        }
    }

    private static bool IsHotkeyPressed(int keyCode)
    {
        if (TryGetGamepadKeyName(keyCode, out _))
        {
            foreach (var state in EnumerateConnectedGamepadStates())
            {
                if (IsGamepadKeyPressed(state.Gamepad, keyCode))
                {
                    return true;
                }
            }

            return false;
        }

        if (keyCode == 0x10)
        {
            return (GetAsyncKeyState(0x10) & 0x8000) != 0 ||
                   (GetAsyncKeyState(0xA0) & 0x8000) != 0 ||
                   (GetAsyncKeyState(0xA1) & 0x8000) != 0;
        }

        if (keyCode == 0x11)
        {
            return (GetAsyncKeyState(0x11) & 0x8000) != 0 ||
                   (GetAsyncKeyState(0xA2) & 0x8000) != 0 ||
                   (GetAsyncKeyState(0xA3) & 0x8000) != 0;
        }

        if (keyCode == 0x12)
        {
            return (GetAsyncKeyState(0x12) & 0x8000) != 0 ||
                   (GetAsyncKeyState(0xA4) & 0x8000) != 0 ||
                   (GetAsyncKeyState(0xA5) & 0x8000) != 0;
        }

        return (GetAsyncKeyState(keyCode) & 0x8000) != 0;
    }

    private static IEnumerable<XInputState> EnumerateConnectedGamepadStates()
    {
        var states = new List<XInputState>();

        try
        {
            for (var index = 0; index < 4; index++)
            {
                if (XInputGetState(index, out var state) == 0)
                {
                    states.Add(state);
                }
            }
        }
        catch (DllNotFoundException)
        {
            return states;
        }
        catch (EntryPointNotFoundException)
        {
            return states;
        }

        return states;
    }

    private static void AddPressedGamepadKeys(XInputGamepad gamepad, ISet<int> destination)
    {
        foreach (var keyCode in EnumeratePressedGamepadKeys(gamepad))
        {
            destination.Add(keyCode);
        }
    }

    private static IEnumerable<int> EnumeratePressedGamepadKeys(XInputGamepad gamepad)
    {
        if ((gamepad.wButtons & XInputGamepadA) != 0)
        {
            yield return GamepadA;
        }

        if ((gamepad.wButtons & XInputGamepadB) != 0)
        {
            yield return GamepadB;
        }

        if ((gamepad.wButtons & XInputGamepadX) != 0)
        {
            yield return GamepadX;
        }

        if ((gamepad.wButtons & XInputGamepadY) != 0)
        {
            yield return GamepadY;
        }

        if ((gamepad.wButtons & XInputGamepadLeftShoulder) != 0)
        {
            yield return GamepadLeftShoulder;
        }

        if ((gamepad.wButtons & XInputGamepadRightShoulder) != 0)
        {
            yield return GamepadRightShoulder;
        }

        if ((gamepad.wButtons & XInputGamepadBack) != 0)
        {
            yield return GamepadBack;
        }

        if ((gamepad.wButtons & XInputGamepadStart) != 0)
        {
            yield return GamepadStart;
        }

        if ((gamepad.wButtons & XInputGamepadLeftThumb) != 0)
        {
            yield return GamepadLeftThumb;
        }

        if ((gamepad.wButtons & XInputGamepadRightThumb) != 0)
        {
            yield return GamepadRightThumb;
        }

        if ((gamepad.wButtons & XInputGamepadDPadUp) != 0)
        {
            yield return GamepadDPadUp;
        }

        if ((gamepad.wButtons & XInputGamepadDPadDown) != 0)
        {
            yield return GamepadDPadDown;
        }

        if ((gamepad.wButtons & XInputGamepadDPadLeft) != 0)
        {
            yield return GamepadDPadLeft;
        }

        if ((gamepad.wButtons & XInputGamepadDPadRight) != 0)
        {
            yield return GamepadDPadRight;
        }

        if (gamepad.bLeftTrigger >= TriggerThreshold)
        {
            yield return GamepadLeftTrigger;
        }

        if (gamepad.bRightTrigger >= TriggerThreshold)
        {
            yield return GamepadRightTrigger;
        }
    }

    private static bool IsGamepadKeyPressed(XInputGamepad gamepad, int keyCode)
    {
        foreach (var pressedKey in EnumeratePressedGamepadKeys(gamepad))
        {
            if (pressedKey == keyCode)
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryGetGamepadKeyName(int keyCode, out string name)
    {
        name = keyCode switch
        {
            GamepadA => "手柄 A",
            GamepadB => "手柄 B",
            GamepadX => "手柄 X",
            GamepadY => "手柄 Y",
            GamepadLeftShoulder => "手柄 LB",
            GamepadRightShoulder => "手柄 RB",
            GamepadBack => "手柄 Back",
            GamepadStart => "手柄 Start",
            GamepadLeftThumb => "手柄 LS",
            GamepadRightThumb => "手柄 RS",
            GamepadDPadUp => "手柄 十字键上",
            GamepadDPadDown => "手柄 十字键下",
            GamepadDPadLeft => "手柄 十字键左",
            GamepadDPadRight => "手柄 十字键右",
            GamepadLeftTrigger => "手柄 LT",
            GamepadRightTrigger => "手柄 RT",
            _ => string.Empty
        };

        return !string.IsNullOrEmpty(name);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct XInputState
    {
        public uint dwPacketNumber;
        public XInputGamepad Gamepad;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct XInputGamepad
    {
        public ushort wButtons;
        public byte bLeftTrigger;
        public byte bRightTrigger;
        public short sThumbLX;
        public short sThumbLY;
        public short sThumbRX;
        public short sThumbRY;
    }
}
