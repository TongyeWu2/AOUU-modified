using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WinFormsApp1.Models;

namespace WinFormsApp1.Services;

public sealed class RecognitionSession
{
    private readonly ScreenCaptureService _screenCaptureService;
    private readonly TemplateMatcher _templateMatcher;
    private readonly RegionChangeDetector _regionChangeDetector;
    private readonly HealthBaselineService _healthBaselineService;
    private readonly HealthBarAnalyzerService _healthBarAnalyzerService;
    private readonly CircleLocatorService _circleLocatorService;
    private readonly string _debugRootPath;
    private readonly int _watchWindowMs;
    private readonly int _pollIntervalMs;
    private readonly int _healthGrowthPixelThreshold;

    public RecognitionSession(
        ScreenCaptureService screenCaptureService,
        TemplateMatcher templateMatcher,
        RegionChangeDetector regionChangeDetector,
        HealthBaselineService healthBaselineService,
        HealthBarAnalyzerService healthBarAnalyzerService,
        CircleLocatorService circleLocatorService,
        string debugRootPath,
        int watchWindowMs,
        int pollIntervalMs,
        int healthGrowthPixelThreshold)
    {
        _screenCaptureService = screenCaptureService;
        _templateMatcher = templateMatcher;
        _regionChangeDetector = regionChangeDetector;
        _healthBaselineService = healthBaselineService;
        _healthBarAnalyzerService = healthBarAnalyzerService;
        _circleLocatorService = circleLocatorService;
        _debugRootPath = debugRootPath;
        _watchWindowMs = watchWindowMs;
        _pollIntervalMs = pollIntervalMs;
        _healthGrowthPixelThreshold = healthGrowthPixelThreshold;
    }

