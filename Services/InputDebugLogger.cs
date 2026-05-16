using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using AOUU.Models;

namespace AOUU.Services;

public static class InputDebugLogger
{
    private const int VkControl = 0x11;
    private const int VkShift = 0x10;
    private const int VkMenu = 0x12;
    private const int VkTab = 0x09;
    private const int VkCapital = 0x14;
    private const int VkQ = 0x51;
    private const int VkLShift = 0xA0;
    private const int VkRShift = 0xA1;
    private const int VkLControl = 0xA2;
    private const int VkRControl = 0xA3;
    private const int VkLMenu = 0xA4;
    private const int VkRMenu = 0xA5;

    private static readonly object SyncRoot = new();
    private static string _lastRuntimeLine = string.Empty;
    private static string _lastLiveSummary = "Merged pressed keys: none";
    private static string _lastSeenSummary = "Last seen: none";

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    public static void LogRuntimeKeyboardState(InputBinding binding, bool isPressed, bool wasPressed)
    {
        LogTriggerDecision(
            "runtime keyboard state",
            binding,
            configuredRawValue: $"KeyCode=0x{binding.KeyCode:X2}",
            isPressed,
            wasPressed,
            cooldownBlocked: false,
            edgeBlocked: isPressed && wasPressed,
            willTrigger: isPressed && !wasPressed,
            rejectionReason: isPressed ? "pressed" : "not pressed");
    }

