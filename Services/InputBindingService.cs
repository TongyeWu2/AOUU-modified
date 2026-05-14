using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using AOUU.Models;

namespace AOUU.Services;

public static class InputBindingService
{
    private const int CtrlKey = 0x11;
    private const int AltKey = 0x12;
    private const int ShiftKey = 0x10;
    private const int LeftCtrlKey = 0xA2;
    private const int RightCtrlKey = 0xA3;
    private const int LeftAltKey = 0xA4;
    private const int RightAltKey = 0xA5;
    private const int LeftShiftKey = 0xA0;
    private const int RightShiftKey = 0xA1;

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    public static InputBinding FromLegacyHotkey(int keyCode)
    {
        var binding = new InputBinding
        {
            Kind = TriggerMonitorService.IsGamepadKey(keyCode) ? InputBindingKind.Gamepad : InputBindingKind.Keyboard,
            KeyCode = keyCode,
            Modifiers = KeyboardModifiers.None
        };

        if (binding.Kind == InputBindingKind.Gamepad)
        {
            binding.GamepadKeyCodes = [keyCode];
        }

        binding.DisplayName = GetDisplayName(binding);
        return binding;
    }

    public static InputBinding FromKeyboardEvent(int keyCode)
    {
        var binding = new InputBinding
        {
            Kind = InputBindingKind.Keyboard,
            KeyCode = NormalizeModifierKey(keyCode),
            Modifiers = GetCurrentKeyboardModifiers()
        };

        if (IsModifierKey(keyCode))
        {
            binding.Modifiers = KeyboardModifiers.None;
        }

        binding.DisplayName = GetDisplayName(binding);
        return binding;
    }

    public static InputBinding FromGamepadKey(int keyCode)
    {
        return FromGamepadKeys([keyCode]);
    }

    public static InputBinding FromGamepadKeys(IEnumerable<int> keyCodes)
    {
        var gamepadKeyCodes = keyCodes
            .Where(TriggerMonitorService.IsGamepadKey)
            .Distinct()
            .OrderBy(keyCode => keyCode)
            .ToList();

        var binding = new InputBinding
        {
            Kind = InputBindingKind.Gamepad,
            KeyCode = gamepadKeyCodes.FirstOrDefault(),
            GamepadKeyCodes = gamepadKeyCodes,
            Modifiers = KeyboardModifiers.None
        };
        binding.DisplayName = GetDisplayName(binding);
        return binding;
    }

    public static bool IsSupported(InputBinding? binding)
    {
        if (binding is null)
        {
            return false;
        }

        if (binding.Kind == InputBindingKind.Gamepad)
        {
            var gamepadKeyCodes = GetGamepadKeyCodes(binding);
            return binding.Modifiers == KeyboardModifiers.None &&
                   gamepadKeyCodes.Count > 0 &&
                   gamepadKeyCodes.All(TriggerMonitorService.IsGamepadKey);
        }

        return TriggerMonitorService.IsSupportedKeyboardOrMouseKey(binding.KeyCode);
    }

    public static bool IsPressed(InputBinding binding)
    {
        if (!IsSupported(binding))
        {
            return false;
        }

        if (binding.Kind == InputBindingKind.Gamepad)
        {
            var pressedKeys = TriggerMonitorService.GetPressedGamepadKeys();
            return GetGamepadKeyCodes(binding).All(pressedKeys.Contains);
        }

        if (!IsKeyboardKeyPressed(binding.KeyCode))
        {
            return false;
        }

        return GetCurrentKeyboardModifiers() == binding.Modifiers;
    }

    public static bool Matches(InputBinding configured, InputBinding pressed)
    {
        if (configured.Kind != pressed.Kind)
        {
            return false;
        }

        if (configured.Kind == InputBindingKind.Gamepad)
        {
            var configuredKeys = GetGamepadKeyCodes(configured);
            var pressedKeys = GetGamepadKeyCodes(pressed);
            return configuredKeys.Count > 0 && configuredKeys.All(pressedKeys.Contains);
        }

        return configured.KeyCode == pressed.KeyCode &&
               configured.Modifiers == pressed.Modifiers;
    }

    public static bool Conflicts(InputBinding first, InputBinding second)
    {
        return Matches(first, second) || Matches(second, first);
    }

    public static string GetDisplayName(InputBinding binding)
    {
        if (binding.Kind == InputBindingKind.Gamepad)
        {
            var keyNames = GetGamepadKeyCodes(binding)
                .Select(GetGamepadButtonDisplayName)
                .ToList();

            return keyNames.Count == 0
                ? "不支持的手柄组合"
                : $"Gamepad: {string.Join(" + ", keyNames)}";
        }

        var parts = new List<string>();
        if (binding.Modifiers.HasFlag(KeyboardModifiers.Ctrl))
        {
            parts.Add("Ctrl");
        }

        if (binding.Modifiers.HasFlag(KeyboardModifiers.Alt))
        {
            parts.Add("Alt");
        }

        if (binding.Modifiers.HasFlag(KeyboardModifiers.Shift))
        {
            parts.Add("Shift");
        }

        parts.Add(TriggerMonitorService.GetKeyName(binding.KeyCode));
        return string.Join(" + ", parts);
    }

