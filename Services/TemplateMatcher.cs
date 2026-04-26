using System;
using System.Collections.Concurrent;
using System.Drawing;
using System.IO;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using AOUU.Models;

namespace AOUU.Services;

public sealed class TemplateMatcher : IDisposable
{
    private readonly ConcurrentDictionary<string, CachedTemplate> _templateCache = new(StringComparer.OrdinalIgnoreCase);

    public bool IsSkillReady(Bitmap frame, SkillReadyWatchRegion region)
    {
        return AnalyzeSkillReadiness(frame, region).IsReady;
    }

    public SkillReadinessAnalysis AnalyzeSkillReadiness(Bitmap frame, SkillReadyWatchRegion region)
    {
        var currentProfile = BuildSkillVisualProfile(frame);
        var readyTemplate = GetOrLoadTemplate(region.ReadyTemplateImagePath);

        if (string.IsNullOrWhiteSpace(region.EmptyTemplateImagePath) || !File.Exists(region.EmptyTemplateImagePath))
        {
            var brightnessGap = currentProfile.RingBrightness - readyTemplate.SkillProfile.RingBrightness;
            var coreGap = currentProfile.CoreBrightness - readyTemplate.SkillProfile.CoreBrightness;
            var blueGap = currentProfile.RingBlueRatio - readyTemplate.SkillProfile.RingBlueRatio;
            var brightGap = currentProfile.BrightPixelRatio - readyTemplate.SkillProfile.BrightPixelRatio;

            var looksReady = brightnessGap >= -24 &&
                             coreGap >= -26 &&
                             blueGap >= -0.14 &&
                             brightGap >= -0.16;

            return new SkillReadinessAnalysis(
                looksReady,
                currentProfile,
                readyTemplate.SkillProfile,
                null,
                looksReady ? 1.0 : 0.0,
                "仅使用就绪模板，基于亮环、中心亮度和高亮像素比例判断。");
        }

        var emptyTemplate = GetOrLoadTemplate(region.EmptyTemplateImagePath);
        var readinessProjection = ProjectTowardsReady(
            currentProfile.ToVector(),
            readyTemplate.SkillProfile.ToVector(),
            emptyTemplate.SkillProfile.ToVector());

        var structureSupport = FindBestMatch(frame, region.ReadyTemplateImagePath).Score;
        var isReady = readinessProjection >= 0.18 || (readinessProjection >= 0.08 && structureSupport >= 0.45);

        var reason = isReady
            ? "当前技能图标的亮度/蓝色环光特征更接近就绪模板。"
            : "当前技能图标的亮度/蓝色环光特征仍更接近空条模板。";

        return new SkillReadinessAnalysis(
            isReady,
            currentProfile,
            readyTemplate.SkillProfile,
            emptyTemplate.SkillProfile,
            readinessProjection,
            reason);
    }

    public bool IsTemplateMatched(Bitmap frame, string templateImagePath, double similarityThreshold)
    {
        return FindBestMatch(frame, templateImagePath).Score >= similarityThreshold;
    }

    public TemplateMatchInfo FindBestMatch(Bitmap frame, string templateImagePath)
    {
        if (!File.Exists(templateImagePath))
        {
            throw new FileNotFoundException($"模板图片不存在：{templateImagePath}");
        }

        using var frameGray = ToGray(frame);
        using var templateGray = GetOrLoadTemplate(templateImagePath).Gray.Clone();

        if (templateGray.Width > frameGray.Width || templateGray.Height > frameGray.Height)
        {
            throw new InvalidOperationException($"模板尺寸大于截图尺寸：{templateImagePath}");
        }

        using var result = new Mat();
        Cv2.MatchTemplate(frameGray, templateGray, result, TemplateMatchModes.CCoeffNormed);
        Cv2.MinMaxLoc(result, out _, out var maxValue, out _, out var maxLocation);

        return new TemplateMatchInfo(
            maxValue,
            new Rectangle(maxLocation.X, maxLocation.Y, templateGray.Width, templateGray.Height));
    }

    public void Invalidate(string templateImagePath)
    {
        if (_templateCache.TryRemove(templateImagePath, out var cached))
        {
            cached.Dispose();
        }
    }

    public void Dispose()
    {
        foreach (var template in _templateCache.Values)
        {
            template.Dispose();
        }

        _templateCache.Clear();
    }

    private CachedTemplate GetOrLoadTemplate(string path)
    {
        return _templateCache.GetOrAdd(path, templatePath =>
        {
            using var bitmap = new Bitmap(templatePath);
            var gray = ToGray(bitmap);
            var skillProfile = BuildSkillVisualProfile(bitmap);
            return new CachedTemplate(gray, skillProfile);
        });
    }

