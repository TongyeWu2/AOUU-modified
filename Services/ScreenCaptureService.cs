using System;
using System.Drawing;

namespace AOUU.Services;

public sealed class ScreenCaptureService
{
    public Bitmap Capture(Rectangle bounds)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            throw new ArgumentException("截图区域宽高必须大于 0。", nameof(bounds));
        }

        var bitmap = new Bitmap(bounds.Width, bounds.Height);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size);
        return bitmap;
    }

    public bool LooksBlankOrBlack(Bitmap bitmap)
    {
        if (bitmap.Width == 0 || bitmap.Height == 0)
        {
            return true;
        }

        var stepX = Math.Max(1, bitmap.Width / 8);
        var stepY = Math.Max(1, bitmap.Height / 8);

        var samples = 0;
        double sum = 0;
        double sumSquares = 0;

        for (var y = 0; y < bitmap.Height; y += stepY)
        {
            for (var x = 0; x < bitmap.Width; x += stepX)
            {
                var pixel = bitmap.GetPixel(x, y);
                var brightness = (pixel.R + pixel.G + pixel.B) / 3.0;
                sum += brightness;
                sumSquares += brightness * brightness;
                samples++;
            }
        }

        if (samples == 0)
        {
            return true;
        }

        var mean = sum / samples;
        var variance = (sumSquares / samples) - (mean * mean);
        return mean < 5 && variance < 2;
    }
}