    public async Task<RecognitionResult> RunAsync(IEnumerable<WatchRegion> regions, CancellationToken cancellationToken)
    {
        var debug = new RecognitionDebugSession(_debugRootPath);
        var skillRegion = regions.OfType<SkillReadyWatchRegion>().FirstOrDefault();
        var healthRegion = regions.OfType<HealthChangeWatchRegion>().FirstOrDefault();

        debug.WriteMetric("WatchWindowMs", _watchWindowMs);
        debug.WriteMetric("PollIntervalMs", _pollIntervalMs);
        debug.WriteMetric("HealthGrowthPixelThreshold", _healthGrowthPixelThreshold);

        if (skillRegion is null)
        {
            return Finish(debug, false, null, "未配置技能区域。");
        }

        if (healthRegion is null)
        {
            return Finish(debug, false, null, "未配置血条区域。");
        }

        debug.WriteLine($"SkillRegion={skillRegion.Bounds}");
        debug.WriteLine($"HealthRegion={healthRegion.Bounds}");

        try
        {
            using var skillFrame = _screenCaptureService.Capture(skillRegion.Bounds.ToRectangle());
            debug.SaveBitmap("skill_trigger.png", skillFrame);

            if (_screenCaptureService.LooksBlankOrBlack(skillFrame))
            {
                return Finish(debug, false, null, "截图结果接近黑屏，请将游戏切换为无边框或窗口化后再试。");
            }

            var skillAnalysis = _templateMatcher.AnalyzeSkillReadiness(skillFrame, skillRegion);
            debug.WriteMetric("SkillReadinessProjection", skillAnalysis.ReadinessProjection);
            debug.WriteMetric("SkillCurrentRingBrightness", skillAnalysis.CurrentProfile.RingBrightness);
            debug.WriteMetric("SkillCurrentCoreBrightness", skillAnalysis.CurrentProfile.CoreBrightness);
            debug.WriteMetric("SkillCurrentRingBlueRatio", skillAnalysis.CurrentProfile.RingBlueRatio);
            debug.WriteMetric("SkillCurrentBrightPixelRatio", skillAnalysis.CurrentProfile.BrightPixelRatio);
            debug.WriteMetric("SkillReadyRingBrightness", skillAnalysis.ReadyTemplateProfile.RingBrightness);
            debug.WriteMetric("SkillReadyCoreBrightness", skillAnalysis.ReadyTemplateProfile.CoreBrightness);
            debug.WriteMetric("SkillReadyRingBlueRatio", skillAnalysis.ReadyTemplateProfile.RingBlueRatio);
            debug.WriteMetric("SkillReadyBrightPixelRatio", skillAnalysis.ReadyTemplateProfile.BrightPixelRatio);
            debug.WriteMetric("SkillEmptyRingBrightness", skillAnalysis.EmptyTemplateProfile?.RingBrightness);
            debug.WriteMetric("SkillEmptyCoreBrightness", skillAnalysis.EmptyTemplateProfile?.CoreBrightness);
            debug.WriteMetric("SkillEmptyRingBlueRatio", skillAnalysis.EmptyTemplateProfile?.RingBlueRatio);
            debug.WriteMetric("SkillEmptyBrightPixelRatio", skillAnalysis.EmptyTemplateProfile?.BrightPixelRatio);
            debug.WriteLine($"SkillReason={skillAnalysis.Reason}");

            if (!skillAnalysis.IsReady)
            {
                return Finish(debug, false, skillRegion.Name, "触发时技能未判定为就绪状态，本次不播放提示音。");
            }

            var exactHealthBounds = ResolveExactHealthBounds(healthRegion, debug);
            debug.WriteLine($"ResolvedHealthBounds={exactHealthBounds}");

            using var referenceFrame = _screenCaptureService.Capture(exactHealthBounds);
            debug.SaveBitmap("health_reference.png", referenceFrame);

            if (_screenCaptureService.LooksBlankOrBlack(referenceFrame))
            {
                return Finish(debug, false, null, "截图结果接近黑屏，请将游戏切换为无边框或窗口化后再试。");
            }

            var referenceAnalysis = AnalyzeHealthFrame(referenceFrame);
            WriteHealthAnalysis(debug, "ReferenceHealthFrame", referenceAnalysis);

            if (!referenceAnalysis.Measurement.IsDetected)
            {
                return Finish(debug, false, healthRegion.Name, "触发时未能稳定识别血条。");
            }

            var lockedCircle = referenceAnalysis.Circle;
            debug.WriteLine(
                $"LockedHealthCircle: circleDetected={lockedCircle.IsDetected}, " +
                $"circleX={lockedCircle.Center.X}, circleY={lockedCircle.Center.Y}, circleRadius={lockedCircle.Radius}");

            var growthHits = 0;
            var requiredGrowthHits = Math.Max(3, healthRegion.ConsecutiveFramesRequired);
            var blankFrameRounds = 0;
            var frameIndex = 0;
            var stopwatch = Stopwatch.StartNew();

            while (stopwatch.ElapsedMilliseconds < _watchWindowMs)
            {
                cancellationToken.ThrowIfCancellationRequested();

                using var currentHealthFrame = _screenCaptureService.Capture(exactHealthBounds);
                debug.SaveBitmap($"health_frame_{frameIndex:000}.png", currentHealthFrame);
                var healthBlank = _screenCaptureService.LooksBlankOrBlack(currentHealthFrame);

                blankFrameRounds = healthBlank ? blankFrameRounds + 1 : 0;
                if (blankFrameRounds >= 3)
                {
                    debug.WriteLine($"Frame {frameIndex}: blank_or_black=true");
                    return Finish(debug, false, null, "截图结果接近黑屏，请将游戏切换为无边框或窗口化后再试。");
                }

                var frameAnalysis = AnalyzeHealthFrame(currentHealthFrame, lockedCircle);
                var barGrowth = HasImmediateRedBarGrowth(
                    referenceAnalysis,
                    frameAnalysis,
                    _healthGrowthPixelThreshold,
                    out var growthDelta,
                    out var rightGrowth);

                debug.WriteLine(
                    $"Frame {frameIndex}: elapsedMs={stopwatch.ElapsedMilliseconds}, " +
                    $"circleDetected={frameAnalysis.Circle.IsDetected}, circleX={frameAnalysis.Circle.Center.X}, circleY={frameAnalysis.Circle.Center.Y}, circleRadius={frameAnalysis.Circle.Radius}, " +
                    $"scanBounds={frameAnalysis.Measurement.BarScanBounds}, " +
                    $"healthDetected={frameAnalysis.Measurement.IsDetected}, " +
                    $"healthLength={(frameAnalysis.Measurement.IsDetected ? frameAnalysis.Measurement.Length : -1)}, " +
                    $"leftX={(frameAnalysis.LeftX.HasValue ? frameAnalysis.LeftX.Value.ToString("0.##") : "null")}, " +
                    $"rightX={(frameAnalysis.RightX.HasValue ? frameAnalysis.RightX.Value.ToString("0.##") : "null")}, " +
                    $"healthFillRatio={(frameAnalysis.FillRatio ?? -1):0.####}, " +
                    $"equivalentGrowth={growthDelta}, rightGrowth={rightGrowth:0.##}, growthTrigger={barGrowth}");

                if (barGrowth)
                {
                    growthHits++;
                    if (growthHits >= requiredGrowthHits)
                    {
                        return Finish(
                            debug,
                            true,
                            healthRegion.Name,
                            $"红色血条相对按键瞬间明显变长（参考 {referenceAnalysis.Measurement.Length}，当前 {frameAnalysis.Measurement.Length}，等效增长 {growthDelta}，右端增长 {rightGrowth:0.##}）。");
                    }
                }
                else
                {
                    growthHits = 0;
                }

                frameIndex++;
                await Task.Delay(_pollIntervalMs, cancellationToken);
            }

            return Finish(debug, false, healthRegion.Name, "监听结束：未检测到红色血条明显变长。");
        }
        catch (OperationCanceledException)
        {
            return Finish(debug, false, null, "监听已取消。");
        }
        catch (Exception ex)
        {
            debug.WriteLine($"Exception={ex}");
            return Finish(debug, false, null, $"截图或识别失败：{ex.Message}。如在游戏中使用，请尝试无边框或窗口化。");
        }
    }

