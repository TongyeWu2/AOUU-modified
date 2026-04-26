using System;
using System.Drawing;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using WinFormsApp1.Models;

namespace WinFormsApp1.Services;

public sealed class RegionChangeDetector
{
    public bool IsRegionChanged(Bitmap baseline, Bitmap current, HealthChangeWatchRegion region)
    {
        var redDeltaRatio = GetRedBarDeltaRatio(baseline, current);
        if (redDeltaRatio >= region.ChangedAreaRatioThreshold)
        {
            return true;
        }

        using var baselineGray = ToGray(baseline);
        using var currentGray = ToGray(current);
        using var diff = new Mat();
        using var mask = new Mat();

        Cv2.Absdiff(baselineGray, currentGray, diff);
        Cv2.Threshold(diff, mask, region.DiffPixelThreshold, 255, ThresholdTypes.Binary);

        var changedPixels = Cv2.CountNonZero(mask);
        var totalPixels = mask.Rows * mask.Cols;
        if (totalPixels <= 0)
        {
            return false;
        }

        var changedRatio = changedPixels / (double)totalPixels;
        return changedRatio >= Math.Max(region.ChangedAreaRatioThreshold * 1.3, 0.12);
    }

    private static double GetRedBarDeltaRatio(Bitmap baseline, Bitmap current)
    {
        var baselineLane = GetRedBarLane(baseline);
        var currentLane = GetRedBarLane(current);

        using var baselineMat = BitmapConverter.ToMat(baselineLane);
        using var currentMat = BitmapConverter.ToMat(currentLane);
        using var baselineMask = CreateRedMask(baselineMat);
        using var currentMask = CreateRedMask(currentMat);
        using var diffMask = new Mat();

        Cv2.Absdiff(baselineMask, currentMask, diffMask);
        var deltaPixels = Cv2.CountNonZero(diffMask);
        var totalPixels = diffMask.Rows * diffMask.Cols;
        return totalPixels <= 0 ? 0 : deltaPixels / (double)totalPixels;
    }

    private static Bitmap GetRedBarLane(Bitmap source)
    {
        var x = Math.Clamp((int)(source.Width * 0.18), 0, Math.Max(0, source.Width - 1));
        var y = Math.Clamp((int)(source.Height * 0.02), 0, Math.Max(0, source.Height - 1));
        var width = Math.Max(1, source.Width - x);
        var height = Math.Max(1, (int)(source.Height * 0.30));

        var rectangle = new Rectangle(x, y, Math.Min(width, source.Width - x), Math.Min(height, source.Height - y));
        return source.Clone(rectangle, source.PixelFormat);
    }

    private static Mat CreateRedMask(Mat source)
    {
        using var hsv = new Mat();
        using var lowerMask = new Mat();
        using var upperMask = new Mat();
        var mask = new Mat();

        Cv2.CvtColor(source, hsv, ColorConversionCodes.BGR2HSV);
        Cv2.InRange(hsv, new Scalar(0, 80, 80), new Scalar(12, 255, 255), lowerMask);
        Cv2.InRange(hsv, new Scalar(168, 80, 80), new Scalar(180, 255, 255), upperMask);
        Cv2.BitwiseOr(lowerMask, upperMask, mask);
        return mask;
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
}
