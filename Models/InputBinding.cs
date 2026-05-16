using System;
using System.Collections.Generic;
using System.Linq;

namespace AOUU.Models;

public enum InputBindingKind
{
    Keyboard,
    Gamepad
}

[Flags]
public enum KeyboardModifiers
{
    None = 0,
    Ctrl = 1,
    Alt = 2,
    Shift = 4
}

public sealed class InputBinding
{
    public InputBindingKind Kind { get; set; } = InputBindingKind.Keyboard;

    public int KeyCode { get; set; }

    public List<int> GamepadKeyCodes { get; set; } = [];

    public List<int> KeyboardModifierKeyCodes { get; set; } = [];

    public KeyboardModifiers Modifiers { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    public InputBinding Clone()
    {
        return new InputBinding
        {
            Kind = Kind,
            KeyCode = KeyCode,
            GamepadKeyCodes = GamepadKeyCodes.ToList(),
            KeyboardModifierKeyCodes = KeyboardModifierKeyCodes.ToList(),
            Modifiers = Modifiers,
            DisplayName = DisplayName
        };
    }
}