    private Rectangle ResolveExactHealthBounds(HealthChangeWatchRegion healthRegion, RecognitionDebugSession debug)
    {
        var roughBounds = healthRegion.Bounds.ToRectangle();
        using var roughFrame = _screenCaptureService.Capture(roughBounds);

        if (!string.IsNullOrWhiteSpace(healthRegion.TemplateImagePath) && File.Exists(healthRegion.TemplateImagePath))
        {
            var match = _templateMatcher.FindBestMatch(roughFrame, healthRegion.TemplateImagePath);
            debug.WriteMetric("HealthTemplateMatchScore", match.Score);
            debug.WriteLine($"HealthTemplateMatchBounds={match.Bounds}");

            var flexibleThreshold = Math.Min(healthRegion.TemplateSimilarityThreshold, 0.55);
            if (match.IsValid && match.Score >= flexibleThreshold)
            {
                return new Rectangle(
                    roughBounds.X + match.Bounds.X,
                    roughBounds.Y + match.Bounds.Y,
                    match.Bounds.Width,
                    match.Bounds.Height);
            }
        }

        return roughBounds;
    }

    private HealthFrameAnalysis AnalyzeHealthFrame(Bitmap frame, CircleDetection? lockedCircle = null)
    {
        var circle = lockedCircle ?? _circleLocatorService.FindHealthCircle(frame);
        var measurement = _healthBarAnalyzerService.Measure(frame, circle);
        var leftX = measurement.IsDetected ? ToSourceX(measurement.Left, measurement) : (double?)null;
        var rightX = measurement.IsDetected ? ToSourceX(measurement.Right, measurement) : (double?)null;
        var fillRatio = measurement.IsDetected && measurement.FrameWidth > 0
            ? measurement.Length / (double)measurement.FrameWidth
            : (double?)null;

        return new HealthFrameAnalysis(circle, measurement, leftX, rightX, fillRatio);
    }