    public static InputBinding Normalize(InputBinding? binding, int fallbackKeyCode)
    {
        var normalized = binding?.Clone() ?? FromLegacyHotkey(fallbackKeyCode);
        if (!IsSupported(normalized))
        {
            normalized = FromLegacyHotkey(fallbackKeyCode);
        }
        else if (normalized.Kind == InputBindingKind.Gamepad)
        {
            normalized.GamepadKeyCodes = GetGamepadKeyCodes(normalized);
            normalized.KeyCode = normalized.GamepadKeyCodes.FirstOrDefault();
            normalized.Modifiers = KeyboardModifiers.None;
        }

        normalized.DisplayName = GetDisplayName(normalized);
        return normalized;
    }

    public static KeyboardModifiers GetCurrentKeyboardModifiers()
    {
        var modifiers = KeyboardModifiers.None;

        if (IsKeyboardKeyPressed(CtrlKey) || IsKeyboardKeyPressed(LeftCtrlKey) || IsKeyboardKeyPressed(RightCtrlKey))
        {
            modifiers |= KeyboardModifiers.Ctrl;
        }

        if (IsKeyboardKeyPressed(AltKey) || IsKeyboardKeyPressed(LeftAltKey) || IsKeyboardKeyPressed(RightAltKey))
        {
            modifiers |= KeyboardModifiers.Alt;
        }

        if (IsKeyboardKeyPressed(ShiftKey) || IsKeyboardKeyPressed(LeftShiftKey) || IsKeyboardKeyPressed(RightShiftKey))
        {
            modifiers |= KeyboardModifiers.Shift;
        }

        return modifiers;
    }

    public static bool IsModifierKey(int keyCode)
    {
        return keyCode is CtrlKey or AltKey or ShiftKey or LeftCtrlKey or RightCtrlKey or LeftAltKey or RightAltKey or LeftShiftKey or RightShiftKey;
    }

    public static bool HasNonModifierKeyboardKey(ISet<int> pressedKeys)
    {
        return pressedKeys.Any(keyCode => !TriggerMonitorService.IsGamepadKey(keyCode) && !IsModifierKey(keyCode));
    }

    private static int NormalizeModifierKey(int keyCode)
    {
        return keyCode switch
        {
            LeftCtrlKey or RightCtrlKey => CtrlKey,
            LeftAltKey or RightAltKey => AltKey,
            LeftShiftKey or RightShiftKey => ShiftKey,
            _ => keyCode
        };
    }

    private static bool IsKeyboardKeyPressed(int keyCode)
    {
        return keyCode switch
        {
            CtrlKey => (GetAsyncKeyState(CtrlKey) & 0x8000) != 0 ||
                       (GetAsyncKeyState(LeftCtrlKey) & 0x8000) != 0 ||
                       (GetAsyncKeyState(RightCtrlKey) & 0x8000) != 0,
            AltKey => (GetAsyncKeyState(AltKey) & 0x8000) != 0 ||
                      (GetAsyncKeyState(LeftAltKey) & 0x8000) != 0 ||
                      (GetAsyncKeyState(RightAltKey) & 0x8000) != 0,
            ShiftKey => (GetAsyncKeyState(ShiftKey) & 0x8000) != 0 ||
                        (GetAsyncKeyState(LeftShiftKey) & 0x8000) != 0 ||
                        (GetAsyncKeyState(RightShiftKey) & 0x8000) != 0,
            _ => (GetAsyncKeyState(keyCode) & 0x8000) != 0
        };
    }

    private static List<int> GetGamepadKeyCodes(InputBinding binding)
    {
        var gamepadKeyCodes = binding.GamepadKeyCodes
            .Where(TriggerMonitorService.IsGamepadKey)
            .Distinct()
            .OrderBy(keyCode => keyCode)
            .ToList();

        if (gamepadKeyCodes.Count == 0 && TriggerMonitorService.IsGamepadKey(binding.KeyCode))
        {
            gamepadKeyCodes.Add(binding.KeyCode);
        }

        return gamepadKeyCodes;
    }

    private static string GetGamepadButtonDisplayName(int keyCode)
    {
        const string gamepadPrefix = "Gamepad ";

        var keyName = TriggerMonitorService.GetKeyName(keyCode);
        return keyName.StartsWith(gamepadPrefix, StringComparison.Ordinal)
            ? keyName[gamepadPrefix.Length..]
            : keyName;
    }
}
