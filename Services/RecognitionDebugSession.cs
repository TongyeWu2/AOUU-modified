using System;
using System.Drawing;
using System.Globalization;

namespace WinFormsApp1.Services;

public sealed class RecognitionDebugSession
{
    public RecognitionDebugSession(string rootDirectory)
    {
        SessionDirectory = string.Empty;
    }

    public string SessionDirectory { get; }

    public void SaveBitmap(string fileName, Bitmap bitmap)
    {
    }

    public void WriteLine(string message)
    {
    }

    public void WriteMetric(string key, object? value)
    {
        var text = value switch
        {
            null => "null",
            double number => number.ToString("0.####", CultureInfo.InvariantCulture),
            float number => number.ToString("0.####", CultureInfo.InvariantCulture),
            _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty
        };
        WriteLine($"{key}={text}");
    }
}
