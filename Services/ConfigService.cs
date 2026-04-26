using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using WinFormsApp1.Models;

namespace WinFormsApp1.Services;

public sealed class ConfigService
{
    private readonly string _configPath;
    private readonly string _legacyConfigPath;
    private readonly JsonSerializerOptions _serializerOptions = new()
    {
        WriteIndented = true
    };

    public ConfigService(string baseDirectory)
    {
        _configPath = Path.Combine(baseDirectory, "config.json");
        _legacyConfigPath = Path.Combine(baseDirectory, "config.txt");
        TemplateDirectory = Path.Combine(baseDirectory, "templates");
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
        Directory.CreateDirectory(DefaultAudioDirectory);

        if (File.Exists(_configPath))
        {
            try
            {
                var dto = JsonSerializer.Deserialize<AppConfigDto>(File.ReadAllText(_configPath), _serializerOptions);
                return dto is null ? CreateDefault() : MapFromDto(dto);
            }
            catch
            {
                return CreateDefault();
            }
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
        Directory.CreateDirectory(TemplateDirectory);
        Directory.CreateDirectory(DefaultTemplateDirectory);
        Directory.CreateDirectory(DefaultAudioDirectory);
        var dto = MapToDto(config);
        File.WriteAllText(_configPath, JsonSerializer.Serialize(dto, _serializerOptions));
    }

    private AppConfig CreateDefault()
    {
        return new AppConfig
        {
            AudioPath = File.Exists(DefaultAudioPath) ? DefaultAudioPath : string.Empty
        };
    }

    private AppConfig MapFromDto(AppConfigDto dto)
    {
        var config = CreateDefault();
        config.AudioPath = dto.AudioPath ?? config.AudioPath;
        config.AudioVolume = Math.Clamp(dto.AudioVolume, 0.0f, 1.0f);
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

        if (string.IsNullOrWhiteSpace(config.AudioPath) || !File.Exists(config.AudioPath))
        {
            config.AudioPath = File.Exists(DefaultAudioPath) ? DefaultAudioPath : string.Empty;
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
            AudioVolume = config.AudioVolume,
            TriggerKey = config.TriggerKey,
            TriggerKeyName = config.TriggerKeyName,
            RegionCaptureKey = config.RegionCaptureKey,
            RegionCaptureKeyName = config.RegionCaptureKeyName,
            HealthBaselineRefreshSeconds = config.HealthBaselineRefreshSeconds,
            WatchWindowMs = config.WatchWindowMs,
            PollIntervalMs = config.PollIntervalMs,
            HealthGrowthPixelThreshold = config.HealthGrowthPixelThreshold,
            HealthConsecutiveFramesRequired = config.HealthConsecutiveFramesRequired,
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

    private sealed class AppConfigDto
    {
        public string? AudioPath { get; set; }

        public float AudioVolume { get; set; } = 1.0f;

        public int TriggerKey { get; set; }

        public string? TriggerKeyName { get; set; }

        public int RegionCaptureKey { get; set; }

        public string? RegionCaptureKeyName { get; set; }

        public int HealthBaselineRefreshSeconds { get; set; } = 30;

        public int WatchWindowMs { get; set; } = 1000;

        public int PollIntervalMs { get; set; } = 5;

        public int HealthGrowthPixelThreshold { get; set; } = 1;

        public int HealthConsecutiveFramesRequired { get; set; } = 2;

        public List<WatchRegionDto>? Regions { get; set; }
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