    private static void WriteHealthAnalysis(RecognitionDebugSession debug, string label, HealthFrameAnalysis analysis)
    {
        debug.WriteLine(
            $"{label}: " +
            $"circleDetected={analysis.Circle.IsDetected}, circleX={analysis.Circle.Center.X}, circleY={analysis.Circle.Center.Y}, circleRadius={analysis.Circle.Radius}, " +
            $"scanBounds={analysis.Measurement.BarScanBounds}, " +
            $"healthDetected={analysis.Measurement.IsDetected}, " +
            $"healthLength={(analysis.Measurement.IsDetected ? analysis.Measurement.Length : -1)}, " +
            $"leftX={(analysis.LeftX.HasValue ? analysis.LeftX.Value.ToString("0.##") : "null")}, " +
            $"rightX={(analysis.RightX.HasValue ? analysis.RightX.Value.ToString("0.##") : "null")}, " +
            $"healthFillRatio={(analysis.FillRatio ?? -1):0.####}");
    }

    private static bool HasImmediateRedBarGrowth(
        HealthFrameAnalysis reference,
        HealthFrameAnalysis current,
        int growthThreshold,
        out int growthDelta,
        out double rightGrowth)
    {
        growthDelta = 0;
        rightGrowth = 0;
        var effectiveThreshold = Math.Max(1, growthThreshold);

        if (!reference.Measurement.IsDetected || !current.Measurement.IsDetected)
        {
            return false;
        }

        if (!reference.FillRatio.HasValue || !current.FillRatio.HasValue || reference.Measurement.FrameWidth <= 0)
        {
            growthDelta = current.Measurement.Length - reference.Measurement.Length;
            return growthDelta >= effectiveThreshold;
        }

        var equivalentCurrentLength = current.FillRatio.Value * reference.Measurement.FrameWidth;
        growthDelta = (int)Math.Round(equivalentCurrentLength - reference.Measurement.Length);

        if (reference.LeftX.HasValue && current.LeftX.HasValue)
        {
            var leftShift = Math.Abs(current.LeftX.Value - reference.LeftX.Value);
            if (leftShift > 7.0)
            {
                return false;
            }
        }

        if (reference.RightX.HasValue && current.RightX.HasValue)
        {
            rightGrowth = current.RightX.Value - reference.RightX.Value;
        }

        var fillRatioDelta = current.FillRatio.Value - reference.FillRatio.Value;
        var rightGrowthThreshold = Math.Max(1.0, effectiveThreshold * 0.75);
        var fillRatioThreshold = effectiveThreshold / Math.Max(1.0, reference.Measurement.FrameWidth);
        return growthDelta >= effectiveThreshold ||
               rightGrowth >= rightGrowthThreshold ||
               fillRatioDelta >= fillRatioThreshold;
    }

    private static RecognitionResult Finish(
        RecognitionDebugSession debug,
        bool matched,
        string? regionName,
        string message)
    {
        debug.WriteMetric("Matched", matched);
        debug.WriteMetric("RegionName", regionName);
        debug.WriteLine($"FinalMessage={message}");
        return new RecognitionResult(matched, regionName, message, debug.SessionDirectory);
    }

    private static double ToSourceX(int position, HealthBarMeasurement measurement)
    {
        if (measurement.FrameWidth <= 0 || measurement.BarScanBounds.Width <= 0)
        {
            return measurement.BarScanBounds.X;
        }

        return measurement.BarScanBounds.X + (position / (double)measurement.FrameWidth) * measurement.BarScanBounds.Width;
    }

    private sealed record HealthFrameAnalysis(
        CircleDetection Circle,
        HealthBarMeasurement Measurement,
        double? LeftX,
        double? RightX,
        double? FillRatio);
}
