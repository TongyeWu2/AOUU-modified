using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using NAudio.Wave;
using WinFormsApp1.Models;
using WinFormsApp1.Services;
using WinFormsApp1.UI;

namespace WinFormsApp1;

public partial class Form1 : Form
{
    private enum KeyConfigurationTarget
    {
        None,
        Trigger,
        RegionCapture
    }

    private enum RegionSettingsMode
    {
        Skill,
        Health
    }

    private readonly TextBox _audioPathBox;
    private readonly Button _browseButton;
    private readonly Button _setTriggerKeyButton;
    private readonly Button _setRegionCaptureKeyButton;
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
    private readonly TriggerMonitorService _triggerMonitorService;
    private readonly TriggerMonitorService _regionCaptureMonitorService;
    private readonly InputCaptureService _inputCaptureService;
    private readonly ConfigService _configService;
    private readonly ScreenCaptureService _screenCaptureService;
    private readonly TemplateMatcher _templateMatcher;
    private readonly CircleLocatorService _circleLocatorService;
    private readonly HealthBarAnalyzerService _healthBarAnalyzerService;
    private readonly RegionChangeDetector _regionChangeDetector;
    private readonly HealthBaselineService _healthBaselineService;
    private readonly CancellationTokenSource _shutdownCts = new();
    private readonly object _audioLock = new();

    private AppConfig _config;
    private IWavePlayer? _outputDevice;
    private AudioFileReader? _audioFile;
    private bool _isPlaying;
    private bool _isConfiguringKey;
    private bool _isRecognitionRunning;
    private bool _isRegionCaptureRunning;
    private KeyConfigurationTarget _preparedKeyConfigurationTarget;

    public Form1()
    {
        InitializeComponent();

        Text = "┗|｀O′|┛ 嗷~~";
        Width = 920;
        Height = 690;
        StartPosition = FormStartPosition.CenterScreen;
        MaximizeBox = false;
        Text = "┗|｀O′|┛ 嗷~~";

        _configService = new ConfigService(AppDomain.CurrentDomain.BaseDirectory);
        _inputCaptureService = new InputCaptureService();
        _inputCaptureService.Start();
        _screenCaptureService = new ScreenCaptureService();
        _templateMatcher = new TemplateMatcher();
        _circleLocatorService = new CircleLocatorService();
        _healthBarAnalyzerService = new HealthBarAnalyzerService();
        _regionChangeDetector = new RegionChangeDetector();
        _healthBaselineService = new HealthBaselineService(
            _screenCaptureService,
            _templateMatcher,
            _healthBarAnalyzerService,
            _circleLocatorService);

        _triggerMonitorService = new TriggerMonitorService();
        _triggerMonitorService.Triggered += TriggerMonitorService_Triggered;

        _regionCaptureMonitorService = new TriggerMonitorService();
        _regionCaptureMonitorService.Triggered += RegionCaptureMonitorService_Triggered;

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
        _audioVolumeBar.Scroll += AudioVolumeBar_Scroll;

        _audioVolumeValueLabel = new Label
        {
            Left = 776,
            Top = 224,
            Width = 108,
            Height = 28
        };

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
            Top = 310,
            Width = 860,
            Height = 230,
            HorizontalScrollbar = true
        };

