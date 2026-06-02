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
        var normalizedTriggerKeyCode = NormalizeKeyboardKeyCode(triggerKeyCode);
        var keyboardKeys = pressedKeys
            .Where(keyCode => !TriggerMonitorService.IsGamepadKey(keyCode))
            .Select(NormalizeKeyboardKeyCode)
            .Where(IsKeyboardCaptureKey)
            .Distinct()
            .ToList();
        var modifierKeyCodes = GetKeyboardModifierKeyCodes(keyboardKeys);
        var nonModifierKeyCode = keyboardKeys
            .Where(keyCode => !IsModifierKey(keyCode))
            .OrderBy(keyCode => keyCode)
            .FirstOrDefault();
        var keyCode = !IsModifierKey(normalizedTriggerKeyCode) && IsKeyboardCaptureKey(normalizedTriggerKeyCode)
            ? normalizedTriggerKeyCode
            : nonModifierKeyCode != 0
                ? nonModifierKeyCode
                : ResolveModifierKeyForBinding(normalizedTriggerKeyCode, modifierKeyCodes);

        if (keyCode == 0)
        {
            InputDebugLogger.LogMessage($"Ignored unsupported keyboard capture key 0x{triggerKeyCode:X2}.");
        }

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
            normalized.KeyCode = NormalizeKeyboardKeyCode(normalized.KeyCode);
            normalized.KeyboardModifierKeyCodes = IsModifierKey(normalized.KeyCode)
                ? []
                : GetKeyboardModifierKeyCodes(normalized);
            if (normalized.KeyboardModifierKeyCodes.Count > 0)
            {
                normalized.Modifiers = GetKeyboardModifiers(normalized.KeyboardModifierKeyCodes);
            }
        }

        if (normalized.Kind == InputBindingKind.Keyboard)
        {
            normalized.KeyCode = NormalizeKeyboardKeyCode(normalized.KeyCode);
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
        return NormalizeKeyboardKeyCode(keyCode) is CtrlKey or AltKey or ShiftKey;
    }

    public static bool HasNonModifierKeyboardKey(ISet<int> pressedKeys)
    {
        return pressedKeys.Any(keyCode => !TriggerMonitorService.IsGamepadKey(keyCode) && !IsModifierKey(keyCode));
    }

    private static int ResolveModifierKeyForBinding(int keyCode, IReadOnlyCollection<int> pressedModifierKeyCodes)
    {
        var normalizedKeyCode = NormalizeKeyboardKeyCode(keyCode);
        return normalizedKeyCode switch
        {
            CtrlKey when pressedModifierKeyCodes.Contains(CtrlKey) => CtrlKey,
            AltKey when pressedModifierKeyCodes.Contains(AltKey) => AltKey,
            ShiftKey when pressedModifierKeyCodes.Contains(ShiftKey) => ShiftKey,
            _ => normalizedKeyCode
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
        return NormalizeKeyboardKeyCode(keyCode) switch
        {
            CtrlKey => KeyboardModifiers.Ctrl,
            AltKey => KeyboardModifiers.Alt,
            ShiftKey => KeyboardModifiers.Shift,
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
            .Select(NormalizeKeyboardKeyCode)
            .Where(IsModifierKey)
            .Distinct()
            .ToList();

        return new[]
            {
                CtrlKey,
                AltKey,
                ShiftKey
            }
            .Where(modifierKeyCodes.Contains)
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
        var normalizedConfiguredKeyCode = NormalizeKeyboardKeyCode(configuredKeyCode);
        var normalizedPressedKeyCode = NormalizeKeyboardKeyCode(pressedKeyCode);
        if (normalizedConfiguredKeyCode == normalizedPressedKeyCode)
        {
            return true;
        }

        if (IsModifierKey(normalizedConfiguredKeyCode) && IsModifierKey(normalizedPressedKeyCode))
        {
            return ModifierKeyCodesCompatible(normalizedConfiguredKeyCode, normalizedPressedKeyCode);
        }

        return TriggerMonitorService.IsSameHotkey(normalizedConfiguredKeyCode, normalizedPressedKeyCode);
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
        var normalizedConfiguredKeyCode = NormalizeKeyboardKeyCode(configuredKeyCode);
        var normalizedPressedKeyCode = NormalizeKeyboardKeyCode(pressedKeyCode);
        if (normalizedConfiguredKeyCode == normalizedPressedKeyCode)
        {
            return true;
        }

        if (IsCtrlKey(normalizedConfiguredKeyCode) && IsCtrlKey(normalizedPressedKeyCode))
        {
            return true;
        }

        if (IsAltKey(normalizedConfiguredKeyCode) && IsAltKey(normalizedPressedKeyCode))
        {
            return true;
        }

        if (IsShiftKey(normalizedConfiguredKeyCode) && IsShiftKey(normalizedPressedKeyCode))
        {
            return true;
        }

        return false;
    }

    private static bool IsCtrlKey(int keyCode)
    {
        return NormalizeKeyboardKeyCode(keyCode) == CtrlKey;
    }

    private static bool IsAltKey(int keyCode)
    {
        return NormalizeKeyboardKeyCode(keyCode) == AltKey;
    }

    private static bool IsShiftKey(int keyCode)
    {
        return NormalizeKeyboardKeyCode(keyCode) == ShiftKey;
    }

    private static int NormalizeKeyboardKeyCode(int keyCode)
    {
        return keyCode switch
        {
            LeftCtrlKey or RightCtrlKey => CtrlKey,
            LeftAltKey or RightAltKey => AltKey,
            LeftShiftKey or RightShiftKey => ShiftKey,
            _ => keyCode
        };
    }

    private static bool IsKeyboardCaptureKey(int keyCode)
    {
        return IsModifierKey(keyCode) || TriggerMonitorService.IsSupportedKeyboardOrMouseKey(keyCode);
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
