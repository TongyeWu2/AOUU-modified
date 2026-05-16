using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using AOUU.Models;
using AOUU.Services;

namespace AOUU.UI;

public sealed class KeyCaptureDialog : Form
{
    private readonly Label _resultLabel;
    private readonly Label _statusLabel;
    private readonly Button _startCaptureButton;
    private readonly Button _retryButton;
    private readonly Button _confirmButton;
    private readonly Button _cancelButton;
    private readonly System.Windows.Forms.Timer _armTimer;
    private readonly System.Windows.Forms.Timer _statusTimer;
    private readonly InputCaptureService _inputCaptureService;

    private const int GamepadComboCaptureWindowMilliseconds = 900;
    private const int KeyboardComboCaptureWindowMilliseconds = 650;

    private CaptureState _state = CaptureState.Idle;
    private readonly HashSet<int> _capturedGamepadKeys = [];
    private InputBinding? _capturedKeyboardBinding;
    private DateTime _gamepadComboCaptureDeadlineUtc;
    private DateTime _keyboardComboCaptureDeadlineUtc;

    public KeyCaptureDialog(string keyPurpose, string currentKeyName, InputCaptureService inputCaptureService)
    {
        _inputCaptureService = inputCaptureService;

        Text = $"设置{keyPurpose}";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        TopMost = true;
        Width = 460;
        Height = 344;

        var helpPanel = new Panel
        {
            Left = 24,
            Top = 24,
            Width = 392,
            Height = 96,
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = Color.FromArgb(247, 240, 220)
        };

        var titleLabel = new Label
        {
            Left = 18,
            Top = 12,
            Width = 356,
            Height = 24,
            Text = "快捷键录制",
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font(SystemFonts.DefaultFont.FontFamily, 10, FontStyle.Bold)
        };

        var helpLabel = new Label
        {
            Left = 18,
            Top = 40,
            Width = 356,
            Height = 38,
            Text = "先点“开始录制”，再按一次目标输入。\r\n支持键盘单键、Ctrl/Alt/Shift 组合键和 XInput 手柄按钮组合。",
            TextAlign = ContentAlignment.MiddleCenter
        };

        helpPanel.Controls.Add(titleLabel);
        helpPanel.Controls.Add(helpLabel);

        var currentLabel = new Label
        {
            Left = 24,
            Top = 136,
            Width = 392,
            Height = 24,
            Text = $"当前绑定：{currentKeyName}",
            TextAlign = ContentAlignment.MiddleCenter
        };

        _resultLabel = new Label
        {
            Left = 24,
            Top = 168,
            Width = 392,
            Height = 28,
            Text = "识别结果：等待识别",
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font(SystemFonts.DefaultFont.FontFamily, 10, FontStyle.Bold)
        };

        _statusLabel = new Label
        {
            Left = 24,
            Top = 202,
            Width = 392,
            Height = 58,
            Text = "点击“开始录制”后开始监听输入。",
            TextAlign = ContentAlignment.MiddleCenter
        };

        _startCaptureButton = new Button
        {
            Left = 24,
            Top = 274,
            Width = 116,
            Text = "开始录制"
        };
        _startCaptureButton.Click += StartCaptureButton_Click;

        _retryButton = new Button
        {
            Left = 152,
            Top = 274,
            Width = 88,
            Text = "重试",
            Enabled = false
        };
        _retryButton.Click += StartCaptureButton_Click;

        _confirmButton = new Button
        {
            Left = 252,
            Top = 274,
            Width = 76,
            Text = "确认",
            Enabled = false
        };
        _confirmButton.Click += ConfirmButton_Click;

        _cancelButton = new Button
        {
            Left = 340,
            Top = 274,
            Width = 76,
            Text = "取消"
        };
        _cancelButton.Click += (_, _) =>
        {
            DialogResult = DialogResult.Cancel;
            Close();
        };

        Controls.Add(helpPanel);
        Controls.Add(currentLabel);
        Controls.Add(_resultLabel);
        Controls.Add(_statusLabel);
        Controls.Add(_startCaptureButton);
        Controls.Add(_retryButton);
        Controls.Add(_confirmButton);
        Controls.Add(_cancelButton);

        _armTimer = new System.Windows.Forms.Timer();
        _armTimer.Interval = 180;
        _armTimer.Tick += ArmTimer_Tick;

        _statusTimer = new System.Windows.Forms.Timer();
        _statusTimer.Interval = 100;
        _statusTimer.Tick += StatusTimer_Tick;

        Shown += KeyCaptureDialog_Shown;
        FormClosed += KeyCaptureDialog_FormClosed;
    }

