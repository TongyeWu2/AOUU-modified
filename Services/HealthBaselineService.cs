using System;
using System.Drawing;

namespace AOUU.Services;

public sealed class HealthBaselineService : IDisposable
{
    private readonly ScreenCaptureService _screenCaptureService;
    private readonly TemplateMatcher _templateMatcher;
    private readonly HealthBarAnalyzerService _healthBarAnalyzerService;
    private readonly CircleLocatorService _circleLocatorService;
    private readonly object _syncRoot = new();
    private HealthBaselineSnapshot? _snapshot;

    public HealthBaselineService(
        ScreenCaptureService screenCaptureService,
        TemplateMatcher templateMatcher,
        HealthBarAnalyzerService healthBarAnalyzerService,
        CircleLocatorService circleLocatorService)
    {
        _screenCaptureService = screenCaptureService;
        _templateMatcher = templateMatcher;
        _healthBarAnalyzerService = healthBarAnalyzerService;
        _circleLocatorService = circleLocatorService;
    }

    public bool TryRefresh(Models.HealthChangeWatchRegion region)
    {
        if (region.Bounds.Width <= 0 || region.Bounds.Height <= 0)
        {
            return false;
        }

        using var roughFrame = _screenCaptureService.Capture(region.Bounds.ToRectangle());
        if (_screenCaptureService.LooksBlankOrBlack(roughFrame))
        {
            return false;
        }

        var exactBounds = region.Bounds.ToRectangle();
        if (!string.IsNullOrWhiteSpace(region.TemplateImagePath))
        {
            var match = _templateMatcher.FindBestMatch(roughFrame, region.TemplateImagePath);
            if (match.Score >= region.TemplateSimilarityThreshold && match.IsValid)
            {
                exactBounds = new Rectangle(
                    region.Bounds.X + match.Bounds.X,
                    region.Bounds.Y + match.Bounds.Y,
                    match.Bounds.Width,
                    match.Bounds.Height);
            }
        }

        using var exactFrame = _screenCaptureService.Capture(exactBounds);
        return TryUpdateFromFrame(exactBounds, exactFrame);
    }

    public bool TryRefreshExact(Rectangle exactBounds)
    {
        if (exactBounds.Width <= 0 || exactBounds.Height <= 0)
        {
            return false;
        }

        using var exactFrame = _screenCaptureService.Capture(exactBounds);
        return TryUpdateFromFrame(exactBounds, exactFrame);
    }

    public bool TryUpdateFromFrame(Rectangle exactBounds, Bitmap exactFrame)
    {
        if (exactBounds.Width <= 0 || exactBounds.Height <= 0)
        {
            return false;
        }

        if (_screenCaptureService.LooksBlankOrBlack(exactFrame))
        {
            return false;
        }

        var baselineBitmap = new Bitmap(exactFrame);
        var healthCircle = _circleLocatorService.FindHealthCircle(exactFrame);
        var barMeasurement = _healthBarAnalyzerService.Measure(exactFrame, healthCircle);

        lock (_syncRoot)
        {
            _snapshot?.Dispose();
            _snapshot = new HealthBaselineSnapshot(
                exactBounds,
                baselineBitmap,
                DateTime.UtcNow,
                barMeasurement.IsDetected ? barMeasurement.Length : null,
                barMeasurement.IsDetected && barMeasurement.FrameWidth > 0
                    ? barMeasurement.Length / (double)barMeasurement.FrameWidth
                    : null,
                barMeasurement.FrameWidth,
                barMeasurement.IsDetected ? ToSourceX(barMeasurement.Left, barMeasurement) : null,
                barMeasurement.IsDetected ? ToSourceX(barMeasurement.Right, barMeasurement) : null,
                healthCircle,
                barMeasurement.BarScanBounds);
        }

        return true;
    }

    public HealthBaselineSnapshot? CreateSnapshotClone()
    {
        lock (_syncRoot)
        {
            return _snapshot?.Clone();
        }
    }

    public void Dispose()
    {
        lock (_syncRoot)
        {
            _snapshot?.Dispose();
            _snapshot = null;
        }
    }

    private static double ToSourceX(int position, HealthBarMeasurement measurement)
    {
        if (measurement.FrameWidth <= 0 || measurement.BarScanBounds.Width <= 0)
        {
            return measurement.BarScanBounds.X;
        }

        return measurement.BarScanBounds.X + (position / (double)measurement.FrameWidth) * measurement.BarScanBounds.Width;
    }
}

public sealed class HealthBaselineSnapshot : IDisposable
{
    public HealthBaselineSnapshot(
        Rectangle exactBounds,
        Bitmap baselineBitmap,
        DateTime capturedAtUtc,
        int? baselineRedBarLength,
        double? baselineRedBarFillRatio,
        int baselineMeasurementFrameWidth,
        double? baselineBarLeftSourceX,
        double? baselineBarRightSourceX,
        CircleDetection baselineCircle,
        Rectangle baselineBarScanBounds)
    {
        ExactBounds = exactBounds;
        BaselineBitmap = baselineBitmap;
        CapturedAtUtc = capturedAtUtc;
        BaselineRedBarLength = baselineRedBarLength;
        BaselineRedBarFillRatio = baselineRedBarFillRatio;
        BaselineMeasurementFrameWidth = baselineMeasurementFrameWidth;
        BaselineBarLeftSourceX = baselineBarLeftSourceX;
        BaselineBarRightSourceX = baselineBarRightSourceX;
        BaselineCircle = baselineCircle;
        BaselineBarScanBounds = baselineBarScanBounds;
    }

    public Rectangle ExactBounds { get; }

    public Bitmap BaselineBitmap { get; }

    public DateTime CapturedAtUtc { get; }

    public int? BaselineRedBarLength { get; }

    public double? BaselineRedBarFillRatio { get; }

    public int BaselineMeasurementFrameWidth { get; }

    public double? BaselineBarLeftSourceX { get; }

    public double? BaselineBarRightSourceX { get; }

    public CircleDetection BaselineCircle { get; }

    public Rectangle BaselineBarScanBounds { get; }

    public HealthBaselineSnapshot Clone()
    {
        return new HealthBaselineSnapshot(
            ExactBounds,
            new Bitmap(BaselineBitmap),
            CapturedAtUtc,
            BaselineRedBarLength,
            BaselineRedBarFillRatio,
            BaselineMeasurementFrameWidth,
            BaselineBarLeftSourceX,
            BaselineBarRightSourceX,
            BaselineCircle,
            BaselineBarScanBounds);
    }

    public void Dispose()
    {
        BaselineBitmap.Dispose();
    }
}
