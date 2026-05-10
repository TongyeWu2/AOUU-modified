using System.Collections.Generic;

namespace AOUU.Models;

public sealed class AppConfig
{
    public string AudioPath { get; set; } = string.Empty;

    public float AudioVolume { get; set; } = 1.0f;

    public string AudioOutputDeviceName { get; set; } = string.Empty;

    public int TriggerKey { get; set; } = 0x77;

    public string TriggerKeyName { get; set; } = "F8";

    public int RegionCaptureKey { get; set; } = 0x79;

    public string RegionCaptureKeyName { get; set; } = "F10";

    public int HealthBaselineRefreshSeconds { get; set; } = 30;

    public int WatchWindowMs { get; set; } = 1000;

    public int PollIntervalMs { get; set; } = 5;

    public int HealthGrowthPixelThreshold { get; set; } = 1;

    public int HealthConsecutiveFramesRequired { get; set; } = 2;

    public List<WatchRegion> Regions { get; set; } = [];

    public List<TextTriggerConfig> TextTriggers { get; set; } = [];

    public ImageHotkeyTriggerConfig UltHotkeyTrigger { get; set; } = new();

    public ImageHotkeyTriggerConfig ImageHotkeyTrigger { get; set; } = new();

    public RegionCaptureHotkeysConfig RegionCaptureHotkeys { get; set; } = new();
}

public sealed class RegionCaptureHotkeysConfig
{
    public int SkillRegionKey { get; set; } = 0x75;

    public string SkillRegionKeyName { get; set; } = "F6";

    public int HealthRegionKey { get; set; } = 0x76;

    public string HealthRegionKeyName { get; set; } = "F7";

    public int OcrTextRegionKey { get; set; } = 0x78;

    public string OcrTextRegionKeyName { get; set; } = "F9";
}

public sealed class TextTriggerConfig
{
    public bool Enabled { get; set; } = true;

    public ScreenBounds? Region { get; set; }

    public string Text { get; set; } = "YOU DIED";

    public string MusicPath { get; set; } = string.Empty;

    public int ScanIntervalMs { get; set; } = 500;

    public int CooldownSeconds { get; set; } = 5;
}

public sealed class ImageHotkeyTriggerConfig
{
    public bool Enabled { get; set; }

    public ScreenBounds? Region { get; set; }

    public List<ImageHotkeySkillConfig> Skills { get; set; } = [];

    public int SelectedSkillIndex { get; set; }

    // Legacy single-skill fields are kept so old config files can migrate safely.
    public string TemplateImagePath { get; set; } = string.Empty;

    public double SimilarityThreshold { get; set; } = 0.85;

    public int Hotkey { get; set; } = 0x7A;

    public string HotkeyName { get; set; } = "F11";

    public string AudioPath { get; set; } = string.Empty;

    public int ScanIntervalMs { get; set; } = 200;

    public int CooldownSeconds { get; set; } = 5;
}

public sealed class ImageHotkeySkillConfig
{
    public string Name { get; set; } = string.Empty;

    public string TemplateImagePath { get; set; } = string.Empty;

    public string AudioPath { get; set; } = string.Empty;

    public double SimilarityThreshold { get; set; } = 0.85;
}