    public int? CapturedKeyCode { get; private set; }

    public string? CapturedKeyName { get; private set; }

    public InputBinding? CapturedBinding { get; private set; }

    private void KeyCaptureDialog_Shown(object? sender, EventArgs e)
    {
        Activate();
    }

    private void StartCaptureButton_Click(object? sender, EventArgs e)
    {
        CapturedKeyCode = null;
        CapturedKeyName = null;
        CapturedBinding = null;
        _capturedGamepadKeys.Clear();
        _capturedKeyboardBinding = null;
        _resultLabel.Text = "识别结果：等待识别";
        _statusLabel.Text = "正在准备监听，请稍后按下目标键...";
        _confirmButton.Enabled = false;
        _retryButton.Enabled = false;
        _startCaptureButton.Enabled = false;
        _state = CaptureState.Arming;

        _armTimer.Stop();
        _armTimer.Start();
    }

    private void ArmTimer_Tick(object? sender, EventArgs e)
    {
        _armTimer.Stop();

        if (_state != CaptureState.Arming)
        {
            return;
        }

        _state = CaptureState.Armed;
        _statusLabel.Text = "正在监听，按键盘组合键，或按下手柄按钮组合。";
        _statusTimer.Start();
    }

    private void InputCaptureService_InputBindingPressed(InputBinding binding)
    {
        if (!IsHandleCreated)
        {
            return;
        }

        BeginInvoke(new Action(() => TryCaptureInput(binding)));
    }

    private void TryCaptureInput(InputBinding binding)
    {
        if (_state == CaptureState.GamepadComboCapturing)
        {
            ContinueGamepadComboCapture(binding);
            return;
        }

        if (_state == CaptureState.KeyboardComboCapturing)
        {
            ContinueKeyboardComboCapture(binding);
            return;
        }

        if (_state != CaptureState.Armed)
        {
            return;
        }

        var keyCode = binding.KeyCode;

        if (binding.Kind == InputBindingKind.Gamepad)
        {
            BeginGamepadComboCapture(binding);
            return;
        }

        BeginKeyboardComboCapture(binding);
    }

    private void BeginKeyboardComboCapture(InputBinding binding)
    {
        _keyboardComboCaptureDeadlineUtc = DateTime.UtcNow.AddMilliseconds(KeyboardComboCaptureWindowMilliseconds);
        _state = CaptureState.KeyboardComboCapturing;
        CollectKeyboardBinding(binding);
        UpdateKeyboardComboStatus();
    }

    private void ContinueKeyboardComboCapture(InputBinding binding)
    {
        if (binding.Kind != InputBindingKind.Keyboard)
        {
            _statusLabel.Text = "不支持键盘 + 手柄混合组合键，请只使用键盘组合或只使用手柄按钮组合。";
            return;
        }

        CollectKeyboardBinding(binding);
        UpdateKeyboardComboStatus();
    }

    private void CollectKeyboardBinding(InputBinding binding)
    {
        var pressedKeys = TriggerMonitorService.GetPressedKeyboardAndMouseKeys();
        pressedKeys.Add(binding.KeyCode);
        _capturedKeyboardBinding = InputBindingService.FromKeyboardState(pressedKeys, binding.KeyCode);
    }

