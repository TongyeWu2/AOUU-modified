using System;
using System.Collections.Generic;
using System.Windows.Forms;
using AOUU.Models;

namespace AOUU.Services;

public sealed class InputCaptureService : IDisposable
{
    private readonly GlobalInputHookService _globalInputHookService = new();
    private readonly System.Windows.Forms.Timer _gamepadPollTimer = new();
    private HashSet<int> _lastPressedGamepadKeys = [];

    public InputCaptureService()
    {
        _globalInputHookService.KeyboardPressed += OnKeyboardPressed;
        _globalInputHookService.MousePressed += OnMousePressed;

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
    }

    public void Dispose()
    {
        _globalInputHookService.KeyboardPressed -= OnKeyboardPressed;
        _globalInputHookService.MousePressed -= OnMousePressed;
        _globalInputHookService.Dispose();

        _gamepadPollTimer.Tick -= GamepadPollTimer_Tick;
        _gamepadPollTimer.Stop();
        _gamepadPollTimer.Dispose();
    }

    private void OnKeyboardPressed(int keyCode)
    {
        InputPressed?.Invoke(keyCode);
        InputBindingPressed?.Invoke(InputBindingService.FromKeyboardEvent(keyCode));
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
