using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using NAudio.Wave;
using AOUU.Models;
using AOUU.Services;
using AOUU.UI;

namespace AOUU;

public partial class AOUU : Form
{
    private enum KeyConfigurationTarget
    {
        None,
        Trigger,
        RegionCapture,
        SkillRegionCapture,
        HealthRegionCapture,
        DeathTextRegionCapture
    }

    private enum RegionSettingsMode
    {
        Skill,
        Health
    }

    private readonly TextBox _audioPathBox;
    private readonly Button _browseButton;
    private readonly Button _browseLeftClickAudioButton;
    private readonly Button _browseRightClickAudioButton;
    private readonly Button _setTriggerKeyButton;
    private readonly Button _setRegionCaptureKeyButton;
    private readonly Button _setSkillRegionCaptureKeyButton;
    private readonly Button _setHealthRegionCaptureKeyButton;
    private readonly Button _setDeathTextRegionCaptureKeyButton;
    private readonly Button _setSkillRegionButton;
    private readonly Button _setHealthRegionButton;
    private readonly Button _removeRegionButton;
    private readonly Label _statusLabel;
    private readonly ListBox _regionsListBox;
    private readonly NumericUpDown _watchWindowBox;
    private readonly NumericUpDown _pollIntervalBox;
    private readonly NumericUpDown _healthConsecutiveFramesBox;
    private readonly TrackBar _healthThresholdBar;
    private readonly Label _healthThresholdValueLabel;
    private readonly TrackBar _audioVolumeBar;
    private readonly Label _audioVolumeValueLabel;
    private readonly ComboBox _audioOutputDeviceBox;
    private readonly Button _refreshAudioDevicesButton;
    private readonly CheckBox _useSoundpadOutputBox;
    private readonly TextBox _soundpadPathBox;
    private readonly Button _browseSoundpadButton;
    private readonly NumericUpDown _soundpadSoundIndexBox;
    private readonly CheckBox _textTriggerEnabledBox;
    private readonly TextBox _textTriggerTextBox;
    private readonly TextBox _textTriggerMusicPathBox;
    private readonly Button _browseTextTriggerMusicButton;
    private readonly NumericUpDown _textTriggerCooldownBox;
    private readonly CheckBox _deathTriggerEnabledBox;
    private readonly Button _setDeathHealthRegionButton;
    private readonly Button _setDeathTextRegionButton;
    private readonly TextBox _deathTemplatePathBox;
    private readonly Button _browseDeathTemplateButton;
    private readonly TextBox _deathMusicPathBox;
    private readonly Button _browseDeathMusicButton;
    private readonly NumericUpDown _deathSimilarityThresholdBox;
    private readonly NumericUpDown _deathHealthZeroThresholdBox;
    private readonly NumericUpDown _deathScanIntervalBox;
    private readonly NumericUpDown _deathCooldownBox;
    private readonly TriggerMonitorService _triggerMonitorService;
    private readonly TriggerMonitorService _regionCaptureMonitorService;
    private readonly TriggerMonitorService _skillRegionCaptureMonitorService;
    private readonly TriggerMonitorService _healthRegionCaptureMonitorService;
    private readonly TriggerMonitorService _deathTextRegionCaptureMonitorService;
    private readonly InputCaptureService _inputCaptureService;
    private readonly ConfigService _configService;
    private readonly ScreenCaptureService _screenCaptureService;
    private readonly TemplateMatcher _templateMatcher;
    private readonly CircleLocatorService _circleLocatorService;
    private readonly HealthBarAnalyzerService _healthBarAnalyzerService;
    private readonly RegionChangeDetector _regionChangeDetector;
    private readonly HealthBaselineService _healthBaselineService;
    private readonly ScreenTextRecognizer _screenTextRecognizer;
    private readonly DeathTriggerService _deathTriggerService;
    private readonly System.Windows.Forms.Timer _textTriggerTimer;
    private readonly System.Windows.Forms.Timer _deathTriggerTimer;
    private readonly Dictionary<string, DateTime> _lastTextTriggerUtc = [];
    private readonly CancellationTokenSource _shutdownCts = new();
    private readonly object _audioLock = new();
    private static readonly string[] SupportedAudioExtensions = [".mp3", ".wav"];

    private readonly object _clickSoundLock = new();
    private readonly Random _audioRandom = new();
    private readonly List<(IWavePlayer OutputDevice, AudioFileReader AudioFile)> _clickSoundPlayers = [];

    private AppConfig _config;
    private IWavePlayer? _outputDevice;
    private AudioFileReader? _audioFile;
    private bool _isPlaying;
    private bool _isConfiguringKey;
    private bool _isRecognitionRunning;
    private bool _isRegionCaptureRunning;
    private bool _isApplyingConfigToUi;
    private bool _isTextScanRunning;
    private bool _isDeathScanRunning;
    private bool _deathConditionActive;
    private DateTime _lastDeathTriggerUtc = DateTime.MinValue;
    private DateTime _lastLeftClickSoundUtc = DateTime.MinValue;
    private DateTime _lastRightClickSoundUtc = DateTime.MinValue;
    private KeyConfigurationTarget _preparedKeyConfigurationTarget;