    private void FinishKeyboardComboCapture()
    {
        if (_state != CaptureState.KeyboardComboCapturing || _capturedKeyboardBinding is null)
        {
            return;
        }

        var binding = _capturedKeyboardBinding.Clone();
        var keyCode = binding.KeyCode;

        if (binding.GamepadKeyCodes.Count > 0)
        {
            _statusLabel.Text = "不支持键盘 + 手柄混合组合键，请只使用键盘组合或只使用手柄按钮组合。";
            _state = CaptureState.Armed;
            return;
        }

        if (keyCode == 0x01)
        {
            _statusLabel.Text = "不允许设置为鼠标左键，请换一个键。";
            _state = CaptureState.Armed;
            return;
        }

        if (keyCode == 0x02)
        {
            _statusLabel.Text = "不建议设置为鼠标右键，请换一个键。";
            _state = CaptureState.Armed;
            return;
        }

        if (!InputBindingService.IsSupported(binding))
        {
            _statusLabel.Text = "这个输入不适合作为快捷键，请换成键盘键、鼠标侧键或手柄按钮。";
            _state = CaptureState.Armed;
            return;
        }

        CapturedKeyCode = keyCode;
        CapturedBinding = binding.Clone();
        CapturedBinding.DisplayName = InputBindingService.GetDisplayName(CapturedBinding);
        CapturedKeyName = CapturedBinding.DisplayName;
        _resultLabel.Text = $"识别结果：{CapturedKeyName}";
        _statusLabel.Text = "识别成功，点击“确认”后才会保存。";
        _statusTimer.Stop();
        _confirmButton.Enabled = true;
        _retryButton.Enabled = true;
        _startCaptureButton.Enabled = true;
        _state = CaptureState.Captured;
    }

    private void UpdateKeyboardComboStatus()
    {
        if (_capturedKeyboardBinding is null)
        {
            _resultLabel.Text = "识别结果：等待识别";
            return;
        }

        _capturedKeyboardBinding.DisplayName = InputBindingService.GetDisplayName(_capturedKeyboardBinding);
        _resultLabel.Text = $"识别结果：{_capturedKeyboardBinding.DisplayName}";
        _statusLabel.Text = "正在收集键盘组合，松开按键或稍等片刻完成录制。";
    }

    private void BeginGamepadComboCapture(InputBinding binding)
    {
        _capturedGamepadKeys.Clear();
        _gamepadComboCaptureDeadlineUtc = DateTime.UtcNow.AddMilliseconds(GamepadComboCaptureWindowMilliseconds);
        _state = CaptureState.GamepadComboCapturing;
        CollectGamepadKeys(binding);
        CollectCurrentlyPressedGamepadKeys();
        UpdateGamepadComboStatus();
    }

    private void ContinueGamepadComboCapture(InputBinding binding)
    {
        if (binding.Kind != InputBindingKind.Gamepad)
        {
            _statusLabel.Text = "不支持键盘 + 手柄混合组合键，请只使用键盘组合或只使用手柄按钮组合。";
            return;
        }

        CollectGamepadKeys(binding);
        CollectCurrentlyPressedGamepadKeys();
        UpdateGamepadComboStatus();
    }

    private void CollectGamepadKeys(InputBinding binding)
    {
        foreach (var keyCode in binding.GamepadKeyCodes.Where(TriggerMonitorService.IsGamepadKey))
        {
            _capturedGamepadKeys.Add(keyCode);
        }

        if (TriggerMonitorService.IsGamepadKey(binding.KeyCode))
        {
            _capturedGamepadKeys.Add(binding.KeyCode);
        }
    }

    private void CollectCurrentlyPressedGamepadKeys()
    {
        foreach (var keyCode in TriggerMonitorService.GetPressedGamepadKeys())
        {
            _capturedGamepadKeys.Add(keyCode);
        }
    }

