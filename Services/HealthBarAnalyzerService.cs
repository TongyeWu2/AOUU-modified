using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using OpenCvSharp;
using OpenCvSharp.Extensions;

namespace WinFormsApp1.Services;

public sealed class HealthBarAnalyzerService
{
    private const int StandardLaneHeight = 24;

    public HealthBarMeasurement Measure(Bitmap bitmap, CircleDetection? circle = null)
    {
        if (bitmap.Width <= 0 || bitmap.Height <= 0)
        {
            return HealthBarMeasurement.Empty;
        }

        try
        {
            var laneBounds = circle is null
                ? GetFallbackBarLane(bitmap.Size)
                : GetBarLaneFromCircle(bitmap.Size, circle);

            if (laneBounds.Width <= 3 || laneBounds.Height <= 3)
            {
                return HealthBarMeasurement.Empty;
            }

            using var laneBitmap = bitmap.Clone(laneBounds, bitmap.PixelFormat);
            using var normalizedBitmap = ResizeByHeight(laneBitmap, StandardLaneHeight);
            using var source = BitmapConverter.ToMat(normalizedBitmap);
            using var bgr = EnsureBgr(source);
            using var hsv = new Mat();
            using var lowerMask = new Mat();
            using var upperMask = new Mat();
            using var redMask = new Mat();
            using var kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new OpenCvSharp.Size(3, 3));

            Cv2.CvtColor(bgr, hsv, ColorConversionCodes.BGR2HSV);
            Cv2.InRange(hsv, new Scalar(0, 70, 70), new Scalar(12, 255, 255), lowerMask);
            Cv2.InRange(hsv, new Scalar(168, 70, 70), new Scalar(180, 255, 255), upperMask);
            Cv2.BitwiseOr(lowerMask, upperMask, redMask);
            Cv2.MorphologyEx(redMask, redMask, MorphTypes.Close, kernel);
            Cv2.MorphologyEx(redMask, redMask, MorphTypes.Open, kernel);

            var totalRedPixels = Cv2.CountNonZero(redMask);
            if (totalRedPixels < 8)
            {
                return HealthBarMeasurement.Empty with { BarScanBounds = laneBounds };
            }

            var bestRow = -1;
            var bestRowCount = 0;
            for (var y = 0; y < redMask.Rows; y++)
            {
                var rowCount = 0;
                for (var x = 0; x < redMask.Cols; x++)
                {
                    if (redMask.At<byte>(y, x) > 0)
                    {
                        rowCount++;
                    }
                }

                if (rowCount > bestRowCount)
                {
                    bestRowCount = rowCount;
                    bestRow = y;
                }
            }

            if (bestRow < 0 || bestRowCount < 4)
            {
                return HealthBarMeasurement.Empty with { BarScanBounds = laneBounds };
            }

            var left = int.MaxValue;
            var right = -1;
            var supportingRows = 0;
            var rowStart = Math.Max(0, bestRow - 1);
            var rowEnd = Math.Min(redMask.Rows - 1, bestRow + 1);
            var requiredRowPixels = Math.Max(3, bestRowCount / 4);

            for (var y = rowStart; y <= rowEnd; y++)
            {
                var rowLeft = -1;
                var rowRight = -1;
                var rowCount = 0;

                for (var x = 0; x < redMask.Cols; x++)
                {
                    if (redMask.At<byte>(y, x) == 0)
                    {
                        continue;
                    }

                    rowCount++;
                    rowLeft = rowLeft < 0 ? x : rowLeft;
                    rowRight = x;
                }

                if (rowCount < requiredRowPixels || rowLeft < 0 || rowRight < rowLeft)
                {
                    continue;
                }

                supportingRows++;
                left = Math.Min(left, rowLeft);
                right = Math.Max(right, rowRight);
            }

            if (supportingRows == 0 || right <= left)
            {
                return HealthBarMeasurement.Empty with { BarScanBounds = laneBounds };
            }

            return new HealthBarMeasurement(
                true,
                left,
                right,
                right - left + 1,
                normalizedBitmap.Width,
                normalizedBitmap.Height,
                laneBounds);
        }
        catch
        {
            return HealthBarMeasurement.Empty;
        }
    }

    private static Rectangle GetBarLaneFromCircle(System.Drawing.Size sourceSize, CircleDetection circle)
    {
        var radius = Math.Max(10, circle.Radius);
        var x = (int)Math.Round(circle.Center.X + radius * 0.72);
        var y = (int)Math.Round(circle.Center.Y - radius * 0.95);
        var width = (int)Math.Round(sourceSize.Width - x - radius * 0.18);
        var height = (int)Math.Round(radius * 0.95);

        var rawBounds = new Rectangle(x, y, Math.Max(1, width), Math.Max(1, height));
        return Rectangle.Intersect(new Rectangle(System.Drawing.Point.Empty, sourceSize), rawBounds);
    }

    private static Rectangle GetFallbackBarLane(System.Drawing.Size sourceSize)
    {
        var x = Math.Clamp((int)(sourceSize.Width * 0.18), 0, Math.Max(0, sourceSize.Width - 1));
        var y = Math.Clamp((int)(sourceSize.Height * 0.01), 0, Math.Max(0, sourceSize.Height - 1));
        var width = Math.Max(1, sourceSize.Width - x);
        var height = Math.Max(1, (int)(sourceSize.Height * 0.34));

        var rectangle = new Rectangle(x, y, Math.Min(width, sourceSize.Width - x), Math.Min(height, sourceSize.Height - y));
        return Rectangle.Intersect(new Rectangle(System.Drawing.Point.Empty, sourceSize), rectangle);
    }

    private static Bitmap ResizeByHeight(Bitmap source, int targetHeight)
    {
        var targetWidth = Math.Max(1, (int)Math.Round(source.Width * (targetHeight / (double)Math.Max(1, source.Height))));
        var resized = new Bitmap(targetWidth, targetHeight);

        using var graphics = Graphics.FromImage(resized);
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        graphics.SmoothingMode = SmoothingMode.HighQuality;
        graphics.DrawImage(source, new Rectangle(0, 0, targetWidth, targetHeight));
        return resized;
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
}

public sealed record HealthBarMeasurement(
    bool IsDetected,
    int Left,
    int Right,
    int Length,
    int FrameWidth,
    int FrameHeight,
    Rectangle BarScanBounds)
{
    public static HealthBarMeasurement Empty { get; } = new(false, 0, 0, 0, 0, 0, Rectangle.Empty);
}
