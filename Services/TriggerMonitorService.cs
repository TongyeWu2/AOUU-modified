using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using AOUU.Models;

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
    private const byte TriggerThreshold = 128;
    private const int CtrlKey = 0x11;
    private const int AltKey = 0x12;
    private const int ShiftKey = 0x10;
    private const int LeftCtrlKey = 0xA2;
    private const int RightCtrlKey = 0xA3;
    private const int LeftAltKey = 0xA4;
    private const int RightAltKey = 0xA5;
    private const int LeftShiftKey = 0xA0;
    private const int RightShiftKey = 0xA1;

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

    public InputBinding TriggerBinding { get; set; } = InputBindingService.FromLegacyHotkey(0x77);

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

        pressedKeys.UnionWith(GetPressedKeyboardAndMouseKeys());

        pressedKeys.UnionWith(GetPressedGamepadKeys());
        return pressedKeys;
    }

    public static HashSet<int> GetPressedKeyboardAndMouseKeys()
    {
        var pressedKeys = new HashSet<int>();
        foreach (var candidate in EnumeratePressedKeyboardAndMouseKeys())
        {
            pressedKeys.Add(candidate);
        }

        pressedKeys.UnionWith(GlobalInputHookService.GetPressedKeyboardKeys());
        return pressedKeys;
    }

    public static HashSet<int> GetAsyncPressedKeyboardAndMouseKeys()
    {
        var pressedKeys = new HashSet<int>();
        foreach (var candidate in EnumeratePressedKeyboardAndMouseKeys())
        {
            pressedKeys.Add(candidate);
        }

        return pressedKeys;
    }

    public static HashSet<int> GetHookPressedKeyboardKeys()
    {
        return GlobalInputHookService.GetPressedKeyboardKeys();
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
            0x14 => "Caps Lock",
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
            >= 0x60 and <= 0x69 => $"NumPad {keyCode - 0x60}",
            >= 0x70 and <= 0x7B => $"F{keyCode - 0x6F}",
            >= 0x30 and <= 0x39 => ((char)keyCode).ToString(),
            >= 0x41 and <= 0x5A => ((char)keyCode).ToString(),
            _ => $"未知按键 {keyCode}"
        };
    }

    public static bool IsSupportedHotkey(int keyCode)
    {
        return IsGamepadKey(keyCode) || IsSupportedKeyboardOrMouseKey(keyCode);
    }

    public static bool IsSupportedKeyboardOrMouseKey(int keyCode)
    {
        if (TryGetGamepadKeyName(keyCode, out _))
        {
            return false;
        }

        if (keyCode <= 0 || keyCode > MaxVirtualKey || keyCode == 0x01)
        {
            return false;
        }

        return !GetKeyName(keyCode).StartsWith("未知按键 ", StringComparison.Ordinal);
    }

    public static bool IsGamepadKey(int keyCode)
    {
        return TryGetGamepadKeyName(keyCode, out _);
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
        var isPressed = IsConfiguredHotkeyPressed();
        if (isPressed && !_wasPressed)
        {
            Triggered?.Invoke(this, EventArgs.Empty);
        }

        _wasPressed = isPressed;
    }

    private bool IsConfiguredHotkeyPressed()
    {
        if (!InputBindingService.IsSupported(TriggerBinding))
        {
            return IsHotkeyPressed(TriggerKey);
        }

        if (TriggerBinding.Kind == InputBindingKind.Gamepad)
        {
            return InputBindingService.IsPressed(TriggerBinding);
        }

        if (TriggerBinding.Modifiers != KeyboardModifiers.None)
        {
            return InputBindingService.IsPressed(TriggerBinding);
        }

        return IsHotkeyPressed(TriggerBinding.KeyCode != 0 ? TriggerBinding.KeyCode : TriggerKey);
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

    public static bool IsGamepadKeyPressed(int keyCode)
    {
        if (!TryGetGamepadKeyName(keyCode, out _))
        {
            return false;
        }

        foreach (var state in EnumerateConnectedGamepadStates())
        {
            if (IsGamepadKeyPressed(state.Gamepad, keyCode))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsHotkeyPressed(int keyCode)
    {
        if (TryGetGamepadKeyName(keyCode, out _))
        {
            return IsGamepadKeyPressed(keyCode);
        }

        return keyCode switch
        {
            ShiftKey => IsKeyboardKeyDown(ShiftKey) || IsKeyboardKeyDown(LeftShiftKey) || IsKeyboardKeyDown(RightShiftKey),
            CtrlKey => IsKeyboardKeyDown(CtrlKey) || IsKeyboardKeyDown(LeftCtrlKey) || IsKeyboardKeyDown(RightCtrlKey),
            AltKey => IsKeyboardKeyDown(AltKey) || IsKeyboardKeyDown(LeftAltKey) || IsKeyboardKeyDown(RightAltKey),
            LeftShiftKey => IsKeyboardKeyDown(LeftShiftKey) || (!IsKeyboardKeyDown(RightShiftKey) && IsKeyboardKeyDown(ShiftKey)),
            RightShiftKey => IsKeyboardKeyDown(RightShiftKey) || (!IsKeyboardKeyDown(LeftShiftKey) && IsKeyboardKeyDown(ShiftKey)),
            LeftCtrlKey => IsKeyboardKeyDown(LeftCtrlKey) || (!IsKeyboardKeyDown(RightCtrlKey) && IsKeyboardKeyDown(CtrlKey)),
            RightCtrlKey => IsKeyboardKeyDown(RightCtrlKey) || (!IsKeyboardKeyDown(LeftCtrlKey) && IsKeyboardKeyDown(CtrlKey)),
            LeftAltKey => IsKeyboardKeyDown(LeftAltKey) || (!IsKeyboardKeyDown(RightAltKey) && IsKeyboardKeyDown(AltKey)),
            RightAltKey => IsKeyboardKeyDown(RightAltKey) || (!IsKeyboardKeyDown(LeftAltKey) && IsKeyboardKeyDown(AltKey)),
            _ => IsKeyboardKeyDown(keyCode)
        };
    }

    private static bool IsKeyboardKeyDown(int keyCode)
    {
        return (GetAsyncKeyState(keyCode) & 0x8000) != 0 ||
               GlobalInputHookService.GetPressedKeyboardKeys().Contains(keyCode);
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
            GamepadA => "Gamepad A",
            GamepadB => "Gamepad B",
            GamepadX => "Gamepad X",
            GamepadY => "Gamepad Y",
            GamepadLeftShoulder => "Gamepad LB",
            GamepadRightShoulder => "Gamepad RB",
            GamepadBack => "Gamepad Back",
            GamepadStart => "Gamepad Start",
            GamepadLeftThumb => "Gamepad Left Stick Button",
            GamepadRightThumb => "Gamepad Right Stick Button",
            GamepadDPadUp => "Gamepad DPad Up",
            GamepadDPadDown => "Gamepad DPad Down",
            GamepadDPadLeft => "Gamepad DPad Left",
            GamepadDPadRight => "Gamepad DPad Right",
            GamepadLeftTrigger => "Gamepad LT",
            GamepadRightTrigger => "Gamepad RT",
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