    public static void LogTriggerDecision(
        string triggerName,
        InputBinding configuredBinding,
        string configuredRawValue,
        bool isPressed,
        bool wasPressed,
        bool cooldownBlocked,
        bool edgeBlocked,
        bool willTrigger,
        string rejectionReason)
    {
        try
        {
            var asyncKeys = TriggerMonitorService.GetAsyncPressedKeyboardAndMouseKeys();
            var hookKeys = TriggerMonitorService.GetHookPressedKeyboardKeys();
            var mergedKeys = TriggerMonitorService.GetPressedKeyboardAndMouseKeys();
            UpdateLiveSummaries(mergedKeys);

            var state = string.Join(
                " ",
                new[]
                {
                    FormatKeyState("VK_CONTROL", VkControl, asyncKeys, hookKeys, mergedKeys),
                    FormatKeyState("VK_LCONTROL", VkLControl, asyncKeys, hookKeys, mergedKeys),
                    FormatKeyState("VK_RCONTROL", VkRControl, asyncKeys, hookKeys, mergedKeys),
                    FormatKeyState("VK_SHIFT", VkShift, asyncKeys, hookKeys, mergedKeys),
                    FormatKeyState("VK_LSHIFT", VkLShift, asyncKeys, hookKeys, mergedKeys),
                    FormatKeyState("VK_RSHIFT", VkRShift, asyncKeys, hookKeys, mergedKeys),
                    FormatKeyState("VK_MENU", VkMenu, asyncKeys, hookKeys, mergedKeys),
                    FormatKeyState("VK_LMENU", VkLMenu, asyncKeys, hookKeys, mergedKeys),
                    FormatKeyState("VK_RMENU", VkRMenu, asyncKeys, hookKeys, mergedKeys),
                    FormatKeyState("VK_TAB", VkTab, asyncKeys, hookKeys, mergedKeys),
                    FormatKeyState("VK_CAPITAL", VkCapital, asyncKeys, hookKeys, mergedKeys),
                    FormatKeyState("VK_Q", VkQ, asyncKeys, hookKeys, mergedKeys)
                });

            var runtimeLine =
                $"trigger=\"{triggerName}\" raw=\"{configuredRawValue}\" display=\"{configuredBinding.DisplayName}\" kind={configuredBinding.Kind} " +
                $"required=\"{FormatBindingRequirement(configuredBinding)}\" async=\"{FormatKeySet(asyncKeys)}\" hook=\"{FormatKeySet(hookKeys)}\" merged=\"{FormatKeySet(mergedKeys)}\" " +
                $"leftCtrlPresent={HasKeyFamily(mergedKeys, VkLControl)} genericCtrlPresent={HasKeyFamily(mergedKeys, VkControl)} requiredDown={isPressed} extraKeysBlocking=False cooldownBlocked={cooldownBlocked} edgeBlocked={edgeBlocked} wasPressed={wasPressed} " +
                $"finalDecision={(willTrigger ? "triggered" : "rejected")} reason=\"{rejectionReason}\" {state}";
            lock (SyncRoot)
            {
                if (runtimeLine == _lastRuntimeLine)
                {
                    return;
                }

                _lastRuntimeLine = runtimeLine;
                Directory.CreateDirectory(GetLogDirectory());
                File.AppendAllText(
                    GetLogPath(),
                    $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {runtimeLine}{Environment.NewLine}");
            }
        }
        catch
        {
            // Diagnostic logging must never interfere with hotkey detection.
        }
    }

    public static void LogHookKeyboardEvent(int keyCode, bool isDown, bool changed)
    {
        try
        {
            if (!IsDebugKey(keyCode))
            {
                return;
            }

            lock (SyncRoot)
            {
                Directory.CreateDirectory(GetLogDirectory());
                File.AppendAllText(
                    GetLogPath(),
                    $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} hook {(isDown ? "down" : "up")} key={TriggerMonitorService.GetKeyName(keyCode)} 0x{keyCode:X2} changed={(changed ? 1 : 0)}{Environment.NewLine}");
            }
        }
        catch
        {
            // Diagnostic logging must never interfere with hotkey detection.
        }
    }

    public static void LogMessage(string message)
    {
        try
        {
            lock (SyncRoot)
            {
                Directory.CreateDirectory(GetLogDirectory());
                File.AppendAllText(
                    GetLogPath(),
                    $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {message}{Environment.NewLine}");
            }
        }
        catch
        {
            // Diagnostic logging must never interfere with hotkey detection.
        }
    }

    public static string GetLivePressedKeyStatusText()
    {
        var mergedKeys = TriggerMonitorService.GetPressedKeyboardAndMouseKeys();
        UpdateLiveSummaries(mergedKeys);
        lock (SyncRoot)
        {
            return $"{_lastLiveSummary}. {_lastSeenSummary}. Log: {GetLogPath()}";
        }
    }

    private static string FormatKeyState(
        string name,
        int keyCode,
        ISet<int> asyncKeys,
        ISet<int> hookKeys,
        ISet<int> mergedKeys)
    {
        var asyncDown = asyncKeys.Contains(keyCode) || IsAsyncModifierDownByGenericKey(keyCode);
        return $"{name}[async={(asyncDown ? 1 : 0)},hook={(HasKeyFamily(hookKeys, keyCode) ? 1 : 0)},merged={(HasKeyFamily(mergedKeys, keyCode) ? 1 : 0)}]";
    }

    private static string FormatBindingRequirement(InputBinding binding)
    {
        if (binding.Kind == InputBindingKind.Gamepad)
        {
            var gamepadKeys = binding.GamepadKeyCodes.Count > 0
                ? binding.GamepadKeyCodes
                : [binding.KeyCode];
            return string.Join(" + ", gamepadKeys.Select(TriggerMonitorService.GetKeyName));
        }

        var keyCodes = new List<int>();
        keyCodes.AddRange(binding.KeyboardModifierKeyCodes);
        if (binding.Modifiers.HasFlag(KeyboardModifiers.Ctrl) && !keyCodes.Any(IsCtrlKey))
        {
            keyCodes.Add(VkControl);
        }

        if (binding.Modifiers.HasFlag(KeyboardModifiers.Alt) && !keyCodes.Any(IsAltKey))
        {
            keyCodes.Add(VkMenu);
        }

        if (binding.Modifiers.HasFlag(KeyboardModifiers.Shift) && !keyCodes.Any(IsShiftKey))
        {
            keyCodes.Add(VkShift);
        }

        keyCodes.Add(binding.KeyCode);
        return FormatKeySet(keyCodes.ToHashSet());
    }

    private static string FormatKeySet(ISet<int> keys)
    {
        return keys.Count == 0
            ? "none"
            : string.Join(" + ", keys.OrderBy(keyCode => keyCode).Select(keyCode => $"{TriggerMonitorService.GetKeyName(keyCode)}(0x{keyCode:X2})"));
    }

    private static void UpdateLiveSummaries(ISet<int> mergedKeys)
    {
        var liveSummary = $"Merged pressed keys: {FormatKeySet(mergedKeys)}";
        lock (SyncRoot)
        {
            _lastLiveSummary = liveSummary;
            if (mergedKeys.Count > 0)
            {
                _lastSeenSummary = $"Last seen: {FormatKeySet(mergedKeys)} at {DateTime.Now:HH:mm:ss.fff}";
            }
        }
    }

    private static bool HasKeyFamily(ISet<int> keys, int keyCode)
    {
        return keyCode switch
        {
            VkControl => keys.Contains(VkControl) || keys.Contains(VkLControl) || keys.Contains(VkRControl),
            VkLControl => keys.Contains(VkLControl) || keys.Contains(VkControl),
            VkRControl => keys.Contains(VkRControl) || keys.Contains(VkControl),
            VkShift => keys.Contains(VkShift) || keys.Contains(VkLShift) || keys.Contains(VkRShift),
            VkLShift => keys.Contains(VkLShift) || keys.Contains(VkShift),
            VkRShift => keys.Contains(VkRShift) || keys.Contains(VkShift),
            VkMenu => keys.Contains(VkMenu) || keys.Contains(VkLMenu) || keys.Contains(VkRMenu),
            VkLMenu => keys.Contains(VkLMenu) || keys.Contains(VkMenu),
            VkRMenu => keys.Contains(VkRMenu) || keys.Contains(VkMenu),
            _ => keys.Contains(keyCode)
        };
    }

    private static bool IsCtrlKey(int keyCode)
    {
        return keyCode is VkControl or VkLControl or VkRControl;
    }

    private static bool IsAltKey(int keyCode)
    {
        return keyCode is VkMenu or VkLMenu or VkRMenu;
    }

    private static bool IsShiftKey(int keyCode)
    {
        return keyCode is VkShift or VkLShift or VkRShift;
    }

    private static bool IsAsyncModifierDownByGenericKey(int keyCode)
    {
        return keyCode switch
        {
            VkControl => IsAsyncKeyDown(VkControl) || IsAsyncKeyDown(VkLControl) || IsAsyncKeyDown(VkRControl),
            VkShift => IsAsyncKeyDown(VkShift) || IsAsyncKeyDown(VkLShift) || IsAsyncKeyDown(VkRShift),
            VkMenu => IsAsyncKeyDown(VkMenu) || IsAsyncKeyDown(VkLMenu) || IsAsyncKeyDown(VkRMenu),
            _ => IsAsyncKeyDown(keyCode)
        };
    }

    private static bool IsAsyncKeyDown(int keyCode)
    {
        return (GetAsyncKeyState(keyCode) & 0x8000) != 0;
    }

    private static bool IsDebugKey(int keyCode)
    {
        return keyCode is VkControl
            or VkLControl
            or VkRControl
            or VkShift
            or VkLShift
            or VkRShift
            or VkMenu
            or VkLMenu
            or VkRMenu
            or VkTab
            or VkCapital
            or VkQ;
    }

    private static string GetLogDirectory()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AOUU");
    }

    private static string GetLogPath()
    {
        return Path.Combine(GetLogDirectory(), "input_debug.log");
    }
}
