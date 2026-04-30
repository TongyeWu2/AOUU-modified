using System.Collections.Generic;

namespace AOUU.Models;

public sealed class AppConfig
{
    public string AudioPath { get; set; } = string.Empty;

    public string LeftClickAudioPath { get; set; } = string.Empty;

    public string RightClickAudioPath { get; set; } = string.Empty;

    public float AudioVolume { get; set; } = 1.0f;

    public string AudioOutputDeviceName { get; set; } = string.Empty;

    public bool UseSoundpadOutput { get; set; }

    public string SoundpadExecutablePath { get; set; } = string.Empty;

    public int SoundpadSoundIndex { get; set; } = 1;

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

    public DeathTriggerConfig DeathTrigger { get; set; } = new();

    public RegionCaptureHotkeysConfig RegionCaptureHotkeys { get; set; } = new();
}

public sealed class RegionCaptureHotkeysConfig
{
    public int SkillRegionKey { get; set; } = 0x75;

    public string SkillRegionKeyName { get; set; } = "F6";

    public int HealthRegionKey { get; set; } = 0x76;

    public string HealthRegionKeyName { get; set; } = "F7";

    public int DeathTextRegionKey { get; set; } = 0x78;

    public string DeathTextRegionKeyName { get; set; } = "F9";
}

public sealed class TextTriggerConfig
{
    public bool Enabled { get; set; } = true;

    public string Text { get; set; } = "YOU DIED";

    public string MusicPath { get; set; } = string.Empty;

    public int CooldownSeconds { get; set; } = 5;
}

public sealed class DeathTriggerConfig
{
    public bool Enabled { get; set; }

    public ScreenBounds? HealthRegion { get; set; }

    public ScreenBounds? DeathTextRegion { get; set; }

    public string DeathTemplateImagePath { get; set; } = string.Empty;

    public string DeathMusicPath { get; set; } = string.Empty;

    public double TemplateSimilarityThreshold { get; set; } = 0.75;

    public int HealthZeroPixelThreshold { get; set; } = 3;

    public int ScanIntervalMs { get; set; } = 500;

    public int CooldownSeconds { get; set; } = 8;

    public DeathTriggerConfig Clone()
    {
        return new DeathTriggerConfig
        {
            Enabled = Enabled,
            HealthRegion = CloneBounds(HealthRegion),
            DeathTextRegion = CloneBounds(DeathTextRegion),
            DeathTemplateImagePath = DeathTemplateImagePath,
            DeathMusicPath = DeathMusicPath,
            TemplateSimilarityThreshold = TemplateSimilarityThreshold,
            HealthZeroPixelThreshold = HealthZeroPixelThreshold,
            ScanIntervalMs = ScanIntervalMs,
            CooldownSeconds = CooldownSeconds
        };
    }

    private static ScreenBounds? CloneBounds(ScreenBounds? bounds)
    {
        if (bounds is null)
        {
            return null;
        }

        return new ScreenBounds
        {
            X = bounds.X,
            Y = bounds.Y,
            Width = bounds.Width,
            Height = bounds.Height
        };
    }
}