    private static SkillVisualProfile BuildSkillVisualProfile(Bitmap bitmap)
    {
        using var source = BitmapConverter.ToMat(bitmap);
        using var bgr = EnsureBgr(source);
        using var hsv = new Mat();

        Cv2.CvtColor(bgr, hsv, ColorConversionCodes.BGR2HSV);

        var width = hsv.Width;
        var height = hsv.Height;
        var centerX = width / 2.0;
        var centerY = height / 2.0;
        var radius = Math.Min(width, height) * 0.46;

        double ringBrightnessSum = 0;
        int ringBrightnessCount = 0;
        int ringBlueCount = 0;
        int ringCount = 0;

        double coreBrightnessSum = 0;
        int coreBrightnessCount = 0;
        int brightPixelCount = 0;
        int diskCount = 0;

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var dx = x - centerX;
                var dy = y - centerY;
                var distance = Math.Sqrt((dx * dx) + (dy * dy));
                if (distance > radius)
                {
                    continue;
                }

                var pixel = hsv.At<Vec3b>(y, x);
                var hue = pixel.Item0 * 2.0;
                var saturation = pixel.Item1;
                var value = pixel.Item2;

                diskCount++;
                if (distance <= radius * 0.68)
                {
                    coreBrightnessSum += value;
                    coreBrightnessCount++;

                    if (value >= 128)
                    {
                        brightPixelCount++;
                    }
                }

                if (distance >= radius * 0.74 && distance <= radius * 0.98)
                {
                    ringCount++;
                    ringBrightnessSum += value;
                    ringBrightnessCount++;

                    if (hue >= 170 && hue <= 250 && saturation >= 48 && value >= 72)
                    {
                        ringBlueCount++;
                    }
                }
            }
        }

        return new SkillVisualProfile(
            RingBrightness: ringBrightnessCount == 0 ? 0 : ringBrightnessSum / ringBrightnessCount,
            CoreBrightness: coreBrightnessCount == 0 ? 0 : coreBrightnessSum / coreBrightnessCount,
            RingBlueRatio: ringCount == 0 ? 0 : ringBlueCount / (double)ringCount,
            BrightPixelRatio: diskCount == 0 ? 0 : brightPixelCount / (double)diskCount);
    }

    private static double ProjectTowardsReady(double[] current, double[] ready, double[] empty)
    {
        if (current.Length != ready.Length || ready.Length != empty.Length)
        {
            return 0;
        }

        double numerator = 0;
        double denominator = 0;
        for (var i = 0; i < current.Length; i++)
        {
            var direction = ready[i] - empty[i];
            numerator += (current[i] - empty[i]) * direction;
            denominator += direction * direction;
        }

        if (denominator <= 1e-6)
        {
            return 0;
        }

        return numerator / denominator;
    }

    private static Mat EnsureBgr(Mat source)
    {
        if (source.Channels() == 3)
        {
            return source.Clone();
        }

        var converted = new Mat();
        if (source.Channels() == 4)
        {
            Cv2.CvtColor(source, converted, ColorConversionCodes.BGRA2BGR);
            return converted;
        }

        Cv2.CvtColor(source, converted, ColorConversionCodes.GRAY2BGR);
        return converted;
    }

    private static Mat ToGray(Bitmap bitmap)
    {
        using var source = BitmapConverter.ToMat(bitmap);
        var gray = new Mat();

        if (source.Channels() == 4)
        {
            Cv2.CvtColor(source, gray, ColorConversionCodes.BGRA2GRAY);
        }
        else if (source.Channels() == 3)
        {
            Cv2.CvtColor(source, gray, ColorConversionCodes.BGR2GRAY);
        }
        else
        {
            gray = source.Clone();
        }

        return gray;
    }

    private sealed class CachedTemplate : IDisposable
    {
        public CachedTemplate(Mat gray, SkillVisualProfile skillProfile)
        {
            Gray = gray;
            SkillProfile = skillProfile;
        }

        public Mat Gray { get; }

        public SkillVisualProfile SkillProfile { get; }

        public void Dispose()
        {
            Gray.Dispose();
        }
    }
}

public sealed record TemplateMatchInfo(double Score, Rectangle Bounds)
{
    public bool IsValid => Bounds.Width > 0 && Bounds.Height > 0;
}

public sealed record SkillReadinessAnalysis(
    bool IsReady,
    SkillVisualProfile CurrentProfile,
    SkillVisualProfile ReadyTemplateProfile,
    SkillVisualProfile? EmptyTemplateProfile,
    double ReadinessProjection,
    string Reason);

public sealed record SkillVisualProfile(
    double RingBrightness,
    double CoreBrightness,
    double RingBlueRatio,
    double BrightPixelRatio)
{
    public double[] ToVector()
    {
        return
        [
            RingBrightness,
            CoreBrightness,
            RingBlueRatio * 255.0,
            BrightPixelRatio * 255.0
        ];
    }
}