    public AOUU()
    {
        InitializeComponent();

        Text = "┗|｀O′|┛ 嗷~~";
        Width = 920;
        Height = 990;
        StartPosition = FormStartPosition.CenterScreen;
        MaximizeBox = false;
        Text = "┗|｀O′|┛ 嗷~~";

        _configService = new ConfigService(AppDomain.CurrentDomain.BaseDirectory);
        _inputCaptureService = new InputCaptureService();
        _inputCaptureService.InputPressed += InputCaptureService_InputPressed;
        _inputCaptureService.Start();
        _screenCaptureService = new ScreenCaptureService();
        _screenTextRecognizer = new ScreenTextRecognizer();
        _templateMatcher = new TemplateMatcher();
        _circleLocatorService = new CircleLocatorService();
        _healthBarAnalyzerService = new HealthBarAnalyzerService();
        _regionChangeDetector = new RegionChangeDetector();
        _healthBaselineService = new HealthBaselineService(
            _screenCaptureService,
            _templateMatcher,
            _healthBarAnalyzerService,
            _circleLocatorService);
        _deathTriggerService = new DeathTriggerService(
            _screenCaptureService,
            _templateMatcher,
            _healthBarAnalyzerService,
            _circleLocatorService);

        _triggerMonitorService = new TriggerMonitorService();
        _triggerMonitorService.Triggered += TriggerMonitorService_Triggered;

        _regionCaptureMonitorService = new TriggerMonitorService();
        _regionCaptureMonitorService.Triggered += RegionCaptureMonitorService_Triggered;

        _skillRegionCaptureMonitorService = new TriggerMonitorService();
        _skillRegionCaptureMonitorService.Triggered += SkillRegionCaptureMonitorService_Triggered;

        _healthRegionCaptureMonitorService = new TriggerMonitorService();
        _healthRegionCaptureMonitorService.Triggered += HealthRegionCaptureMonitorService_Triggered;

        _deathTextRegionCaptureMonitorService = new TriggerMonitorService();
        _deathTextRegionCaptureMonitorService.Triggered += DeathTextRegionCaptureMonitorService_Triggered;

        _textTriggerTimer = new System.Windows.Forms.Timer
        {
            Interval = 1500
        };
        _textTriggerTimer.Tick += TextTriggerTimer_Tick;

        _deathTriggerTimer = new System.Windows.Forms.Timer
        {
            Interval = 500
        };
        _deathTriggerTimer.Tick += DeathTriggerTimer_Tick;

        _audioPathBox = new TextBox
        {
            Left = 24,
            Top = 40,
            Width = 560,
            ReadOnly = true,
            TabStop = false
        };

        _browseButton = new Button
        {
            Left = 600,
            Top = 36,
            Width = 120,
            Text = "配置音频"
        };
        _browseButton.Click += BrowseButton_Click;

        _browseLeftClickAudioButton = new Button
        {
            Left = 736,
            Top = 36,
            Width = 70,
            Text = "左键音效"
        };
        _browseLeftClickAudioButton.Click += BrowseLeftClickAudioButton_Click;

        _browseRightClickAudioButton = new Button
        {
            Left = 814,
            Top = 36,
            Width = 70,
            Text = "右键音效"
        };
        _browseRightClickAudioButton.Click += BrowseRightClickAudioButton_Click;

        _setTriggerKeyButton = new Button
        {
            Left = 24,
            Top = 90,
            Width = 180,
            Text = "配置触发键"
        };
        _setTriggerKeyButton.Click += SetTriggerKeyButton_Click;

        _setRegionCaptureKeyButton = new Button
        {
            Left = 24,
            Top = 126,
            Width = 180,
            Text = "配置截图键"
        };
        _setRegionCaptureKeyButton.Click += SetRegionCaptureKeyButton_Click;

        _setSkillRegionCaptureKeyButton = new Button
        {
            Left = 220,
            Top = 90,
            Width = 180
        };
        _setSkillRegionCaptureKeyButton.Click += (_, _) => ConfigureHotkey(KeyConfigurationTarget.SkillRegionCapture, "技能区域快捷键");

        _setHealthRegionCaptureKeyButton = new Button
        {
            Left = 416,
            Top = 90,
            Width = 180
        };
        _setHealthRegionCaptureKeyButton.Click += (_, _) => ConfigureHotkey(KeyConfigurationTarget.HealthRegionCapture, "血条区域快捷键");

        _setDeathTextRegionCaptureKeyButton = new Button
        {
            Left = 612,
            Top = 90,
            Width = 180
        };
        _setDeathTextRegionCaptureKeyButton.Click += (_, _) => ConfigureHotkey(KeyConfigurationTarget.DeathTextRegionCapture, "死亡区域快捷键");

        _watchWindowBox = new NumericUpDown
        {
            Left = 250,
            Top = 126,
            Width = 120,
            Minimum = 200,
            Maximum = 10000,
            Increment = 100
        };
        _watchWindowBox.ValueChanged += TimingBox_ValueChanged;

        _pollIntervalBox = new NumericUpDown
        {
            Left = 470,
            Top = 126,
            Width = 120,
            Minimum = 5,
            Maximum = 1000,
            Increment = 5
        };
        _pollIntervalBox.ValueChanged += TimingBox_ValueChanged;

        _healthConsecutiveFramesBox = new NumericUpDown
        {
            Left = 690,
            Top = 126,
            Width = 120,
            Minimum = 1,
            Maximum = 10,
            Increment = 1
        };
        _healthConsecutiveFramesBox.ValueChanged += HealthConsecutiveFramesBox_ValueChanged;

        _healthThresholdBar = new TrackBar
        {
            Left = 24,
            Top = 224,
            Width = 300,
            Minimum = 1,
            Maximum = 20,
            TickFrequency = 1,
            SmallChange = 1,
            LargeChange = 1
        };
        _healthThresholdBar.Scroll += HealthThresholdBar_Scroll;

        _healthThresholdValueLabel = new Label
        {
            Left = 330,
            Top = 224,
            Width = 100,
            Height = 28
        };

        _audioVolumeBar = new TrackBar
        {
            Left = 470,
            Top = 224,
            Width = 300,
            Minimum = 0,
            Maximum = 100,
            TickFrequency = 10,
            SmallChange = 5,
            LargeChange = 10
        };
        _audioVolumeBar.ValueChanged += AudioVolumeBar_ValueChanged;

        _audioVolumeValueLabel = new Label
        {
            Left = 776,
            Top = 224,
            Width = 108,
            Height = 28
        };

        _audioOutputDeviceBox = new ComboBox
        {
            Left = 470,
            Top = 270,
            Width = 300,
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        _audioOutputDeviceBox.SelectedIndexChanged += AudioOutputDeviceBox_SelectedIndexChanged;

        _refreshAudioDevicesButton = new Button
        {
            Left = 776,
            Top = 268,
            Width = 108,
            Text = "刷新设备"
        };
        _refreshAudioDevicesButton.Click += (_, _) =>
        {
            RefreshAudioOutputDevices();
            UpdateStatus();
        };

        _useSoundpadOutputBox = new CheckBox
        {
            Left = 24,
            Top = 310,
            Width = 180,
            Text = "使用 Soundpad"
        };
        _useSoundpadOutputBox.CheckedChanged += SoundpadSettings_Changed;

        _soundpadPathBox = new TextBox
        {
            Left = 220,
            Top = 308,
            Width = 360,
            ReadOnly = true,
            TabStop = false
        };

        _browseSoundpadButton = new Button
        {
            Left = 596,
            Top = 306,
            Width = 120,
            Text = "选择 Soundpad"
        };
        _browseSoundpadButton.Click += BrowseSoundpadButton_Click;

        _soundpadSoundIndexBox = new NumericUpDown
        {
            Left = 810,
            Top = 308,
            Width = 74,
            Minimum = 1,
            Maximum = 9999,
            Increment = 1
        };
        _soundpadSoundIndexBox.ValueChanged += SoundpadSettings_Changed;

        _textTriggerEnabledBox = new CheckBox
        {
            Left = 24,
            Top = 348,
            Width = 170,
            Text = "屏幕文字触发"
        };
        _textTriggerEnabledBox.CheckedChanged += TextTriggerSettings_Changed;

        _textTriggerTextBox = new TextBox
        {
            Left = 196,
            Top = 346,
            Width = 130
        };
        _textTriggerTextBox.TextChanged += TextTriggerSettings_Changed;

        _textTriggerMusicPathBox = new TextBox
        {
            Left = 336,
            Top = 346,
            Width = 300,
            ReadOnly = true,
            TabStop = false
        };

        _browseTextTriggerMusicButton = new Button
        {
            Left = 646,
            Top = 344,
            Width = 90,
            Text = "死亡音乐"
        };
        _browseTextTriggerMusicButton.Click += BrowseTextTriggerMusicButton_Click;

        _textTriggerCooldownBox = new NumericUpDown
        {
            Left = 810,
            Top = 346,
            Width = 74,
            Minimum = 1,
            Maximum = 3600,
            Increment = 1
        };
        _textTriggerCooldownBox.ValueChanged += TextTriggerSettings_Changed;

        _deathTriggerEnabledBox = new CheckBox
        {
            Left = 24,
            Top = 386,
            Width = 170,
            Text = "死亡自动触发"
        };
        _deathTriggerEnabledBox.CheckedChanged += DeathTriggerSettings_Changed;

        _setDeathHealthRegionButton = new Button
        {
            Left = 196,
            Top = 382,
            Width = 130,
            Text = "死亡血条区域",
            Visible = false,
            Enabled = false
        };
        _setDeathHealthRegionButton.Click += (_, _) => ConfigureDeathRegion(isHealthRegion: true);

        _setDeathTextRegionButton = new Button
        {
            Left = 196,
            Top = 382,
            Width = 130,
            Text = "YOU DIED 区域"
        };
        _setDeathTextRegionButton.Click += (_, _) => ConfigureDeathRegion(isHealthRegion: false);

        _deathTemplatePathBox = new TextBox
        {
            Left = 336,
            Top = 384,
            Width = 390,
            ReadOnly = true,
            TabStop = false
        };

        _browseDeathTemplateButton = new Button
        {
            Left = 736,
            Top = 382,
            Width = 148,
            Text = "选择死亡模板"
        };
        _browseDeathTemplateButton.Click += BrowseDeathTemplateButton_Click;

        _deathMusicPathBox = new TextBox
        {
            Left = 24,
            Top = 424,
            Width = 442,
            ReadOnly = true,
            TabStop = false
        };

        _browseDeathMusicButton = new Button
        {
            Left = 476,
            Top = 422,
            Width = 130,
            Text = "选择死亡音乐"
        };
        _browseDeathMusicButton.Click += BrowseDeathMusicButton_Click;

        _deathSimilarityThresholdBox = new NumericUpDown
        {
            Left = 700,
            Top = 424,
            Width = 70,
            Minimum = 0.10M,
            Maximum = 1.00M,
            Increment = 0.05M,
            DecimalPlaces = 2
        };
        _deathSimilarityThresholdBox.ValueChanged += DeathTriggerSettings_Changed;

        _deathHealthZeroThresholdBox = new NumericUpDown
        {
            Left = 814,
            Top = 424,
            Width = 70,
            Minimum = 0,
            Maximum = 200,
            Increment = 1,
            Visible = false,
            Enabled = false
        };
        _deathHealthZeroThresholdBox.ValueChanged += DeathTriggerSettings_Changed;

        _deathScanIntervalBox = new NumericUpDown
        {
            Left = 700,
            Top = 464,
            Width = 70,
            Minimum = 100,
            Maximum = 10000,
            Increment = 100
        };
        _deathScanIntervalBox.ValueChanged += DeathTriggerSettings_Changed;

        _deathCooldownBox = new NumericUpDown
        {
            Left = 814,
            Top = 464,
            Width = 70,
            Minimum = 1,
            Maximum = 3600,
            Increment = 1
        };
        _deathCooldownBox.ValueChanged += DeathTriggerSettings_Changed;

        _setSkillRegionButton = new Button
        {
            Left = 24,
            Top = 180,
            Width = 180,
            Text = "重框技能区域"
        };
        _setSkillRegionButton.Click += (_, _) => ConfigureSingleRegionSession(RegionSettingsMode.Skill, restoreWindowAfter: true);

        _setHealthRegionButton = new Button
        {
            Left = 220,
            Top = 180,
            Width = 180,
            Text = "重框血条区域"
        };
        _setHealthRegionButton.Click += (_, _) => ConfigureSingleRegionSession(RegionSettingsMode.Health, restoreWindowAfter: true);

        _removeRegionButton = new Button
        {
            Left = 416,
            Top = 180,
            Width = 180,
            Text = "删除选中区域"
        };
        _removeRegionButton.Click += RemoveRegionButton_Click;

        _regionsListBox = new ListBox
        {
            Left = 24,
            Top = 540,
            Width = 860,
            Height = 190,
            HorizontalScrollbar = true
        };

        _statusLabel = new Label
        {
            Left = 24,
            Top = 870,
            Width = 860,
            Height = 60
        };

        Controls.Add(new Label
        {
            Left = 24,
            Top = 16,
            Width = 120,
            Text = "音频文件"
        });
        Controls.Add(_audioPathBox);
        Controls.Add(_browseButton);
        Controls.Add(_browseLeftClickAudioButton);
        Controls.Add(_browseRightClickAudioButton);
        Controls.Add(_setTriggerKeyButton);
        Controls.Add(_setRegionCaptureKeyButton);
        Controls.Add(_setSkillRegionCaptureKeyButton);
        Controls.Add(_setHealthRegionCaptureKeyButton);
        Controls.Add(_setDeathTextRegionCaptureKeyButton);
        Controls.Add(new Label
        {
            Left = 250,
            Top = 106,
            Width = 180,
            Text = "监听时长 (ms)"
        });
        Controls.Add(_watchWindowBox);
        Controls.Add(new Label
        {
            Left = 470,
            Top = 106,
            Width = 180,
            Text = "截图间隔 (ms)"
        });
        Controls.Add(_pollIntervalBox);
        Controls.Add(new Label
        {
            Left = 690,
            Top = 106,
            Width = 140,
            Text = "连续命中帧"
        });
        Controls.Add(_healthConsecutiveFramesBox);
        Controls.Add(_setSkillRegionButton);
        Controls.Add(_setHealthRegionButton);
        Controls.Add(_removeRegionButton);
        Controls.Add(new Label
        {
            Left = 24,
            Top = 204,
            Width = 180,
            Text = "血条变长阈值"
        });
        Controls.Add(_healthThresholdBar);
        Controls.Add(_healthThresholdValueLabel);
        Controls.Add(new Label
        {
            Left = 470,
            Top = 204,
            Width = 120,
            Text = "音量"
        });
        Controls.Add(_audioVolumeBar);
        Controls.Add(_audioVolumeValueLabel);
        Controls.Add(new Label
        {
            Left = 470,
            Top = 250,
            Width = 180,
            Text = "音频输出设备"
        });
        Controls.Add(_audioOutputDeviceBox);
        Controls.Add(_refreshAudioDevicesButton);
        Controls.Add(_useSoundpadOutputBox);
        Controls.Add(_soundpadPathBox);
        Controls.Add(_browseSoundpadButton);
        Controls.Add(new Label
        {
            Left = 730,
            Top = 312,
            Width = 74,
            Text = "声音序号"
        });
        Controls.Add(_soundpadSoundIndexBox);
        Controls.Add(_textTriggerEnabledBox);
        Controls.Add(_textTriggerTextBox);
        Controls.Add(_textTriggerMusicPathBox);
        Controls.Add(_browseTextTriggerMusicButton);
        Controls.Add(new Label
        {
            Left = 746,
            Top = 350,
            Width = 58,
            Text = "冷却秒"
        });
        Controls.Add(_textTriggerCooldownBox);
        Controls.Add(_deathTriggerEnabledBox);
        Controls.Add(_setDeathHealthRegionButton);
        Controls.Add(_setDeathTextRegionButton);
        Controls.Add(_deathTemplatePathBox);
        Controls.Add(_browseDeathTemplateButton);
        Controls.Add(_deathMusicPathBox);
        Controls.Add(_browseDeathMusicButton);
        Controls.Add(new Label
        {
            Left = 626,
            Top = 428,
            Width = 68,
            Text = "相似度"
        });
        Controls.Add(_deathSimilarityThresholdBox);
        Controls.Add(new Label
        {
            Left = 778,
            Top = 428,
            Width = 34,
            Text = "空血",
            Visible = false
        });
        Controls.Add(_deathHealthZeroThresholdBox);
        Controls.Add(new Label
        {
            Left = 626,
            Top = 468,
            Width = 68,
            Text = "扫描ms"
        });
        Controls.Add(_deathScanIntervalBox);
        Controls.Add(new Label
        {
            Left = 778,
            Top = 468,
            Width = 34,
            Text = "冷却"
        });
        Controls.Add(_deathCooldownBox);
        Controls.Add(new Label
        {
            Left = 24,
            Top = 518,
            Width = 260,
            Text = "当前检测区域"
        });
        Controls.Add(_regionsListBox);
        Controls.Add(new Label
        {
            Left = 24,
            Top = 840,
            Width = 860,
            Text = "点击配置键位后会弹出识别框，只有确认后的结果才会保存，支持键盘、鼠标侧键和手柄按钮，鼠标左键不会被接受。"
        });
        Controls.Add(_statusLabel);

        _config = _configService.Load();

        RefreshAudioOutputDevices();
        ApplyConfigToUi();
        LoadAudio();
        RefreshRegionList();
        UpdateStatus();

        FormClosing += AOUU_FormClosing;
        FormClosed += AOUU_FormClosed;

        _triggerMonitorService.Enabled = true;
        _regionCaptureMonitorService.Enabled = true;
        SetRegionCaptureHotkeyMonitorsEnabled(true);
        _textTriggerTimer.Start();
        SyncDeathTriggerTimer();
    }

    private void BrowseButton_Click(object? sender, EventArgs e)
    {
        if (TryBrowseAudioFile(out var path))
        {
            _config.AudioPath = path;
            SaveConfig();
            LoadAudio();
            UpdateAudioDisplay();
            UpdateStatus();
        }
    }

    private void BrowseLeftClickAudioButton_Click(object? sender, EventArgs e)
    {
        if (TryBrowseAudioFile(out var path))
        {
            _config.LeftClickAudioPath = path;
            SaveConfig();
            UpdateStatus();
        }
    }

    private void BrowseRightClickAudioButton_Click(object? sender, EventArgs e)
    {
        if (TryBrowseAudioFile(out var path))
        {
            _config.RightClickAudioPath = path;
            SaveConfig();
            UpdateStatus();
        }
    }

    private bool TryBrowseAudioFile(out string path)
    {
        var choice = MessageBox.Show(
            this,
            "选择文件夹请点“是”，选择单个音频文件请点“否”。",
            "选择音频来源",
            MessageBoxButtons.YesNoCancel,
            MessageBoxIcon.Question);

        if (choice == DialogResult.Yes)
        {
            using var folderDialog = new FolderBrowserDialog
            {
                Description = "选择包含音频文件的文件夹",
                UseDescriptionForTitle = true
            };

            if (folderDialog.ShowDialog(this) == DialogResult.OK)
            {
                path = folderDialog.SelectedPath;
                return true;
            }
        }
        else if (choice == DialogResult.No)
        {
            using var fileDialog = new OpenFileDialog();
            fileDialog.Filter = "音频文件|*.mp3;*.wav";

            if (fileDialog.ShowDialog(this) == DialogResult.OK)
            {
                path = fileDialog.FileName;
                return true;
            }
        }

        path = string.Empty;
        return false;
    }

    private void SetTriggerKeyButton_Click(object? sender, EventArgs e)
    {
        ConfigureHotkey(KeyConfigurationTarget.Trigger, "技能触发键");
    }

    private void SetRegionCaptureKeyButton_Click(object? sender, EventArgs e)
    {
        ConfigureHotkey(KeyConfigurationTarget.RegionCapture, "截图键");
    }

    private void ConfigureHotkey(KeyConfigurationTarget target, string title)
    {
        if (_isRecognitionRunning || _isRegionCaptureRunning)
        {
            SetStatus("识别或框选进行中，暂时不能修改快捷键。");
            return;
        }

        if (_preparedKeyConfigurationTarget != target)
        {
            PrepareKeyConfiguration(target);
        }

        var currentKeyName = GetConfiguredHotkeyName(target);

        using var dialog = new KeyCaptureDialog(title, currentKeyName, _inputCaptureService);
        var result = dialog.ShowDialog(this);
        var refreshStatus = true;

        if (result == DialogResult.OK && dialog.CapturedKeyCode.HasValue)
        {
            if (ApplyCapturedHotkey(
                target,
                dialog.CapturedKeyCode.Value,
                dialog.CapturedKeyName ?? TriggerMonitorService.GetKeyName(dialog.CapturedKeyCode.Value)))
            {
                SetStatus($"{title}已设置为：{dialog.CapturedKeyName}");
            }
            else
            {
                refreshStatus = false;
            }
        }
        else
        {
            SetStatus($"已取消设置{title}。");
        }

        _preparedKeyConfigurationTarget = KeyConfigurationTarget.None;
        _isConfiguringKey = false;
        _triggerMonitorService.Enabled = true;
        _regionCaptureMonitorService.Enabled = true;
        SetRegionCaptureHotkeyMonitorsEnabled(true);
        if (refreshStatus)
        {
            UpdateStatus();
        }
    }

    private void PrepareKeyConfiguration(KeyConfigurationTarget target)
    {
        _isConfiguringKey = true;
        _preparedKeyConfigurationTarget = target;
        _triggerMonitorService.Enabled = false;
        _regionCaptureMonitorService.Enabled = false;
        SetRegionCaptureHotkeyMonitorsEnabled(false);
        SetStatus("按键识别框已打开。先松开鼠标，再在识别框内按下新的键位。");
    }

    private bool ApplyCapturedHotkey(KeyConfigurationTarget target, int keyCode, string keyName)
    {
        if (TryGetHotkeyConflict(target, keyCode, out var conflictMessage))
        {
            SetStatus(conflictMessage);
            return false;
        }

        if (target == KeyConfigurationTarget.Trigger)
        {
            _config.TriggerKey = keyCode;
            _config.TriggerKeyName = keyName;
            _triggerMonitorService.TriggerKey = keyCode;
        }
        else if (target == KeyConfigurationTarget.RegionCapture)
        {
            _config.RegionCaptureKey = keyCode;
            _config.RegionCaptureKeyName = keyName;
            _regionCaptureMonitorService.TriggerKey = keyCode;
        }
        else if (target == KeyConfigurationTarget.SkillRegionCapture)
        {
            _config.RegionCaptureHotkeys.SkillRegionKey = keyCode;
            _config.RegionCaptureHotkeys.SkillRegionKeyName = keyName;
            _skillRegionCaptureMonitorService.TriggerKey = keyCode;
        }
        else if (target == KeyConfigurationTarget.HealthRegionCapture)
        {
            _config.RegionCaptureHotkeys.HealthRegionKey = keyCode;
            _config.RegionCaptureHotkeys.HealthRegionKeyName = keyName;
            _healthRegionCaptureMonitorService.TriggerKey = keyCode;
        }
        else if (target == KeyConfigurationTarget.DeathTextRegionCapture)
        {
            _config.RegionCaptureHotkeys.DeathTextRegionKey = keyCode;
            _config.RegionCaptureHotkeys.DeathTextRegionKeyName = keyName;
            _deathTextRegionCaptureMonitorService.TriggerKey = keyCode;
        }

        SaveConfig();
        UpdateRegionCaptureHotkeyButtonText();
        return true;
    }

    private string GetConfiguredHotkeyName(KeyConfigurationTarget target)
    {
        return target switch
        {
            KeyConfigurationTarget.Trigger => _config.TriggerKeyName,
            KeyConfigurationTarget.RegionCapture => _config.RegionCaptureKeyName,
            KeyConfigurationTarget.SkillRegionCapture => _config.RegionCaptureHotkeys.SkillRegionKeyName,
            KeyConfigurationTarget.HealthRegionCapture => _config.RegionCaptureHotkeys.HealthRegionKeyName,
            KeyConfigurationTarget.DeathTextRegionCapture => _config.RegionCaptureHotkeys.DeathTextRegionKeyName,
            _ => string.Empty
        };
    }

    private bool TryGetHotkeyConflict(KeyConfigurationTarget target, int keyCode, out string message)
    {
        foreach (var (otherTarget, label, configuredKey) in EnumerateConfiguredHotkeys())
        {
            if (otherTarget == target || configuredKey != keyCode)
            {
                continue;
            }

            message = $"快捷键冲突：{TriggerMonitorService.GetKeyName(keyCode)} 已用于{label}，请换一个键。";
            return true;
        }

        message = string.Empty;
        return false;
    }

    private IEnumerable<(KeyConfigurationTarget Target, string Label, int KeyCode)> EnumerateConfiguredHotkeys()
    {
        yield return (KeyConfigurationTarget.Trigger, "技能触发键", _config.TriggerKey);
        yield return (KeyConfigurationTarget.RegionCapture, "截图键", _config.RegionCaptureKey);
        yield return (KeyConfigurationTarget.SkillRegionCapture, "技能区域快捷键", _config.RegionCaptureHotkeys.SkillRegionKey);
        yield return (KeyConfigurationTarget.HealthRegionCapture, "血条区域快捷键", _config.RegionCaptureHotkeys.HealthRegionKey);
        yield return (KeyConfigurationTarget.DeathTextRegionCapture, "死亡区域快捷键", _config.RegionCaptureHotkeys.DeathTextRegionKey);
    }

    private void TimingBox_ValueChanged(object? sender, EventArgs e)
    {
        if (_isApplyingConfigToUi)
        {
            return;
        }

        _config.WatchWindowMs = (int)_watchWindowBox.Value;
        _config.PollIntervalMs = (int)_pollIntervalBox.Value;

        SaveConfig();
        UpdateStatus();
    }

    private void HealthConsecutiveFramesBox_ValueChanged(object? sender, EventArgs e)
    {
        if (_isApplyingConfigToUi)
        {
            return;
        }

        _config.HealthConsecutiveFramesRequired = (int)_healthConsecutiveFramesBox.Value;
        SyncHealthConsecutiveFramesToRegion();
        SaveConfig();
        RefreshRegionList();
        UpdateStatus();
    }

    private void HealthThresholdBar_Scroll(object? sender, EventArgs e)
    {
        _config.HealthGrowthPixelThreshold = _healthThresholdBar.Value;
        UpdateThresholdDisplay();
        SaveConfig();
        UpdateStatus();
    }

    private void AudioVolumeBar_ValueChanged(object? sender, EventArgs e)
    {
        if (_isApplyingConfigToUi)
        {
            return;
        }

        _config.AudioVolume = _audioVolumeBar.Value / 100f;
        UpdateVolumeDisplay();
        ApplyAudioVolume();
        SaveConfig();
        UpdateStatus();
    }

    private void AudioOutputDeviceBox_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_isApplyingConfigToUi || _audioOutputDeviceBox.SelectedItem is not AudioOutputDeviceItem selectedDevice)
        {
            return;
        }

