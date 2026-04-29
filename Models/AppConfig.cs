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
}