    private void FinishGamepadComboCapture()
    {
        if (_state != CaptureState.GamepadComboCapturing || _capturedGamepadKeys.Count == 0)
        {
            return;
        }

        var binding = InputBindingService.FromGamepadKeys(_capturedGamepadKeys);
        if (!InputBindingService.IsSupported(binding))
        {
            _statusLabel.Text = "这个手柄输入不适合作为快捷键，请换一个手柄按钮组合。";
            _state = CaptureState.Armed;
            return;
        }

        CapturedKeyCode = binding.KeyCode;
        CapturedBinding = binding.Clone();
        CapturedBinding.DisplayName = InputBindingService.GetDisplayName(CapturedBinding);
        CapturedKeyName = CapturedBinding.DisplayName;
        _resultLabel.Text = $"识别结果：{CapturedKeyName}";
        _statusLabel.Text = "识别成功，点击“确认”后才会保存。";
        _statusTimer.Stop();
        _confirmButton.Enabled = true;
        _retryButton.Enabled = true;
        _startCaptureButton.Enabled = true;
        _state = CaptureState.Captured;
    }

    private void UpdateGamepadComboStatus()
    {
        var binding = InputBindingService.FromGamepadKeys(_capturedGamepadKeys);
        _resultLabel.Text = $"识别结果：{binding.DisplayName}";
        _statusLabel.Text = "正在收集手柄组合，松开所有手柄按钮或稍等片刻完成录制。";
    }

    private void ConfirmButton_Click(object? sender, EventArgs e)
    {
        if (!CapturedKeyCode.HasValue)
        {
            return;
        }

        DialogResult = DialogResult.OK;
        Close();
    }

    private void KeyCaptureDialog_FormClosed(object? sender, FormClosedEventArgs e)
    {
        _armTimer.Dispose();
        _statusTimer.Dispose();
        _inputCaptureService.InputBindingPressed -= InputCaptureService_InputBindingPressed;
    }

    private void StatusTimer_Tick(object? sender, EventArgs e)
    {
        if (_state == CaptureState.KeyboardComboCapturing)
        {
            var pressedKeyboardKeys = TriggerMonitorService.GetPressedKeyboardAndMouseKeys();
            if (pressedKeyboardKeys.Count == 0 || DateTime.UtcNow >= _keyboardComboCaptureDeadlineUtc)
            {
                FinishKeyboardComboCapture();
                return;
            }

            if (TriggerMonitorService.GetPressedGamepadKeys().Count > 0)
            {
                _statusLabel.Text = "不支持键盘 + 手柄混合组合键，请只使用键盘组合或只使用手柄按钮组合。";
                return;
            }

            UpdateKeyboardComboStatus();
            return;
        }

        if (_state == CaptureState.GamepadComboCapturing)
        {
            CollectCurrentlyPressedGamepadKeys();

            var currentlyPressedGamepadKeys = TriggerMonitorService.GetPressedGamepadKeys();
            if (currentlyPressedGamepadKeys.Count == 0 || DateTime.UtcNow >= _gamepadComboCaptureDeadlineUtc)
            {
                FinishGamepadComboCapture();
                return;
            }

            UpdateGamepadComboStatus();
            return;
        }

        if (_state != CaptureState.Armed)
        {
            _statusTimer.Stop();
            return;
        }

        var pressedGamepadKeys = TriggerMonitorService.GetPressedGamepadKeys();
        if (pressedGamepadKeys.Count == 0)
        {
            _statusLabel.Text = "正在监听。当前手柄按钮：无。";
            return;
        }

        var pressedNames = string.Join(" + ", pressedGamepadKeys.Select(TriggerMonitorService.GetKeyName));
        _statusLabel.Text = $"正在监听。当前手柄按钮：{pressedNames}";
    }

    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);

        if (Visible)
        {
            _inputCaptureService.InputBindingPressed += InputCaptureService_InputBindingPressed;
        }
    }

    private enum CaptureState
    {
        Idle,
        Arming,
        Armed,
        KeyboardComboCapturing,
        GamepadComboCapturing,
        Captured
    }
}
