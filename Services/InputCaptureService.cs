using System;
using System.Collections.Generic;
using System.Windows.Forms;
using AOUU.Models;

namespace AOUU.Services;

public sealed class InputCaptureService : IDisposable
{
    private const int TabKey = 0x09;
    private const int CapsLockKey = 0x14;
    private const int CtrlKey = 0x11;
    private const int AltKey = 0x12;
    private const int ShiftKey = 0x10;
    private const int LeftCtrlKey = 0xA2;
    private const int RightCtrlKey = 0xA3;
    private const int LeftAltKey = 0xA4;
    private const int RightAltKey = 0xA5;
    private const int LeftShiftKey = 0xA0;
    private const int RightShiftKey = 0xA1;

    private readonly GlobalInputHookService _globalInputHookService = new();
    private readonly System.Windows.Forms.Timer _keyboardPollTimer = new();
    private readonly System.Windows.Forms.Timer _gamepadPollTimer = new();
    private HashSet<int> _lastPolledKeyboardKeys = [];
    private HashSet<int> _lastPressedGamepadKeys = [];

    public InputCaptureService()
    {
        _globalInputHookService.KeyboardPressed += OnKeyboardPressed;
        _globalInputHookService.MousePressed += OnMousePressed;
        _keyboardPollTimer.Interval = 20;
        _keyboardPollTimer.Tick += KeyboardPollTimer_Tick;

        _gamepadPollTimer.Interval = 25;
        _gamepadPollTimer.Tick += GamepadPollTimer_Tick;
    }

    public event Action<int>? InputPressed;

    public event Action<InputBinding>? InputBindingPressed;

    public void Start()
    {
        if (!_globalInputHookService.IsInstalled)
        {
            _globalInputHookService.Install();
        }

        if (!_gamepadPollTimer.Enabled)
        {
            _gamepadPollTimer.Start();
        }

        if (!_keyboardPollTimer.Enabled)
        {
            _keyboardPollTimer.Start();
        }
    }

    public void Dispose()
    {
        _globalInputHookService.KeyboardPressed -= OnKeyboardPressed;
        _globalInputHookService.MousePressed -= OnMousePressed;
        _globalInputHookService.Dispose();

        _keyboardPollTimer.Tick -= KeyboardPollTimer_Tick;
        _keyboardPollTimer.Stop();
        _keyboardPollTimer.Dispose();

        _gamepadPollTimer.Tick -= GamepadPollTimer_Tick;
        _gamepadPollTimer.Stop();
        _gamepadPollTimer.Dispose();
    }

    private void OnKeyboardPressed(int keyCode)
    {
        InputPressed?.Invoke(keyCode);
        InputBindingPressed?.Invoke(InputBindingService.FromKeyboardEvent(keyCode));
    }

    private void KeyboardPollTimer_Tick(object? sender, EventArgs e)
    {
        var pressedKeys = GetPolledKeyboardHotkeyKeys();
        var hookPressedKeys = GlobalInputHookService.GetPressedKeyboardKeys();
        foreach (var keyCode in pressedKeys)
        {
            if (_lastPolledKeyboardKeys.Contains(keyCode) || IsAlreadySeenByHook(keyCode, hookPressedKeys))
            {
                continue;
            }

            InputPressed?.Invoke(keyCode);
            InputBindingPressed?.Invoke(InputBindingService.FromKeyboardState(
                TriggerMonitorService.GetPressedKeyboardAndMouseKeys(),
                keyCode));
        }

        _lastPolledKeyboardKeys = pressedKeys;
    }

    private static HashSet<int> GetPolledKeyboardHotkeyKeys()
    {
        var pressedKeys = TriggerMonitorService.GetAsyncPressedKeyboardAndMouseKeys();
        var polledKeys = new HashSet<int>();

        AddModifierPollKey(pressedKeys, polledKeys, CtrlKey, LeftCtrlKey, RightCtrlKey);
        AddModifierPollKey(pressedKeys, polledKeys, ShiftKey, LeftShiftKey, RightShiftKey);
        AddModifierPollKey(pressedKeys, polledKeys, AltKey, LeftAltKey, RightAltKey);

        if (pressedKeys.Contains(TabKey))
        {
            polledKeys.Add(TabKey);
        }

        if (pressedKeys.Contains(CapsLockKey))
        {
            polledKeys.Add(CapsLockKey);
        }

        return polledKeys;
    }

    private static bool IsAlreadySeenByHook(int keyCode, ISet<int> hookPressedKeys)
    {
        return keyCode switch
        {
            CtrlKey or LeftCtrlKey or RightCtrlKey => hookPressedKeys.Contains(CtrlKey) ||
                                                      hookPressedKeys.Contains(LeftCtrlKey) ||
                                                      hookPressedKeys.Contains(RightCtrlKey),
            ShiftKey or LeftShiftKey or RightShiftKey => hookPressedKeys.Contains(ShiftKey) ||
                                                         hookPressedKeys.Contains(LeftShiftKey) ||
                                                         hookPressedKeys.Contains(RightShiftKey),
            AltKey or LeftAltKey or RightAltKey => hookPressedKeys.Contains(AltKey) ||
                                                   hookPressedKeys.Contains(LeftAltKey) ||
                                                   hookPressedKeys.Contains(RightAltKey),
            _ => hookPressedKeys.Contains(keyCode)
        };
    }

    private static void AddModifierPollKey(
        ISet<int> pressedKeys,
        ISet<int> destination,
        int genericKey,
        int leftKey,
        int rightKey)
    {
        var hasSideSpecificKey = false;
        if (pressedKeys.Contains(leftKey))
        {
            destination.Add(leftKey);
            hasSideSpecificKey = true;
        }

        if (pressedKeys.Contains(rightKey))
        {
            destination.Add(rightKey);
            hasSideSpecificKey = true;
        }

        if (!hasSideSpecificKey && pressedKeys.Contains(genericKey))
        {
            destination.Add(genericKey);
        }
    }

    private void OnMousePressed(int keyCode)
    {
        InputPressed?.Invoke(keyCode);
        InputBindingPressed?.Invoke(InputBindingService.FromKeyboardEvent(keyCode));
    }

    private void GamepadPollTimer_Tick(object? sender, EventArgs e)
    {
        var pressedKeys = TriggerMonitorService.GetPressedGamepadKeys();

        var hasNewGamepadPress = false;
        foreach (var keyCode in pressedKeys)
        {
            if (!_lastPressedGamepadKeys.Contains(keyCode))
            {
                InputPressed?.Invoke(keyCode);
                hasNewGamepadPress = true;
            }
        }

        if (hasNewGamepadPress)
        {
            InputBindingPressed?.Invoke(InputBindingService.FromGamepadKeys(pressedKeys));
        }

        _lastPressedGamepadKeys = pressedKeys;
    }
}