        _config.AudioOutputDeviceName = selectedDevice.ProductName;
        SaveConfig();
        UpdateStatus();
    }

    private void SoundpadSettings_Changed(object? sender, EventArgs e)
    {
        if (_isApplyingConfigToUi)
        {
            return;
        }

        _config.UseSoundpadOutput = _useSoundpadOutputBox.Checked;
        _config.SoundpadSoundIndex = (int)_soundpadSoundIndexBox.Value;
        SaveConfig();
        UpdateSoundpadDisplay();
        UpdateStatus();
    }

    private void BrowseSoundpadButton_Click(object? sender, EventArgs e)
    {
        using var fileDialog = new OpenFileDialog
        {
            Filter = "Soundpad|Soundpad.exe|可执行文件|*.exe",
            FileName = "Soundpad.exe"
        };

        var initialPath = ResolveSoundpadExecutablePath(_config.SoundpadExecutablePath);
        if (!string.IsNullOrWhiteSpace(initialPath))
        {
            fileDialog.InitialDirectory = Path.GetDirectoryName(initialPath);
        }

        if (fileDialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        _config.SoundpadExecutablePath = fileDialog.FileName;
        SaveConfig();
        UpdateSoundpadDisplay();
        UpdateStatus();
    }

    private void TextTriggerSettings_Changed(object? sender, EventArgs e)
    {
        if (_isApplyingConfigToUi)
        {
            return;
        }

        var trigger = EnsureDefaultTextTrigger();
        trigger.Enabled = _textTriggerEnabledBox.Checked;
        trigger.Text = string.IsNullOrWhiteSpace(_textTriggerTextBox.Text)
            ? "YOU DIED"
            : _textTriggerTextBox.Text.Trim();
        trigger.CooldownSeconds = (int)_textTriggerCooldownBox.Value;

        SaveConfig();
        UpdateTextTriggerDisplay();
        UpdateStatus();
    }

    private void BrowseTextTriggerMusicButton_Click(object? sender, EventArgs e)
    {
        using var fileDialog = new OpenFileDialog
        {
            Filter = "音频文件|*.mp3;*.wav",
            FileName = Path.GetFileName(EnsureDefaultTextTrigger().MusicPath)
        };

        if (fileDialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        var trigger = EnsureDefaultTextTrigger();
        trigger.MusicPath = fileDialog.FileName;
        SaveConfig();
        UpdateTextTriggerDisplay();
        UpdateStatus();
    }

    private void DeathTriggerSettings_Changed(object? sender, EventArgs e)
    {
        if (_isApplyingConfigToUi)
        {
            return;
        }

        _config.DeathTrigger.Enabled = _deathTriggerEnabledBox.Checked;
        _config.DeathTrigger.TemplateSimilarityThreshold = (double)_deathSimilarityThresholdBox.Value;
        _config.DeathTrigger.HealthZeroPixelThreshold = (int)_deathHealthZeroThresholdBox.Value;
        _config.DeathTrigger.ScanIntervalMs = (int)_deathScanIntervalBox.Value;
        _config.DeathTrigger.CooldownSeconds = (int)_deathCooldownBox.Value;

        SaveConfig();
        UpdateDeathTriggerDisplay();
        SyncDeathTriggerTimer();
        UpdateStatus();
    }

    private void ConfigureDeathRegion(bool isHealthRegion)
    {
        if (_isRecognitionRunning || _isRegionCaptureRunning || _isConfiguringKey)
        {
            return;
        }

        _isRegionCaptureRunning = true;
        _triggerMonitorService.Enabled = false;
        _regionCaptureMonitorService.Enabled = false;
        SetRegionCaptureHotkeyMonitorsEnabled(false);
        _deathTriggerTimer.Stop();
        HideForSelection();

        try
        {
            var deathPromptSize = isHealthRegion ? null : TryGetDeathTemplatePromptSize();
            var step = isHealthRegion
                ? new SelectionStep("框选死亡检测血条区域", "尽量包含左侧等级圆和红色血条。")
                : new SelectionStep(
                    "框选 YOU DIED 文字区域",
                    "只框住死亡提示文字附近，避免包含太多会变化的背景。",
                    deathPromptSize);

            if (!TrySelectBoundsSession(step, out var selectedBounds))
            {
                SetStatus("已取消死亡触发区域框选。");
                return;
            }

            if (isHealthRegion)
            {
                _config.DeathTrigger.HealthRegion = ScreenBounds.FromRectangle(selectedBounds);
                SetStatus("死亡触发血条区域已更新。");
            }
            else
            {
                _config.DeathTrigger.DeathTextRegion = ScreenBounds.FromRectangle(selectedBounds);
                SetStatus("YOU DIED 区域已更新。");
            }

            SaveConfig();
            UpdateDeathTriggerDisplay();
            RefreshRegionList();
        }
        catch (Exception ex)
        {
            SetStatus($"死亡触发区域框选失败：{ex.Message}");
        }
        finally
        {
            _isRegionCaptureRunning = false;
            _triggerMonitorService.Enabled = true;
            _regionCaptureMonitorService.Enabled = true;
            SetRegionCaptureHotkeyMonitorsEnabled(true);
            SyncDeathTriggerTimer();
            RestoreAfterSelection();
        }
    }

    private void BrowseDeathTemplateButton_Click(object? sender, EventArgs e)
    {
        using var fileDialog = new OpenFileDialog
        {
            Filter = "图片文件|*.png;*.jpg;*.jpeg;*.bmp|所有文件|*.*",
            FileName = Path.GetFileName(_config.DeathTrigger.DeathTemplateImagePath)
        };

        if (fileDialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        _config.DeathTrigger.DeathTemplateImagePath = fileDialog.FileName;
        SaveConfig();
        UpdateDeathTriggerDisplay();
        UpdateStatus();
    }

    private void BrowseDeathMusicButton_Click(object? sender, EventArgs e)
    {
        using var fileDialog = new OpenFileDialog
        {
            Filter = "音频文件|*.mp3;*.wav",
            FileName = Path.GetFileName(_config.DeathTrigger.DeathMusicPath)
        };

        if (fileDialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        _config.DeathTrigger.DeathMusicPath = fileDialog.FileName;
        SaveConfig();
        UpdateDeathTriggerDisplay();
        UpdateStatus();
    }

    private Size? TryGetDeathTemplatePromptSize()
    {
        var templatePath = _config.DeathTrigger.DeathTemplateImagePath;
        if (string.IsNullOrWhiteSpace(templatePath) || !File.Exists(templatePath))
        {
            return null;
        }

        try
        {
            using var image = Image.FromFile(templatePath);
            return image.Size;
        }
        catch
        {
            return null;
        }
    }

    private async void TextTriggerTimer_Tick(object? sender, EventArgs e)
    {
        if (_isTextScanRunning || _isRegionCaptureRunning || _config.TextTriggers.All(trigger => !trigger.Enabled))
        {
            return;
        }

        _isTextScanRunning = true;

        try
        {
            using var screenshot = _screenCaptureService.CaptureVirtualScreen();
            var result = await Task.Run(() =>
            {
                var success = _screenTextRecognizer.TryRecognize(screenshot, out var text, out var error);
                return new TextRecognitionResult(success, text, error);
            }, _shutdownCts.Token);

            if (!result.Success)
            {
                SetStatus($"屏幕文字识别不可用：{result.ErrorMessage}");
                _textTriggerTimer.Stop();
                _textTriggerEnabledBox.Checked = false;
                return;
            }

            HandleRecognizedScreenText(result.Text);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            SetStatus($"屏幕文字识别失败：{ex.Message}");
        }
        finally
        {
            _isTextScanRunning = false;
        }
    }

    private void HandleRecognizedScreenText(string recognizedText)
    {
        var normalizedRecognizedText = NormalizeRecognizedText(recognizedText);
        if (string.IsNullOrWhiteSpace(normalizedRecognizedText))
        {
            return;
        }

        var now = DateTime.UtcNow;
        foreach (var trigger in _config.TextTriggers.Where(trigger => trigger.Enabled))
        {
            var triggerText = NormalizeRecognizedText(trigger.Text);
            if (string.IsNullOrWhiteSpace(triggerText) ||
                !normalizedRecognizedText.Contains(triggerText, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var cooldown = TimeSpan.FromSeconds(Math.Clamp(trigger.CooldownSeconds, 1, 3600));
            if (_lastTextTriggerUtc.TryGetValue(triggerText, out var lastTriggeredAt) &&
                now - lastTriggeredAt < cooldown)
            {
                continue;
            }

            _lastTextTriggerUtc[triggerText] = now;

            if (!File.Exists(trigger.MusicPath))
            {
                SetStatus($"检测到文字“{trigger.Text}”，但音乐文件不存在：{trigger.MusicPath}");
                continue;
            }

            if (!PlayAudioPath(trigger.MusicPath, out var playbackMessage))
            {
                SetStatus(playbackMessage);
                continue;
            }

            SetStatus($"检测到文字“{trigger.Text}”，已播放对应音乐。");
        }
    }

    private static string NormalizeRecognizedText(string text)
    {
        return string.Join(' ', text.Split([' ', '\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries));
    }

    private async void DeathTriggerTimer_Tick(object? sender, EventArgs e)
    {
        if (_isDeathScanRunning || _isRegionCaptureRunning || _isConfiguringKey || !_config.DeathTrigger.Enabled)
        {
            return;
        }

        _isDeathScanRunning = true;

        try
        {
            var configSnapshot = _config.DeathTrigger.Clone();
            var result = await Task.Run(() => _deathTriggerService.Evaluate(configSnapshot), _shutdownCts.Token);

            if (!result.Matched)
            {
                _deathConditionActive = false;
                if (IsDeathTriggerConfigurationWarning(result.Message))
                {
                    SetStatus(result.Message);
                }
                return;
            }

            var wasAlreadyActive = _deathConditionActive;
            _deathConditionActive = true;
            if (wasAlreadyActive)
            {
                return;
            }

            var now = DateTime.UtcNow;
            var cooldown = TimeSpan.FromSeconds(Math.Clamp(configSnapshot.CooldownSeconds, 1, 3600));
            if (now - _lastDeathTriggerUtc < cooldown)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(configSnapshot.DeathMusicPath) || !File.Exists(configSnapshot.DeathMusicPath))
            {
                SetStatus($"死亡触发已命中，但死亡音乐不存在：{configSnapshot.DeathMusicPath}");
                return;
            }

            if (!PlayAudioPath(configSnapshot.DeathMusicPath, out var playbackMessage))
            {
                SetStatus(playbackMessage);
                return;
            }

            _lastDeathTriggerUtc = now;
            SetStatus($"检测到死亡画面，已播放死亡音乐。{result.Message}");
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _deathConditionActive = false;
            SetStatus($"死亡触发检测失败：{ex.Message}");
        }
        finally
        {
            _isDeathScanRunning = false;
        }
    }

    private static bool IsDeathTriggerConfigurationWarning(string message)
    {
        return message.Contains("缺少", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("不存在", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("黑屏", StringComparison.OrdinalIgnoreCase);
    }

    private async void TriggerMonitorService_Triggered(object? sender, EventArgs e)
    {
        if (_isConfiguringKey || _isRecognitionRunning || _isRegionCaptureRunning)
        {
            return;
        }

        if (!HasRequiredRegions())
        {
            SetStatus("需要同时配置技能区域和血条区域。");
            return;
        }

        if (!HasPlayableAudio(_config.AudioPath))
        {
            SetStatus("未加载音频，请先配置音频文件或包含音频的文件夹。");
            return;
        }

        _isRecognitionRunning = true;
        SetStatus($"已触发监听，正在等待 {_config.WatchWindowMs} ms 内的血条变化。");

        try
        {
            var regionsSnapshot = _config.Regions.Select(region => region.Clone()).ToList();
            var session = new RecognitionSession(
                _screenCaptureService,
                _templateMatcher,
                _regionChangeDetector,
                _healthBaselineService,
                _healthBarAnalyzerService,
                _circleLocatorService,
                _configService.RecognitionDebugDirectory,
                _config.WatchWindowMs,
                _config.PollIntervalMs,
                _config.HealthGrowthPixelThreshold);

            var result = await session.RunAsync(regionsSnapshot, _shutdownCts.Token);
            if (result.Matched)
            {
                PlayAudio();
            }

            SetStatus(result.Message);
        }
        finally
        {
            _isRecognitionRunning = false;
        }
    }

    private void RegionCaptureMonitorService_Triggered(object? sender, EventArgs e)
    {
        if (_isConfiguringKey || _isRecognitionRunning || _isRegionCaptureRunning)
        {
            return;
        }

        if (InvokeRequired)
        {
            BeginInvoke(new Action(() => CaptureBothRegionsSession(restoreWindowAfter: true)));
            return;
        }

        CaptureBothRegionsSession(restoreWindowAfter: true);
    }

    private void SkillRegionCaptureMonitorService_Triggered(object? sender, EventArgs e)
    {
        BeginRegionCaptureFromHotkey(() => ConfigureSingleRegionSession(RegionSettingsMode.Skill, restoreWindowAfter: true));
    }

    private void HealthRegionCaptureMonitorService_Triggered(object? sender, EventArgs e)
    {
        BeginRegionCaptureFromHotkey(() => ConfigureSingleRegionSession(RegionSettingsMode.Health, restoreWindowAfter: true));
    }

    private void DeathTextRegionCaptureMonitorService_Triggered(object? sender, EventArgs e)
    {
        BeginRegionCaptureFromHotkey(() => ConfigureDeathRegion(isHealthRegion: false));
    }

    private void BeginRegionCaptureFromHotkey(Action action)
    {
        if (_isConfiguringKey || _isRecognitionRunning || _isRegionCaptureRunning)
        {
            return;
        }

        if (InvokeRequired)
        {
            BeginInvoke(action);
            return;
        }

        action();
    }

    private void InputCaptureService_InputPressed(int keyCode)
    {
        if (keyCode == 0x01)
        {
            TryPlayClickSound(_config.LeftClickAudioPath, isLeftButton: true);
        }
        else if (keyCode == 0x02)
        {
            TryPlayClickSound(_config.RightClickAudioPath, isLeftButton: false);
        }
    }

    private void RemoveRegionButton_Click(object? sender, EventArgs e)
    {
        if (_regionsListBox.SelectedItem is not WatchRegion selectedRegion)
        {
            return;
        }

        _config.Regions.Remove(selectedRegion);
        SaveConfig();
        RefreshRegionList();
        SetStatus($"已删除区域：{selectedRegion.Name}");
    }

    private void ConfigureSingleRegionSession(RegionSettingsMode mode, bool restoreWindowAfter)
    {
        if (_isRecognitionRunning || _isRegionCaptureRunning || _isConfiguringKey)
        {
            SetStatus("当前正在识别、框选或设置按键，暂时不能修改区域。");
            return;
        }

        _isRegionCaptureRunning = true;
        _triggerMonitorService.Enabled = false;
        _regionCaptureMonitorService.Enabled = false;
        SetRegionCaptureHotkeyMonitorsEnabled(false);
        HideForSelection();

        try
        {
            var step = mode == RegionSettingsMode.Skill
                ? new SelectionStep("框选技能图标的大致区域", "范围可以适当放大，后续会根据模板继续精确定位。")
                : new SelectionStep("框选血条的大致区域", "尽量把左侧等级圆和三根条都框进去。");

            if (!TrySelectBoundsSession(step, out var selectedBounds))
            {
                SetStatus("已取消区域框选。");
                return;
            }

            if (mode == RegionSettingsMode.Skill)
            {
                var readyTemplatePath = EnsureDefaultSkillReadyTemplate(selectedBounds);
                var refinedBounds = RefineSkillBounds(selectedBounds, readyTemplatePath);
                ReplaceRegion<SkillReadyWatchRegion>(CreateSkillRegion(refinedBounds, readyTemplatePath));
                SetStatus("技能区域已更新。");
            }
            else
            {
                var healthTemplatePath = EnsureDefaultHealthTemplate(selectedBounds);
                ReplaceRegion<HealthChangeWatchRegion>(CreateHealthRegion(selectedBounds, healthTemplatePath));
                SetStatus("血条区域已更新。");
            }

            SaveConfig();
            RefreshRegionList();
        }
        catch (Exception ex)
        {
            SetStatus($"框选失败：{ex.Message}");
        }
        finally
        {
            _isRegionCaptureRunning = false;
            _triggerMonitorService.Enabled = true;
            _regionCaptureMonitorService.Enabled = true;
            SetRegionCaptureHotkeyMonitorsEnabled(true);

            if (restoreWindowAfter)
            {
                RestoreAfterSelection();
            }
        }
    }

    private void CaptureBothRegionsSession(bool restoreWindowAfter)
    {
        if (_isRecognitionRunning || _isRegionCaptureRunning || _isConfiguringKey)
        {
            return;
        }

        _isRegionCaptureRunning = true;
        _triggerMonitorService.Enabled = false;
        _regionCaptureMonitorService.Enabled = false;
        SetRegionCaptureHotkeyMonitorsEnabled(false);
        HideForSelection();

        try
        {
            var healthStep = new SelectionStep(
                "框选血条的大致区域",
                "第一步先框血条区域，尽量包含左侧等级圆和三根颜色条。");
            var skillStep = new SelectionStep(
                "框选技能图标的大致区域",
                "第二步继续在同一张冻结截图上框选技能图标区域。");

            CapturePreviewResult? previewResult = null;
            using var overlay = new SelectionOverlayForm(
                new[] { healthStep, skillStep },
                context =>
                {
                    previewResult = BuildCapturePreviewResult(
                        context.Snapshot,
                        context.ScreenBounds,
                        context.SelectedBoundsScreenList[0],
                        context.SelectedBoundsScreenList[1]);

                    return new SelectionReviewData(
                        "识别结果预览",
                        "当前快照上已经标出等级圆心和技能圆心，确认后才会保存。",
                        new[] { previewResult.HealthMarker, previewResult.SkillMarker });
                });

            if (overlay.ShowDialog() != DialogResult.OK || previewResult is null)
            {
                SetStatus("已取消截图框选。");
                return;
            }

            ReplaceRegion<HealthChangeWatchRegion>(previewResult.HealthRegion);
            ReplaceRegion<SkillReadyWatchRegion>(previewResult.SkillRegion);

            SaveConfig();
            RefreshRegionList();
            SetStatus("已完成两步框选，并标记了等级圆心和技能圆心。");
        }
        catch (Exception ex)
        {
            SetStatus($"快捷框选失败：{ex.Message}");
        }
        finally
        {
            _isRegionCaptureRunning = false;
            _triggerMonitorService.Enabled = true;
            _regionCaptureMonitorService.Enabled = true;
            SetRegionCaptureHotkeyMonitorsEnabled(true);

            if (restoreWindowAfter)
            {
                RestoreAfterSelection();
            }
        }
    }

    private bool TrySelectBoundsSession(SelectionStep step, out Rectangle selectedBounds)
    {
        selectedBounds = Rectangle.Empty;

        using var overlay = new SelectionOverlayForm(new[] { step });
        if (overlay.ShowDialog() != DialogResult.OK || overlay.SelectedBoundsScreen is not Rectangle result)
        {
            return false;
        }

        selectedBounds = result;
        return true;
    }

    private void HideForSelection()
    {
        WindowState = FormWindowState.Minimized;
        Hide();
    }

    private void RestoreAfterSelection()
    {
        Show();
        WindowState = FormWindowState.Normal;
        Activate();
    }

    private CapturePreviewResult BuildCapturePreviewResult(
        Bitmap snapshot,
        Rectangle snapshotScreenBounds,
        Rectangle healthBounds,
        Rectangle roughSkillBounds)
    {
        var healthTemplatePath = EnsureDefaultHealthTemplateFromSnapshot(snapshot, snapshotScreenBounds, healthBounds);
        var healthRegion = CreateHealthRegion(healthBounds, healthTemplatePath);
        var healthMarker = ResolveHealthMarkerFromSnapshot(healthRegion, snapshot, snapshotScreenBounds);

        var readyTemplatePath = EnsureDefaultSkillReadyTemplateFromSnapshot(snapshot, snapshotScreenBounds, roughSkillBounds);
        var refinedSkillBounds = RefineSkillBoundsFromSnapshot(roughSkillBounds, readyTemplatePath, snapshot, snapshotScreenBounds);
        var skillRegion = CreateSkillRegion(refinedSkillBounds, readyTemplatePath);
        var skillMarker = ResolveSkillMarkerFromSnapshot(skillRegion, snapshot, snapshotScreenBounds);

        return new CapturePreviewResult(healthRegion, skillRegion, healthMarker, skillMarker);
    }

    private string EnsureDefaultSkillReadyTemplateFromSnapshot(Bitmap snapshot, Rectangle snapshotScreenBounds, Rectangle roughBounds)
    {
        Directory.CreateDirectory(_configService.DefaultTemplateDirectory);

        using var bitmap = CropSnapshot(snapshot, snapshotScreenBounds, roughBounds);
        bitmap.Save(_configService.DefaultSkillReadyTemplatePath);
        _templateMatcher.Invalidate(_configService.DefaultSkillReadyTemplatePath);
        return _configService.DefaultSkillReadyTemplatePath;
    }

    private string EnsureDefaultHealthTemplateFromSnapshot(Bitmap snapshot, Rectangle snapshotScreenBounds, Rectangle roughBounds)
    {
        Directory.CreateDirectory(_configService.DefaultTemplateDirectory);

        if (_configService.HasDefaultHealthTemplate())
        {
            return _configService.DefaultHealthTemplatePath;
        }

        using var bitmap = CropSnapshot(snapshot, snapshotScreenBounds, roughBounds);
        bitmap.Save(_configService.DefaultHealthTemplatePath);
        return _configService.DefaultHealthTemplatePath;
    }

    private Rectangle RefineSkillBoundsFromSnapshot(
        Rectangle roughBounds,
        string templatePath,
        Bitmap snapshot,
        Rectangle snapshotScreenBounds)
    {
        using var bitmap = CropSnapshot(snapshot, snapshotScreenBounds, roughBounds);
        var matchResult = _templateMatcher.FindBestMatch(bitmap, templatePath);

        if (!matchResult.IsValid)
        {
            return roughBounds;
        }

        return new Rectangle(
            roughBounds.X + matchResult.Bounds.X,
            roughBounds.Y + matchResult.Bounds.Y,
            matchResult.Bounds.Width,
            matchResult.Bounds.Height);
    }

    private MarkerPoint ResolveHealthMarkerFromSnapshot(
        HealthChangeWatchRegion region,
        Bitmap snapshot,
        Rectangle snapshotScreenBounds)
    {
        var exactBounds = region.Bounds.ToRectangle();

        if (!string.IsNullOrWhiteSpace(region.TemplateImagePath))
        {
            using var roughBitmap = CropSnapshot(snapshot, snapshotScreenBounds, exactBounds);
            var match = _templateMatcher.FindBestMatch(roughBitmap, region.TemplateImagePath);
            if (match.Score >= region.TemplateSimilarityThreshold && match.IsValid)
            {
                exactBounds = new Rectangle(
                    exactBounds.X + match.Bounds.X,
                    exactBounds.Y + match.Bounds.Y,
                    match.Bounds.Width,
                    match.Bounds.Height);
            }
        }

        using var exactBitmap = CropSnapshot(snapshot, snapshotScreenBounds, exactBounds);
        var circleCenter = _circleLocatorService.FindHealthCircleCenter(exactBitmap);
        return new MarkerPoint(
            new Point(exactBounds.X + circleCenter.X, exactBounds.Y + circleCenter.Y),
            "等级圆",
            Color.OrangeRed);
    }

    private MarkerPoint ResolveSkillMarkerFromSnapshot(
        SkillReadyWatchRegion region,
        Bitmap snapshot,
        Rectangle snapshotScreenBounds)
    {
        var bounds = region.Bounds.ToRectangle();
        using var bitmap = CropSnapshot(snapshot, snapshotScreenBounds, bounds);
        var circleCenter = _circleLocatorService.FindSkillCircleCenter(bitmap);
        return new MarkerPoint(
            new Point(bounds.X + circleCenter.X, bounds.Y + circleCenter.Y),
            "技能圆",
            Color.DeepSkyBlue);
    }

    private static Bitmap CropSnapshot(Bitmap snapshot, Rectangle snapshotScreenBounds, Rectangle targetScreenBounds)
    {
        var localBounds = new Rectangle(
            targetScreenBounds.X - snapshotScreenBounds.X,
            targetScreenBounds.Y - snapshotScreenBounds.Y,
            targetScreenBounds.Width,
            targetScreenBounds.Height);
        var validBounds = Rectangle.Intersect(new Rectangle(Point.Empty, snapshot.Size), localBounds);

        if (validBounds.Width <= 0 || validBounds.Height <= 0)
        {
            throw new InvalidOperationException("截图区域超出了当前冻结快照范围。");
        }

        return snapshot.Clone(validBounds, snapshot.PixelFormat);
    }

    private string EnsureDefaultSkillReadyTemplate(Rectangle roughBounds)
    {
        Directory.CreateDirectory(_configService.DefaultTemplateDirectory);

        using var bitmap = _screenCaptureService.Capture(roughBounds);
        bitmap.Save(_configService.DefaultSkillReadyTemplatePath);
        _templateMatcher.Invalidate(_configService.DefaultSkillReadyTemplatePath);
        return _configService.DefaultSkillReadyTemplatePath;
    }

    private string EnsureDefaultHealthTemplate(Rectangle roughBounds)
    {
        Directory.CreateDirectory(_configService.DefaultTemplateDirectory);

        if (_configService.HasDefaultHealthTemplate())
        {
            return _configService.DefaultHealthTemplatePath;
        }

        using var bitmap = _screenCaptureService.Capture(roughBounds);
        bitmap.Save(_configService.DefaultHealthTemplatePath);
        return _configService.DefaultHealthTemplatePath;
    }

    private Rectangle RefineSkillBounds(Rectangle roughBounds, string templatePath)
    {
        using var bitmap = _screenCaptureService.Capture(roughBounds);
        var matchResult = _templateMatcher.FindBestMatch(bitmap, templatePath);

        if (!matchResult.IsValid)
        {
            return roughBounds;
        }

        return new Rectangle(
            roughBounds.X + matchResult.Bounds.X,
            roughBounds.Y + matchResult.Bounds.Y,
            matchResult.Bounds.Width,
            matchResult.Bounds.Height);
    }

    private SkillReadyWatchRegion CreateSkillRegion(Rectangle bounds, string readyTemplatePath)
    {
        return new SkillReadyWatchRegion
        {
            Name = "技能区域",
            Bounds = ScreenBounds.FromRectangle(bounds),
            ConsecutiveFramesRequired = 1,
            ReadySimilarityThreshold = 0.86,
            ReadyTemplateImagePath = readyTemplatePath,
            EmptyTemplateImagePath = _configService.HasDefaultSkillEmptyTemplate()
                ? _configService.DefaultSkillEmptyTemplatePath
                : string.Empty,
            ReadyVsEmptyMargin = 0.015
        };
    }

    private HealthChangeWatchRegion CreateHealthRegion(Rectangle bounds, string healthTemplatePath)
    {
        return new HealthChangeWatchRegion
        {
            Name = "血条区域",
            Bounds = ScreenBounds.FromRectangle(bounds),
            ConsecutiveFramesRequired = _config.HealthConsecutiveFramesRequired,
            TemplateImagePath = healthTemplatePath,
            TemplateSimilarityThreshold = 0.75,
            DiffPixelThreshold = 30,
            ChangedAreaRatioThreshold = 0.08
        };
    }

    private void ReplaceRegion<TRegion>(WatchRegion region) where TRegion : WatchRegion
    {
        _config.Regions.RemoveAll(existing => existing is TRegion);
        _config.Regions.Add(region);
    }

    private bool HasRequiredRegions()
    {
        return _config.Regions.OfType<SkillReadyWatchRegion>().Any() &&
               _config.Regions.OfType<HealthChangeWatchRegion>().Any();
    }

    private void LoadAudio()
    {
        DisposeAudio();
    }

    private void PlayAudio()
    {
        if (_config.UseSoundpadOutput)
        {
            TryPlaySoundpadSound();
            return;
        }

        if (!PlayAudioPath(_config.AudioPath, out var message))
        {
            SetStatus(message);
        }
    }

    private bool PlayAudioPath(string path, out string message)
    {
        lock (_audioLock)
        {
            if (_isPlaying)
            {
                message = "音频正在播放中，本次触发已忽略。";
                return false;
            }

            if (!TryResolveAudioPath(path, out var audioPath))
            {
                message = "音频播放失败，请检查文件格式或路径。";
                return false;
            }

            try
            {
                _audioFile = new AudioFileReader(audioPath);
                _audioFile.Volume = Math.Clamp(_config.AudioVolume, 0f, 1f);
                _outputDevice = CreateOutputDevice();
                _outputDevice.PlaybackStopped += OutputDevice_PlaybackStopped;
                _outputDevice.Init(_audioFile);
                _outputDevice.Play();
                _isPlaying = true;
                message = "音频已开始播放。";
                return true;
            }
            catch
            {
                DisposeMainAudioLocked();
                message = "音频播放失败，请检查文件格式或路径。";
                return false;
            }
        }
    }

    private void OutputDevice_PlaybackStopped(object? sender, StoppedEventArgs e)
    {
        lock (_audioLock)
        {
            _isPlaying = false;

            DisposeMainAudioLocked();
        }
    }

    private void DisposeAudio()
    {
        lock (_audioLock)
        {
            DisposeMainAudioLocked();
        }

        DisposeClickSounds();
    }

    private void DisposeMainAudioLocked()
    {
        _isPlaying = false;

        if (_outputDevice is not null)
        {
            _outputDevice.PlaybackStopped -= OutputDevice_PlaybackStopped;
            _outputDevice.Stop();
            _outputDevice.Dispose();
            _outputDevice = null;
        }

        _audioFile?.Dispose();
        _audioFile = null;
    }

    private void TryPlayClickSound(string path, bool isLeftButton)
    {
        lock (_audioLock)
        {
            if (!_isPlaying)
            {
                return;
            }
        }

        if (!TryResolveAudioPath(path, out var audioPath))
        {
            return;
        }

        lock (_clickSoundLock)
        {
            var now = DateTime.UtcNow;
            var lastPlayedAt = isLeftButton ? _lastLeftClickSoundUtc : _lastRightClickSoundUtc;
            if (now - lastPlayedAt < TimeSpan.FromSeconds(1))
            {
                return;
            }

            if (isLeftButton)
            {
                _lastLeftClickSoundUtc = now;
            }
            else
            {
                _lastRightClickSoundUtc = now;
            }
        }

        PlayTransientSound(audioPath);
    }

    private void PlayTransientSound(string path)
    {
        AudioFileReader? audioFile = null;
        IWavePlayer? outputDevice = null;

        try
        {
            audioFile = new AudioFileReader(path)
            {
                Volume = Math.Clamp(_config.AudioVolume, 0f, 1f)
            };
            outputDevice = CreateOutputDevice();
            outputDevice.PlaybackStopped += ClickSound_PlaybackStopped;
            outputDevice.Init(audioFile);

            lock (_clickSoundLock)
            {
                _clickSoundPlayers.Add((outputDevice, audioFile));
            }

            outputDevice.Play();
        }
        catch
        {
            outputDevice?.Dispose();
            audioFile?.Dispose();
        }
    }

    private void ClickSound_PlaybackStopped(object? sender, StoppedEventArgs e)
    {
        if (sender is not IWavePlayer outputDevice)
        {
            return;
        }

        AudioFileReader? audioFile = null;
        lock (_clickSoundLock)
        {
            var index = _clickSoundPlayers.FindIndex(player => ReferenceEquals(player.OutputDevice, outputDevice));
            if (index >= 0)
            {
                audioFile = _clickSoundPlayers[index].AudioFile;
                _clickSoundPlayers.RemoveAt(index);
            }
        }

        outputDevice.PlaybackStopped -= ClickSound_PlaybackStopped;
        outputDevice.Dispose();
        audioFile?.Dispose();
    }

    private void DisposeClickSounds()
    {
        List<(IWavePlayer OutputDevice, AudioFileReader AudioFile)> players;

        lock (_clickSoundLock)
        {
            players = _clickSoundPlayers.ToList();
            _clickSoundPlayers.Clear();
        }

        foreach (var (outputDevice, audioFile) in players)
        {
            outputDevice.PlaybackStopped -= ClickSound_PlaybackStopped;
            outputDevice.Stop();
            outputDevice.Dispose();
            audioFile.Dispose();
        }
    }

    private bool HasPlayableAudio(string path)
    {
        return TryResolveAudioPath(path, out _);
    }

    private void TryPlaySoundpadSound()
    {
        var soundpadPath = ResolveSoundpadExecutablePath(_config.SoundpadExecutablePath);
        if (string.IsNullOrWhiteSpace(soundpadPath))
        {
            SetStatus("未找到 Soundpad.exe，请先选择 Soundpad 程序路径。");
            return;
        }

        try
        {
            var command = $"DoPlaySound({_config.SoundpadSoundIndex},true,true)";
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = soundpadPath,
                Arguments = $"-rc {command}",
                UseShellExecute = false,
                CreateNoWindow = true
            });

            SetStatus($"已发送 Soundpad 播放命令：第 {_config.SoundpadSoundIndex} 个声音。");
        }
        catch (Exception ex)
        {
            SetStatus($"Soundpad 播放失败：{ex.Message}");
        }
    }

    private static string ResolveSoundpadExecutablePath(string configuredPath)
    {
        if (!string.IsNullOrWhiteSpace(configuredPath) && File.Exists(configuredPath))
        {
            return configuredPath;
        }

        var candidates = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Soundpad", "Soundpad.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam", "steamapps", "common", "Soundpad", "Soundpad.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Steam", "steamapps", "common", "Soundpad", "Soundpad.exe")
        };

        return candidates.FirstOrDefault(File.Exists) ?? string.Empty;
    }

    private WaveOutEvent CreateOutputDevice()
    {
        return new WaveOutEvent
        {
            DeviceNumber = ResolveAudioOutputDeviceNumber(_config.AudioOutputDeviceName)
        };
    }

    private static int ResolveAudioOutputDeviceNumber(string productName)
    {
        if (string.IsNullOrWhiteSpace(productName))
        {
            return -1;
        }

        try
        {
            for (var deviceNumber = 0; deviceNumber < WaveOut.DeviceCount; deviceNumber++)
            {
                var capabilities = WaveOut.GetCapabilities(deviceNumber);
                if (string.Equals(capabilities.ProductName, productName, StringComparison.OrdinalIgnoreCase))
                {
                    return deviceNumber;
                }
            }
        }
        catch
        {
            return -1;
        }

        return -1;
    }

    private void RefreshAudioOutputDevices()
    {
        var wasApplyingConfigToUi = _isApplyingConfigToUi;
        _isApplyingConfigToUi = true;

        try
        {
            _audioOutputDeviceBox.Items.Clear();
            _audioOutputDeviceBox.Items.Add(new AudioOutputDeviceItem(-1, string.Empty, "系统默认输出设备"));

            try
            {
                for (var deviceNumber = 0; deviceNumber < WaveOut.DeviceCount; deviceNumber++)
                {
                    var capabilities = WaveOut.GetCapabilities(deviceNumber);
                    _audioOutputDeviceBox.Items.Add(new AudioOutputDeviceItem(
                        deviceNumber,
                        capabilities.ProductName,
                        $"{deviceNumber}: {capabilities.ProductName}"));
                }
            }
            catch
            {
                SetStatus("读取音频输出设备失败，已保留系统默认输出设备。");
            }

            SelectConfiguredAudioOutputDevice();
        }
        finally
        {
            _isApplyingConfigToUi = wasApplyingConfigToUi;
        }
    }

    private void SelectConfiguredAudioOutputDevice()
    {
        var selectedIndex = 0;

        for (var index = 0; index < _audioOutputDeviceBox.Items.Count; index++)
        {
            if (_audioOutputDeviceBox.Items[index] is AudioOutputDeviceItem item &&
                string.Equals(item.ProductName, _config.AudioOutputDeviceName, StringComparison.OrdinalIgnoreCase))
            {
                selectedIndex = index;
                break;
            }
        }

        _audioOutputDeviceBox.SelectedIndex = selectedIndex;
    }

    private bool TryResolveAudioPath(string path, out string audioPath)
    {
        if (File.Exists(path) && IsSupportedAudioFile(path))
        {
            audioPath = path;
            return true;
        }

        if (Directory.Exists(path))
        {
            var files = Directory.EnumerateFiles(path)
                .Where(IsSupportedAudioFile)
                .ToList();

            if (files.Count > 0)
            {
                audioPath = files[_audioRandom.Next(files.Count)];
                return true;
            }
        }

        audioPath = string.Empty;
        return false;
    }

    private static bool IsSupportedAudioFile(string path)
    {
        var extension = Path.GetExtension(path);
        return SupportedAudioExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase);
    }

    private void ApplyConfigToUi()
    {
        _isApplyingConfigToUi = true;

        try
        {
            UpdateAudioDisplay();
            _watchWindowBox.Value = Math.Clamp(_config.WatchWindowMs, (int)_watchWindowBox.Minimum, (int)_watchWindowBox.Maximum);
            _pollIntervalBox.Value = Math.Clamp(_config.PollIntervalMs, (int)_pollIntervalBox.Minimum, (int)_pollIntervalBox.Maximum);
            _healthConsecutiveFramesBox.Value = Math.Clamp(_config.HealthConsecutiveFramesRequired, (int)_healthConsecutiveFramesBox.Minimum, (int)_healthConsecutiveFramesBox.Maximum);
            _healthThresholdBar.Value = Math.Clamp(_config.HealthGrowthPixelThreshold, _healthThresholdBar.Minimum, _healthThresholdBar.Maximum);
            _audioVolumeBar.Value = Math.Clamp((int)Math.Round(_config.AudioVolume * 100f), _audioVolumeBar.Minimum, _audioVolumeBar.Maximum);
            SelectConfiguredAudioOutputDevice();
            _useSoundpadOutputBox.Checked = _config.UseSoundpadOutput;
            _soundpadSoundIndexBox.Value = Math.Clamp(_config.SoundpadSoundIndex, (int)_soundpadSoundIndexBox.Minimum, (int)_soundpadSoundIndexBox.Maximum);
            UpdateSoundpadDisplay();
            var textTrigger = EnsureDefaultTextTrigger();
            _textTriggerEnabledBox.Checked = textTrigger.Enabled;
            _textTriggerTextBox.Text = textTrigger.Text;
            _textTriggerCooldownBox.Value = Math.Clamp(textTrigger.CooldownSeconds, (int)_textTriggerCooldownBox.Minimum, (int)_textTriggerCooldownBox.Maximum);
            UpdateTextTriggerDisplay();
            _deathTriggerEnabledBox.Checked = _config.DeathTrigger.Enabled;
            _deathSimilarityThresholdBox.Value = Math.Clamp((decimal)_config.DeathTrigger.TemplateSimilarityThreshold, _deathSimilarityThresholdBox.Minimum, _deathSimilarityThresholdBox.Maximum);
            _deathHealthZeroThresholdBox.Value = Math.Clamp(_config.DeathTrigger.HealthZeroPixelThreshold, (int)_deathHealthZeroThresholdBox.Minimum, (int)_deathHealthZeroThresholdBox.Maximum);
            _deathScanIntervalBox.Value = Math.Clamp(_config.DeathTrigger.ScanIntervalMs, (int)_deathScanIntervalBox.Minimum, (int)_deathScanIntervalBox.Maximum);
            _deathCooldownBox.Value = Math.Clamp(_config.DeathTrigger.CooldownSeconds, (int)_deathCooldownBox.Minimum, (int)_deathCooldownBox.Maximum);
            UpdateDeathTriggerDisplay();
            SyncDeathTriggerTimer();
            UpdateRegionCaptureHotkeyButtonText();
            UpdateThresholdDisplay();
            UpdateVolumeDisplay();
            ApplyAudioVolume();
            _triggerMonitorService.TriggerKey = _config.TriggerKey;
            _regionCaptureMonitorService.TriggerKey = _config.RegionCaptureKey;
            _skillRegionCaptureMonitorService.TriggerKey = _config.RegionCaptureHotkeys.SkillRegionKey;
            _healthRegionCaptureMonitorService.TriggerKey = _config.RegionCaptureHotkeys.HealthRegionKey;
            _deathTextRegionCaptureMonitorService.TriggerKey = _config.RegionCaptureHotkeys.DeathTextRegionKey;
        }
        finally
        {
            _isApplyingConfigToUi = false;
        }
    }

    private void RefreshRegionList()
    {
        _regionsListBox.BeginUpdate();
        _regionsListBox.Items.Clear();

        foreach (var region in _config.Regions.OrderBy(region => region.Name))
        {
            _regionsListBox.Items.Add(region);
        }

        if (_config.DeathTrigger.DeathTextRegion is not null)
        {
            _regionsListBox.Items.Add($"死亡文字区域 | YOU DIED | {_config.DeathTrigger.DeathTextRegion}");
        }

        _regionsListBox.EndUpdate();
    }

    private void SaveConfig()
    {
        _config.WatchWindowMs = (int)_watchWindowBox.Value;
        _config.PollIntervalMs = (int)_pollIntervalBox.Value;
        _config.HealthConsecutiveFramesRequired = (int)_healthConsecutiveFramesBox.Value;
        SyncHealthConsecutiveFramesToRegion();
        _config.HealthGrowthPixelThreshold = _healthThresholdBar.Value;
        _config.AudioVolume = _audioVolumeBar.Value / 100f;
        if (_audioOutputDeviceBox.SelectedItem is AudioOutputDeviceItem selectedDevice)
        {
            _config.AudioOutputDeviceName = selectedDevice.ProductName;
        }
        _config.UseSoundpadOutput = _useSoundpadOutputBox.Checked;
        _config.SoundpadSoundIndex = (int)_soundpadSoundIndexBox.Value;
        var textTrigger = EnsureDefaultTextTrigger();
        textTrigger.Enabled = _textTriggerEnabledBox.Checked;
        textTrigger.Text = string.IsNullOrWhiteSpace(_textTriggerTextBox.Text)
            ? "YOU DIED"
            : _textTriggerTextBox.Text.Trim();
        textTrigger.CooldownSeconds = (int)_textTriggerCooldownBox.Value;
        _config.DeathTrigger.Enabled = _deathTriggerEnabledBox.Checked;
        _config.DeathTrigger.TemplateSimilarityThreshold = (double)_deathSimilarityThresholdBox.Value;
        _config.DeathTrigger.HealthZeroPixelThreshold = (int)_deathHealthZeroThresholdBox.Value;
        _config.DeathTrigger.ScanIntervalMs = (int)_deathScanIntervalBox.Value;
        _config.DeathTrigger.CooldownSeconds = (int)_deathCooldownBox.Value;
        _configService.Save(_config);
    }

    private void UpdateRegionCaptureHotkeyButtonText()
    {
        _setSkillRegionCaptureKeyButton.Text = $"设置技能区域快捷键：{_config.RegionCaptureHotkeys.SkillRegionKeyName}";
        _setHealthRegionCaptureKeyButton.Text = $"设置血条区域快捷键：{_config.RegionCaptureHotkeys.HealthRegionKeyName}";
        _setDeathTextRegionCaptureKeyButton.Text = $"设置死亡区域快捷键：{_config.RegionCaptureHotkeys.DeathTextRegionKeyName}";
    }

    private void UpdateAudioDisplay()
    {
        if (string.IsNullOrWhiteSpace(_config.AudioPath))
        {
            _audioPathBox.Text = "未配置音频";
            return;
        }

        _audioPathBox.Text = Path.GetFileName(_config.AudioPath);
    }

    private void UpdateSoundpadDisplay()
    {
        var soundpadPath = ResolveSoundpadExecutablePath(_config.SoundpadExecutablePath);
        _soundpadPathBox.Text = string.IsNullOrWhiteSpace(soundpadPath)
            ? "未找到 Soundpad.exe"
            : soundpadPath;
    }

    private void UpdateTextTriggerDisplay()
    {
        var trigger = EnsureDefaultTextTrigger();
        _textTriggerMusicPathBox.Text = string.IsNullOrWhiteSpace(trigger.MusicPath)
            ? "未选择死亡音乐"
            : Path.GetFileName(trigger.MusicPath);
    }

    private void UpdateDeathTriggerDisplay()
    {
        var textRegionText = _config.DeathTrigger.DeathTextRegion is null
            ? "未设文字"
            : $"文字 {_config.DeathTrigger.DeathTextRegion}";

        _deathTemplatePathBox.Text = string.IsNullOrWhiteSpace(_config.DeathTrigger.DeathTemplateImagePath)
            ? $"{textRegionText} / 未选模板"
            : Path.GetFileName(_config.DeathTrigger.DeathTemplateImagePath);
        _deathMusicPathBox.Text = string.IsNullOrWhiteSpace(_config.DeathTrigger.DeathMusicPath)
            ? "未选择死亡触发音乐"
            : Path.GetFileName(_config.DeathTrigger.DeathMusicPath);
    }

    private void SyncDeathTriggerTimer()
    {
        _deathTriggerTimer.Interval = Math.Clamp(_config.DeathTrigger.ScanIntervalMs, 100, 10000);

        if (_config.DeathTrigger.Enabled && !_isRegionCaptureRunning)
        {
            _deathTriggerTimer.Start();
        }
        else
        {
            _deathTriggerTimer.Stop();
            _deathConditionActive = false;
        }
    }

    private void SetRegionCaptureHotkeyMonitorsEnabled(bool enabled)
    {
        _skillRegionCaptureMonitorService.Enabled = enabled;
        _healthRegionCaptureMonitorService.Enabled = enabled;
        _deathTextRegionCaptureMonitorService.Enabled = enabled;
    }

    private TextTriggerConfig EnsureDefaultTextTrigger()
    {
        var trigger = _config.TextTriggers.FirstOrDefault();
        if (trigger is not null)
        {
            return trigger;
        }

        trigger = new TextTriggerConfig
        {
            Enabled = true,
            Text = "YOU DIED",
            MusicPath = _config.AudioPath,
            CooldownSeconds = 5
        };
        _config.TextTriggers.Add(trigger);
        return trigger;
    }

    private void UpdateStatus()
    {
        var audioState = _config.UseSoundpadOutput
            ? "Soundpad 模式"
            : HasPlayableAudio(_config.AudioPath) ? "已加载音频" : "未选择音频";
        var outputDevice = string.IsNullOrWhiteSpace(_config.AudioOutputDeviceName)
            ? "系统默认输出设备"
            : _config.AudioOutputDeviceName;
        if (_config.UseSoundpadOutput)
        {
            outputDevice = $"Soundpad 第 {_config.SoundpadSoundIndex} 个声音";
        }
        var enabledTextTriggerCount = _config.TextTriggers.Count(trigger => trigger.Enabled);
        var deathTriggerState = _config.DeathTrigger.Enabled ? "开" : "关";
        var regionCount = _config.Regions.Count;
        _statusLabel.Text =
            $"{audioState}。输出：{outputDevice}。文字触发：{enabledTextTriggerCount}。死亡触发：{deathTriggerState}。技能触发键：{_config.TriggerKeyName}。截图键：{_config.RegionCaptureKeyName}。检测区域数量：{regionCount}。";
    }

    private void UpdateThresholdDisplay()
    {
        _healthThresholdValueLabel.Text = $"{_healthThresholdBar.Value}px";
    }

    private void UpdateVolumeDisplay()
    {
        _audioVolumeValueLabel.Text = $"{_audioVolumeBar.Value}%";
    }

    private void SyncHealthConsecutiveFramesToRegion()
    {
        var healthRegion = _config.Regions.OfType<HealthChangeWatchRegion>().FirstOrDefault();
        if (healthRegion is not null)
        {
            healthRegion.ConsecutiveFramesRequired = _config.HealthConsecutiveFramesRequired;
        }
    }

    private void ApplyAudioVolume()
    {
        lock (_audioLock)
        {
            if (_audioFile is not null)
            {
                _audioFile.Volume = Math.Clamp(_config.AudioVolume, 0f, 1f);
            }
        }
    }

    private void SetStatus(string message)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action<string>(SetStatus), message);
            return;
        }

        _statusLabel.Text = message;
    }

    private void AOUU_FormClosing(object? sender, FormClosingEventArgs e)
    {
        _shutdownCts.Cancel();
        SaveConfig();
    }

    private void AOUU_FormClosed(object? sender, FormClosedEventArgs e)
    {
        _triggerMonitorService.Dispose();
        _regionCaptureMonitorService.Dispose();
        _skillRegionCaptureMonitorService.Dispose();
        _healthRegionCaptureMonitorService.Dispose();
        _deathTextRegionCaptureMonitorService.Dispose();
        _textTriggerTimer.Stop();
        _textTriggerTimer.Tick -= TextTriggerTimer_Tick;
        _textTriggerTimer.Dispose();
        _deathTriggerTimer.Stop();
        _deathTriggerTimer.Tick -= DeathTriggerTimer_Tick;
        _deathTriggerTimer.Dispose();
        _inputCaptureService.InputPressed -= InputCaptureService_InputPressed;
        _inputCaptureService.Dispose();
        _healthBaselineService.Dispose();
        _templateMatcher.Dispose();
        DisposeAudio();
        _shutdownCts.Dispose();
    }

    private sealed record CapturePreviewResult(
        HealthChangeWatchRegion HealthRegion,
        SkillReadyWatchRegion SkillRegion,
        MarkerPoint HealthMarker,
        MarkerPoint SkillMarker);

    private sealed record AudioOutputDeviceItem(int DeviceNumber, string ProductName, string DisplayName)
    {
        public override string ToString()
        {
            return DisplayName;
        }
    }

    private sealed record TextRecognitionResult(bool Success, string Text, string ErrorMessage);
}
