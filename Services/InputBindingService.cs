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
        var pressedKeys = TriggerMonitorService.GetPressedKeyboardAndMouseKeys();
        pressedKeys.Add(keyCode);
        return FromKeyboardState(pressedKeys, keyCode);
    }

    public static InputBinding FromKeyboardState(ISet<int> pressedKeys, int triggerKeyCode)
    {
        var keyboardKeys = pressedKeys
            .Where(keyCode => !TriggerMonitorService.IsGamepadKey(keyCode))
            .Distinct()
            .ToList();
        var modifierKeyCodes = GetKeyboardModifierKeyCodes(keyboardKeys);
        var nonModifierKeyCode = keyboardKeys
            .Where(keyCode => !IsModifierKey(keyCode))
            .OrderBy(keyCode => keyCode)
            .FirstOrDefault();
        var keyCode = !IsModifierKey(triggerKeyCode)
            ? triggerKeyCode
            : nonModifierKeyCode != 0
                ? nonModifierKeyCode
                : ResolveModifierKeyForBinding(triggerKeyCode, modifierKeyCodes);

        var binding = new InputBinding
        {
            Kind = InputBindingKind.Keyboard,
            KeyCode = keyCode,
            KeyboardModifierKeyCodes = IsModifierKey(keyCode) ? [] : modifierKeyCodes,
            Modifiers = IsModifierKey(keyCode) ? KeyboardModifiers.None : GetKeyboardModifiers(modifierKeyCodes)
        };

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

        return TriggerMonitorService.IsSupportedKeyboardOrMouseKey(binding.KeyCode) &&
               GetKeyboardModifierKeyCodes(binding).All(TriggerMonitorService.IsSupportedKeyboardOrMouseKey);
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

        if (IsModifierKey(binding.KeyCode) && binding.Modifiers == KeyboardModifiers.None)
        {
            return IsSingleModifierBindingPressed(binding.KeyCode);
        }

        return AreKeyboardModifiersPressed(binding);
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

        return KeyboardKeyCodesMatch(configured.KeyCode, pressed.KeyCode) &&
               KeyboardModifiersMatch(configured, pressed);
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
        var exactModifierNames = !IsModifierKey(binding.KeyCode)
            ? GetKeyboardModifierKeyCodes(binding)
                .Select(TriggerMonitorService.GetKeyName)
                .ToList()
            : [];

        if (exactModifierNames.Count > 0)
        {
            parts.AddRange(exactModifierNames);
        }
        else if (binding.Modifiers.HasFlag(KeyboardModifiers.Ctrl))
        {
            parts.Add("Ctrl");
        }

        if (exactModifierNames.Count == 0 && binding.Modifiers.HasFlag(KeyboardModifiers.Alt))
        {
            parts.Add("Alt");
        }

        if (exactModifierNames.Count == 0 && binding.Modifiers.HasFlag(KeyboardModifiers.Shift))
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
            normalized.KeyboardModifierKeyCodes = [];
        }
        else
        {
            normalized.KeyboardModifierKeyCodes = IsModifierKey(normalized.KeyCode)
                ? []
                : GetKeyboardModifierKeyCodes(normalized);
            if (normalized.KeyboardModifierKeyCodes.Count > 0)
            {
                normalized.Modifiers = GetKeyboardModifiers(normalized.KeyboardModifierKeyCodes);
            }
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

    private static int ResolveModifierKeyForBinding(int keyCode, IReadOnlyCollection<int> pressedModifierKeyCodes)
    {
        if (IsSideSpecificModifierKey(keyCode))
        {
            return keyCode;
        }

        return keyCode switch
        {
            CtrlKey when pressedModifierKeyCodes.Contains(LeftCtrlKey) => LeftCtrlKey,
            CtrlKey when pressedModifierKeyCodes.Contains(RightCtrlKey) => RightCtrlKey,
            AltKey when pressedModifierKeyCodes.Contains(LeftAltKey) => LeftAltKey,
            AltKey when pressedModifierKeyCodes.Contains(RightAltKey) => RightAltKey,
            ShiftKey when pressedModifierKeyCodes.Contains(LeftShiftKey) => LeftShiftKey,
            ShiftKey when pressedModifierKeyCodes.Contains(RightShiftKey) => RightShiftKey,
            _ => keyCode
        };
    }

    private static bool IsSideSpecificModifierKey(int keyCode)
    {
        return keyCode is LeftCtrlKey or RightCtrlKey or LeftAltKey or RightAltKey or LeftShiftKey or RightShiftKey;
    }

    private static KeyboardModifiers GetKeyboardModifiers(IEnumerable<int> modifierKeyCodes)
    {
        var modifiers = KeyboardModifiers.None;
        foreach (var keyCode in modifierKeyCodes)
        {
            modifiers |= GetModifierFlag(keyCode);
        }

        return modifiers;
    }

    private static KeyboardModifiers GetModifierFlag(int keyCode)
    {
        return keyCode switch
        {
            CtrlKey or LeftCtrlKey or RightCtrlKey => KeyboardModifiers.Ctrl,
            AltKey or LeftAltKey or RightAltKey => KeyboardModifiers.Alt,
            ShiftKey or LeftShiftKey or RightShiftKey => KeyboardModifiers.Shift,
            _ => KeyboardModifiers.None
        };
    }

    private static List<int> GetKeyboardModifierKeyCodes(InputBinding binding)
    {
        return GetKeyboardModifierKeyCodes(binding.KeyboardModifierKeyCodes);
    }

    private static List<int> GetKeyboardModifierKeyCodes(IEnumerable<int> keyCodes)
    {
        var modifierKeyCodes = keyCodes
            .Where(IsModifierKey)
            .Distinct()
            .ToList();

        return new[]
            {
                LeftCtrlKey,
                RightCtrlKey,
                CtrlKey,
                LeftAltKey,
                RightAltKey,
                AltKey,
                LeftShiftKey,
                RightShiftKey,
                ShiftKey
            }
            .Where(keyCode => modifierKeyCodes.Contains(keyCode) && !HasSideSpecificEquivalent(keyCode, modifierKeyCodes))
            .ToList();
    }

    private static bool HasSideSpecificEquivalent(int keyCode, IReadOnlyCollection<int> modifierKeyCodes)
    {
        return keyCode switch
        {
            CtrlKey => modifierKeyCodes.Contains(LeftCtrlKey) || modifierKeyCodes.Contains(RightCtrlKey),
            AltKey => modifierKeyCodes.Contains(LeftAltKey) || modifierKeyCodes.Contains(RightAltKey),
            ShiftKey => modifierKeyCodes.Contains(LeftShiftKey) || modifierKeyCodes.Contains(RightShiftKey),
            _ => false
        };
    }

    private static bool KeyboardKeyCodesMatch(int configuredKeyCode, int pressedKeyCode)
    {
        if (configuredKeyCode == pressedKeyCode)
        {
            return true;
        }

        if (IsModifierKey(configuredKeyCode) && IsModifierKey(pressedKeyCode))
        {
            return ModifierKeyCodesCompatible(configuredKeyCode, pressedKeyCode);
        }

        return TriggerMonitorService.IsSameHotkey(configuredKeyCode, pressedKeyCode);
    }

    private static bool KeyboardModifiersMatch(InputBinding configured, InputBinding pressed)
    {
        if ((pressed.Modifiers & configured.Modifiers) != configured.Modifiers)
        {
            return false;
        }

        var configuredModifierKeyCodes = GetKeyboardModifierKeyCodes(configured);
        if (configuredModifierKeyCodes.Count == 0)
        {
            return true;
        }

        return ModifierKeyCodesSatisfied(configuredModifierKeyCodes, GetKeyboardModifierKeyCodes(pressed));
    }

    private static bool AreKeyboardModifiersPressed(InputBinding binding)
    {
        if ((GetCurrentKeyboardModifiers() & binding.Modifiers) != binding.Modifiers)
        {
            return false;
        }

        var requiredModifierKeyCodes = GetKeyboardModifierKeyCodes(binding);
        return requiredModifierKeyCodes.Count == 0 ||
               ModifierKeyCodesSatisfied(requiredModifierKeyCodes, GetCurrentKeyboardModifierKeyCodes());
    }

    private static bool IsSingleModifierBindingPressed(int keyCode)
    {
        return !IsSideSpecificModifierKey(keyCode) ||
               ModifierKeyCodesSatisfied([keyCode], GetCurrentKeyboardModifierKeyCodes());
    }

    private static List<int> GetCurrentKeyboardModifierKeyCodes()
    {
        var modifierKeyCodes = new List<int>();

        if (IsKeyboardKeyPressed(LeftCtrlKey))
        {
            modifierKeyCodes.Add(LeftCtrlKey);
        }

        if (IsKeyboardKeyPressed(RightCtrlKey))
        {
            modifierKeyCodes.Add(RightCtrlKey);
        }

        if (!modifierKeyCodes.Any(IsCtrlKey) && IsKeyboardKeyPressed(CtrlKey))
        {
            modifierKeyCodes.Add(CtrlKey);
        }

        if (IsKeyboardKeyPressed(LeftAltKey))
        {
            modifierKeyCodes.Add(LeftAltKey);
        }

        if (IsKeyboardKeyPressed(RightAltKey))
        {
            modifierKeyCodes.Add(RightAltKey);
        }

        if (!modifierKeyCodes.Any(IsAltKey) && IsKeyboardKeyPressed(AltKey))
        {
            modifierKeyCodes.Add(AltKey);
        }

        if (IsKeyboardKeyPressed(LeftShiftKey))
        {
            modifierKeyCodes.Add(LeftShiftKey);
        }

        if (IsKeyboardKeyPressed(RightShiftKey))
        {
            modifierKeyCodes.Add(RightShiftKey);
        }

        if (!modifierKeyCodes.Any(IsShiftKey) && IsKeyboardKeyPressed(ShiftKey))
        {
            modifierKeyCodes.Add(ShiftKey);
        }

        return modifierKeyCodes;
    }

    private static bool ModifierKeyCodesSatisfied(IReadOnlyCollection<int> requiredKeyCodes, IReadOnlyCollection<int> actualKeyCodes)
    {
        return requiredKeyCodes.All(requiredKeyCode => actualKeyCodes.Any(actualKeyCode => ModifierKeyCodesCompatible(requiredKeyCode, actualKeyCode)));
    }

    private static bool ModifierKeyCodesCompatible(int configuredKeyCode, int pressedKeyCode)
    {
        if (configuredKeyCode == pressedKeyCode)
        {
            return true;
        }

        if (IsCtrlKey(configuredKeyCode) && IsCtrlKey(pressedKeyCode))
        {
            return configuredKeyCode == CtrlKey || pressedKeyCode == CtrlKey;
        }

        if (IsAltKey(configuredKeyCode) && IsAltKey(pressedKeyCode))
        {
            return configuredKeyCode == AltKey || pressedKeyCode == AltKey;
        }

        if (IsShiftKey(configuredKeyCode) && IsShiftKey(pressedKeyCode))
        {
            return configuredKeyCode == ShiftKey || pressedKeyCode == ShiftKey;
        }

        return false;
    }

    private static bool IsCtrlKey(int keyCode)
    {
        return keyCode is CtrlKey or LeftCtrlKey or RightCtrlKey;
    }

    private static bool IsAltKey(int keyCode)
    {
        return keyCode is AltKey or LeftAltKey or RightAltKey;
    }

    private static bool IsShiftKey(int keyCode)
    {
        return keyCode is ShiftKey or LeftShiftKey or RightShiftKey;
    }

    private static bool IsKeyboardKeyPressed(int keyCode)
    {
        return keyCode switch
        {
            CtrlKey => (GetAsyncKeyState(CtrlKey) & 0x8000) != 0 ||
                       (GetAsyncKeyState(LeftCtrlKey) & 0x8000) != 0 ||
                       (GetAsyncKeyState(RightCtrlKey) & 0x8000) != 0 ||
                       IsHookKeyPressed(CtrlKey) ||
                       IsHookKeyPressed(LeftCtrlKey) ||
                       IsHookKeyPressed(RightCtrlKey),
            AltKey => (GetAsyncKeyState(AltKey) & 0x8000) != 0 ||
                      (GetAsyncKeyState(LeftAltKey) & 0x8000) != 0 ||
                      (GetAsyncKeyState(RightAltKey) & 0x8000) != 0 ||
                      IsHookKeyPressed(AltKey) ||
                      IsHookKeyPressed(LeftAltKey) ||
                      IsHookKeyPressed(RightAltKey),
            ShiftKey => (GetAsyncKeyState(ShiftKey) & 0x8000) != 0 ||
                        (GetAsyncKeyState(LeftShiftKey) & 0x8000) != 0 ||
                        (GetAsyncKeyState(RightShiftKey) & 0x8000) != 0 ||
                        IsHookKeyPressed(ShiftKey) ||
                        IsHookKeyPressed(LeftShiftKey) ||
                        IsHookKeyPressed(RightShiftKey),
            LeftCtrlKey => IsSideSpecificModifierPressed(LeftCtrlKey, RightCtrlKey, CtrlKey),
            RightCtrlKey => IsSideSpecificModifierPressed(RightCtrlKey, LeftCtrlKey, CtrlKey),
            LeftAltKey => IsSideSpecificModifierPressed(LeftAltKey, RightAltKey, AltKey),
            RightAltKey => IsSideSpecificModifierPressed(RightAltKey, LeftAltKey, AltKey),
            LeftShiftKey => IsSideSpecificModifierPressed(LeftShiftKey, RightShiftKey, ShiftKey),
            RightShiftKey => IsSideSpecificModifierPressed(RightShiftKey, LeftShiftKey, ShiftKey),
            _ => (GetAsyncKeyState(keyCode) & 0x8000) != 0 || IsHookKeyPressed(keyCode)
        };
    }

    private static bool IsSideSpecificModifierPressed(int sideKeyCode, int oppositeSideKeyCode, int genericKeyCode)
    {
        if ((GetAsyncKeyState(sideKeyCode) & 0x8000) != 0)
        {
            return true;
        }

        if ((GetAsyncKeyState(oppositeSideKeyCode) & 0x8000) != 0)
        {
            return false;
        }

        if (IsHookKeyPressed(sideKeyCode))
        {
            return true;
        }

        if (IsHookKeyPressed(oppositeSideKeyCode))
        {
            return false;
        }

        if (IsHookKeyPressed(genericKeyCode))
        {
            return true;
        }

        return (GetAsyncKeyState(genericKeyCode) & 0x8000) != 0;
    }

    private static bool IsHookKeyPressed(int keyCode)
    {
        return GlobalInputHookService.GetPressedKeyboardKeys().Contains(keyCode);
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
