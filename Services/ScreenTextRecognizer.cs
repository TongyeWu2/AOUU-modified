using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using Tesseract;

namespace AOUU.Services;

public sealed class ScreenTextRecognizer
{
    private readonly string _tessdataPath;
    private readonly string _englishDataPath;
    private bool _initializationFailed;

    public ScreenTextRecognizer()
    {
        _tessdataPath = Path.Combine(AppContext.BaseDirectory, "tessdata");
        _englishDataPath = Path.Combine(_tessdataPath, "eng.traineddata");
        Debug.WriteLine($"AOUU OCR tessdata path: {_tessdataPath}");
    }

    public string TessdataPath => _tessdataPath;

    public string EnglishDataPath => _englishDataPath;

    public bool IsAvailable => !_initializationFailed;

    public bool TryRecognize(Bitmap screenshot, out string text, out string errorMessage)
    {
        text = string.Empty;
        errorMessage = string.Empty;

        Debug.WriteLine($"AOUU OCR tessdata path: {_tessdataPath}");

        if (_initializationFailed)
        {
            errorMessage = $"屏幕文字识别已停用。请确认英文 OCR 数据文件存在：{_englishDataPath}";
            return false;
        }

        if (!File.Exists(_englishDataPath))
        {
            _initializationFailed = true;
            errorMessage = $"缺少英文 OCR 数据文件。请把 eng.traineddata 放到：{_englishDataPath}";
            Debug.WriteLine($"AOUU OCR missing eng.traineddata: {_englishDataPath}");
            return false;
        }

        try
        {
            using var ocrBitmap = CreateHighContrastBitmap(screenshot);
            using var engine = new TesseractEngine(_tessdataPath, "eng", EngineMode.Default);
            engine.SetVariable("tessedit_char_whitelist", "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz ");

            using var pix = PixConverter.ToPix(ocrBitmap);
            using var page = engine.Process(pix, PageSegMode.Auto);
            text = page.GetText() ?? string.Empty;
            return true;
        }
        catch (Exception ex)
        {
            _initializationFailed = true;
            errorMessage = $"Tesseract 初始化失败。AOUU 正在使用的 tessdata 路径是：{_tessdataPath}。请确认 eng.traineddata 位于：{_englishDataPath}。原始错误：{ex.Message}";
            Debug.WriteLine($"AOUU OCR initialization failed. tessdata={_tessdataPath}; eng={_englishDataPath}; error={ex}");
            return false;
        }
    }

    private static Bitmap CreateHighContrastBitmap(Bitmap source)
    {
        var bitmap = new Bitmap(source.Width, source.Height);

        for (var y = 0; y < source.Height; y++)
        {
            for (var x = 0; x < source.Width; x++)
            {
                var pixel = source.GetPixel(x, y);
                var maxChannel = Math.Max(pixel.R, Math.Max(pixel.G, pixel.B));
                var redTextCandidate = pixel.R >= 90 && pixel.R > pixel.G * 1.35 && pixel.R > pixel.B * 1.35;
                var brightTextCandidate = maxChannel >= 155;
                bitmap.SetPixel(x, y, redTextCandidate || brightTextCandidate ? Color.White : Color.Black);
            }
        }

        return bitmap;
    }
}
