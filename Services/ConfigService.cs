using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using AOUU.Models;

namespace AOUU.Services;

public sealed class ConfigService
{
    private readonly string _configPath;
    private readonly string _appDirectoryConfigPath;
    private readonly string _legacyConfigPath;
    private readonly string _bundledDefaultTemplateDirectory;
    private readonly JsonSerializerOptions _serializerOptions = new()
    {
        WriteIndented = true
    };

    public ConfigService(string baseDirectory)
    {
        var userDataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AOUU");

        _configPath = Path.Combine(userDataDirectory, "config.json");
        _appDirectoryConfigPath = Path.Combine(baseDirectory, "config.json");
        _legacyConfigPath = Path.Combine(baseDirectory, "config.txt");
        _bundledDefaultTemplateDirectory = Path.Combine(baseDirectory, "templates", "defaults");
        TemplateDirectory = Path.Combine(userDataDirectory, "templates");
        DefaultTemplateDirectory = Path.Combine(TemplateDirectory, "defaults");
        DefaultAudioDirectory = Path.Combine(baseDirectory, "assets", "audio");
        DefaultAudioPath = Path.Combine(DefaultAudioDirectory, "default.wav");
        RecognitionDebugDirectory = Path.Combine(baseDirectory, "debug", "recognition");
        DefaultSkillReadyTemplatePath = Path.Combine(DefaultTemplateDirectory, "skill_ready.png");
        DefaultSkillEmptyTemplatePath = Path.Combine(DefaultTemplateDirectory, "skill_empty.png");
        DefaultHealthTemplatePath = Path.Combine(DefaultTemplateDirectory, "health_anchor.png");
    }

    public string TemplateDirectory { get; }

    public string DefaultTemplateDirectory { get; }

    public string DefaultAudioDirectory { get; }

    public string DefaultAudioPath { get; }

    public string RecognitionDebugDirectory { get; }

    public string DefaultSkillReadyTemplatePath { get; }

    public string DefaultSkillEmptyTemplatePath { get; }

    public string DefaultHealthTemplatePath { get; }

    public bool HasDefaultSkillReadyTemplate()
    {
        return File.Exists(DefaultSkillReadyTemplatePath);
    }

    public bool HasDefaultSkillEmptyTemplate()
    {
        return File.Exists(DefaultSkillEmptyTemplatePath);
    }

    public bool HasDefaultHealthTemplate()
    {
        return File.Exists(DefaultHealthTemplatePath);
    }

    public AppConfig Load()
    {
        Directory.CreateDirectory(TemplateDirectory);
        Directory.CreateDirectory(DefaultTemplateDirectory);
        EnsureBundledDefaultTemplates();

        if (TryLoadJson(_configPath, out var userConfig))
        {
            return userConfig;
        }

        if (TryLoadJson(_appDirectoryConfigPath, out var appDirectoryConfig))
        {
            Save(appDirectoryConfig);
            return appDirectoryConfig;
        }

        if (!File.Exists(_legacyConfigPath))
        {
            return CreateDefault();
        }

        var legacyConfig = CreateDefault();
        var lines = File.ReadAllLines(_legacyConfigPath);

        if (lines.Length >= 1)
        {
            legacyConfig.AudioPath = lines[0];
        }

        if (lines.Length >= 2 && int.TryParse(lines[1], out var savedKey))
        {
            legacyConfig.TriggerKey = savedKey;
        }

        legacyConfig.TriggerKeyName = lines.Length >= 3
            ? lines[2]
            : TriggerMonitorService.GetKeyName(legacyConfig.TriggerKey);
        legacyConfig.RegionCaptureKeyName = TriggerMonitorService.GetKeyName(legacyConfig.RegionCaptureKey);

        Save(legacyConfig);
        return legacyConfig;
    }