        _statusLabel = new Label
        {
            Left = 24,
            Top = 580,
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
        Controls.Add(_setTriggerKeyButton);
        Controls.Add(_setRegionCaptureKeyButton);
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
            Left = 24,
            Top = 288,
            Width = 260,
            Text = "当前检测区域"
        });
        Controls.Add(_regionsListBox);
        Controls.Add(new Label
        {
            Left = 24,
            Top = 550,
            Width = 860,
            Text = "点击配置键位后会弹出识别框，只有确认后的结果才会保存，支持键盘、鼠标侧键和手柄按钮，鼠标左键不会被接受。"
        });
        Controls.Add(_statusLabel);

        _config = _configService.Load();

        ApplyConfigToUi();
        LoadAudio();
        RefreshRegionList();
        UpdateStatus();
        SaveConfig();

        FormClosing += Form1_FormClosing;
        FormClosed += Form1_FormClosed;

        _triggerMonitorService.Enabled = true;
        _regionCaptureMonitorService.Enabled = true;
    }

    private void BrowseButton_Click(object? sender, EventArgs e)
    {
        using var dialog = new OpenFileDialog();
        dialog.Filter = "音频文件|*.mp3;*.wav";

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        _config.AudioPath = dialog.FileName;
        SaveConfig();
        LoadAudio();
        UpdateAudioDisplay();
        UpdateStatus();
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

        var currentKeyName = target == KeyConfigurationTarget.Trigger
            ? _config.TriggerKeyName
            : _config.RegionCaptureKeyName;

        using var dialog = new KeyCaptureDialog(title, currentKeyName, _inputCaptureService);
        var result = dialog.ShowDialog(this);

        if (result == DialogResult.OK && dialog.CapturedKeyCode.HasValue)
        {
            ApplyCapturedHotkey(
                target,
                dialog.CapturedKeyCode.Value,
                dialog.CapturedKeyName ?? TriggerMonitorService.GetKeyName(dialog.CapturedKeyCode.Value));
            SetStatus($"{title}已设置为：{dialog.CapturedKeyName}");
        }
        else
        {
            SetStatus($"已取消设置{title}。");
        }

        _preparedKeyConfigurationTarget = KeyConfigurationTarget.None;
        _isConfiguringKey = false;
        _triggerMonitorService.Enabled = true;
        _regionCaptureMonitorService.Enabled = true;
        UpdateStatus();
    }

    private void PrepareKeyConfiguration(KeyConfigurationTarget target)
    {
        _isConfiguringKey = true;
        _preparedKeyConfigurationTarget = target;
        _triggerMonitorService.Enabled = false;
        _regionCaptureMonitorService.Enabled = false;
        SetStatus("按键识别框已打开。先松开鼠标，再在识别框内按下新的键位。");
    }

    private void ApplyCapturedHotkey(KeyConfigurationTarget target, int keyCode, string keyName)
    {
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

        SaveConfig();
    }

    private void TimingBox_ValueChanged(object? sender, EventArgs e)
    {
        _config.WatchWindowMs = (int)_watchWindowBox.Value;
        _config.PollIntervalMs = (int)_pollIntervalBox.Value;

        SaveConfig();
        UpdateStatus();
    }

    private void HealthConsecutiveFramesBox_ValueChanged(object? sender, EventArgs e)
    {
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

    private void AudioVolumeBar_Scroll(object? sender, EventArgs e)
    {
        _config.AudioVolume = _audioVolumeBar.Value / 100f;
        UpdateVolumeDisplay();
        ApplyAudioVolume();
        SaveConfig();
        UpdateStatus();
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

        if (!File.Exists(_config.AudioPath))
        {
            SetStatus("未加载音频，请先配置一个音频文件。");
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

        if (!File.Exists(_config.AudioPath))
        {
            return;
        }

        try
        {
            _audioFile = new AudioFileReader(_config.AudioPath);
            _audioFile.Volume = Math.Clamp(_config.AudioVolume, 0f, 1f);
            _outputDevice = new WaveOutEvent();
            _outputDevice.PlaybackStopped += OutputDevice_PlaybackStopped;
            _outputDevice.Init(_audioFile);
        }
        catch
        {
            DisposeAudio();
            SetStatus("音频加载失败，请检查文件格式或路径。");
        }
    }

    private void PlayAudio()
    {
        lock (_audioLock)
        {
            if (_audioFile is null || _outputDevice is null || _isPlaying)
            {
                return;
            }

            _audioFile.Position = 0;
            _audioFile.Volume = Math.Clamp(_config.AudioVolume, 0f, 1f);
            _outputDevice.Play();
            _isPlaying = true;
        }
    }

    private void OutputDevice_PlaybackStopped(object? sender, StoppedEventArgs e)
    {
        lock (_audioLock)
        {
            _isPlaying = false;

            if (_audioFile is not null)
            {
                _audioFile.Position = 0;
            }
        }
    }

    private void DisposeAudio()
    {
        lock (_audioLock)
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
    }

    private void ApplyConfigToUi()
    {
        UpdateAudioDisplay();
        _watchWindowBox.Value = Math.Clamp(_config.WatchWindowMs, (int)_watchWindowBox.Minimum, (int)_watchWindowBox.Maximum);
        _pollIntervalBox.Value = Math.Clamp(_config.PollIntervalMs, (int)_pollIntervalBox.Minimum, (int)_pollIntervalBox.Maximum);
        _healthConsecutiveFramesBox.Value = Math.Clamp(_config.HealthConsecutiveFramesRequired, (int)_healthConsecutiveFramesBox.Minimum, (int)_healthConsecutiveFramesBox.Maximum);
        _healthThresholdBar.Value = Math.Clamp(_config.HealthGrowthPixelThreshold, _healthThresholdBar.Minimum, _healthThresholdBar.Maximum);
        _audioVolumeBar.Value = Math.Clamp((int)Math.Round(_config.AudioVolume * 100f), _audioVolumeBar.Minimum, _audioVolumeBar.Maximum);
        UpdateThresholdDisplay();
        UpdateVolumeDisplay();
        ApplyAudioVolume();
        _triggerMonitorService.TriggerKey = _config.TriggerKey;
        _regionCaptureMonitorService.TriggerKey = _config.RegionCaptureKey;
    }

    private void RefreshRegionList()
    {
        _regionsListBox.BeginUpdate();
        _regionsListBox.Items.Clear();

        foreach (var region in _config.Regions.OrderBy(region => region.Name))
        {
            _regionsListBox.Items.Add(region);
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
        _configService.Save(_config);
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

    private void UpdateStatus()
    {
        var audioState = File.Exists(_config.AudioPath) ? "已加载音频" : "未选择音频";
        var regionCount = _config.Regions.Count;
        _statusLabel.Text =
            $"{audioState}。技能触发键：{_config.TriggerKeyName}。截图键：{_config.RegionCaptureKeyName}。检测区域数量：{regionCount}。";
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

    private void Form1_FormClosing(object? sender, FormClosingEventArgs e)
    {
        _shutdownCts.Cancel();
        SaveConfig();
    }

    private void Form1_FormClosed(object? sender, FormClosedEventArgs e)
    {
        _triggerMonitorService.Dispose();
        _regionCaptureMonitorService.Dispose();
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
}
