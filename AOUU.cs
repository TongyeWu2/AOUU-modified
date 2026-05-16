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
        OcrTextRegionCapture,
        ImageHotkeyTrigger,
        KeyAudioTrigger1,
        KeyAudioTrigger2,
        KeyAudioTrigger3
    }

    private enum RegionSettingsMode
    {
        Skill,
        Health
    }

    private readonly CheckBox _ultHotkeyTriggerEnabledBox;
    private readonly Button _setUltHotkeyRegionButton;
    private readonly TextBox _ultHotkeyRegionBox;
    private readonly ListBox _ultHotkeySkillsListBox;
    private readonly Button _addUltHotkeySkillButton;
    private readonly Button _deleteUltHotkeySkillButton;
    private readonly TextBox _ultHotkeySkillNameBox;
    private readonly TextBox _ultHotkeyTemplatePathBox;
    private readonly Button _browseUltHotkeyTemplateButton;
    private readonly NumericUpDown _ultHotkeySimilarityBox;
    private readonly TextBox _ultHotkeyAudioPathBox;
    private readonly Button _browseUltHotkeyAudioButton;
    private readonly NumericUpDown _ultHotkeyScanIntervalBox;
    private readonly NumericUpDown _ultHotkeyCooldownBox;
    private readonly Button _setTriggerKeyButton;
    private readonly Button _setRegionCaptureKeyButton;
    private readonly Button _setSkillRegionCaptureKeyButton;
    private readonly Button _setHealthRegionCaptureKeyButton;
    private readonly Button _setOcrTextRegionCaptureKeyButton;
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
    private readonly CheckBox _textTriggerEnabledBox;
    private readonly TextBox _textTriggerTextBox;
    private readonly TextBox _textTriggerMusicPathBox;
    private readonly Button _browseTextTriggerMusicButton;
    private readonly Button _setOcrTextRegionButton;
    private readonly NumericUpDown _textTriggerScanIntervalBox;
    private readonly NumericUpDown _textTriggerCooldownBox;
    private readonly CheckBox _imageHotkeyTriggerEnabledBox;
    private readonly Button _setImageHotkeyRegionButton;
    private readonly TextBox _imageHotkeyRegionBox;
    private readonly ListBox _imageHotkeySkillsListBox;
    private readonly Button _addImageHotkeySkillButton;
    private readonly Button _deleteImageHotkeySkillButton;
    private readonly TextBox _imageHotkeySkillNameBox;
    private readonly TextBox _imageHotkeyTemplatePathBox;
    private readonly Button _browseImageHotkeyTemplateButton;
    private readonly NumericUpDown _imageHotkeySimilarityBox;
    private readonly Button _setImageHotkeyButton;
    private readonly TextBox _imageHotkeyAudioPathBox;
    private readonly Button _browseImageHotkeyAudioButton;
    private readonly NumericUpDown _imageHotkeyScanIntervalBox;
    private readonly NumericUpDown _imageHotkeyCooldownBox;
    private readonly CheckBox _keyAudioTriggerEnabledBox;
    private readonly NumericUpDown _keyAudioCooldownBox;
    private readonly Button _setKeyAudioKey1Button;
    private readonly TextBox _keyAudioPath1Box;
    private readonly Button _browseKeyAudio1Button;
    private readonly Button _setKeyAudioKey2Button;
    private readonly TextBox _keyAudioPath2Box;
    private readonly Button _browseKeyAudio2Button;
    private readonly Button _setKeyAudioKey3Button;
    private readonly TextBox _keyAudioPath3Box;
    private readonly Button _browseKeyAudio3Button;
    private readonly TriggerMonitorService _triggerMonitorService;
    private readonly TriggerMonitorService _regionCaptureMonitorService;
    private readonly TriggerMonitorService _skillRegionCaptureMonitorService;
    private readonly TriggerMonitorService _healthRegionCaptureMonitorService;
    private readonly TriggerMonitorService _ocrTextRegionCaptureMonitorService;
    private readonly InputCaptureService _inputCaptureService;
    private readonly ConfigService _configService;
    private readonly ScreenCaptureService _screenCaptureService;
    private readonly TemplateMatcher _templateMatcher;
    private readonly CircleLocatorService _circleLocatorService;
    private readonly HealthBarAnalyzerService _healthBarAnalyzerService;
    private readonly RegionChangeDetector _regionChangeDetector;
    private readonly HealthBaselineService _healthBaselineService;
    private readonly ScreenTextRecognizer _screenTextRecognizer;
    private readonly System.Windows.Forms.Timer _textTriggerTimer;
    private readonly System.Windows.Forms.Timer _ultHotkeyScanTimer;
    private readonly System.Windows.Forms.Timer _imageHotkeyScanTimer;
    private readonly Dictionary<string, DateTime> _lastTextTriggerUtc = [];
    private readonly CancellationTokenSource _shutdownCts = new();
    private readonly object _audioLock = new();
    private readonly List<OneShotAudioPlayback> _oneShotAudioPlaybacks = [];
    private static readonly string[] SupportedAudioExtensions = [".mp3", ".wav"];

    private readonly Random _audioRandom = new();

    private AppConfig _config;
    private IWavePlayer? _outputDevice;
    private AudioFileReader? _audioFile;
    private bool _isPlaying;
    private bool _isConfiguringKey;
    private bool _isRecognitionRunning;
    private bool _isRegionCaptureRunning;
    private bool _isApplyingConfigToUi;
    private bool _isTextScanRunning;
    private bool _isUltHotkeyScanRunning;
    private bool _ultHotkeyMatched;
    private ImageHotkeySkillConfig? _matchedUltHotkeySkill;
    private double _lastUltHotkeyScore;
    private DateTime _lastUltHotkeyTriggerUtc = DateTime.MinValue;
    private bool _isImageHotkeyScanRunning;
    private bool _imageHotkeyMatched;
    private ImageHotkeySkillConfig? _matchedImageHotkeySkill;
    private double _lastImageHotkeyScore;
    private DateTime _lastImageHotkeyTriggerUtc = DateTime.MinValue;
    private DateTime _lastKeyAudioTrigger1Utc = DateTime.MinValue;
    private DateTime _lastKeyAudioTrigger2Utc = DateTime.MinValue;
    private DateTime _lastKeyAudioTrigger3Utc = DateTime.MinValue;
    private KeyConfigurationTarget _preparedKeyConfigurationTarget;

    public AOUU()
    {
        InitializeComponent();

        Text = "┗|｀O′|┛ 嗷~~";
        Width = 1120;
        Height = 1220;
        MinimumSize = new Size(1040, 760);
        AutoScroll = true;
        StartPosition = FormStartPosition.CenterScreen;
        MaximizeBox = false;
        Text = "┗|｀O′|┛ 嗷~~";
        _isRecognitionRunning = false;

        _configService = new ConfigService(AppDomain.CurrentDomain.BaseDirectory);
        _inputCaptureService = new InputCaptureService();
        _inputCaptureService.InputBindingPressed += InputCaptureService_InputBindingPressed;
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
        _triggerMonitorService = new TriggerMonitorService();
        _triggerMonitorService.Triggered += TriggerMonitorService_Triggered;

        _regionCaptureMonitorService = new TriggerMonitorService();
        _regionCaptureMonitorService.Triggered += RegionCaptureMonitorService_Triggered;

        _skillRegionCaptureMonitorService = new TriggerMonitorService();
        _skillRegionCaptureMonitorService.Triggered += SkillRegionCaptureMonitorService_Triggered;

        _healthRegionCaptureMonitorService = new TriggerMonitorService();
        _healthRegionCaptureMonitorService.Triggered += HealthRegionCaptureMonitorService_Triggered;

        _ocrTextRegionCaptureMonitorService = new TriggerMonitorService();
        _ocrTextRegionCaptureMonitorService.Triggered += OcrTextRegionCaptureMonitorService_Triggered;

        _textTriggerTimer = new System.Windows.Forms.Timer
        {
            Interval = 500
        };
        _textTriggerTimer.Tick += TextTriggerTimer_Tick;

        _ultHotkeyScanTimer = new System.Windows.Forms.Timer
        {
            Interval = 200
        };
        _ultHotkeyScanTimer.Tick += UltHotkeyScanTimer_Tick;

        _imageHotkeyScanTimer = new System.Windows.Forms.Timer
        {
            Interval = 200
        };
        _imageHotkeyScanTimer.Tick += ImageHotkeyScanTimer_Tick;

        _ultHotkeyTriggerEnabledBox = new CheckBox
        {
            Left = 24,
            Top = 16,
            Width = 120,
            Text = "大招音效"
        };
        _ultHotkeyTriggerEnabledBox.CheckedChanged += UltHotkeyTriggerSettings_Changed;

        _setUltHotkeyRegionButton = new Button
        {
            Left = 150,
            Top = 12,
            Width = 120,
            Text = "设置大招区域"
        };
        _setUltHotkeyRegionButton.Click += (_, _) => ConfigureUltHotkeyRegion();

        _ultHotkeyRegionBox = new TextBox
        {
            Left = 280,
            Top = 14,
            Width = 250,
            ReadOnly = true,
            TabStop = false
        };

        _addUltHotkeySkillButton = new Button
        {
            Left = 540,
            Top = 12,
            Width = 100,
            Text = "新增大招"
        };
        _addUltHotkeySkillButton.Click += AddUltHotkeySkillButton_Click;

        _deleteUltHotkeySkillButton = new Button
        {
            Left = 648,
            Top = 12,
            Width = 100,
            Text = "删除大招"
        };
        _deleteUltHotkeySkillButton.Click += DeleteUltHotkeySkillButton_Click;

        _setTriggerKeyButton = new Button
        {
            Left = 756,
            Top = 12,
            Width = 128,
            Text = "大招按键"
        };
        _setTriggerKeyButton.Click += SetTriggerKeyButton_Click;

        _ultHotkeySkillsListBox = new ListBox
        {
            Left = 24,
            Top = 52,
            Width = 210,
            Height = 108,
            HorizontalScrollbar = true
        };
        _ultHotkeySkillsListBox.SelectedIndexChanged += UltHotkeySkillsListBox_SelectedIndexChanged;

        _ultHotkeySkillNameBox = new TextBox
        {
            Left = 310,
            Top = 52,
            Width = 176
        };
        _ultHotkeySkillNameBox.TextChanged += UltHotkeySkillNameBox_TextChanged;
        _ultHotkeySkillNameBox.Leave += UltHotkeySkillNameBox_Leave;

        _browseUltHotkeyTemplateButton = new Button
        {
            Left = 504,
            Top = 50,
            Width = 110,
            Text = "选择大招"
        };
        _browseUltHotkeyTemplateButton.Click += BrowseUltHotkeyTemplateButton_Click;

        _ultHotkeyTemplatePathBox = new TextBox
        {
            Left = 622,
            Top = 52,
            Width = 262,
            ReadOnly = true,
            TabStop = false
        };

        _ultHotkeySimilarityBox = new NumericUpDown
        {
            Left = 336,
            Top = 90,
            Width = 70,
            Minimum = 0.10M,
            Maximum = 1.00M,
            Increment = 0.05M,
            DecimalPlaces = 2
        };
        _ultHotkeySimilarityBox.ValueChanged += UltHotkeySkillSettings_Changed;

        _browseUltHotkeyAudioButton = new Button
        {
            Left = 424,
            Top = 88,
            Width = 110,
            Text = "选择音效"
        };
        _browseUltHotkeyAudioButton.Click += BrowseUltHotkeyAudioButton_Click;

        _ultHotkeyAudioPathBox = new TextBox
        {
            Left = 542,
            Top = 90,
            Width = 220,
            ReadOnly = true,
            TabStop = false
        };

        _ultHotkeyScanIntervalBox = new NumericUpDown
        {
            Left = 748,
            Top = 90,
            Width = 70,
            Minimum = 100,
            Maximum = 10000,
            Increment = 100
        };
        _ultHotkeyScanIntervalBox.ValueChanged += UltHotkeyTriggerSettings_Changed;

        _ultHotkeyCooldownBox = new NumericUpDown
        {
            Left = 830,
            Top = 90,
            Width = 54,
            Minimum = 1,
            Maximum = 3600,
            Increment = 1
        };
        _ultHotkeyCooldownBox.ValueChanged += UltHotkeyTriggerSettings_Changed;

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

        _setOcrTextRegionCaptureKeyButton = new Button
        {
            Left = 612,
            Top = 90,
            Width = 180
        };
        _setOcrTextRegionCaptureKeyButton.Click += (_, _) => ConfigureHotkey(KeyConfigurationTarget.OcrTextRegionCapture, "OCR文字区域快捷键");

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

        _textTriggerEnabledBox = new CheckBox
        {
            Left = 24,
            Top = 348,
            Width = 130,
            Text = "OCR文字触发"
        };
        _textTriggerEnabledBox.CheckedChanged += TextTriggerSettings_Changed;

        _setOcrTextRegionButton = new Button
        {
            Left = 160,
            Top = 344,
            Width = 140,
            Text = "设置OCR文字区域"
        };
        _setOcrTextRegionButton.Click += (_, _) => ConfigureOcrTextRegion();

        _textTriggerTextBox = new TextBox
        {
            Left = 374,
            Top = 346,
            Width = 120
        };
        _textTriggerTextBox.TextChanged += TextTriggerSettings_Changed;

        _textTriggerMusicPathBox = new TextBox
        {
            Left = 160,
            Top = 386,
            Width = 334,
            ReadOnly = true,
            TabStop = false
        };

        _browseTextTriggerMusicButton = new Button
        {
            Left = 504,
            Top = 384,
            Width = 110,
            Text = "触发音频"
        };
        _browseTextTriggerMusicButton.Click += BrowseTextTriggerMusicButton_Click;

        _textTriggerScanIntervalBox = new NumericUpDown
        {
            Left = 700,
            Top = 386,
            Width = 70,
            Minimum = 100,
            Maximum = 10000,
            Increment = 100
        };
        _textTriggerScanIntervalBox.ValueChanged += TextTriggerSettings_Changed;

        _textTriggerCooldownBox = new NumericUpDown
        {
            Left = 810,
            Top = 386,
            Width = 74,
            Minimum = 1,
            Maximum = 3600,
            Increment = 1
        };
        _textTriggerCooldownBox.ValueChanged += TextTriggerSettings_Changed;

        _imageHotkeyTriggerEnabledBox = new CheckBox
        {
            Left = 24,
            Top = 424,
            Width = 160,
            Text = "战技音效"
        };
        _imageHotkeyTriggerEnabledBox.CheckedChanged += ImageHotkeyTriggerSettings_Changed;

        _setImageHotkeyRegionButton = new Button
        {
            Left = 196,
            Top = 420,
            Width = 130,
            Text = "设置战技区域"
        };
        _setImageHotkeyRegionButton.Click += (_, _) => ConfigureImageHotkeyRegion();

        _imageHotkeyRegionBox = new TextBox
        {
            Left = 336,
            Top = 422,
            Width = 230,
            ReadOnly = true,
            TabStop = false
        };

        _addImageHotkeySkillButton = new Button
        {
            Left = 576,
            Top = 420,
            Width = 110,
            Text = "新增战技"
        };
        _addImageHotkeySkillButton.Click += AddImageHotkeySkillButton_Click;

        _deleteImageHotkeySkillButton = new Button
        {
            Left = 696,
            Top = 420,
            Width = 110,
            Text = "删除战技"
        };
        _deleteImageHotkeySkillButton.Click += DeleteImageHotkeySkillButton_Click;

        _setImageHotkeyButton = new Button
        {
            Left = 816,
            Top = 420,
            Width = 68
        };
        _setImageHotkeyButton.Click += (_, _) => ConfigureHotkey(KeyConfigurationTarget.ImageHotkeyTrigger, "战技触发按键");

        _imageHotkeySkillsListBox = new ListBox
        {
            Left = 24,
            Top = 464,
            Width = 210,
            Height = 108,
            HorizontalScrollbar = true
        };
        _imageHotkeySkillsListBox.SelectedIndexChanged += ImageHotkeySkillsListBox_SelectedIndexChanged;

        _imageHotkeySkillNameBox = new TextBox
        {
            Left = 310,
            Top = 464,
            Width = 176
        };
        _imageHotkeySkillNameBox.TextChanged += ImageHotkeySkillNameBox_TextChanged;
        _imageHotkeySkillNameBox.Leave += ImageHotkeySkillNameBox_Leave;

        _browseImageHotkeyTemplateButton = new Button
        {
            Left = 504,
            Top = 462,
            Width = 110,
            Text = "选择战技"
        };
        _browseImageHotkeyTemplateButton.Click += BrowseImageHotkeyTemplateButton_Click;

        _imageHotkeyTemplatePathBox = new TextBox
        {
            Left = 622,
            Top = 464,
            Width = 262,
            ReadOnly = true,
            TabStop = false
        };

        _imageHotkeySimilarityBox = new NumericUpDown
        {
            Left = 336,
            Top = 502,
            Width = 70,
            Minimum = 0.10M,
            Maximum = 1.00M,
            Increment = 0.05M,
            DecimalPlaces = 2
        };
        _imageHotkeySimilarityBox.ValueChanged += ImageHotkeySkillSettings_Changed;

        _imageHotkeyAudioPathBox = new TextBox
        {
            Left = 542,
            Top = 502,
            Width = 220,
            ReadOnly = true,
            TabStop = false
        };

        _browseImageHotkeyAudioButton = new Button
        {
            Left = 424,
            Top = 500,
            Width = 110,
            Text = "选择音效"
        };
        _browseImageHotkeyAudioButton.Click += BrowseImageHotkeyAudioButton_Click;

        _imageHotkeyScanIntervalBox = new NumericUpDown
        {
            Left = 748,
            Top = 502,
            Width = 70,
            Minimum = 100,
            Maximum = 10000,
            Increment = 100
        };
        _imageHotkeyScanIntervalBox.ValueChanged += ImageHotkeyTriggerSettings_Changed;

        _imageHotkeyCooldownBox = new NumericUpDown
        {
            Left = 830,
            Top = 502,
            Width = 54,
            Minimum = 1,
            Maximum = 3600,
            Increment = 1
        };
        _imageHotkeyCooldownBox.ValueChanged += ImageHotkeyTriggerSettings_Changed;

        _keyAudioTriggerEnabledBox = new CheckBox
        {
            Text = "启用按键音效"
        };
        _keyAudioTriggerEnabledBox.CheckedChanged += KeyAudioTriggerSettings_Changed;

        _keyAudioCooldownBox = new NumericUpDown
        {
            Minimum = 1,
            Maximum = 3600,
            Increment = 1,
            Value = 1
        };
        _keyAudioCooldownBox.ValueChanged += KeyAudioTriggerSettings_Changed;

        _setKeyAudioKey1Button = new Button
        {
            Text = "按键1：1"
        };
        _setKeyAudioKey1Button.Click += (_, _) => ConfigureHotkey(KeyConfigurationTarget.KeyAudioTrigger1, "按键音效 1");

        _keyAudioPath1Box = new TextBox
        {
            ReadOnly = true,
            TabStop = false
        };

        _browseKeyAudio1Button = new Button
        {
            Text = "音频1"
        };
        _browseKeyAudio1Button.Click += (_, _) => BrowseKeyAudioButton_Click(1);

        _setKeyAudioKey2Button = new Button
        {
            Text = "按键2：2"
        };
        _setKeyAudioKey2Button.Click += (_, _) => ConfigureHotkey(KeyConfigurationTarget.KeyAudioTrigger2, "按键音效 2");

        _keyAudioPath2Box = new TextBox
        {
            ReadOnly = true,
            TabStop = false
        };

        _browseKeyAudio2Button = new Button
        {
            Text = "音频2"
        };
        _browseKeyAudio2Button.Click += (_, _) => BrowseKeyAudioButton_Click(2);

        _setKeyAudioKey3Button = new Button
        {
            Text = "按键3：3"
        };
        _setKeyAudioKey3Button.Click += (_, _) => ConfigureHotkey(KeyConfigurationTarget.KeyAudioTrigger3, "按键音效 3");

        _keyAudioPath3Box = new TextBox
        {
            ReadOnly = true,
            TabStop = false
        };

        _browseKeyAudio3Button = new Button
        {
            Text = "音频3"
        };
        _browseKeyAudio3Button.Click += (_, _) => BrowseKeyAudioButton_Click(3);

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
            Top = 938,
            Width = 1036,
            Height = 130,
            HorizontalScrollbar = true
        };

        _statusLabel = new Label
        {
            Left = 24,
            Top = 1126,
            Width = 1036,
            Height = 60
        };

        Controls.Add(CreateImageHotkeySection(
            "大招音效",
            top: 16,
            _ultHotkeyTriggerEnabledBox,
            _setUltHotkeyRegionButton,
            _ultHotkeyRegionBox,
            _addUltHotkeySkillButton,
            _deleteUltHotkeySkillButton,
            _setTriggerKeyButton,
            _ultHotkeySkillsListBox,
            _ultHotkeySkillNameBox,
            _browseUltHotkeyTemplateButton,
            _ultHotkeyTemplatePathBox,
            _ultHotkeySimilarityBox,
            _browseUltHotkeyAudioButton,
            _ultHotkeyAudioPathBox,
            _ultHotkeyScanIntervalBox,
            _ultHotkeyCooldownBox));
        Controls.Add(CreateImageHotkeySection(
            "战技音效",
            top: 238,
            _imageHotkeyTriggerEnabledBox,
            _setImageHotkeyRegionButton,
            _imageHotkeyRegionBox,
            _addImageHotkeySkillButton,
            _deleteImageHotkeySkillButton,
            _setImageHotkeyButton,
            _imageHotkeySkillsListBox,
            _imageHotkeySkillNameBox,
            _browseImageHotkeyTemplateButton,
            _imageHotkeyTemplatePathBox,
            _imageHotkeySimilarityBox,
            _browseImageHotkeyAudioButton,
            _imageHotkeyAudioPathBox,
            _imageHotkeyScanIntervalBox,
            _imageHotkeyCooldownBox));
        Controls.Add(CreateOcrTextTriggerSection(top: 460));
        Controls.Add(CreateKeyAudioTriggerSection(top: 596));
        Controls.Add(CreateGeneralSettingsSection(top: 774));
        Controls.Add(new Label
        {
            Left = 24,
            Top = 908,
            Width = 260,
            Text = "当前检测区域"
        });
        Controls.Add(_regionsListBox);
        Controls.Add(new Label
        {
            Left = 24,
            Top = 1094,
            Width = 860,
            Text = "点击配置键位后会弹出识别框，只有确认后的结果才会保存，支持键盘、鼠标侧键和手柄按钮，鼠标左键不会被接受。"
        });
        Controls.Add(_statusLabel);

        AttachEnterConfirmationHandlers();

        _config = _configService.Load();

        RefreshAudioOutputDevices();
        ApplyConfigToUi();
        RefreshRegionList();
        UpdateStatus();

        FormClosing += AOUU_FormClosing;
        FormClosed += AOUU_FormClosed;

        _triggerMonitorService.Enabled = true;
        _regionCaptureMonitorService.Enabled = true;
        SetRegionCaptureHotkeyMonitorsEnabled(true);
        SyncTextTriggerTimer();
        SyncUltHotkeyScanTimer();
        SyncImageHotkeyScanTimer();
    }

    private GroupBox CreateImageHotkeySection(
        string title,
        int top,
        CheckBox enabledBox,
        Button regionButton,
        TextBox regionBox,
        Button addButton,
        Button deleteButton,
        Button hotkeyButton,
        ListBox entriesListBox,
        TextBox nameBox,
        Button templateButton,
        TextBox templatePathBox,
        NumericUpDown thresholdBox,
        Button audioButton,
        TextBox audioPathBox,
        NumericUpDown scanIntervalBox,
        NumericUpDown cooldownBox)
    {
        var groupBox = new GroupBox
        {
            Left = 24,
            Top = top,
            Width = 1036,
            Height = 206,
            Text = title,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            Tag = "PinnedLayout"
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12, 14, 12, 12),
            ColumnCount = 10,
            RowCount = 4
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 210));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 108));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 108));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 82));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 116));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 76));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 84));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 76));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 84));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        PrepareSectionControl(enabledBox);
        PrepareSectionControl(regionButton);
        PrepareSectionControl(regionBox);
        PrepareSectionControl(addButton);
        PrepareSectionControl(deleteButton);
        PrepareSectionControl(hotkeyButton);
        PrepareSectionControl(entriesListBox);
        PrepareSectionControl(nameBox);
        PrepareSectionControl(templateButton);
        PrepareSectionControl(templatePathBox);
        PrepareSectionControl(thresholdBox);
        PrepareSectionControl(audioButton);
        PrepareSectionControl(audioPathBox);
        PrepareSectionControl(scanIntervalBox);
        PrepareSectionControl(cooldownBox);

        layout.Controls.Add(enabledBox, 0, 0);
        layout.Controls.Add(regionButton, 1, 0);
        layout.Controls.Add(regionBox, 2, 0);
        layout.SetColumnSpan(regionBox, 3);
        layout.Controls.Add(addButton, 5, 0);
        layout.Controls.Add(deleteButton, 6, 0);
        layout.Controls.Add(hotkeyButton, 7, 0);
        layout.SetColumnSpan(hotkeyButton, 3);

        layout.Controls.Add(entriesListBox, 0, 1);
        layout.SetRowSpan(entriesListBox, 3);

        layout.Controls.Add(CreateSectionLabel("名称"), 1, 1);
        layout.Controls.Add(nameBox, 2, 1);
        layout.SetColumnSpan(nameBox, 2);
        layout.Controls.Add(templateButton, 4, 1);
        layout.Controls.Add(templatePathBox, 5, 1);
        layout.SetColumnSpan(templatePathBox, 4);

        layout.Controls.Add(CreateSectionLabel("匹配阈值"), 1, 2);
        layout.Controls.Add(thresholdBox, 2, 2);
        layout.Controls.Add(audioButton, 4, 2);
        layout.Controls.Add(audioPathBox, 5, 2);
        layout.Controls.Add(CreateSectionLabel("扫描ms"), 6, 2);
        layout.Controls.Add(scanIntervalBox, 7, 2);
        layout.Controls.Add(CreateSectionLabel("冷却秒"), 8, 2);
        layout.Controls.Add(cooldownBox, 9, 2);

        groupBox.Controls.Add(layout);
        return groupBox;
    }

    private GroupBox CreateOcrTextTriggerSection(int top)
    {
        var groupBox = new GroupBox
        {
            Left = 24,
            Top = top,
            Width = 1036,
            Height = 118,
            Text = "OCR文字触发 / 死亡检测",
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            Tag = "PinnedLayout"
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12, 14, 12, 12),
            ColumnCount = 8,
            RowCount = 2
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 132));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 148));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 116));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 88));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 76));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 88));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));

        PrepareSectionControl(_textTriggerEnabledBox);
        PrepareSectionControl(_setOcrTextRegionButton);
        PrepareSectionControl(_setOcrTextRegionCaptureKeyButton);
        PrepareSectionControl(_textTriggerTextBox);
        PrepareSectionControl(_textTriggerMusicPathBox);
        PrepareSectionControl(_browseTextTriggerMusicButton);
        PrepareSectionControl(_textTriggerScanIntervalBox);
        PrepareSectionControl(_textTriggerCooldownBox);

        layout.Controls.Add(_textTriggerEnabledBox, 0, 0);
        layout.Controls.Add(_setOcrTextRegionButton, 1, 0);
        layout.Controls.Add(_setOcrTextRegionCaptureKeyButton, 2, 0);
        layout.SetColumnSpan(_setOcrTextRegionCaptureKeyButton, 2);
        layout.Controls.Add(CreateSectionLabel("目标文字"), 4, 0);
        layout.Controls.Add(_textTriggerTextBox, 5, 0);
        layout.SetColumnSpan(_textTriggerTextBox, 3);

        layout.Controls.Add(CreateSectionLabel("触发音频"), 0, 1);
        layout.Controls.Add(_textTriggerMusicPathBox, 1, 1);
        layout.SetColumnSpan(_textTriggerMusicPathBox, 3);
        layout.Controls.Add(_browseTextTriggerMusicButton, 4, 1);
        layout.Controls.Add(CreateSectionLabel("扫描ms"), 5, 1);
        layout.Controls.Add(_textTriggerScanIntervalBox, 6, 1);
        layout.Controls.Add(_textTriggerCooldownBox, 7, 1);

        groupBox.Controls.Add(layout);
        return groupBox;
    }

    private GroupBox CreateKeyAudioTriggerSection(int top)
    {
        var groupBox = new GroupBox
        {
            Left = 24,
            Top = top,
            Width = 1036,
            Height = 160,
            Text = "按键音效",
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            Tag = "PinnedLayout"
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12, 14, 12, 12),
            ColumnCount = 7,
            RowCount = 4
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 132));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 132));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 78));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 92));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 82));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 88));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));

        foreach (var control in new Control[]
                 {
                     _keyAudioTriggerEnabledBox,
                     _keyAudioCooldownBox,
                     _setKeyAudioKey1Button,
                     _keyAudioPath1Box,
                     _browseKeyAudio1Button,
                     _setKeyAudioKey2Button,
                     _keyAudioPath2Box,
                     _browseKeyAudio2Button,
                     _setKeyAudioKey3Button,
                     _keyAudioPath3Box,
                     _browseKeyAudio3Button
                 })
        {
            PrepareSectionControl(control);
        }

        layout.Controls.Add(_keyAudioTriggerEnabledBox, 0, 0);
        layout.SetColumnSpan(_keyAudioTriggerEnabledBox, 2);
        layout.Controls.Add(CreateSectionLabel("冷却秒"), 4, 0);
        layout.Controls.Add(_keyAudioCooldownBox, 5, 0);

        layout.Controls.Add(_setKeyAudioKey1Button, 0, 1);
        layout.Controls.Add(CreateSectionLabel("音频1"), 1, 1);
        layout.Controls.Add(_keyAudioPath1Box, 2, 1);
        layout.SetColumnSpan(_keyAudioPath1Box, 3);
        layout.Controls.Add(_browseKeyAudio1Button, 5, 1);

        layout.Controls.Add(_setKeyAudioKey2Button, 0, 2);
        layout.Controls.Add(CreateSectionLabel("音频2"), 1, 2);
        layout.Controls.Add(_keyAudioPath2Box, 2, 2);
        layout.SetColumnSpan(_keyAudioPath2Box, 3);
        layout.Controls.Add(_browseKeyAudio2Button, 5, 2);

        layout.Controls.Add(_setKeyAudioKey3Button, 0, 3);
        layout.Controls.Add(CreateSectionLabel("音频3"), 1, 3);
        layout.Controls.Add(_keyAudioPath3Box, 2, 3);
        layout.SetColumnSpan(_keyAudioPath3Box, 3);
        layout.Controls.Add(_browseKeyAudio3Button, 5, 3);

        groupBox.Controls.Add(layout);
        return groupBox;
    }

    private GroupBox CreateGeneralSettingsSection(int top)
    {
        var groupBox = new GroupBox
        {
            Left = 24,
            Top = top,
            Width = 1036,
            Height = 110,
            Text = "通用设置",
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            Tag = "PinnedLayout"
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12, 14, 12, 12),
            ColumnCount = 6,
            RowCount = 2
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 112));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));

        PrepareSectionControl(_audioVolumeBar);
        PrepareSectionControl(_audioVolumeValueLabel);
        PrepareSectionControl(_audioOutputDeviceBox);
        PrepareSectionControl(_refreshAudioDevicesButton);

        layout.Controls.Add(CreateSectionLabel("音量"), 0, 0);
        layout.Controls.Add(_audioVolumeBar, 1, 0);
        layout.Controls.Add(_audioVolumeValueLabel, 2, 0);
        layout.Controls.Add(CreateSectionLabel("音频输出设备"), 0, 1);
        layout.Controls.Add(_audioOutputDeviceBox, 1, 1);
        layout.SetColumnSpan(_audioOutputDeviceBox, 3);
        layout.Controls.Add(_refreshAudioDevicesButton, 4, 1);

        groupBox.Controls.Add(layout);
        return groupBox;
    }

    private static Label CreateSectionLabel(string text)
    {
        return new Label
        {
            Text = text,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleRight,
            AutoSize = false,
            Margin = new Padding(4, 5, 4, 5)
        };
    }

    private static void PrepareSectionControl(Control control)
    {
        control.Dock = DockStyle.Fill;
        control.Margin = new Padding(4, 5, 4, 5);
    }

    private void OffsetDirectControlsBelow(int topThreshold, int offset)
    {
        foreach (Control control in Controls)
        {
            if (Equals(control.Tag, "PinnedLayout"))
            {
                continue;
            }

            if (control.Top >= topThreshold)
            {
                control.Top += offset;
            }
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

    private void AttachEnterConfirmationHandlers()
    {
        foreach (var control in new Control[]
                 {
                     _ultHotkeySkillNameBox,
                     _ultHotkeySimilarityBox,
                     _ultHotkeyScanIntervalBox,
                     _ultHotkeyCooldownBox,
                     _textTriggerTextBox,
                     _textTriggerScanIntervalBox,
                     _textTriggerCooldownBox,
                     _keyAudioCooldownBox,
                     _imageHotkeySkillNameBox,
                     _imageHotkeySimilarityBox,
                     _imageHotkeyScanIntervalBox,
                     _imageHotkeyCooldownBox,
                 })
        {
            control.KeyDown += EditableControl_KeyDown;
        }
    }

    private void EditableControl_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode != Keys.Enter || _isApplyingConfigToUi)
        {
            return;
        }

        e.SuppressKeyPress = true;
        e.Handled = true;
        ConfirmEditableControl(sender as Control);
    }

    private void ConfirmEditableControl(Control? control)
    {
        if (control is null)
        {
            return;
        }

        if (ReferenceEquals(control, _ultHotkeySkillNameBox))
        {
            ConfirmUltHotkeySkillName();
        }
        else if (ReferenceEquals(control, _imageHotkeySkillNameBox))
        {
            ConfirmImageHotkeySkillName();
        }
        else if (ReferenceEquals(control, _textTriggerTextBox) ||
                 ReferenceEquals(control, _textTriggerScanIntervalBox) ||
                 ReferenceEquals(control, _textTriggerCooldownBox))
        {
            TextTriggerSettings_Changed(control, EventArgs.Empty);
        }
        else if (ReferenceEquals(control, _keyAudioCooldownBox))
        {
            KeyAudioTriggerSettings_Changed(control, EventArgs.Empty);
        }
        else if (ReferenceEquals(control, _ultHotkeySimilarityBox))
        {
            UltHotkeySkillSettings_Changed(control, EventArgs.Empty);
        }
        else if (ReferenceEquals(control, _ultHotkeyScanIntervalBox))
        {
            UltHotkeyTriggerSettings_Changed(control, EventArgs.Empty);
        }
        else if (ReferenceEquals(control, _ultHotkeyCooldownBox))
        {
            UltHotkeyTriggerSettings_Changed(control, EventArgs.Empty);
        }
        else if (ReferenceEquals(control, _imageHotkeySimilarityBox))
        {
            ImageHotkeySkillSettings_Changed(control, EventArgs.Empty);
        }
        else if (ReferenceEquals(control, _imageHotkeyScanIntervalBox))
        {
            ImageHotkeyTriggerSettings_Changed(control, EventArgs.Empty);
        }
        else if (ReferenceEquals(control, _imageHotkeyCooldownBox))
        {
            ImageHotkeyTriggerSettings_Changed(control, EventArgs.Empty);
        }
    }

    private void SetTriggerKeyButton_Click(object? sender, EventArgs e)
    {
        ConfigureHotkey(KeyConfigurationTarget.Trigger, "大招触发按键");
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

        if (result == DialogResult.OK && dialog.CapturedBinding is not null)
        {
            if (ApplyCapturedHotkey(
                target,
                dialog.CapturedBinding,
                dialog.CapturedKeyName ?? InputBindingService.GetDisplayName(dialog.CapturedBinding)))
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

    private bool ApplyCapturedHotkey(KeyConfigurationTarget target, InputBinding binding, string keyName)
    {
        binding.DisplayName = keyName;

        if (TryGetHotkeyConflict(target, binding, out var conflictMessage))
        {
            SetStatus(conflictMessage);
            return false;
        }

        if (target == KeyConfigurationTarget.Trigger)
        {
            _config.TriggerInput = binding.Clone();
            _config.TriggerKey = binding.KeyCode;
            _config.TriggerKeyName = keyName;
            _config.UltHotkeyTrigger.HotkeyInput = binding.Clone();
            _config.UltHotkeyTrigger.Hotkey = binding.KeyCode;
            _config.UltHotkeyTrigger.HotkeyName = keyName;
            _triggerMonitorService.TriggerKey = binding.KeyCode;
            _triggerMonitorService.TriggerBinding = binding.Clone();
        }
        else if (target == KeyConfigurationTarget.RegionCapture)
        {
            _config.RegionCaptureInput = binding.Clone();
            _config.RegionCaptureKey = binding.KeyCode;
            _config.RegionCaptureKeyName = keyName;
            _regionCaptureMonitorService.TriggerKey = binding.KeyCode;
            _regionCaptureMonitorService.TriggerBinding = binding.Clone();
        }
        else if (target == KeyConfigurationTarget.SkillRegionCapture)
        {
            _config.RegionCaptureHotkeys.SkillRegionInput = binding.Clone();
            _config.RegionCaptureHotkeys.SkillRegionKey = binding.KeyCode;
            _config.RegionCaptureHotkeys.SkillRegionKeyName = keyName;
            _skillRegionCaptureMonitorService.TriggerKey = binding.KeyCode;
            _skillRegionCaptureMonitorService.TriggerBinding = binding.Clone();
        }
        else if (target == KeyConfigurationTarget.HealthRegionCapture)
        {
            _config.RegionCaptureHotkeys.HealthRegionInput = binding.Clone();
            _config.RegionCaptureHotkeys.HealthRegionKey = binding.KeyCode;
            _config.RegionCaptureHotkeys.HealthRegionKeyName = keyName;
            _healthRegionCaptureMonitorService.TriggerKey = binding.KeyCode;
            _healthRegionCaptureMonitorService.TriggerBinding = binding.Clone();
        }
        else if (target == KeyConfigurationTarget.OcrTextRegionCapture)
        {
            _config.RegionCaptureHotkeys.OcrTextRegionInput = binding.Clone();
            _config.RegionCaptureHotkeys.OcrTextRegionKey = binding.KeyCode;
            _config.RegionCaptureHotkeys.OcrTextRegionKeyName = keyName;
            _ocrTextRegionCaptureMonitorService.TriggerKey = binding.KeyCode;
            _ocrTextRegionCaptureMonitorService.TriggerBinding = binding.Clone();
        }
        else if (target == KeyConfigurationTarget.ImageHotkeyTrigger)
        {
            _config.ImageHotkeyTrigger.HotkeyInput = binding.Clone();
            _config.ImageHotkeyTrigger.Hotkey = binding.KeyCode;
            _config.ImageHotkeyTrigger.HotkeyName = keyName;
        }
        else if (target == KeyConfigurationTarget.KeyAudioTrigger1)
        {
            _config.KeyAudioTrigger.Input1 = binding.Clone();
            _config.KeyAudioTrigger.Key1 = binding.KeyCode;
            _config.KeyAudioTrigger.Key1Name = keyName;
        }
        else if (target == KeyConfigurationTarget.KeyAudioTrigger2)
        {
            _config.KeyAudioTrigger.Input2 = binding.Clone();
            _config.KeyAudioTrigger.Key2 = binding.KeyCode;
            _config.KeyAudioTrigger.Key2Name = keyName;
        }
        else if (target == KeyConfigurationTarget.KeyAudioTrigger3)
        {
            _config.KeyAudioTrigger.Input3 = binding.Clone();
            _config.KeyAudioTrigger.Key3 = binding.KeyCode;
            _config.KeyAudioTrigger.Key3Name = keyName;
        }

        SaveConfig();
        UpdateRegionCaptureHotkeyButtonText();
        UpdateKeyAudioTriggerDisplay();
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
            KeyConfigurationTarget.OcrTextRegionCapture => _config.RegionCaptureHotkeys.OcrTextRegionKeyName,
            KeyConfigurationTarget.ImageHotkeyTrigger => _config.ImageHotkeyTrigger.HotkeyName,
            KeyConfigurationTarget.KeyAudioTrigger1 => _config.KeyAudioTrigger.Key1Name,
            KeyConfigurationTarget.KeyAudioTrigger2 => _config.KeyAudioTrigger.Key2Name,
            KeyConfigurationTarget.KeyAudioTrigger3 => _config.KeyAudioTrigger.Key3Name,
            _ => string.Empty
        };
    }

    private bool TryGetHotkeyConflict(KeyConfigurationTarget target, InputBinding binding, out string message)
    {
        foreach (var (otherTarget, label, configuredBinding) in EnumerateConfiguredHotkeys())
        {
            if (otherTarget == target || !InputBindingService.Conflicts(configuredBinding, binding))
            {
                continue;
            }

            message = $"快捷键冲突：{binding.DisplayName} 已用于{label}，请换一个键。";
            return true;
        }

        message = string.Empty;
        return false;
    }

    private IEnumerable<(KeyConfigurationTarget Target, string Label, InputBinding Binding)> EnumerateConfiguredHotkeys()
    {
        yield return (KeyConfigurationTarget.Trigger, "大招触发按键", _config.TriggerInput);
        yield return (KeyConfigurationTarget.RegionCapture, "截图键", _config.RegionCaptureInput);
        yield return (KeyConfigurationTarget.SkillRegionCapture, "技能区域快捷键", _config.RegionCaptureHotkeys.SkillRegionInput);
        yield return (KeyConfigurationTarget.HealthRegionCapture, "血条区域快捷键", _config.RegionCaptureHotkeys.HealthRegionInput);
        yield return (KeyConfigurationTarget.OcrTextRegionCapture, "OCR文字区域快捷键", _config.RegionCaptureHotkeys.OcrTextRegionInput);
        yield return (KeyConfigurationTarget.ImageHotkeyTrigger, "战技触发按键", _config.ImageHotkeyTrigger.HotkeyInput);
        yield return (KeyConfigurationTarget.KeyAudioTrigger1, "按键音效 1", _config.KeyAudioTrigger.Input1);
        yield return (KeyConfigurationTarget.KeyAudioTrigger2, "按键音效 2", _config.KeyAudioTrigger.Input2);
        yield return (KeyConfigurationTarget.KeyAudioTrigger3, "按键音效 3", _config.KeyAudioTrigger.Input3);
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
        trigger.ScanIntervalMs = (int)_textTriggerScanIntervalBox.Value;
        trigger.CooldownSeconds = (int)_textTriggerCooldownBox.Value;

        SaveConfig();
        UpdateTextTriggerDisplay();
        SyncTextTriggerTimer();
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

    private void KeyAudioTriggerSettings_Changed(object? sender, EventArgs e)
    {
        if (_isApplyingConfigToUi)
        {
            return;
        }

        _config.KeyAudioTrigger.Enabled = _keyAudioTriggerEnabledBox.Checked;
        _config.KeyAudioTrigger.CooldownSeconds = (int)_keyAudioCooldownBox.Value;
        SaveConfig();
        UpdateKeyAudioTriggerDisplay();
        UpdateStatus();
    }

    private void BrowseKeyAudioButton_Click(int index)
    {
        if (!TryBrowseAudioFile(out var path))
        {
            return;
        }

        if (index == 1)
        {
            _config.KeyAudioTrigger.AudioPath1 = path;
        }
        else if (index == 2)
        {
            _config.KeyAudioTrigger.AudioPath2 = path;
        }
        else if (index == 3)
        {
            _config.KeyAudioTrigger.AudioPath3 = path;
        }

        SaveConfig();
        UpdateKeyAudioTriggerDisplay();
        SetStatus($"按键音效 {index} 的音频已更新。");
    }

    private void UltHotkeyTriggerSettings_Changed(object? sender, EventArgs e)
    {
        if (_isApplyingConfigToUi)
        {
            return;
        }

        _config.UltHotkeyTrigger.Enabled = _ultHotkeyTriggerEnabledBox.Checked;
        _config.UltHotkeyTrigger.ScanIntervalMs = (int)_ultHotkeyScanIntervalBox.Value;
        _config.UltHotkeyTrigger.CooldownSeconds = (int)_ultHotkeyCooldownBox.Value;

        SaveConfig();
        UpdateUltHotkeyTriggerDisplay(keepSelection: true);
        SyncUltHotkeyScanTimer();
        UpdateStatus();
    }

    private void UltHotkeySkillSettings_Changed(object? sender, EventArgs e)
    {
        if (_isApplyingConfigToUi)
        {
            return;
        }

        var ult = GetSelectedUltHotkeySkill();
        if (ult is null)
        {
            return;
        }

        ult.SimilarityThreshold = (double)_ultHotkeySimilarityBox.Value;
        SaveConfig();
        UpdateSelectedUltHotkeySkillListItem();
        SyncUltHotkeyScanTimer();
    }

    private void UltHotkeySkillNameBox_TextChanged(object? sender, EventArgs e)
    {
        if (_isApplyingConfigToUi)
        {
            return;
        }

        var ult = GetSelectedUltHotkeySkill();
        if (ult is not null)
        {
            ult.Name = _ultHotkeySkillNameBox.Text;
        }
    }

    private void UltHotkeySkillNameBox_Leave(object? sender, EventArgs e)
    {
        ConfirmUltHotkeySkillName();
    }

    private void ConfirmUltHotkeySkillName()
    {
        if (_isApplyingConfigToUi)
        {
            return;
        }

        var ult = GetSelectedUltHotkeySkill();
        if (ult is null)
        {
            return;
        }

        var fallbackName = $"大招 {_ultHotkeySkillsListBox.SelectedIndex + 1}";
        ult.Name = string.IsNullOrWhiteSpace(_ultHotkeySkillNameBox.Text)
            ? fallbackName
            : _ultHotkeySkillNameBox.Text.Trim();

        if (!string.Equals(_ultHotkeySkillNameBox.Text, ult.Name, StringComparison.Ordinal))
        {
            var wasApplyingConfigToUi = _isApplyingConfigToUi;
            _isApplyingConfigToUi = true;
            try
            {
                _ultHotkeySkillNameBox.Text = ult.Name;
            }
            finally
            {
                _isApplyingConfigToUi = wasApplyingConfigToUi;
            }
        }

        SaveConfig();
        UpdateSelectedUltHotkeySkillListItem();
    }

    private void UltHotkeySkillsListBox_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (!_isApplyingConfigToUi)
        {
            _config.UltHotkeyTrigger.SelectedSkillIndex = _ultHotkeySkillsListBox.SelectedIndex < 0
                ? 0
                : _ultHotkeySkillsListBox.SelectedIndex;
            SaveConfig();
            SyncUltHotkeyScanTimer();
        }

        UpdateSelectedUltHotkeySkillDisplay();
    }

    private void AddUltHotkeySkillButton_Click(object? sender, EventArgs e)
    {
        var ult = new ImageHotkeySkillConfig
        {
            Name = $"大招 {_config.UltHotkeyTrigger.Skills.Count + 1}",
            SimilarityThreshold = 0.85
        };

        _config.UltHotkeyTrigger.Skills.Add(ult);
        _config.UltHotkeyTrigger.SelectedSkillIndex = _config.UltHotkeyTrigger.Skills.Count - 1;
        SaveConfig();
        UpdateUltHotkeyTriggerDisplay(selectedIndex: _config.UltHotkeyTrigger.Skills.Count - 1);
        SyncUltHotkeyScanTimer();
        SetStatus("已新增大招。");
    }

    private void DeleteUltHotkeySkillButton_Click(object? sender, EventArgs e)
    {
        var index = _ultHotkeySkillsListBox.SelectedIndex;
        if (index < 0 || index >= _config.UltHotkeyTrigger.Skills.Count)
        {
            SetStatus("请先选择要删除的大招。");
            return;
        }

        var ultName = _config.UltHotkeyTrigger.Skills[index].Name;
        _config.UltHotkeyTrigger.Skills.RemoveAt(index);
        _config.UltHotkeyTrigger.SelectedSkillIndex = _config.UltHotkeyTrigger.Skills.Count == 0
            ? 0
            : Math.Min(index, _config.UltHotkeyTrigger.Skills.Count - 1);
        SaveConfig();
        UpdateUltHotkeyTriggerDisplay(selectedIndex: _config.UltHotkeyTrigger.SelectedSkillIndex);
        SyncUltHotkeyScanTimer();
        SetStatus($"已删除大招：{ultName}");
    }

    private void ConfigureUltHotkeyRegion()
    {
        if (_isRecognitionRunning || _isRegionCaptureRunning || _isConfiguringKey)
        {
            SetStatus("当前正在识别、框选或设置按键，暂时不能修改大招区域。");
            return;
        }

        _isRegionCaptureRunning = true;
        _triggerMonitorService.Enabled = false;
        _regionCaptureMonitorService.Enabled = false;
        SetRegionCaptureHotkeyMonitorsEnabled(false);
        _ultHotkeyScanTimer.Stop();
        HideForSelection();

        try
        {
            var step = new SelectionStep(
                "框选大招检测区域",
                "只框住要和大招模板比较的屏幕区域。");

            if (!TrySelectBoundsSession(step, out var selectedBounds))
            {
                SetStatus("已取消大招区域框选。");
                return;
            }

            _config.UltHotkeyTrigger.Region = ScreenBounds.FromRectangle(selectedBounds);
            SaveConfig();
            UpdateUltHotkeyTriggerDisplay(keepSelection: true);
            RefreshRegionList();
            SetStatus("大招区域已更新。");
        }
        catch (Exception ex)
        {
            SetStatus($"大招区域框选失败：{ex.Message}");
        }
        finally
        {
            _isRegionCaptureRunning = false;
            _triggerMonitorService.Enabled = true;
            _regionCaptureMonitorService.Enabled = true;
            SetRegionCaptureHotkeyMonitorsEnabled(true);
            SyncUltHotkeyScanTimer();
            RestoreAfterSelection();
        }
    }

    private void BrowseUltHotkeyTemplateButton_Click(object? sender, EventArgs e)
    {
        var ult = EnsureSelectedUltHotkeySkill();
        using var fileDialog = new OpenFileDialog
        {
            Filter = "图片文件|*.png;*.jpg;*.jpeg;*.bmp|所有文件|*.*",
            FileName = Path.GetFileName(ult.TemplateImagePath)
        };

        if (fileDialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        ult.TemplateImagePath = fileDialog.FileName;
        SaveConfig();
        UpdateUltHotkeyTriggerDisplay(keepSelection: true);
        SetStatus("大招模板已更新。");
    }

    private void BrowseUltHotkeyAudioButton_Click(object? sender, EventArgs e)
    {
        var ult = EnsureSelectedUltHotkeySkill();
        if (!TryBrowseAudioFile(out var path))
        {
            return;
        }

        ult.AudioPath = path;
        SaveConfig();
        UpdateUltHotkeyTriggerDisplay(keepSelection: true);
        SetStatus("大招音效已更新。");
    }

    private void ImageHotkeyTriggerSettings_Changed(object? sender, EventArgs e)
    {
        if (_isApplyingConfigToUi)
        {
            return;
        }

        _config.ImageHotkeyTrigger.Enabled = _imageHotkeyTriggerEnabledBox.Checked;
        _config.ImageHotkeyTrigger.ScanIntervalMs = (int)_imageHotkeyScanIntervalBox.Value;
        _config.ImageHotkeyTrigger.CooldownSeconds = (int)_imageHotkeyCooldownBox.Value;

        SaveConfig();
        UpdateImageHotkeyTriggerDisplay();
        SyncImageHotkeyScanTimer();
        UpdateStatus();
    }

    private void ImageHotkeySkillSettings_Changed(object? sender, EventArgs e)
    {
        if (_isApplyingConfigToUi)
        {
            return;
        }

        var skill = GetSelectedImageHotkeySkill();
        if (skill is null)
        {
            return;
        }

        skill.SimilarityThreshold = (double)_imageHotkeySimilarityBox.Value;

        SaveConfig();
        UpdateSelectedImageHotkeySkillListItem();
        SyncImageHotkeyScanTimer();
    }

    private void ImageHotkeySkillNameBox_TextChanged(object? sender, EventArgs e)
    {
        if (_isApplyingConfigToUi)
        {
            return;
        }

        var skill = GetSelectedImageHotkeySkill();
        if (skill is null)
        {
            return;
        }

        skill.Name = _imageHotkeySkillNameBox.Text;
    }

    private void ImageHotkeySkillNameBox_Leave(object? sender, EventArgs e)
    {
        ConfirmImageHotkeySkillName();
    }

    private void ConfirmImageHotkeySkillName()
    {
        if (_isApplyingConfigToUi)
        {
            return;
        }

        var skill = GetSelectedImageHotkeySkill();
        if (skill is null)
        {
            return;
        }

        var fallbackName = $"战技 {_imageHotkeySkillsListBox.SelectedIndex + 1}";
        skill.Name = string.IsNullOrWhiteSpace(_imageHotkeySkillNameBox.Text)
            ? fallbackName
            : _imageHotkeySkillNameBox.Text.Trim();

        if (!string.Equals(_imageHotkeySkillNameBox.Text, skill.Name, StringComparison.Ordinal))
        {
            _isApplyingConfigToUi = true;
            try
            {
                _imageHotkeySkillNameBox.Text = skill.Name;
            }
            finally
            {
                _isApplyingConfigToUi = false;
            }
        }

        SaveConfig();
        UpdateSelectedImageHotkeySkillListItem();
    }

    private void ImageHotkeySkillsListBox_SelectedIndexChanged(object? sender, EventArgs e)
    {
        UpdateSelectedImageHotkeySkillDisplay();
    }

    private void AddImageHotkeySkillButton_Click(object? sender, EventArgs e)
    {
        var skill = new ImageHotkeySkillConfig
        {
            Name = $"战技 {_config.ImageHotkeyTrigger.Skills.Count + 1}",
            SimilarityThreshold = 0.85
        };

        _config.ImageHotkeyTrigger.Skills.Add(skill);
        SaveConfig();
        UpdateImageHotkeyTriggerDisplay(selectedIndex: _config.ImageHotkeyTrigger.Skills.Count - 1);
        SyncImageHotkeyScanTimer();
        SetStatus("已新增战技。");
    }

    private void DeleteImageHotkeySkillButton_Click(object? sender, EventArgs e)
    {
        var index = _imageHotkeySkillsListBox.SelectedIndex;
        if (index < 0 || index >= _config.ImageHotkeyTrigger.Skills.Count)
        {
            SetStatus("请先选择要删除的战技。");
            return;
        }

        var skillName = _config.ImageHotkeyTrigger.Skills[index].Name;
        _config.ImageHotkeyTrigger.Skills.RemoveAt(index);
        SaveConfig();
        UpdateImageHotkeyTriggerDisplay(selectedIndex: Math.Min(index, _config.ImageHotkeyTrigger.Skills.Count - 1));
        SyncImageHotkeyScanTimer();
        SetStatus($"已删除战技：{skillName}");
    }

    private void ConfigureImageHotkeyRegion()
    {
        if (_isRecognitionRunning || _isRegionCaptureRunning || _isConfiguringKey)
        {
            SetStatus("当前正在识别、框选或设置按键，暂时不能修改战技区域。");
            return;
        }

        _isRegionCaptureRunning = true;
        _triggerMonitorService.Enabled = false;
        _regionCaptureMonitorService.Enabled = false;
        SetRegionCaptureHotkeyMonitorsEnabled(false);
        _imageHotkeyScanTimer.Stop();
        HideForSelection();

        try
        {
            var step = new SelectionStep(
                "框选战技检测区域",
                "只框住要和战技模板比较的屏幕区域。");

            if (!TrySelectBoundsSession(step, out var selectedBounds))
            {
                SetStatus("已取消战技区域框选。");
                return;
            }

            _config.ImageHotkeyTrigger.Region = ScreenBounds.FromRectangle(selectedBounds);
            SaveConfig();
            UpdateImageHotkeyTriggerDisplay();
            RefreshRegionList();
            SetStatus("战技区域已更新。");
        }
        catch (Exception ex)
        {
            SetStatus($"战技区域框选失败：{ex.Message}");
        }
        finally
        {
            _isRegionCaptureRunning = false;
            _triggerMonitorService.Enabled = true;
            _regionCaptureMonitorService.Enabled = true;
            SetRegionCaptureHotkeyMonitorsEnabled(true);
            SyncImageHotkeyScanTimer();
            RestoreAfterSelection();
        }
    }

    private void BrowseImageHotkeyTemplateButton_Click(object? sender, EventArgs e)
    {
        var skill = EnsureSelectedImageHotkeySkill();
        using var fileDialog = new OpenFileDialog
        {
            Filter = "图片文件|*.png;*.jpg;*.jpeg;*.bmp|所有文件|*.*",
            FileName = Path.GetFileName(skill.TemplateImagePath)
        };

        if (fileDialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        skill.TemplateImagePath = fileDialog.FileName;
        SaveConfig();
        UpdateImageHotkeyTriggerDisplay(keepSelection: true);
        SetStatus("战技模板已更新。");
    }

    private void BrowseImageHotkeyAudioButton_Click(object? sender, EventArgs e)
    {
        var skill = EnsureSelectedImageHotkeySkill();
        using var fileDialog = new OpenFileDialog
        {
            Filter = "音频文件|*.mp3;*.wav",
            FileName = Path.GetFileName(skill.AudioPath)
        };

        if (fileDialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        skill.AudioPath = fileDialog.FileName;
        SaveConfig();
        UpdateImageHotkeyTriggerDisplay(keepSelection: true);
        SetStatus("战技音效已更新。");
    }

    private void ConfigureOcrTextRegion()
    {
        if (_isRecognitionRunning || _isRegionCaptureRunning || _isConfiguringKey)
        {
            return;
        }

        _isRegionCaptureRunning = true;
        _triggerMonitorService.Enabled = false;
        _regionCaptureMonitorService.Enabled = false;
        SetRegionCaptureHotkeyMonitorsEnabled(false);
        _textTriggerTimer.Stop();
        HideForSelection();

        try
        {
            var step = new SelectionStep(
                "框选 OCR 文字触发区域",
                "只框住要识别的目标文字附近，避免包含太多会变化的背景。");

            if (!TrySelectBoundsSession(step, out var selectedBounds))
            {
                SetStatus("已取消 OCR 文字区域框选。");
                return;
            }

            EnsureDefaultTextTrigger().Region = ScreenBounds.FromRectangle(selectedBounds);
            SetStatus("OCR 文字触发区域已更新。");

            SaveConfig();
            UpdateTextTriggerDisplay();
            RefreshRegionList();
        }
        catch (Exception ex)
        {
            SetStatus($"OCR 文字区域框选失败：{ex.Message}");
        }
        finally
        {
            _isRegionCaptureRunning = false;
            _triggerMonitorService.Enabled = true;
            _regionCaptureMonitorService.Enabled = true;
            SetRegionCaptureHotkeyMonitorsEnabled(true);
            SyncTextTriggerTimer();
            RestoreAfterSelection();
        }
    }

    private async void TextTriggerTimer_Tick(object? sender, EventArgs e)
    {
        var trigger = EnsureDefaultTextTrigger();
        if (_isTextScanRunning || _isRegionCaptureRunning || _isConfiguringKey || !trigger.Enabled)
        {
            return;
        }

        if (trigger.Region is null || trigger.Region.Width <= 0 || trigger.Region.Height <= 0)
        {
            SetStatus("OCR 文字触发缺少检测区域。");
            return;
        }

        _isTextScanRunning = true;

        try
        {
            using var screenshot = _screenCaptureService.Capture(trigger.Region.ToRectangle());
            var result = await Task.Run(() =>
            {
                var success = _screenTextRecognizer.TryRecognize(screenshot, out var text, out var error);
                return new TextRecognitionResult(success, text, error);
            }, _shutdownCts.Token);

            if (!result.Success)
            {
                SetStatus($"OCR 文字识别不可用：{result.ErrorMessage}");
                _textTriggerTimer.Stop();
                return;
            }

            HandleRecognizedScreenText(trigger, result.Text);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            SetStatus($"OCR 文字识别失败：{ex.Message}");
        }
        finally
        {
            _isTextScanRunning = false;
        }
    }

    private void HandleRecognizedScreenText(TextTriggerConfig trigger, string recognizedText)
    {
        var normalizedRecognizedText = OcrTextTriggerMatcher.Normalize(recognizedText);
        if (string.IsNullOrWhiteSpace(normalizedRecognizedText) ||
            !OcrTextTriggerMatcher.IsMatch(recognizedText, trigger.Text))
        {
            return;
        }

        var now = DateTime.UtcNow;
        var triggerText = OcrTextTriggerMatcher.Normalize(trigger.Text);
        var cooldown = TimeSpan.FromSeconds(Math.Clamp(trigger.CooldownSeconds, 1, 3600));
        if (_lastTextTriggerUtc.TryGetValue(triggerText, out var lastTriggeredAt) &&
            now - lastTriggeredAt < cooldown)
        {
            return;
        }

        _lastTextTriggerUtc[triggerText] = now;

        if (!File.Exists(trigger.MusicPath))
        {
            SetStatus($"检测到 OCR 文字“{trigger.Text}”，但音频文件不存在：{trigger.MusicPath}");
            return;
        }

        if (!PlayAudioPath(trigger.MusicPath, out var playbackMessage))
        {
            SetStatus(playbackMessage);
            return;
        }

        SetStatus($"检测到 OCR 文字“{trigger.Text}”（{normalizedRecognizedText}），已播放触发音频。");
    }

    private async void UltHotkeyScanTimer_Tick(object? sender, EventArgs e)
    {
        if (_isUltHotkeyScanRunning || _isRegionCaptureRunning || _isConfiguringKey || !_config.UltHotkeyTrigger.Enabled)
        {
            return;
        }

        var trigger = _config.UltHotkeyTrigger;
        if (trigger.Region is null || trigger.Region.Width <= 0 || trigger.Region.Height <= 0)
        {
            _ultHotkeyMatched = false;
            _matchedUltHotkeySkill = null;
            SetStatus("大招音效缺少检测区域。");
            return;
        }

        var selectedUlt = GetActiveUltHotkeySkill();
        if (selectedUlt is null)
        {
            _ultHotkeyMatched = false;
            _matchedUltHotkeySkill = null;
            SetStatus("大招音效未选择可用的大招。");
            return;
        }

        if (string.IsNullOrWhiteSpace(selectedUlt.TemplateImagePath) || !File.Exists(selectedUlt.TemplateImagePath))
        {
            _ultHotkeyMatched = false;
            _matchedUltHotkeySkill = null;
            SetStatus($"当前大招模板图片不存在：{selectedUlt.TemplateImagePath}");
            return;
        }

        _isUltHotkeyScanRunning = true;

        try
        {
            var candidate = CloneImageHotkeySkill(selectedUlt);
            var scanResult = await ScanImageHotkeySkillsAsync(trigger.Region.ToRectangle(), [candidate]);
            _lastUltHotkeyScore = scanResult.BestOverallScore;
            _matchedUltHotkeySkill = scanResult.BestPassingSkill;
            _ultHotkeyMatched = scanResult.BestPassingSkill is not null;
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _ultHotkeyMatched = false;
            _matchedUltHotkeySkill = null;
            SetStatus($"大招匹配失败：{ex.Message}");
        }
        finally
        {
            _isUltHotkeyScanRunning = false;
        }
    }

    private void HandleUltHotkeyPressed()
    {
        var trigger = _config.UltHotkeyTrigger;
        if (!trigger.Enabled)
        {
            InputDebugLogger.LogTriggerDecision("Ultimate hotkey audio decision", trigger.HotkeyInput, $"configured=0x{trigger.HotkeyInput.KeyCode:X2}", true, false, false, false, false, "ultimate trigger disabled");
            return;
        }

        if (trigger.Region is null || trigger.Region.Width <= 0 || trigger.Region.Height <= 0)
        {
            InputDebugLogger.LogTriggerDecision("Ultimate hotkey audio decision", trigger.HotkeyInput, $"configured=0x{trigger.HotkeyInput.KeyCode:X2}", true, false, false, false, false, "ultimate rejected: missing region");
            SetStatus("大招音效缺少检测区域。");
            return;
        }

        var selectedUlt = GetActiveUltHotkeySkill();
        if (selectedUlt is null)
        {
            InputDebugLogger.LogTriggerDecision("Ultimate hotkey audio decision", trigger.HotkeyInput, $"configured=0x{trigger.HotkeyInput.KeyCode:X2}", true, false, false, false, false, "ultimate rejected: no active skill");
            SetStatus("大招音效未选择可用的大招。");
            return;
        }

        if (!_ultHotkeyMatched || _matchedUltHotkeySkill is null)
        {
            InputDebugLogger.LogTriggerDecision("Ultimate hotkey audio decision", trigger.HotkeyInput, $"configured=0x{trigger.HotkeyInput.KeyCode:X2}", true, false, false, false, false, $"ultimate rejected: no current template match; score={_lastUltHotkeyScore:0.###}");
            SetStatus($"大招触发按键已按下，但大招未匹配。当前大招匹配度：{_lastUltHotkeyScore:0.###}");
            return;
        }

        var now = DateTime.UtcNow;
        var cooldown = TimeSpan.FromSeconds(Math.Clamp(trigger.CooldownSeconds, 1, 3600));
        if (now - _lastUltHotkeyTriggerUtc < cooldown)
        {
            InputDebugLogger.LogTriggerDecision("Ultimate hotkey audio decision", trigger.HotkeyInput, $"configured=0x{trigger.HotkeyInput.KeyCode:X2}", true, false, true, false, false, "ultimate rejected: cooldown");
            SetStatus($"大招音效冷却中。当前大招匹配度：{_lastUltHotkeyScore:0.###}");
            return;
        }

        var matchedUlt = _matchedUltHotkeySkill;
        if (string.IsNullOrWhiteSpace(matchedUlt.AudioPath) || !File.Exists(matchedUlt.AudioPath))
        {
            InputDebugLogger.LogTriggerDecision("Ultimate hotkey audio decision", trigger.HotkeyInput, $"configured=0x{trigger.HotkeyInput.KeyCode:X2}", true, false, false, false, false, "ultimate rejected: audio file missing");
            SetStatus($"大招“{matchedUlt.Name}”的音效文件不存在：{matchedUlt.AudioPath}");
            return;
        }

        if (!PlayAudioPath(matchedUlt.AudioPath, out var playbackMessage))
        {
            InputDebugLogger.LogTriggerDecision("Ultimate hotkey audio decision", trigger.HotkeyInput, $"configured=0x{trigger.HotkeyInput.KeyCode:X2}", true, false, false, false, false, $"ultimate rejected: playback failed: {playbackMessage}");
            SetStatus(playbackMessage);
            return;
        }

        _lastUltHotkeyTriggerUtc = now;
        InputDebugLogger.LogTriggerDecision("Ultimate hotkey audio decision", trigger.HotkeyInput, $"configured=0x{trigger.HotkeyInput.KeyCode:X2}", true, false, false, false, true, "ultimate triggered");
        SetStatus($"大招“{matchedUlt.Name}”匹配且按键命中，已播放大招音效。大招匹配度：{_lastUltHotkeyScore:0.###}");
    }

    private async void ImageHotkeyScanTimer_Tick(object? sender, EventArgs e)
    {
        if (_isImageHotkeyScanRunning || _isRegionCaptureRunning || _isConfiguringKey || !_config.ImageHotkeyTrigger.Enabled)
        {
            return;
        }

        var trigger = _config.ImageHotkeyTrigger;
        if (trigger.Region is null || trigger.Region.Width <= 0 || trigger.Region.Height <= 0)
        {
            _imageHotkeyMatched = false;
            _matchedImageHotkeySkill = null;
            SetStatus("战技音效缺少检测区域。");
            return;
        }

        var skills = trigger.Skills
            .Where(skill => !string.IsNullOrWhiteSpace(skill.TemplateImagePath))
            .Select(CloneImageHotkeySkill)
            .ToList();

        if (skills.Count == 0)
        {
            _imageHotkeyMatched = false;
            _matchedImageHotkeySkill = null;
            SetStatus("战技音效未配置任何战技模板。");
            return;
        }

        _isImageHotkeyScanRunning = true;

        try
        {
            var scanResult = await ScanImageHotkeySkillsAsync(trigger.Region.ToRectangle(), skills);

            _lastImageHotkeyScore = scanResult.BestPassingSkill is null
                ? scanResult.BestOverallScore
                : scanResult.BestPassingScore;
            _matchedImageHotkeySkill = scanResult.BestPassingSkill;
            _imageHotkeyMatched = scanResult.BestPassingSkill is not null;

            if (scanResult.ValidTemplateCount == 0)
            {
                SetStatus("战技音效没有可用模板图片，请检查每个战技的模板路径。");
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _imageHotkeyMatched = false;
            _matchedImageHotkeySkill = null;
            SetStatus($"战技匹配失败：{ex.Message}");
        }
        finally
        {
            _isImageHotkeyScanRunning = false;
        }
    }

    private Task<ImageHotkeySkillScanResult> ScanImageHotkeySkillsAsync(Rectangle region, List<ImageHotkeySkillConfig> skills)
    {
        return Task.Run(() =>
        {
            using var frame = _screenCaptureService.Capture(region);
            var bestOverallScore = 0.0;
            ImageHotkeySkillConfig? bestPassingSkill = null;
            var bestPassingScore = 0.0;
            var validTemplateCount = 0;

            foreach (var skill in skills)
            {
                if (!File.Exists(skill.TemplateImagePath))
                {
                    continue;
                }

                validTemplateCount++;
                var match = _templateMatcher.FindBestMatch(frame, skill.TemplateImagePath);
                if (match.IsValid && match.Score > bestOverallScore)
                {
                    bestOverallScore = match.Score;
                }

                var threshold = Math.Clamp(skill.SimilarityThreshold, 0.1, 1.0);
                if (match.IsValid && match.Score >= threshold && match.Score > bestPassingScore)
                {
                    bestPassingSkill = skill;
                    bestPassingScore = match.Score;
                }
            }

            return new ImageHotkeySkillScanResult(bestPassingSkill, bestPassingScore, bestOverallScore, validTemplateCount);
        }, _shutdownCts.Token);
    }

    private void HandleImageHotkeyPressed(InputBinding binding)
    {
        var trigger = _config.ImageHotkeyTrigger;
        var matchesHotkey = InputBindingService.Matches(trigger.HotkeyInput, binding);
        if (!trigger.Enabled || !matchesHotkey)
        {
            InputDebugLogger.LogTriggerDecision(
                "Image hotkey event path",
                trigger.HotkeyInput,
                $"configured=0x{trigger.HotkeyInput.KeyCode:X2}; incoming=0x{binding.KeyCode:X2}",
                matchesHotkey,
                wasPressed: false,
                cooldownBlocked: false,
                edgeBlocked: false,
                willTrigger: false,
                !trigger.Enabled ? "image hotkey trigger disabled" : $"incoming binding did not match: {binding.DisplayName}");
            return;
        }

        if (!InputBindingService.IsSupported(trigger.HotkeyInput))
        {
            InputDebugLogger.LogTriggerDecision("Image hotkey event path", trigger.HotkeyInput, $"configured=0x{trigger.HotkeyInput.KeyCode:X2}", false, false, false, false, false, "configured image hotkey unsupported");
            SetStatus("战技音效未配置有效按键。");
            return;
        }

        if (trigger.Region is null || trigger.Region.Width <= 0 || trigger.Region.Height <= 0)
        {
            InputDebugLogger.LogTriggerDecision("Image hotkey event path", trigger.HotkeyInput, $"configured=0x{trigger.HotkeyInput.KeyCode:X2}", true, false, false, false, false, "image hotkey rejected: missing region");
            SetStatus("战技音效缺少检测区域。");
            return;
        }

        if (trigger.Skills.Count == 0)
        {
            InputDebugLogger.LogTriggerDecision("Image hotkey event path", trigger.HotkeyInput, $"configured=0x{trigger.HotkeyInput.KeyCode:X2}", true, false, false, false, false, "image hotkey rejected: no skills");
            SetStatus("战技音效未配置任何战技。");
            return;
        }

        if (!_imageHotkeyMatched || _matchedImageHotkeySkill is null)
        {
            InputDebugLogger.LogTriggerDecision("Image hotkey event path", trigger.HotkeyInput, $"configured=0x{trigger.HotkeyInput.KeyCode:X2}", true, false, false, false, false, $"image hotkey rejected: no current template match; score={_lastImageHotkeyScore:0.###}");
            SetStatus($"战技触发按键已按下，但战技未匹配。当前战技匹配度：{_lastImageHotkeyScore:0.###}");
            return;
        }

        var now = DateTime.UtcNow;
        var cooldown = TimeSpan.FromSeconds(Math.Clamp(trigger.CooldownSeconds, 1, 3600));
        if (now - _lastImageHotkeyTriggerUtc < cooldown)
        {
            InputDebugLogger.LogTriggerDecision("Image hotkey event path", trigger.HotkeyInput, $"configured=0x{trigger.HotkeyInput.KeyCode:X2}", true, false, true, false, false, "image hotkey rejected: cooldown");
            SetStatus($"战技音效冷却中。当前战技匹配度：{_lastImageHotkeyScore:0.###}");
            return;
        }

        var matchedSkill = _matchedImageHotkeySkill;
        if (string.IsNullOrWhiteSpace(matchedSkill.AudioPath) || !File.Exists(matchedSkill.AudioPath))
        {
            InputDebugLogger.LogTriggerDecision("Image hotkey event path", trigger.HotkeyInput, $"configured=0x{trigger.HotkeyInput.KeyCode:X2}", true, false, false, false, false, "image hotkey rejected: audio file missing");
            SetStatus($"战技“{matchedSkill.Name}”的音效文件不存在：{matchedSkill.AudioPath}");
            return;
        }

        if (!PlayAudioPath(matchedSkill.AudioPath, out var playbackMessage))
        {
            InputDebugLogger.LogTriggerDecision("Image hotkey event path", trigger.HotkeyInput, $"configured=0x{trigger.HotkeyInput.KeyCode:X2}", true, false, false, false, false, $"image hotkey rejected: playback failed: {playbackMessage}");
            SetStatus(playbackMessage);
            return;
        }

        _lastImageHotkeyTriggerUtc = now;
        InputDebugLogger.LogTriggerDecision("Image hotkey event path", trigger.HotkeyInput, $"configured=0x{trigger.HotkeyInput.KeyCode:X2}", true, false, false, false, true, "image hotkey triggered");
        SetStatus($"战技“{matchedSkill.Name}”匹配且按键命中，已播放战技音效。战技匹配度：{_lastImageHotkeyScore:0.###}");
    }

    private void TriggerMonitorService_Triggered(object? sender, EventArgs e)
    {
        if (_isConfiguringKey || _isRecognitionRunning || _isRegionCaptureRunning)
        {
            InputDebugLogger.LogMessage("Ultimate hotkey monitor triggered but UI gate rejected it: configuring/recognition/region-capture active.");
            return;
        }

        HandleUltHotkeyPressed();
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

    private void OcrTextRegionCaptureMonitorService_Triggered(object? sender, EventArgs e)
    {
        BeginRegionCaptureFromHotkey(ConfigureOcrTextRegion);
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

    private void InputCaptureService_InputBindingPressed(InputBinding binding)
    {
        HandleKeyAudioTriggerPressed(binding);
        HandleImageHotkeyPressed(binding);
    }

    private void HandleKeyAudioTriggerPressed(InputBinding binding)
    {
        var trigger = _config.KeyAudioTrigger;
        if (!trigger.Enabled || _isConfiguringKey)
        {
            InputDebugLogger.LogTriggerDecision(
                "Key audio event path",
                trigger.Input1,
                $"incoming=0x{binding.KeyCode:X2}",
                false,
                wasPressed: false,
                cooldownBlocked: false,
                edgeBlocked: false,
                willTrigger: false,
                !trigger.Enabled ? "key audio trigger disabled" : "key configuration dialog active");
            return;
        }

        var matchedInput1 = InputBindingService.Matches(trigger.Input1, binding);
        InputDebugLogger.LogTriggerDecision("Key audio 1 event path", trigger.Input1, $"configured=0x{trigger.Input1.KeyCode:X2}; incoming=0x{binding.KeyCode:X2}", matchedInput1, false, false, false, false, matchedInput1 ? "matched; checking cooldown/audio" : $"incoming binding did not match: {binding.DisplayName}");
        if (matchedInput1)
        {
            TryPlayKeyAudio(1, trigger.Input1, trigger.AudioPath1, ref _lastKeyAudioTrigger1Utc);
            return;
        }

        var matchedInput2 = InputBindingService.Matches(trigger.Input2, binding);
        InputDebugLogger.LogTriggerDecision("Key audio 2 event path", trigger.Input2, $"configured=0x{trigger.Input2.KeyCode:X2}; incoming=0x{binding.KeyCode:X2}", matchedInput2, false, false, false, false, matchedInput2 ? "matched; checking cooldown/audio" : $"incoming binding did not match: {binding.DisplayName}");
        if (matchedInput2)
        {
            TryPlayKeyAudio(2, trigger.Input2, trigger.AudioPath2, ref _lastKeyAudioTrigger2Utc);
            return;
        }

        var matchedInput3 = InputBindingService.Matches(trigger.Input3, binding);
        InputDebugLogger.LogTriggerDecision("Key audio 3 event path", trigger.Input3, $"configured=0x{trigger.Input3.KeyCode:X2}; incoming=0x{binding.KeyCode:X2}", matchedInput3, false, false, false, false, matchedInput3 ? "matched; checking cooldown/audio" : $"incoming binding did not match: {binding.DisplayName}");
        if (matchedInput3)
        {
            TryPlayKeyAudio(3, trigger.Input3, trigger.AudioPath3, ref _lastKeyAudioTrigger3Utc);
        }
    }

    private void TryPlayKeyAudio(int index, InputBinding binding, string audioPath, ref DateTime lastTriggerUtc)
    {
        var now = DateTime.UtcNow;
        var cooldown = TimeSpan.FromSeconds(Math.Max(1, _config.KeyAudioTrigger.CooldownSeconds));
        if (now - lastTriggerUtc < cooldown)
        {
            InputDebugLogger.LogTriggerDecision($"Key audio {index} playback", binding, $"configured=0x{binding.KeyCode:X2}", true, false, true, false, false, "key audio rejected: cooldown");
            return;
        }

        if (string.IsNullOrWhiteSpace(audioPath) ||
            (!File.Exists(audioPath) && !Directory.Exists(audioPath)))
        {
            InputDebugLogger.LogTriggerDecision($"Key audio {index} playback", binding, $"configured=0x{binding.KeyCode:X2}", true, false, false, false, false, "key audio rejected: audio path missing");
            SetStatus($"按键音效 {index} 的音频文件不存在：{audioPath}");
            lastTriggerUtc = now;
            return;
        }

        if (!PlayOneShotAudioPath(audioPath, out var playbackMessage))
        {
            InputDebugLogger.LogTriggerDecision($"Key audio {index} playback", binding, $"configured=0x{binding.KeyCode:X2}", true, false, false, false, false, $"key audio rejected: playback failed: {playbackMessage}");
            SetStatus(playbackMessage);
            lastTriggerUtc = now;
            return;
        }

        lastTriggerUtc = now;
        InputDebugLogger.LogTriggerDecision($"Key audio {index} playback", binding, $"configured=0x{binding.KeyCode:X2}", true, false, false, false, true, "key audio triggered");
        SetStatus($"按键音效 {index} 已触发。");
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

    private bool PlayOneShotAudioPath(string path, out string message)
    {
        lock (_audioLock)
        {
            if (!TryResolveAudioPath(path, out var audioPath))
            {
                message = "音频播放失败，请检查文件格式或路径。";
                return false;
            }

            AudioFileReader? audioFile = null;
            WaveOutEvent? outputDevice = null;
            try
            {
                audioFile = new AudioFileReader(audioPath)
                {
                    Volume = Math.Clamp(_config.AudioVolume, 0f, 1f)
                };
                outputDevice = CreateOutputDevice();
                var playback = new OneShotAudioPlayback(outputDevice, audioFile);
                outputDevice.PlaybackStopped += OneShotOutputDevice_PlaybackStopped;
                _oneShotAudioPlaybacks.Add(playback);
                outputDevice.Init(audioFile);
                outputDevice.Play();
                message = "音频已开始播放。";
                return true;
            }
            catch
            {
                if (outputDevice is not null)
                {
                    _oneShotAudioPlaybacks.RemoveAll(item => ReferenceEquals(item.OutputDevice, outputDevice));
                    outputDevice.PlaybackStopped -= OneShotOutputDevice_PlaybackStopped;
                    outputDevice.Dispose();
                }

                audioFile?.Dispose();
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

    private void OneShotOutputDevice_PlaybackStopped(object? sender, StoppedEventArgs e)
    {
        lock (_audioLock)
        {
            var playback = _oneShotAudioPlaybacks.FirstOrDefault(item => ReferenceEquals(item.OutputDevice, sender));
            if (playback is null)
            {
                return;
            }

            _oneShotAudioPlaybacks.Remove(playback);
            playback.OutputDevice.PlaybackStopped -= OneShotOutputDevice_PlaybackStopped;
            playback.OutputDevice.Dispose();
            playback.AudioFile.Dispose();
        }
    }

    private void DisposeAudio()
    {
        lock (_audioLock)
        {
            DisposeMainAudioLocked();
            DisposeOneShotAudioLocked();
        }
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

    private void DisposeOneShotAudioLocked()
    {
        foreach (var playback in _oneShotAudioPlaybacks.ToList())
        {
            playback.OutputDevice.PlaybackStopped -= OneShotOutputDevice_PlaybackStopped;
            playback.OutputDevice.Stop();
            playback.OutputDevice.Dispose();
            playback.AudioFile.Dispose();
        }

        _oneShotAudioPlaybacks.Clear();
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
            _watchWindowBox.Value = Math.Clamp(_config.WatchWindowMs, (int)_watchWindowBox.Minimum, (int)_watchWindowBox.Maximum);
            _pollIntervalBox.Value = Math.Clamp(_config.PollIntervalMs, (int)_pollIntervalBox.Minimum, (int)_pollIntervalBox.Maximum);
            _healthConsecutiveFramesBox.Value = Math.Clamp(_config.HealthConsecutiveFramesRequired, (int)_healthConsecutiveFramesBox.Minimum, (int)_healthConsecutiveFramesBox.Maximum);
            _healthThresholdBar.Value = Math.Clamp(_config.HealthGrowthPixelThreshold, _healthThresholdBar.Minimum, _healthThresholdBar.Maximum);
            _audioVolumeBar.Value = Math.Clamp((int)Math.Round(_config.AudioVolume * 100f), _audioVolumeBar.Minimum, _audioVolumeBar.Maximum);
            SelectConfiguredAudioOutputDevice();
            var textTrigger = EnsureDefaultTextTrigger();
            _textTriggerEnabledBox.Checked = textTrigger.Enabled;
            _textTriggerTextBox.Text = textTrigger.Text;
            _textTriggerScanIntervalBox.Value = Math.Clamp(textTrigger.ScanIntervalMs, (int)_textTriggerScanIntervalBox.Minimum, (int)_textTriggerScanIntervalBox.Maximum);
            _textTriggerCooldownBox.Value = Math.Clamp(textTrigger.CooldownSeconds, (int)_textTriggerCooldownBox.Minimum, (int)_textTriggerCooldownBox.Maximum);
            UpdateTextTriggerDisplay();
            _ultHotkeyTriggerEnabledBox.Checked = _config.UltHotkeyTrigger.Enabled;
            _ultHotkeyScanIntervalBox.Value = Math.Clamp(_config.UltHotkeyTrigger.ScanIntervalMs, (int)_ultHotkeyScanIntervalBox.Minimum, (int)_ultHotkeyScanIntervalBox.Maximum);
            _ultHotkeyCooldownBox.Value = Math.Clamp(_config.UltHotkeyTrigger.CooldownSeconds, (int)_ultHotkeyCooldownBox.Minimum, (int)_ultHotkeyCooldownBox.Maximum);
            UpdateUltHotkeyTriggerDisplay();
            _imageHotkeyTriggerEnabledBox.Checked = _config.ImageHotkeyTrigger.Enabled;
            _imageHotkeyScanIntervalBox.Value = Math.Clamp(_config.ImageHotkeyTrigger.ScanIntervalMs, (int)_imageHotkeyScanIntervalBox.Minimum, (int)_imageHotkeyScanIntervalBox.Maximum);
            _imageHotkeyCooldownBox.Value = Math.Clamp(_config.ImageHotkeyTrigger.CooldownSeconds, (int)_imageHotkeyCooldownBox.Minimum, (int)_imageHotkeyCooldownBox.Maximum);
            UpdateImageHotkeyTriggerDisplay();
            _keyAudioTriggerEnabledBox.Checked = _config.KeyAudioTrigger.Enabled;
            _keyAudioCooldownBox.Value = Math.Clamp(_config.KeyAudioTrigger.CooldownSeconds, (int)_keyAudioCooldownBox.Minimum, (int)_keyAudioCooldownBox.Maximum);
            UpdateKeyAudioTriggerDisplay();
            SyncTextTriggerTimer();
            SyncUltHotkeyScanTimer();
            SyncImageHotkeyScanTimer();
            UpdateRegionCaptureHotkeyButtonText();
            UpdateThresholdDisplay();
            UpdateVolumeDisplay();
            ApplyAudioVolume();
            _triggerMonitorService.TriggerKey = _config.TriggerKey;
            _triggerMonitorService.TriggerBinding = _config.TriggerInput.Clone();
            _regionCaptureMonitorService.TriggerKey = _config.RegionCaptureKey;
            _regionCaptureMonitorService.TriggerBinding = _config.RegionCaptureInput.Clone();
            _skillRegionCaptureMonitorService.TriggerKey = _config.RegionCaptureHotkeys.SkillRegionKey;
            _skillRegionCaptureMonitorService.TriggerBinding = _config.RegionCaptureHotkeys.SkillRegionInput.Clone();
            _healthRegionCaptureMonitorService.TriggerKey = _config.RegionCaptureHotkeys.HealthRegionKey;
            _healthRegionCaptureMonitorService.TriggerBinding = _config.RegionCaptureHotkeys.HealthRegionInput.Clone();
            _ocrTextRegionCaptureMonitorService.TriggerKey = _config.RegionCaptureHotkeys.OcrTextRegionKey;
            _ocrTextRegionCaptureMonitorService.TriggerBinding = _config.RegionCaptureHotkeys.OcrTextRegionInput.Clone();
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

        var textTrigger = EnsureDefaultTextTrigger();
        if (textTrigger.Region is not null)
        {
            _regionsListBox.Items.Add($"OCR文字触发区域 | {textTrigger.Text} | {textTrigger.Region}");
        }

        if (_config.UltHotkeyTrigger.Region is not null)
        {
            _regionsListBox.Items.Add($"大招区域 | {_config.UltHotkeyTrigger.Region}");
        }

        if (_config.ImageHotkeyTrigger.Region is not null)
        {
            _regionsListBox.Items.Add($"战技区域 | {_config.ImageHotkeyTrigger.Region}");
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
        var textTrigger = EnsureDefaultTextTrigger();
        textTrigger.Enabled = _textTriggerEnabledBox.Checked;
        textTrigger.Text = string.IsNullOrWhiteSpace(_textTriggerTextBox.Text)
            ? "YOU DIED"
            : _textTriggerTextBox.Text.Trim();
        textTrigger.ScanIntervalMs = (int)_textTriggerScanIntervalBox.Value;
        textTrigger.CooldownSeconds = (int)_textTriggerCooldownBox.Value;
        _config.UltHotkeyTrigger.Enabled = _ultHotkeyTriggerEnabledBox.Checked;
        _config.UltHotkeyTrigger.HotkeyInput = _config.TriggerInput.Clone();
        _config.UltHotkeyTrigger.Hotkey = _config.TriggerInput.KeyCode;
        _config.UltHotkeyTrigger.HotkeyName = _config.TriggerInput.DisplayName;
        _config.UltHotkeyTrigger.SelectedSkillIndex = _ultHotkeySkillsListBox.SelectedIndex < 0
            ? 0
            : _ultHotkeySkillsListBox.SelectedIndex;
        _config.UltHotkeyTrigger.ScanIntervalMs = (int)_ultHotkeyScanIntervalBox.Value;
        _config.UltHotkeyTrigger.CooldownSeconds = (int)_ultHotkeyCooldownBox.Value;
        _config.ImageHotkeyTrigger.Enabled = _imageHotkeyTriggerEnabledBox.Checked;
        _config.ImageHotkeyTrigger.ScanIntervalMs = (int)_imageHotkeyScanIntervalBox.Value;
        _config.ImageHotkeyTrigger.CooldownSeconds = (int)_imageHotkeyCooldownBox.Value;
        _config.KeyAudioTrigger.Enabled = _keyAudioTriggerEnabledBox.Checked;
        _config.KeyAudioTrigger.CooldownSeconds = (int)_keyAudioCooldownBox.Value;
        _configService.Save(_config);
    }

    private void UpdateRegionCaptureHotkeyButtonText()
    {
        _setSkillRegionCaptureKeyButton.Text = $"设置技能区域快捷键：{_config.RegionCaptureHotkeys.SkillRegionKeyName}";
        _setHealthRegionCaptureKeyButton.Text = $"设置血条区域快捷键：{_config.RegionCaptureHotkeys.HealthRegionKeyName}";
        _setOcrTextRegionCaptureKeyButton.Text = $"设置OCR文字区域快捷键：{_config.RegionCaptureHotkeys.OcrTextRegionKeyName}";
        _setTriggerKeyButton.Text = $"大招按键：{_config.TriggerKeyName}";
        _setImageHotkeyButton.Text = $"战技按键：{_config.ImageHotkeyTrigger.HotkeyName}";
        UpdateKeyAudioTriggerDisplay();
    }

    private void UpdateKeyAudioTriggerDisplay()
    {
        _setKeyAudioKey1Button.Text = $"按键1：{_config.KeyAudioTrigger.Key1Name}";
        _setKeyAudioKey2Button.Text = $"按键2：{_config.KeyAudioTrigger.Key2Name}";
        _setKeyAudioKey3Button.Text = $"按键3：{_config.KeyAudioTrigger.Key3Name}";
        _keyAudioPath1Box.Text = string.IsNullOrWhiteSpace(_config.KeyAudioTrigger.AudioPath1)
            ? "未选择音频1"
            : Path.GetFileName(_config.KeyAudioTrigger.AudioPath1);
        _keyAudioPath2Box.Text = string.IsNullOrWhiteSpace(_config.KeyAudioTrigger.AudioPath2)
            ? "未选择音频2"
            : Path.GetFileName(_config.KeyAudioTrigger.AudioPath2);
        _keyAudioPath3Box.Text = string.IsNullOrWhiteSpace(_config.KeyAudioTrigger.AudioPath3)
            ? "未选择音频3"
            : Path.GetFileName(_config.KeyAudioTrigger.AudioPath3);
    }

    private void UpdateTextTriggerDisplay()
    {
        var trigger = EnsureDefaultTextTrigger();
        var regionText = trigger.Region is null
            ? "未设置OCR区域"
            : $"OCR区域 {trigger.Region}";
        _textTriggerMusicPathBox.Text = string.IsNullOrWhiteSpace(trigger.MusicPath)
            ? $"{regionText} / 未选择触发音频"
            : $"{regionText} / {Path.GetFileName(trigger.MusicPath)}";
    }

    private void UpdateUltHotkeyTriggerDisplay(bool keepSelection = false, int selectedIndex = -2)
    {
        var trigger = _config.UltHotkeyTrigger;
        var previousIndex = _ultHotkeySkillsListBox.SelectedIndex;
        if (selectedIndex == -2)
        {
            selectedIndex = keepSelection
                ? previousIndex
                : trigger.Skills.Count == 0
                    ? -1
                    : Math.Clamp(trigger.SelectedSkillIndex, 0, trigger.Skills.Count - 1);
        }

        var wasApplyingConfigToUi = _isApplyingConfigToUi;
        _isApplyingConfigToUi = true;
        try
        {
            _ultHotkeySkillsListBox.BeginUpdate();
            _ultHotkeySkillsListBox.Items.Clear();
            for (var i = 0; i < trigger.Skills.Count; i++)
            {
                _ultHotkeySkillsListBox.Items.Add(FormatImageHotkeySkillListItem(trigger.Skills[i], i, "大招"));
            }
            _ultHotkeySkillsListBox.EndUpdate();

            if (trigger.Skills.Count > 0)
            {
                trigger.SelectedSkillIndex = Math.Clamp(selectedIndex, 0, trigger.Skills.Count - 1);
                _ultHotkeySkillsListBox.SelectedIndex = trigger.SelectedSkillIndex;
            }
            else
            {
                trigger.SelectedSkillIndex = 0;
                _ultHotkeySkillsListBox.SelectedIndex = -1;
            }
        }
        finally
        {
            _isApplyingConfigToUi = wasApplyingConfigToUi;
        }

        _ultHotkeyRegionBox.Text = trigger.Region is null
            ? "未设置大招区域"
            : trigger.Region.ToString();
        UpdateSelectedUltHotkeySkillDisplay();
        UpdateRegionCaptureHotkeyButtonText();
    }

    private void UpdateSelectedUltHotkeySkillDisplay()
    {
        var ult = GetSelectedUltHotkeySkill();
        var wasApplyingConfigToUi = _isApplyingConfigToUi;
        _isApplyingConfigToUi = true;
        try
        {
            _ultHotkeySkillNameBox.Enabled = ult is not null;
            _browseUltHotkeyTemplateButton.Enabled = ult is not null;
            _browseUltHotkeyAudioButton.Enabled = ult is not null;
            _ultHotkeySimilarityBox.Enabled = ult is not null;
            _deleteUltHotkeySkillButton.Enabled = ult is not null;

            if (ult is null)
            {
                _ultHotkeySkillNameBox.Text = string.Empty;
                _ultHotkeyTemplatePathBox.Text = "未添加大招";
                _ultHotkeyAudioPathBox.Text = "未添加大招";
                _ultHotkeySimilarityBox.Value = 0.85M;
                return;
            }

            _ultHotkeySkillNameBox.Text = ult.Name;
            _ultHotkeyTemplatePathBox.Text = string.IsNullOrWhiteSpace(ult.TemplateImagePath)
                ? "未选择大招模板"
                : Path.GetFileName(ult.TemplateImagePath);
            _ultHotkeyAudioPathBox.Text = string.IsNullOrWhiteSpace(ult.AudioPath)
                ? "未选择大招音效"
                : Path.GetFileName(ult.AudioPath);
            _ultHotkeySimilarityBox.Value = Math.Clamp((decimal)ult.SimilarityThreshold, _ultHotkeySimilarityBox.Minimum, _ultHotkeySimilarityBox.Maximum);
        }
        finally
        {
            _isApplyingConfigToUi = wasApplyingConfigToUi;
        }
    }

    private void UpdateSelectedUltHotkeySkillListItem()
    {
        var index = _ultHotkeySkillsListBox.SelectedIndex;
        if (index < 0 || index >= _config.UltHotkeyTrigger.Skills.Count)
        {
            return;
        }

        _ultHotkeySkillsListBox.Items[index] = FormatImageHotkeySkillListItem(_config.UltHotkeyTrigger.Skills[index], index, "大招");
    }

    private ImageHotkeySkillConfig? GetSelectedUltHotkeySkill()
    {
        var index = _ultHotkeySkillsListBox.SelectedIndex;
        return index >= 0 && index < _config.UltHotkeyTrigger.Skills.Count
            ? _config.UltHotkeyTrigger.Skills[index]
            : null;
    }

    private ImageHotkeySkillConfig? GetActiveUltHotkeySkill()
    {
        var index = _config.UltHotkeyTrigger.SelectedSkillIndex;
        return index >= 0 && index < _config.UltHotkeyTrigger.Skills.Count
            ? _config.UltHotkeyTrigger.Skills[index]
            : null;
    }

    private ImageHotkeySkillConfig EnsureSelectedUltHotkeySkill()
    {
        var ult = GetSelectedUltHotkeySkill();
        if (ult is not null)
        {
            return ult;
        }

        ult = new ImageHotkeySkillConfig
        {
            Name = $"大招 {_config.UltHotkeyTrigger.Skills.Count + 1}",
            SimilarityThreshold = 0.85
        };
        _config.UltHotkeyTrigger.Skills.Add(ult);
        UpdateUltHotkeyTriggerDisplay(selectedIndex: _config.UltHotkeyTrigger.Skills.Count - 1);
        return ult;
    }

    private void UpdateImageHotkeyTriggerDisplay(bool keepSelection = false, int selectedIndex = -2)
    {
        var trigger = _config.ImageHotkeyTrigger;
        var previousIndex = _imageHotkeySkillsListBox.SelectedIndex;
        if (selectedIndex == -2)
        {
            selectedIndex = keepSelection ? previousIndex : Math.Min(previousIndex < 0 ? 0 : previousIndex, trigger.Skills.Count - 1);
        }

        var wasApplyingConfigToUi = _isApplyingConfigToUi;
        _isApplyingConfigToUi = true;
        try
        {
            _imageHotkeySkillsListBox.BeginUpdate();
            _imageHotkeySkillsListBox.Items.Clear();
            for (var i = 0; i < trigger.Skills.Count; i++)
            {
                _imageHotkeySkillsListBox.Items.Add(FormatImageHotkeySkillListItem(trigger.Skills[i], i, "战技"));
            }
            _imageHotkeySkillsListBox.EndUpdate();

            if (trigger.Skills.Count > 0)
            {
                _imageHotkeySkillsListBox.SelectedIndex = Math.Clamp(selectedIndex, 0, trigger.Skills.Count - 1);
            }
            else
            {
                _imageHotkeySkillsListBox.SelectedIndex = -1;
            }
        }
        finally
        {
            _isApplyingConfigToUi = wasApplyingConfigToUi;
        }

        _imageHotkeyRegionBox.Text = trigger.Region is null
            ? "未设置战技区域"
            : trigger.Region.ToString();
        UpdateSelectedImageHotkeySkillDisplay();
        UpdateRegionCaptureHotkeyButtonText();
    }

    private void UpdateSelectedImageHotkeySkillDisplay()
    {
        var skill = GetSelectedImageHotkeySkill();
        var wasApplyingConfigToUi = _isApplyingConfigToUi;
        _isApplyingConfigToUi = true;
        try
        {
            _imageHotkeySkillNameBox.Enabled = skill is not null;
            _browseImageHotkeyTemplateButton.Enabled = skill is not null;
            _browseImageHotkeyAudioButton.Enabled = skill is not null;
            _imageHotkeySimilarityBox.Enabled = skill is not null;
            _deleteImageHotkeySkillButton.Enabled = skill is not null;

            if (skill is null)
            {
                _imageHotkeySkillNameBox.Text = string.Empty;
                _imageHotkeyTemplatePathBox.Text = "未添加战技";
                _imageHotkeyAudioPathBox.Text = "未添加战技";
                _imageHotkeySimilarityBox.Value = 0.85M;
                return;
            }

            _imageHotkeySkillNameBox.Text = skill.Name;
            _imageHotkeyTemplatePathBox.Text = string.IsNullOrWhiteSpace(skill.TemplateImagePath)
                ? "未选择战技模板"
                : Path.GetFileName(skill.TemplateImagePath);
            _imageHotkeyAudioPathBox.Text = string.IsNullOrWhiteSpace(skill.AudioPath)
                ? "未选择战技音效"
                : Path.GetFileName(skill.AudioPath);
            _imageHotkeySimilarityBox.Value = Math.Clamp((decimal)skill.SimilarityThreshold, _imageHotkeySimilarityBox.Minimum, _imageHotkeySimilarityBox.Maximum);
        }
        finally
        {
            _isApplyingConfigToUi = wasApplyingConfigToUi;
        }
    }

    private void UpdateSelectedImageHotkeySkillListItem()
    {
        var index = _imageHotkeySkillsListBox.SelectedIndex;
        if (index < 0 || index >= _config.ImageHotkeyTrigger.Skills.Count)
        {
            return;
        }

        _imageHotkeySkillsListBox.Items[index] = FormatImageHotkeySkillListItem(_config.ImageHotkeyTrigger.Skills[index], index, "战技");
    }

    private ImageHotkeySkillConfig? GetSelectedImageHotkeySkill()
    {
        var index = _imageHotkeySkillsListBox.SelectedIndex;
        return index >= 0 && index < _config.ImageHotkeyTrigger.Skills.Count
            ? _config.ImageHotkeyTrigger.Skills[index]
            : null;
    }

    private ImageHotkeySkillConfig EnsureSelectedImageHotkeySkill()
    {
        var skill = GetSelectedImageHotkeySkill();
        if (skill is not null)
        {
            return skill;
        }

        skill = new ImageHotkeySkillConfig
        {
            Name = $"战技 {_config.ImageHotkeyTrigger.Skills.Count + 1}",
            SimilarityThreshold = 0.85
        };
        _config.ImageHotkeyTrigger.Skills.Add(skill);
        UpdateImageHotkeyTriggerDisplay(selectedIndex: _config.ImageHotkeyTrigger.Skills.Count - 1);
        return skill;
    }

    private static ImageHotkeySkillConfig CloneImageHotkeySkill(ImageHotkeySkillConfig skill)
    {
        return new ImageHotkeySkillConfig
        {
            Name = skill.Name,
            TemplateImagePath = skill.TemplateImagePath,
            AudioPath = skill.AudioPath,
            SimilarityThreshold = skill.SimilarityThreshold
        };
    }

    private string FormatImageHotkeySkillListItem(ImageHotkeySkillConfig skill, int index, string defaultNamePrefix)
    {
        var activePrefix = defaultNamePrefix == "大招" && index == _config.UltHotkeyTrigger.SelectedSkillIndex ? "当前 " : string.Empty;
        var name = string.IsNullOrWhiteSpace(skill.Name) ? $"{defaultNamePrefix} {index + 1}" : skill.Name;
        var templateName = string.IsNullOrWhiteSpace(skill.TemplateImagePath) ? "未选模板" : Path.GetFileName(skill.TemplateImagePath);
        var audioName = string.IsNullOrWhiteSpace(skill.AudioPath) ? "未选音效" : Path.GetFileName(skill.AudioPath);
        return $"{activePrefix}{name} | 阈值 {skill.SimilarityThreshold:0.##} | {templateName} | {audioName}";
    }

    private void SyncTextTriggerTimer()
    {
        var trigger = EnsureDefaultTextTrigger();
        _textTriggerTimer.Interval = Math.Clamp(trigger.ScanIntervalMs, 100, 10000);

        if (trigger.Enabled && !_isRegionCaptureRunning)
        {
            _textTriggerTimer.Start();
        }
        else
        {
            _textTriggerTimer.Stop();
        }
    }

    private void SyncImageHotkeyScanTimer()
    {
        _imageHotkeyScanTimer.Interval = Math.Clamp(_config.ImageHotkeyTrigger.ScanIntervalMs, 100, 10000);
        if (_config.ImageHotkeyTrigger.Enabled && !_isRegionCaptureRunning)
        {
            _imageHotkeyScanTimer.Start();
        }
        else
        {
            _imageHotkeyScanTimer.Stop();
            _imageHotkeyMatched = false;
            _matchedImageHotkeySkill = null;
            _lastImageHotkeyScore = 0;
        }
    }

    private void SyncUltHotkeyScanTimer()
    {
        _ultHotkeyScanTimer.Interval = Math.Clamp(_config.UltHotkeyTrigger.ScanIntervalMs, 100, 10000);
        if (_config.UltHotkeyTrigger.Enabled && !_isRegionCaptureRunning)
        {
            _ultHotkeyScanTimer.Start();
        }
        else
        {
            _ultHotkeyScanTimer.Stop();
            _ultHotkeyMatched = false;
            _matchedUltHotkeySkill = null;
            _lastUltHotkeyScore = 0;
        }
    }

    private void SetRegionCaptureHotkeyMonitorsEnabled(bool enabled)
    {
        _skillRegionCaptureMonitorService.Enabled = enabled;
        _healthRegionCaptureMonitorService.Enabled = enabled;
        _ocrTextRegionCaptureMonitorService.Enabled = enabled;
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
            Region = null,
            Text = "YOU DIED",
            MusicPath = _config.AudioPath,
            ScanIntervalMs = 500,
            CooldownSeconds = 5
        };
        _config.TextTriggers.Add(trigger);
        return trigger;
    }

    private void UpdateStatus()
    {
        var outputDevice = string.IsNullOrWhiteSpace(_config.AudioOutputDeviceName)
            ? "系统默认输出设备"
            : _config.AudioOutputDeviceName;
        var enabledTextTriggerCount = _config.TextTriggers.Count(trigger => trigger.Enabled);
        var ultHotkeyTriggerState = _config.UltHotkeyTrigger.Enabled ? "开" : "关";
        var imageHotkeyTriggerState = _config.ImageHotkeyTrigger.Enabled ? "开" : "关";
        var keyAudioTriggerState = _config.KeyAudioTrigger.Enabled ? "开" : "关";
        var regionCount = _config.Regions.Count;
        _statusLabel.Text =
            $"输出：{outputDevice}。OCR文字触发：{enabledTextTriggerCount}。大招音效：{ultHotkeyTriggerState}。战技音效：{imageHotkeyTriggerState}。按键音效：{keyAudioTriggerState}。大招触发键：{_config.TriggerKeyName}。截图键：{_config.RegionCaptureKeyName}。检测区域数量：{regionCount}。";
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

            foreach (var playback in _oneShotAudioPlaybacks)
            {
                playback.AudioFile.Volume = Math.Clamp(_config.AudioVolume, 0f, 1f);
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
        _ocrTextRegionCaptureMonitorService.Dispose();
        _textTriggerTimer.Stop();
        _textTriggerTimer.Tick -= TextTriggerTimer_Tick;
        _textTriggerTimer.Dispose();
        _ultHotkeyScanTimer.Stop();
        _ultHotkeyScanTimer.Tick -= UltHotkeyScanTimer_Tick;
        _ultHotkeyScanTimer.Dispose();
        _imageHotkeyScanTimer.Stop();
        _imageHotkeyScanTimer.Tick -= ImageHotkeyScanTimer_Tick;
        _imageHotkeyScanTimer.Dispose();
        _inputCaptureService.InputBindingPressed -= InputCaptureService_InputBindingPressed;
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

    private sealed record OneShotAudioPlayback(WaveOutEvent OutputDevice, AudioFileReader AudioFile);

    private sealed record TextRecognitionResult(bool Success, string Text, string ErrorMessage);

    private sealed record ImageHotkeySkillScanResult(
        ImageHotkeySkillConfig? BestPassingSkill,
        double BestPassingScore,
        double BestOverallScore,
        int ValidTemplateCount);
}
