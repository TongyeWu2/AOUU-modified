using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using AOUU.Models;

namespace AOUU.Services;

public sealed class ConfigService
{
    private readonly string _configPath;
    private readonly string _appDirectoryConfigPath;
    private readonly string _legacyConfigPath;
    private readonly string _bundledDefaultConfigPath;
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
        _bundledDefaultConfigPath = Path.Combine(baseDirectory, "assets", "default_config.json");
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
        EnsureUserConfigFromBundledPreset();

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
        SyncLegacyHotkeyFields(legacyConfig);

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
                    Region = null,
                    Text = "YOU DIED",
                    MusicPath = defaultAudioPath,
                    ScanIntervalMs = 500,
                    CooldownSeconds = 5
                }
            ],
            UltHotkeyTrigger = new ImageHotkeyTriggerConfig
            {
                Enabled = false,
                SimilarityThreshold = 0.85,
                Hotkey = 0x77,
                HotkeyName = "F8",
                HotkeyInput = InputBindingService.FromLegacyHotkey(0x77),
                CooldownSeconds = 5
            },
            ImageHotkeyTrigger = new ImageHotkeyTriggerConfig
            {
                Enabled = false,
                SimilarityThreshold = 0.85,
                Hotkey = 0x7A,
                HotkeyName = "F11",
                HotkeyInput = InputBindingService.FromLegacyHotkey(0x7A),
                CooldownSeconds = 5
            },
            KeyAudioTrigger = new KeyAudioTriggerConfig
            {
                Enabled = false,
                CooldownSeconds = 1,
                Key1 = 0x31,
                Key1Name = "1",
                Input1 = InputBindingService.FromLegacyHotkey(0x31),
                Key2 = 0x32,
                Key2Name = "2",
                Input2 = InputBindingService.FromLegacyHotkey(0x32),
                Key3 = 0x33,
                Key3Name = "3",
                Input3 = InputBindingService.FromLegacyHotkey(0x33)
            }
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

    private void EnsureUserConfigFromBundledPreset()
    {
        if (File.Exists(_configPath) || !File.Exists(_bundledDefaultConfigPath))
        {
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(_configPath)!);
        File.Copy(_bundledDefaultConfigPath, _configPath);
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
        config.AudioPath = ResolveConfiguredPath(dto.AudioPath) ?? config.AudioPath;
        config.AudioVolume = Math.Clamp(dto.AudioVolume, 0.0f, 1.0f);
        config.AudioOutputDeviceName = dto.AudioOutputDeviceName ?? string.Empty;
        config.TriggerKey = TriggerMonitorService.IsSupportedHotkey(dto.TriggerKey) ? dto.TriggerKey : 0x77;
        config.TriggerInput = InputBindingService.Normalize(dto.TriggerInput, config.TriggerKey);
        config.TriggerKey = config.TriggerInput.KeyCode;
        config.TriggerKeyName = config.TriggerInput.DisplayName;
        config.RegionCaptureKey = TriggerMonitorService.IsSupportedHotkey(dto.RegionCaptureKey) ? dto.RegionCaptureKey : 0x79;
        config.RegionCaptureInput = InputBindingService.Normalize(dto.RegionCaptureInput, config.RegionCaptureKey);
        config.RegionCaptureKey = config.RegionCaptureInput.KeyCode;
        config.RegionCaptureKeyName = config.RegionCaptureInput.DisplayName;
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
                    Region = IsValidBounds(textTriggerDto.Region) ? textTriggerDto.Region : null,
                    Text = triggerText,
                    MusicPath = ResolveConfiguredPath(textTriggerDto.MusicPath) ?? string.Empty,
                    ScanIntervalMs = Math.Clamp(textTriggerDto.ScanIntervalMs <= 0 ? 500 : textTriggerDto.ScanIntervalMs, 100, 10000),
                    CooldownSeconds = Math.Clamp(textTriggerDto.CooldownSeconds <= 0 ? 5 : textTriggerDto.CooldownSeconds, 1, 3600)
                });
            }
        }

        if (dto.DeathTrigger is not null)
        {
            MigrateDeathTrigger(config, dto.DeathTrigger);
        }

        if (dto.ImageHotkeyTrigger is not null)
        {
            config.ImageHotkeyTrigger = MapImageHotkeyTriggerFromDto(dto.ImageHotkeyTrigger, "战技");
        }

        if (dto.UltHotkeyTrigger is not null)
        {
            config.UltHotkeyTrigger = MapImageHotkeyTriggerFromDto(dto.UltHotkeyTrigger, "大招");
            config.TriggerInput = config.UltHotkeyTrigger.HotkeyInput.Clone();
            config.TriggerKey = config.TriggerInput.KeyCode;
            config.TriggerKeyName = config.TriggerInput.DisplayName;
        }

        if (dto.KeyAudioTrigger is not null)
        {
            config.KeyAudioTrigger = MapKeyAudioTriggerFromDto(dto.KeyAudioTrigger);
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

                var readyTemplatePath = ResolveConfiguredPath(regionDto.ReadyTemplateImagePath) ?? string.Empty;
                if (string.Equals(Path.GetFileName(readyTemplatePath), Path.GetFileName(DefaultSkillReadyTemplatePath), StringComparison.OrdinalIgnoreCase) &&
                    File.Exists(DefaultSkillReadyTemplatePath))
                {
                    readyTemplatePath = DefaultSkillReadyTemplatePath;
                }

                var emptyTemplatePath = ResolveConfiguredPath(regionDto.EmptyTemplateImagePath) ?? string.Empty;
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

                var healthTemplatePath = ResolveConfiguredPath(regionDto.TemplateImagePath) ?? string.Empty;
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

        if (dto.UltHotkeyTrigger is null)
        {
            MigrateLegacyUltTrigger(config);
        }

        SyncLegacyHotkeyFields(config);

        return config;
    }

    private static AppConfigDto MapToDto(AppConfig config)
    {
        return new AppConfigDto
        {
            AudioPath = config.AudioPath,
            AudioVolume = config.AudioVolume,
            AudioOutputDeviceName = config.AudioOutputDeviceName,
            TriggerKey = config.TriggerKey,
            TriggerKeyName = config.TriggerKeyName,
            TriggerInput = config.TriggerInput,
            RegionCaptureKey = config.RegionCaptureKey,
            RegionCaptureKeyName = config.RegionCaptureKeyName,
            RegionCaptureInput = config.RegionCaptureInput,
            HealthBaselineRefreshSeconds = config.HealthBaselineRefreshSeconds,
            WatchWindowMs = config.WatchWindowMs,
            PollIntervalMs = config.PollIntervalMs,
            HealthGrowthPixelThreshold = config.HealthGrowthPixelThreshold,
            HealthConsecutiveFramesRequired = config.HealthConsecutiveFramesRequired,
            TextTriggers = config.TextTriggers.Select(trigger => new TextTriggerDto
            {
                Enabled = trigger.Enabled,
                Region = trigger.Region,
                Text = trigger.Text,
                MusicPath = trigger.MusicPath,
                ScanIntervalMs = trigger.ScanIntervalMs,
                CooldownSeconds = trigger.CooldownSeconds
            }).ToList(),
            UltHotkeyTrigger = MapImageHotkeyTriggerToDto(config.UltHotkeyTrigger),
            ImageHotkeyTrigger = MapImageHotkeyTriggerToDto(config.ImageHotkeyTrigger),
            KeyAudioTrigger = MapKeyAudioTriggerToDto(config.KeyAudioTrigger),
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

    private static KeyAudioTriggerConfig MapKeyAudioTriggerFromDto(KeyAudioTriggerDto dto)
    {
        var config = new KeyAudioTriggerConfig
        {
            Enabled = dto.Enabled,
            CooldownSeconds = Math.Clamp(dto.CooldownSeconds <= 0 ? 1 : dto.CooldownSeconds, 1, 3600),
            Key1 = TriggerMonitorService.IsSupportedHotkey(dto.Key1) ? dto.Key1 : 0x31,
            Input1 = InputBindingService.Normalize(dto.Input1, TriggerMonitorService.IsSupportedHotkey(dto.Key1) ? dto.Key1 : 0x31),
            AudioPath1 = ResolveConfiguredPath(dto.AudioPath1) ?? string.Empty,
            Key2 = TriggerMonitorService.IsSupportedHotkey(dto.Key2) ? dto.Key2 : 0x32,
            Input2 = InputBindingService.Normalize(dto.Input2, TriggerMonitorService.IsSupportedHotkey(dto.Key2) ? dto.Key2 : 0x32),
            AudioPath2 = ResolveConfiguredPath(dto.AudioPath2) ?? string.Empty,
            Key3 = TriggerMonitorService.IsSupportedHotkey(dto.Key3) ? dto.Key3 : 0x33,
            Input3 = InputBindingService.Normalize(dto.Input3, TriggerMonitorService.IsSupportedHotkey(dto.Key3) ? dto.Key3 : 0x33),
            AudioPath3 = ResolveConfiguredPath(dto.AudioPath3) ?? string.Empty
        };

        config.Key1 = config.Input1.KeyCode;
        config.Key1Name = config.Input1.DisplayName;
        config.Key2 = config.Input2.KeyCode;
        config.Key2Name = config.Input2.DisplayName;
        config.Key3 = config.Input3.KeyCode;
        config.Key3Name = config.Input3.DisplayName;
        return config;
    }

    private static KeyAudioTriggerDto MapKeyAudioTriggerToDto(KeyAudioTriggerConfig config)
    {
        return new KeyAudioTriggerDto
        {
            Enabled = config.Enabled,
            CooldownSeconds = config.CooldownSeconds,
            Key1 = config.Key1,
            Key1Name = config.Key1Name,
            Input1 = config.Input1,
            AudioPath1 = config.AudioPath1,
            Key2 = config.Key2,
            Key2Name = config.Key2Name,
            Input2 = config.Input2,
            AudioPath2 = config.AudioPath2,
            Key3 = config.Key3,
            Key3Name = config.Key3Name,
            Input3 = config.Input3,
            AudioPath3 = config.AudioPath3
        };
    }

    private static RegionCaptureHotkeysConfig MapRegionCaptureHotkeysFromDto(RegionCaptureHotkeysDto dto)
    {
        var hotkeys = new RegionCaptureHotkeysConfig();
        hotkeys.SkillRegionKey = TriggerMonitorService.IsSupportedHotkey(dto.SkillRegionKey) ? dto.SkillRegionKey : 0x75;
        hotkeys.SkillRegionInput = InputBindingService.Normalize(dto.SkillRegionInput, hotkeys.SkillRegionKey);
        hotkeys.SkillRegionKey = hotkeys.SkillRegionInput.KeyCode;
        hotkeys.SkillRegionKeyName = hotkeys.SkillRegionInput.DisplayName;
        hotkeys.HealthRegionKey = TriggerMonitorService.IsSupportedHotkey(dto.HealthRegionKey) ? dto.HealthRegionKey : 0x76;
        hotkeys.HealthRegionInput = InputBindingService.Normalize(dto.HealthRegionInput, hotkeys.HealthRegionKey);
        hotkeys.HealthRegionKey = hotkeys.HealthRegionInput.KeyCode;
        hotkeys.HealthRegionKeyName = hotkeys.HealthRegionInput.DisplayName;
        var ocrTextRegionKey = dto.OcrTextRegionKey != 0 ? dto.OcrTextRegionKey : dto.DeathTextRegionKey;
        hotkeys.OcrTextRegionKey = TriggerMonitorService.IsSupportedHotkey(ocrTextRegionKey) ? ocrTextRegionKey : 0x78;
        hotkeys.OcrTextRegionInput = InputBindingService.Normalize(dto.OcrTextRegionInput, hotkeys.OcrTextRegionKey);
        hotkeys.OcrTextRegionKey = hotkeys.OcrTextRegionInput.KeyCode;
        hotkeys.OcrTextRegionKeyName = hotkeys.OcrTextRegionInput.DisplayName;
        return hotkeys;
    }

    private static RegionCaptureHotkeysDto MapRegionCaptureHotkeysToDto(RegionCaptureHotkeysConfig config)
    {
        return new RegionCaptureHotkeysDto
        {
            SkillRegionKey = config.SkillRegionKey,
            SkillRegionKeyName = config.SkillRegionKeyName,
            SkillRegionInput = config.SkillRegionInput,
            HealthRegionKey = config.HealthRegionKey,
            HealthRegionKeyName = config.HealthRegionKeyName,
            HealthRegionInput = config.HealthRegionInput,
            OcrTextRegionKey = config.OcrTextRegionKey,
            OcrTextRegionKeyName = config.OcrTextRegionKeyName,
            OcrTextRegionInput = config.OcrTextRegionInput
        };
    }

    private static ImageHotkeyTriggerConfig MapImageHotkeyTriggerFromDto(ImageHotkeyTriggerDto dto, string defaultEntryNamePrefix)
    {
        var hotkey = TriggerMonitorService.IsSupportedHotkey(dto.Hotkey) ? dto.Hotkey : 0x7A;
        var hotkeyInput = InputBindingService.Normalize(dto.HotkeyInput, hotkey);
        var config = new ImageHotkeyTriggerConfig
        {
            Enabled = dto.Enabled,
            Region = IsValidBounds(dto.Region) ? dto.Region : null,
            TemplateImagePath = ResolveConfiguredPath(dto.TemplateImagePath) ?? string.Empty,
            SimilarityThreshold = Math.Clamp(dto.SimilarityThreshold <= 0 ? 0.85 : dto.SimilarityThreshold, 0.1, 1.0),
            SelectedSkillIndex = Math.Max(0, dto.SelectedSkillIndex),
            Hotkey = hotkeyInput.KeyCode,
            HotkeyName = hotkeyInput.DisplayName,
            HotkeyInput = hotkeyInput,
            AudioPath = ResolveConfiguredPath(dto.AudioPath) ?? string.Empty,
            ScanIntervalMs = Math.Clamp(dto.ScanIntervalMs <= 0 ? 200 : dto.ScanIntervalMs, 100, 10000),
            CooldownSeconds = Math.Clamp(dto.CooldownSeconds <= 0 ? 5 : dto.CooldownSeconds, 1, 3600)
        };

        if (dto.Skills is not null)
        {
            foreach (var skillDto in dto.Skills)
            {
                var skill = MapImageHotkeySkillFromDto(skillDto, config.Skills.Count + 1, defaultEntryNamePrefix);
                if (!string.IsNullOrWhiteSpace(skill.Name) ||
                    !string.IsNullOrWhiteSpace(skill.TemplateImagePath) ||
                    !string.IsNullOrWhiteSpace(skill.AudioPath))
                {
                    config.Skills.Add(skill);
                }
            }
        }

        if (config.Skills.Count == 0 &&
            (!string.IsNullOrWhiteSpace(config.TemplateImagePath) || !string.IsNullOrWhiteSpace(config.AudioPath)))
        {
            config.Skills.Add(new ImageHotkeySkillConfig
            {
                Name = $"{defaultEntryNamePrefix} 1",
                TemplateImagePath = config.TemplateImagePath,
                AudioPath = config.AudioPath,
                SimilarityThreshold = config.SimilarityThreshold
            });
        }

        config.SelectedSkillIndex = config.Skills.Count == 0
            ? 0
            : Math.Clamp(config.SelectedSkillIndex, 0, config.Skills.Count - 1);

        return config;
    }

    private static ImageHotkeyTriggerDto MapImageHotkeyTriggerToDto(ImageHotkeyTriggerConfig config)
    {
        var firstSkill = config.Skills.FirstOrDefault();
        return new ImageHotkeyTriggerDto
        {
            Enabled = config.Enabled,
            Region = config.Region,
            Skills = config.Skills.Select(MapImageHotkeySkillToDto).ToList(),
            SelectedSkillIndex = config.Skills.Count == 0 ? 0 : Math.Clamp(config.SelectedSkillIndex, 0, config.Skills.Count - 1),
            TemplateImagePath = firstSkill?.TemplateImagePath ?? config.TemplateImagePath,
            SimilarityThreshold = firstSkill?.SimilarityThreshold ?? config.SimilarityThreshold,
            Hotkey = config.Hotkey,
            HotkeyName = config.HotkeyName,
            HotkeyInput = config.HotkeyInput,
            AudioPath = firstSkill?.AudioPath ?? config.AudioPath,
            ScanIntervalMs = config.ScanIntervalMs,
            CooldownSeconds = config.CooldownSeconds
        };
    }

    private static ImageHotkeySkillConfig MapImageHotkeySkillFromDto(ImageHotkeySkillDto dto, int index, string defaultEntryNamePrefix)
    {
        return new ImageHotkeySkillConfig
        {
            Name = string.IsNullOrWhiteSpace(dto.Name) ? $"{defaultEntryNamePrefix} {index}" : dto.Name.Trim(),
            TemplateImagePath = ResolveConfiguredPath(dto.TemplateImagePath) ?? string.Empty,
            AudioPath = ResolveConfiguredPath(dto.AudioPath) ?? string.Empty,
            SimilarityThreshold = Math.Clamp(dto.SimilarityThreshold <= 0 ? 0.85 : dto.SimilarityThreshold, 0.1, 1.0)
        };
    }

    private static ImageHotkeySkillDto MapImageHotkeySkillToDto(ImageHotkeySkillConfig skill)
    {
        return new ImageHotkeySkillDto
        {
            Name = skill.Name,
            TemplateImagePath = skill.TemplateImagePath,
            AudioPath = skill.AudioPath,
            SimilarityThreshold = skill.SimilarityThreshold
        };
    }

    private static void MigrateLegacyUltTrigger(AppConfig config)
    {
        var skillRegion = config.Regions.OfType<SkillReadyWatchRegion>().FirstOrDefault();
        if (skillRegion is null &&
            string.IsNullOrWhiteSpace(config.AudioPath))
        {
            return;
        }

        config.UltHotkeyTrigger.Enabled = false;
        config.UltHotkeyTrigger.Region = skillRegion?.Bounds;
        config.UltHotkeyTrigger.HotkeyInput = config.TriggerInput.Clone();
        config.UltHotkeyTrigger.Hotkey = config.TriggerInput.KeyCode;
        config.UltHotkeyTrigger.HotkeyName = config.TriggerInput.DisplayName;
        config.UltHotkeyTrigger.CooldownSeconds = 5;
        config.UltHotkeyTrigger.SelectedSkillIndex = 0;

        if (config.UltHotkeyTrigger.Skills.Count == 0)
        {
            config.UltHotkeyTrigger.Skills.Add(new ImageHotkeySkillConfig
            {
                Name = "大招 1",
                TemplateImagePath = skillRegion?.ReadyTemplateImagePath ?? string.Empty,
                AudioPath = config.AudioPath,
                SimilarityThreshold = skillRegion?.ReadySimilarityThreshold ?? 0.85
            });
        }
    }

    private static void MigrateDeathTrigger(AppConfig config, DeathTriggerDto dto)
    {
        var trigger = config.TextTriggers.FirstOrDefault();
        if (trigger is null)
        {
            trigger = new TextTriggerConfig();
            config.TextTriggers.Add(trigger);
        }

        if (dto.Enabled)
        {
            trigger.Enabled = true;
        }

        if (IsValidBounds(dto.DeathTextRegion))
        {
            trigger.Region = dto.DeathTextRegion;
        }

        if (!string.IsNullOrWhiteSpace(dto.DeathMusicPath))
        {
            trigger.MusicPath = ResolveConfiguredPath(dto.DeathMusicPath) ?? string.Empty;
        }

        trigger.Text = string.IsNullOrWhiteSpace(trigger.Text) ? "YOU DIED" : trigger.Text;
        trigger.ScanIntervalMs = Math.Clamp(dto.ScanIntervalMs <= 0 ? trigger.ScanIntervalMs : dto.ScanIntervalMs, 100, 10000);
        trigger.CooldownSeconds = Math.Clamp(dto.CooldownSeconds <= 0 ? trigger.CooldownSeconds : dto.CooldownSeconds, 1, 3600);
    }

    private static void SyncLegacyHotkeyFields(AppConfig config)
    {
        config.TriggerInput = InputBindingService.Normalize(config.TriggerInput, config.TriggerKey);
        config.TriggerKey = config.TriggerInput.KeyCode;
        config.TriggerKeyName = config.TriggerInput.DisplayName;

        config.RegionCaptureInput = InputBindingService.Normalize(config.RegionCaptureInput, config.RegionCaptureKey);
        config.RegionCaptureKey = config.RegionCaptureInput.KeyCode;
        config.RegionCaptureKeyName = config.RegionCaptureInput.DisplayName;

        config.UltHotkeyTrigger.HotkeyInput = InputBindingService.Normalize(config.UltHotkeyTrigger.HotkeyInput, config.TriggerKey);
        config.UltHotkeyTrigger.Hotkey = config.UltHotkeyTrigger.HotkeyInput.KeyCode;
        config.UltHotkeyTrigger.HotkeyName = config.UltHotkeyTrigger.HotkeyInput.DisplayName;

        config.ImageHotkeyTrigger.HotkeyInput = InputBindingService.Normalize(config.ImageHotkeyTrigger.HotkeyInput, config.ImageHotkeyTrigger.Hotkey);
        config.ImageHotkeyTrigger.Hotkey = config.ImageHotkeyTrigger.HotkeyInput.KeyCode;
        config.ImageHotkeyTrigger.HotkeyName = config.ImageHotkeyTrigger.HotkeyInput.DisplayName;

        config.KeyAudioTrigger.Input1 = InputBindingService.Normalize(config.KeyAudioTrigger.Input1, config.KeyAudioTrigger.Key1);
        config.KeyAudioTrigger.Key1 = config.KeyAudioTrigger.Input1.KeyCode;
        config.KeyAudioTrigger.Key1Name = config.KeyAudioTrigger.Input1.DisplayName;
        config.KeyAudioTrigger.Input2 = InputBindingService.Normalize(config.KeyAudioTrigger.Input2, config.KeyAudioTrigger.Key2);
        config.KeyAudioTrigger.Key2 = config.KeyAudioTrigger.Input2.KeyCode;
        config.KeyAudioTrigger.Key2Name = config.KeyAudioTrigger.Input2.DisplayName;
        config.KeyAudioTrigger.Input3 = InputBindingService.Normalize(config.KeyAudioTrigger.Input3, config.KeyAudioTrigger.Key3);
        config.KeyAudioTrigger.Key3 = config.KeyAudioTrigger.Input3.KeyCode;
        config.KeyAudioTrigger.Key3Name = config.KeyAudioTrigger.Input3.DisplayName;

        config.RegionCaptureHotkeys.SkillRegionInput = InputBindingService.Normalize(config.RegionCaptureHotkeys.SkillRegionInput, config.RegionCaptureHotkeys.SkillRegionKey);
        config.RegionCaptureHotkeys.SkillRegionKey = config.RegionCaptureHotkeys.SkillRegionInput.KeyCode;
        config.RegionCaptureHotkeys.SkillRegionKeyName = config.RegionCaptureHotkeys.SkillRegionInput.DisplayName;
        config.RegionCaptureHotkeys.HealthRegionInput = InputBindingService.Normalize(config.RegionCaptureHotkeys.HealthRegionInput, config.RegionCaptureHotkeys.HealthRegionKey);
        config.RegionCaptureHotkeys.HealthRegionKey = config.RegionCaptureHotkeys.HealthRegionInput.KeyCode;
        config.RegionCaptureHotkeys.HealthRegionKeyName = config.RegionCaptureHotkeys.HealthRegionInput.DisplayName;
        config.RegionCaptureHotkeys.OcrTextRegionInput = InputBindingService.Normalize(config.RegionCaptureHotkeys.OcrTextRegionInput, config.RegionCaptureHotkeys.OcrTextRegionKey);
        config.RegionCaptureHotkeys.OcrTextRegionKey = config.RegionCaptureHotkeys.OcrTextRegionInput.KeyCode;
        config.RegionCaptureHotkeys.OcrTextRegionKeyName = config.RegionCaptureHotkeys.OcrTextRegionInput.DisplayName;
    }

    private static string? ResolveConfiguredPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return path;
        }

        if (Path.IsPathRooted(path))
        {
            return path;
        }

        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, path.Replace('/', Path.DirectorySeparatorChar)));
    }

    private static bool IsValidBounds(ScreenBounds? bounds)
    {
        return bounds is not null && bounds.Width > 0 && bounds.Height > 0;
    }

    private sealed class AppConfigDto
    {
        public string? AudioPath { get; set; }

        public float AudioVolume { get; set; } = 1.0f;

        public string? AudioOutputDeviceName { get; set; }

        public int TriggerKey { get; set; }

        public string? TriggerKeyName { get; set; }

        public InputBinding? TriggerInput { get; set; }

        public int RegionCaptureKey { get; set; }

        public string? RegionCaptureKeyName { get; set; }

        public InputBinding? RegionCaptureInput { get; set; }

        public int HealthBaselineRefreshSeconds { get; set; } = 30;

        public int WatchWindowMs { get; set; } = 1000;

        public int PollIntervalMs { get; set; } = 5;

        public int HealthGrowthPixelThreshold { get; set; } = 1;

        public int HealthConsecutiveFramesRequired { get; set; } = 2;

        public List<TextTriggerDto>? TextTriggers { get; set; }

        public ImageHotkeyTriggerDto? UltHotkeyTrigger { get; set; }

        public ImageHotkeyTriggerDto? ImageHotkeyTrigger { get; set; }

        public KeyAudioTriggerDto? KeyAudioTrigger { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public DeathTriggerDto? DeathTrigger { get; set; }

        public RegionCaptureHotkeysDto? RegionCaptureHotkeys { get; set; }

        public List<WatchRegionDto>? Regions { get; set; }
    }

    private sealed class KeyAudioTriggerDto
    {
        public bool Enabled { get; set; }

        public int CooldownSeconds { get; set; } = 1;

        public int Key1 { get; set; } = 0x31;

        public string? Key1Name { get; set; }

        public InputBinding? Input1 { get; set; }

        public string? AudioPath1 { get; set; }

        public int Key2 { get; set; } = 0x32;

        public string? Key2Name { get; set; }

        public InputBinding? Input2 { get; set; }

        public string? AudioPath2 { get; set; }

        public int Key3 { get; set; } = 0x33;

        public string? Key3Name { get; set; }

        public InputBinding? Input3 { get; set; }

        public string? AudioPath3 { get; set; }
    }

    private sealed class RegionCaptureHotkeysDto
    {
        public int SkillRegionKey { get; set; } = 0x75;

        public string? SkillRegionKeyName { get; set; }

        public InputBinding? SkillRegionInput { get; set; }

        public int HealthRegionKey { get; set; } = 0x76;

        public string? HealthRegionKeyName { get; set; }

        public InputBinding? HealthRegionInput { get; set; }

        public int OcrTextRegionKey { get; set; } = 0x78;

        public string? OcrTextRegionKeyName { get; set; }

        public InputBinding? OcrTextRegionInput { get; set; }

        // Legacy name kept only so old configs can migrate to OcrTextRegionKey.
        public int DeathTextRegionKey { get; set; } = 0x78;

        public string? DeathTextRegionKeyName { get; set; }
    }

    private sealed class DeathTriggerDto
    {
        public bool Enabled { get; set; }

        public ScreenBounds? HealthRegion { get; set; }

        public ScreenBounds? DeathTextRegion { get; set; }

        public string? DeathMusicPath { get; set; }

        public int HealthZeroPixelThreshold { get; set; } = 3;

        public int ScanIntervalMs { get; set; } = 500;

        public int CooldownSeconds { get; set; } = 8;
    }

    private sealed class TextTriggerDto
    {
        public bool Enabled { get; set; } = true;

        public ScreenBounds? Region { get; set; }

        public string? Text { get; set; }

        public string? MusicPath { get; set; }

        public int ScanIntervalMs { get; set; } = 500;

        public int CooldownSeconds { get; set; } = 5;
    }

    private sealed class ImageHotkeyTriggerDto
    {
        public bool Enabled { get; set; }

        public ScreenBounds? Region { get; set; }

        public List<ImageHotkeySkillDto>? Skills { get; set; }

        public int SelectedSkillIndex { get; set; }

        // Legacy single-skill fields are kept only for backward compatibility.
        public string? TemplateImagePath { get; set; }

        public double SimilarityThreshold { get; set; } = 0.85;

        public int Hotkey { get; set; } = 0x7A;

        public string? HotkeyName { get; set; }

        public InputBinding? HotkeyInput { get; set; }

        public string? AudioPath { get; set; }

        public int ScanIntervalMs { get; set; } = 200;

        public int CooldownSeconds { get; set; } = 5;
    }

    private sealed class ImageHotkeySkillDto
    {
        public string? Name { get; set; }

        public string? TemplateImagePath { get; set; }

        public string? AudioPath { get; set; }

        public double SimilarityThreshold { get; set; } = 0.85;
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