    public void Save(AppConfig config)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_configPath)!);
        Directory.CreateDirectory(TemplateDirectory);
        Directory.CreateDirectory(DefaultTemplateDirectory);
        EnsureBundledDefaultTemplates();
        var dto = MapToDto(config);
        File.WriteAllText(_configPath, JsonSerializer.Serialize(dto, _serializerOptions));
    }

    private AppConfig CreateDefault()
    {
        var defaultAudioPath = File.Exists(DefaultAudioPath) ? DefaultAudioPath : string.Empty;
        return new AppConfig
        {
            AudioPath = defaultAudioPath,
            TextTriggers =
            [
                new TextTriggerConfig
                {
                    Enabled = true,
                    Text = "YOU DIED",
                    MusicPath = defaultAudioPath,
                    CooldownSeconds = 5
                }
            ],
            DeathTrigger = new DeathTriggerConfig()
        };
    }

    private void EnsureBundledDefaultTemplates()
    {
        CopyBundledDefaultTemplate("skill_ready.png");
        CopyBundledDefaultTemplate("skill_empty.png");
        CopyBundledDefaultTemplate("health_anchor.png");
    }

    private void CopyBundledDefaultTemplate(string fileName)
    {
        var sourcePath = Path.Combine(_bundledDefaultTemplateDirectory, fileName);
        var destinationPath = Path.Combine(DefaultTemplateDirectory, fileName);

        if (!File.Exists(destinationPath) && File.Exists(sourcePath))
        {
            File.Copy(sourcePath, destinationPath);
        }
    }

    private bool TryLoadJson(string path, out AppConfig config)
    {
        config = null!;

        if (!File.Exists(path))
        {
            return false;
        }

        try
        {
            var dto = JsonSerializer.Deserialize<AppConfigDto>(File.ReadAllText(path), _serializerOptions);
            if (dto is null)
            {
                return false;
            }

            config = MapFromDto(dto);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private AppConfig MapFromDto(AppConfigDto dto)
    {
        var config = CreateDefault();
        config.AudioPath = dto.AudioPath ?? config.AudioPath;
        config.LeftClickAudioPath = dto.LeftClickAudioPath ?? string.Empty;
        config.RightClickAudioPath = dto.RightClickAudioPath ?? string.Empty;
        config.AudioVolume = Math.Clamp(dto.AudioVolume, 0.0f, 1.0f);
        config.AudioOutputDeviceName = dto.AudioOutputDeviceName ?? string.Empty;
        config.UseSoundpadOutput = dto.UseSoundpadOutput;
        config.SoundpadExecutablePath = dto.SoundpadExecutablePath ?? string.Empty;
        config.SoundpadSoundIndex = Math.Clamp(dto.SoundpadSoundIndex <= 0 ? 1 : dto.SoundpadSoundIndex, 1, 9999);
        config.TriggerKey = TriggerMonitorService.IsSupportedHotkey(dto.TriggerKey) ? dto.TriggerKey : 0x77;
        config.TriggerKeyName = TriggerMonitorService.GetKeyName(config.TriggerKey);
        config.RegionCaptureKey = TriggerMonitorService.IsSupportedHotkey(dto.RegionCaptureKey) ? dto.RegionCaptureKey : 0x79;
        config.RegionCaptureKeyName = TriggerMonitorService.GetKeyName(config.RegionCaptureKey);
        config.HealthBaselineRefreshSeconds = Math.Clamp(dto.HealthBaselineRefreshSeconds, 5, 300);
        config.WatchWindowMs = Math.Clamp(dto.WatchWindowMs, 200, 10000);
        config.PollIntervalMs = Math.Clamp(dto.PollIntervalMs, 5, 1000);
        config.HealthGrowthPixelThreshold = Math.Clamp(dto.HealthGrowthPixelThreshold, 1, 20);
        config.HealthConsecutiveFramesRequired = Math.Clamp(dto.HealthConsecutiveFramesRequired <= 0 ? 2 : dto.HealthConsecutiveFramesRequired, 1, 20);

        if (!string.IsNullOrWhiteSpace(config.AudioPath) &&
            File.Exists(DefaultAudioPath) &&
            string.Equals(Path.GetFileName(config.AudioPath), Path.GetFileName(DefaultAudioPath), StringComparison.OrdinalIgnoreCase))
        {
            config.AudioPath = DefaultAudioPath;
        }

        if (!string.IsNullOrWhiteSpace(config.AudioPath) &&
            File.Exists(DefaultAudioPath) &&
            string.Equals(Path.GetFileName(config.AudioPath), "Maroon 5 - Animals.wav", StringComparison.OrdinalIgnoreCase))
        {
            config.AudioPath = DefaultAudioPath;
        }

        if (string.IsNullOrWhiteSpace(config.AudioPath) ||
            (!File.Exists(config.AudioPath) && !Directory.Exists(config.AudioPath)))
        {
            config.AudioPath = File.Exists(DefaultAudioPath) ? DefaultAudioPath : string.Empty;
        }

        if (dto.TextTriggers is not null)
        {
            config.TextTriggers.Clear();

            foreach (var textTriggerDto in dto.TextTriggers)
            {
                var triggerText = string.IsNullOrWhiteSpace(textTriggerDto.Text)
                    ? "YOU DIED"
                    : textTriggerDto.Text.Trim();

                config.TextTriggers.Add(new TextTriggerConfig
                {
                    Enabled = textTriggerDto.Enabled,
                    Text = triggerText,
                    MusicPath = textTriggerDto.MusicPath ?? string.Empty,
                    CooldownSeconds = Math.Clamp(textTriggerDto.CooldownSeconds <= 0 ? 5 : textTriggerDto.CooldownSeconds, 1, 3600)
                });
            }
        }

        if (dto.DeathTrigger is not null)
        {
            config.DeathTrigger = MapDeathTriggerFromDto(dto.DeathTrigger);
        }

        if (dto.RegionCaptureHotkeys is not null)
        {
            config.RegionCaptureHotkeys = MapRegionCaptureHotkeysFromDto(dto.RegionCaptureHotkeys);
        }

        foreach (var regionDto in dto.Regions ?? [])
        {
            if (regionDto.Bounds is null || regionDto.Bounds.Width <= 0 || regionDto.Bounds.Height <= 0)
            {
                continue;
            }

            var name = string.IsNullOrWhiteSpace(regionDto.Name) ? "未命名区域" : regionDto.Name;
            var skillConsecutiveFramesRequired = Math.Clamp(regionDto.ConsecutiveFramesRequired, 1, 20);

            if (string.Equals(regionDto.Kind, "skill", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(regionDto.ReadyTemplateImagePath))
                {
                    continue;
                }

                var readyTemplatePath = regionDto.ReadyTemplateImagePath;
                if (string.Equals(Path.GetFileName(readyTemplatePath), Path.GetFileName(DefaultSkillReadyTemplatePath), StringComparison.OrdinalIgnoreCase) &&
                    File.Exists(DefaultSkillReadyTemplatePath))
                {
                    readyTemplatePath = DefaultSkillReadyTemplatePath;
                }

                var emptyTemplatePath = regionDto.EmptyTemplateImagePath ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(emptyTemplatePath) &&
                    string.Equals(Path.GetFileName(emptyTemplatePath), Path.GetFileName(DefaultSkillEmptyTemplatePath), StringComparison.OrdinalIgnoreCase) &&
                    File.Exists(DefaultSkillEmptyTemplatePath))
                {
                    emptyTemplatePath = DefaultSkillEmptyTemplatePath;
                }

                config.Regions.Add(new SkillReadyWatchRegion
                {
                    Name = name,
                    Bounds = regionDto.Bounds,
                    ConsecutiveFramesRequired = skillConsecutiveFramesRequired,
                    ReadyTemplateImagePath = readyTemplatePath,
                    ReadySimilarityThreshold = Math.Clamp(regionDto.ReadySimilarityThreshold, 0.1, 1.0),
                    EmptyTemplateImagePath = emptyTemplatePath,
                    ReadyVsEmptyMargin = Math.Clamp(regionDto.ReadyVsEmptyMargin, 0.01, 0.30)
                });
                continue;
            }

            if (string.Equals(regionDto.Kind, "health", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(regionDto.Kind, "change", StringComparison.OrdinalIgnoreCase))
            {
                var healthConsecutiveFramesRequired = Math.Clamp(
                    regionDto.ConsecutiveFramesRequired <= 0 ? config.HealthConsecutiveFramesRequired : regionDto.ConsecutiveFramesRequired,
                    1,
                    20);

                var healthTemplatePath = regionDto.TemplateImagePath ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(healthTemplatePath) &&
                    string.Equals(Path.GetFileName(healthTemplatePath), Path.GetFileName(DefaultHealthTemplatePath), StringComparison.OrdinalIgnoreCase) &&
                    File.Exists(DefaultHealthTemplatePath))
                {
                    healthTemplatePath = DefaultHealthTemplatePath;
                }

                config.Regions.Add(new HealthChangeWatchRegion
                {
                    Name = name,
                    Bounds = regionDto.Bounds,
                    ConsecutiveFramesRequired = healthConsecutiveFramesRequired,
                    TemplateImagePath = healthTemplatePath,
                    TemplateSimilarityThreshold = Math.Clamp(regionDto.TemplateSimilarityThreshold, 0.1, 1.0),
                    DiffPixelThreshold = (byte)Math.Clamp(regionDto.DiffPixelThreshold, 1, 255),
                    ChangedAreaRatioThreshold = Math.Clamp(regionDto.ChangedAreaRatioThreshold, 0.01, 1.0)
                });
            }
        }

        return config;
    }

    private static AppConfigDto MapToDto(AppConfig config)
    {
        return new AppConfigDto
        {
            AudioPath = config.AudioPath,
            LeftClickAudioPath = config.LeftClickAudioPath,
            RightClickAudioPath = config.RightClickAudioPath,
            AudioVolume = config.AudioVolume,
            AudioOutputDeviceName = config.AudioOutputDeviceName,
            UseSoundpadOutput = config.UseSoundpadOutput,
            SoundpadExecutablePath = config.SoundpadExecutablePath,
            SoundpadSoundIndex = config.SoundpadSoundIndex,
            TriggerKey = config.TriggerKey,
            TriggerKeyName = config.TriggerKeyName,
            RegionCaptureKey = config.RegionCaptureKey,
            RegionCaptureKeyName = config.RegionCaptureKeyName,
            HealthBaselineRefreshSeconds = config.HealthBaselineRefreshSeconds,
            WatchWindowMs = config.WatchWindowMs,
            PollIntervalMs = config.PollIntervalMs,
            HealthGrowthPixelThreshold = config.HealthGrowthPixelThreshold,
            HealthConsecutiveFramesRequired = config.HealthConsecutiveFramesRequired,
            TextTriggers = config.TextTriggers.Select(trigger => new TextTriggerDto
            {
                Enabled = trigger.Enabled,
                Text = trigger.Text,
                MusicPath = trigger.MusicPath,
                CooldownSeconds = trigger.CooldownSeconds
            }).ToList(),
            DeathTrigger = MapDeathTriggerToDto(config.DeathTrigger),
            RegionCaptureHotkeys = MapRegionCaptureHotkeysToDto(config.RegionCaptureHotkeys),
            Regions = config.Regions.Select(region =>
            {
                if (region is SkillReadyWatchRegion skillRegion)
                {
                    return new WatchRegionDto
                    {
                        Kind = "skill",
                        Name = skillRegion.Name,
                        Bounds = skillRegion.Bounds,
                        ConsecutiveFramesRequired = skillRegion.ConsecutiveFramesRequired,
                        ReadyTemplateImagePath = skillRegion.ReadyTemplateImagePath,
                        ReadySimilarityThreshold = skillRegion.ReadySimilarityThreshold,
                        EmptyTemplateImagePath = skillRegion.EmptyTemplateImagePath,
                        ReadyVsEmptyMargin = skillRegion.ReadyVsEmptyMargin
                    };
                }

                var healthRegion = (HealthChangeWatchRegion)region;
                return new WatchRegionDto
                {
                    Kind = "health",
                    Name = healthRegion.Name,
                    Bounds = healthRegion.Bounds,
                    ConsecutiveFramesRequired = healthRegion.ConsecutiveFramesRequired,
                    TemplateImagePath = healthRegion.TemplateImagePath,
                    TemplateSimilarityThreshold = healthRegion.TemplateSimilarityThreshold,
                    DiffPixelThreshold = healthRegion.DiffPixelThreshold,
                    ChangedAreaRatioThreshold = healthRegion.ChangedAreaRatioThreshold
                };
            }).ToList()
        };
    }

    private static RegionCaptureHotkeysConfig MapRegionCaptureHotkeysFromDto(RegionCaptureHotkeysDto dto)
    {
        var hotkeys = new RegionCaptureHotkeysConfig();
        hotkeys.SkillRegionKey = TriggerMonitorService.IsSupportedHotkey(dto.SkillRegionKey) ? dto.SkillRegionKey : 0x75;
        hotkeys.SkillRegionKeyName = TriggerMonitorService.GetKeyName(hotkeys.SkillRegionKey);
        hotkeys.HealthRegionKey = TriggerMonitorService.IsSupportedHotkey(dto.HealthRegionKey) ? dto.HealthRegionKey : 0x76;
        hotkeys.HealthRegionKeyName = TriggerMonitorService.GetKeyName(hotkeys.HealthRegionKey);
        hotkeys.DeathTextRegionKey = TriggerMonitorService.IsSupportedHotkey(dto.DeathTextRegionKey) ? dto.DeathTextRegionKey : 0x78;
        hotkeys.DeathTextRegionKeyName = TriggerMonitorService.GetKeyName(hotkeys.DeathTextRegionKey);
        return hotkeys;
    }

    private static RegionCaptureHotkeysDto MapRegionCaptureHotkeysToDto(RegionCaptureHotkeysConfig config)
    {
        return new RegionCaptureHotkeysDto
        {
            SkillRegionKey = config.SkillRegionKey,
            SkillRegionKeyName = config.SkillRegionKeyName,
            HealthRegionKey = config.HealthRegionKey,
            HealthRegionKeyName = config.HealthRegionKeyName,
            DeathTextRegionKey = config.DeathTextRegionKey,
            DeathTextRegionKeyName = config.DeathTextRegionKeyName
        };
    }

    private static DeathTriggerConfig MapDeathTriggerFromDto(DeathTriggerDto dto)
    {
        return new DeathTriggerConfig
        {
            Enabled = dto.Enabled,
            HealthRegion = IsValidBounds(dto.HealthRegion) ? dto.HealthRegion : null,
            DeathTextRegion = IsValidBounds(dto.DeathTextRegion) ? dto.DeathTextRegion : null,
            DeathTemplateImagePath = dto.DeathTemplateImagePath ?? string.Empty,
            DeathMusicPath = dto.DeathMusicPath ?? string.Empty,
            TemplateSimilarityThreshold = Math.Clamp(dto.TemplateSimilarityThreshold <= 0 ? 0.75 : dto.TemplateSimilarityThreshold, 0.1, 1.0),
            HealthZeroPixelThreshold = Math.Clamp(dto.HealthZeroPixelThreshold < 0 ? 3 : dto.HealthZeroPixelThreshold, 0, 200),
            ScanIntervalMs = Math.Clamp(dto.ScanIntervalMs <= 0 ? 500 : dto.ScanIntervalMs, 100, 10000),
            CooldownSeconds = Math.Clamp(dto.CooldownSeconds <= 0 ? 8 : dto.CooldownSeconds, 1, 3600)
        };
    }

    private static DeathTriggerDto MapDeathTriggerToDto(DeathTriggerConfig config)
    {
        return new DeathTriggerDto
        {
            Enabled = config.Enabled,
            HealthRegion = config.HealthRegion,
            DeathTextRegion = config.DeathTextRegion,
            DeathTemplateImagePath = config.DeathTemplateImagePath,
            DeathMusicPath = config.DeathMusicPath,
            TemplateSimilarityThreshold = config.TemplateSimilarityThreshold,
            HealthZeroPixelThreshold = config.HealthZeroPixelThreshold,
            ScanIntervalMs = config.ScanIntervalMs,
            CooldownSeconds = config.CooldownSeconds
        };
    }

    private static bool IsValidBounds(ScreenBounds? bounds)
    {
        return bounds is not null && bounds.Width > 0 && bounds.Height > 0;
    }

    private sealed class AppConfigDto
    {
        public string? AudioPath { get; set; }

        public string? LeftClickAudioPath { get; set; }

        public string? RightClickAudioPath { get; set; }

        public float AudioVolume { get; set; } = 1.0f;

        public string? AudioOutputDeviceName { get; set; }

        public bool UseSoundpadOutput { get; set; }

        public string? SoundpadExecutablePath { get; set; }

        public int SoundpadSoundIndex { get; set; } = 1;

        public int TriggerKey { get; set; }

        public string? TriggerKeyName { get; set; }

        public int RegionCaptureKey { get; set; }

        public string? RegionCaptureKeyName { get; set; }

        public int HealthBaselineRefreshSeconds { get; set; } = 30;

        public int WatchWindowMs { get; set; } = 1000;

        public int PollIntervalMs { get; set; } = 5;

        public int HealthGrowthPixelThreshold { get; set; } = 1;

        public int HealthConsecutiveFramesRequired { get; set; } = 2;

        public List<TextTriggerDto>? TextTriggers { get; set; }

        public DeathTriggerDto? DeathTrigger { get; set; }

        public RegionCaptureHotkeysDto? RegionCaptureHotkeys { get; set; }

        public List<WatchRegionDto>? Regions { get; set; }
    }

    private sealed class RegionCaptureHotkeysDto
    {
        public int SkillRegionKey { get; set; } = 0x75;

        public string? SkillRegionKeyName { get; set; }

        public int HealthRegionKey { get; set; } = 0x76;

        public string? HealthRegionKeyName { get; set; }

        public int DeathTextRegionKey { get; set; } = 0x78;

        public string? DeathTextRegionKeyName { get; set; }
    }

    private sealed class DeathTriggerDto
    {
        public bool Enabled { get; set; }

        public ScreenBounds? HealthRegion { get; set; }

        public ScreenBounds? DeathTextRegion { get; set; }

        public string? DeathTemplateImagePath { get; set; }

        public string? DeathMusicPath { get; set; }

        public double TemplateSimilarityThreshold { get; set; } = 0.75;

        public int HealthZeroPixelThreshold { get; set; } = 3;

        public int ScanIntervalMs { get; set; } = 500;

        public int CooldownSeconds { get; set; } = 8;
    }

    private sealed class TextTriggerDto
    {
        public bool Enabled { get; set; } = true;

        public string? Text { get; set; }

        public string? MusicPath { get; set; }

        public int CooldownSeconds { get; set; } = 5;
    }

    private sealed class WatchRegionDto
    {
        public string? Kind { get; set; }

        public string? Name { get; set; }

        public ScreenBounds? Bounds { get; set; }

        public int ConsecutiveFramesRequired { get; set; } = 2;

        public string? ReadyTemplateImagePath { get; set; }

        public double ReadySimilarityThreshold { get; set; } = 0.92;

        public string? EmptyTemplateImagePath { get; set; }

        public double ReadyVsEmptyMargin { get; set; } = 0.03;

        public string? TemplateImagePath { get; set; }

        public double TemplateSimilarityThreshold { get; set; } = 0.75;

        public int DiffPixelThreshold { get; set; } = 30;

        public double ChangedAreaRatioThreshold { get; set; } = 0.08;
    }
}
