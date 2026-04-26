namespace WinFormsApp1.Models;

public abstract class WatchRegion
{
    public string Name { get; set; } = string.Empty;

    public ScreenBounds Bounds { get; set; } = new();

    public int ConsecutiveFramesRequired { get; set; } = 2;

    public abstract string RegionType { get; }

    public abstract WatchRegion Clone();

    public override string ToString()
    {
        return $"{RegionType} | {Name} | {Bounds}";
    }
}

public sealed class SkillReadyWatchRegion : WatchRegion
{
    public string ReadyTemplateImagePath { get; set; } = string.Empty;

    public double ReadySimilarityThreshold { get; set; } = 0.92;

    public string EmptyTemplateImagePath { get; set; } = string.Empty;

    public double ReadyVsEmptyMargin { get; set; } = 0.03;

    public override string RegionType => "技能就绪";

    public override WatchRegion Clone()
    {
        return new SkillReadyWatchRegion
        {
            Name = Name,
            Bounds = new ScreenBounds
            {
                X = Bounds.X,
                Y = Bounds.Y,
                Width = Bounds.Width,
                Height = Bounds.Height
            },
            ConsecutiveFramesRequired = ConsecutiveFramesRequired,
            ReadyTemplateImagePath = ReadyTemplateImagePath,
            ReadySimilarityThreshold = ReadySimilarityThreshold,
            EmptyTemplateImagePath = EmptyTemplateImagePath,
            ReadyVsEmptyMargin = ReadyVsEmptyMargin
        };
    }

    public override string ToString() => base.ToString();
}

public sealed class HealthChangeWatchRegion : WatchRegion
{
    public string TemplateImagePath { get; set; } = string.Empty;

    public double TemplateSimilarityThreshold { get; set; } = 0.75;

    public byte DiffPixelThreshold { get; set; } = 30;

    public double ChangedAreaRatioThreshold { get; set; } = 0.08;

    public override string RegionType => "血条变化";

    public override WatchRegion Clone()
    {
        return new HealthChangeWatchRegion
        {
            Name = Name,
            Bounds = new ScreenBounds
            {
                X = Bounds.X,
                Y = Bounds.Y,
                Width = Bounds.Width,
                Height = Bounds.Height
            },
            ConsecutiveFramesRequired = ConsecutiveFramesRequired,
            TemplateImagePath = TemplateImagePath,
            TemplateSimilarityThreshold = TemplateSimilarityThreshold,
            DiffPixelThreshold = DiffPixelThreshold,
            ChangedAreaRatioThreshold = ChangedAreaRatioThreshold
        };
    }

    public override string ToString() => base.ToString();
}
