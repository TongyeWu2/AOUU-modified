using System;
using System.Drawing;
using System.Linq;
using OpenCvSharp;
using OpenCvSharp.Extensions;

namespace AOUU.Services;

public sealed class CircleLocatorService
{
    public CircleDetection FindHealthCircle(Bitmap bitmap)
    {
        var searchArea = new Rectangle(0, 0, Math.Max(1, (int)(bitmap.Width * 0.24)), bitmap.Height);
        var fallbackCenter = new System.Drawing.Point(
            Math.Max(10, (int)(bitmap.Height * 0.42)),
            Math.Max(10, bitmap.Height / 2));
        var fallbackRadius = Math.Max(12, Math.Min(searchArea.Width, searchArea.Height) / 3);
        return FindCircle(bitmap, searchArea, fallbackCenter, fallbackRadius);
    }

    public System.Drawing.Point FindHealthCircleCenter(Bitmap bitmap)
    {
        return FindHealthCircle(bitmap).Center;
    }

    public CircleDetection FindSkillCircle(Bitmap bitmap)
    {
        var fallbackCenter = new System.Drawing.Point(bitmap.Width / 2, bitmap.Height / 2);
        var fallbackRadius = Math.Max(12, Math.Min(bitmap.Width, bitmap.Height) / 3);
        return FindCircle(bitmap, new Rectangle(0, 0, bitmap.Width, bitmap.Height), fallbackCenter, fallbackRadius);
    }

    public System.Drawing.Point FindSkillCircleCenter(Bitmap bitmap)
    {
        return FindSkillCircle(bitmap).Center;
    }

    private static CircleDetection FindCircle(Bitmap bitmap, Rectangle searchArea, System.Drawing.Point fallbackCenter, int fallbackRadius)
    {
        try
        {
            using var roiBitmap = bitmap.Clone(searchArea, bitmap.PixelFormat);
            using var source = BitmapConverter.ToMat(roiBitmap);
            using var gray = new Mat();
            using var blurred = new Mat();

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
                source.CopyTo(gray);
            }

            Cv2.GaussianBlur(gray, blurred, new OpenCvSharp.Size(9, 9), 2);

            var minRadius = Math.Max(8, Math.Min(searchArea.Width, searchArea.Height) / 8);
            var maxRadius = Math.Max(minRadius + 6, Math.Min(searchArea.Width, searchArea.Height) / 2);
            var circles = Cv2.HoughCircles(
                blurred,
                HoughModes.Gradient,
                1,
                Math.Max(20, searchArea.Height / 3),
                100,
                22,
                minRadius,
                maxRadius);

            if (circles.Length == 0)
            {
                return new CircleDetection(
                    new System.Drawing.Point(searchArea.X + fallbackCenter.X, searchArea.Y + fallbackCenter.Y),
                    fallbackRadius,
                    false);
            }

            var bestCircle = circles
                .OrderByDescending(circle => circle.Radius)
                .First();

            return new CircleDetection(
                new System.Drawing.Point(
                    searchArea.X + (int)Math.Round(bestCircle.Center.X),
                    searchArea.Y + (int)Math.Round(bestCircle.Center.Y)),
                (int)Math.Round(bestCircle.Radius),
                true);
        }
        catch
        {
            return new CircleDetection(
                new System.Drawing.Point(searchArea.X + fallbackCenter.X, searchArea.Y + fallbackCenter.Y),
                fallbackRadius,
                false);
        }
    }
}

public sealed record CircleDetection(System.Drawing.Point Center, int Radius, bool IsDetected);
