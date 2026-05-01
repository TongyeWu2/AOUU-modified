using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using Tesseract;

namespace AOUU.Services;

public sealed class ScreenTextRecognizer
{
    private const string EnglishDataFileName = "eng.traineddata";

    private readonly string _sourceTessdataPath;
    private readonly string _sourceEnglishDataPath;
    private readonly string _runtimeTessdataPath;
    private readonly string _runtimeEnglishDataPath;
    private bool _initializationFailed;

    public ScreenTextRecognizer()
    {
        _sourceTessdataPath = Path.Combine(AppContext.BaseDirectory, "tessdata");
        _sourceEnglishDataPath = Path.Combine(_sourceTessdataPath, EnglishDataFileName);
        _runtimeTessdataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AOUU",
            "tessdata");
        _runtimeEnglishDataPath = Path.Combine(_runtimeTessdataPath, EnglishDataFileName);
        Debug.WriteLine($"AOUU OCR source tessdata path: {_sourceTessdataPath}");
        Debug.WriteLine($"AOUU OCR runtime tessdata path: {_runtimeTessdataPath}");
    }

    public string TessdataPath => _runtimeTessdataPath;

    public string EnglishDataPath => _runtimeEnglishDataPath;

    public string SourceTessdataPath => _sourceTessdataPath;

    public string SourceEnglishDataPath => _sourceEnglishDataPath;

    public bool IsAvailable => !_initializationFailed;

    public bool TryRecognize(Bitmap screenshot, out string text, out string errorMessage)
    {
        text = string.Empty;
        errorMessage = string.Empty;

        Debug.WriteLine($"AOUU OCR source tessdata path: {_sourceTessdataPath}");
        Debug.WriteLine($"AOUU OCR runtime tessdata path: {_runtimeTessdataPath}");

        if (_initializationFailed)
        {
            errorMessage = $"OCR 已暂停。{BuildTessdataDiagnostics()}";
            return false;
        }

        if (!TryPrepareRuntimeTessdata(out errorMessage))
        {
            _initializationFailed = true;
            return false;
        }

        try
        {
            using var ocrBitmap = CreateOcrBitmap(screenshot);
            using var engine = new TesseractEngine(_runtimeTessdataPath, "eng", EngineMode.Default);
            engine.SetVariable("tessedit_char_whitelist", "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789");
            engine.SetVariable("classify_bln_numeric_mode", "0");

            using var pix = PixConverter.ToPix(ocrBitmap);
            using var page = engine.Process(pix, PageSegMode.SingleLine);
            text = page.GetText() ?? string.Empty;
            return true;
        }
        catch (Exception ex)
        {
            _initializationFailed = true;
            errorMessage = $"Tesseract OCR 初始化失败。{BuildTessdataDiagnostics()} 异常：{BuildExceptionMessage(ex)}";
            Debug.WriteLine($"AOUU OCR initialization failed. {BuildTessdataDiagnostics()} exception={ex}");
            return false;
        }
    }

    private bool TryPrepareRuntimeTessdata(out string errorMessage)
    {
        errorMessage = string.Empty;

        try
        {
            if (!File.Exists(_sourceEnglishDataPath))
            {
                errorMessage = $"缺少英文 OCR 数据文件 {EnglishDataFileName}。{BuildTessdataDiagnostics()}";
                Debug.WriteLine($"AOUU OCR missing source eng.traineddata. {BuildTessdataDiagnostics()}");
                return false;
            }

            Directory.CreateDirectory(_runtimeTessdataPath);

            if (ShouldCopyEnglishData())
            {
                File.Copy(_sourceEnglishDataPath, _runtimeEnglishDataPath, overwrite: true);
                Debug.WriteLine($"AOUU OCR copied eng.traineddata to runtime tessdata path. {BuildTessdataDiagnostics()}");
            }

            if (!File.Exists(_runtimeEnglishDataPath))
            {
                errorMessage = $"无法准备英文 OCR 数据文件 {EnglishDataFileName}。{BuildTessdataDiagnostics()}";
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            errorMessage = $"准备 Tesseract OCR 数据文件失败。{BuildTessdataDiagnostics()} 异常：{BuildExceptionMessage(ex)}";
            Debug.WriteLine($"AOUU OCR tessdata preparation failed. {BuildTessdataDiagnostics()} exception={ex}");
            return false;
        }
    }

    private bool ShouldCopyEnglishData()
    {
        if (!File.Exists(_runtimeEnglishDataPath))
        {
            return true;
        }

        var sourceInfo = new FileInfo(_sourceEnglishDataPath);
        var runtimeInfo = new FileInfo(_runtimeEnglishDataPath);
        return sourceInfo.Length != runtimeInfo.Length ||
               sourceInfo.LastWriteTimeUtc > runtimeInfo.LastWriteTimeUtc;
    }

    private string BuildTessdataDiagnostics()
    {
        return
            $"源 tessdata 路径：{_sourceTessdataPath}；" +
            $"运行时 tessdata 路径：{_runtimeTessdataPath}；" +
            $"源 eng.traineddata 存在：{File.Exists(_sourceEnglishDataPath)}，大小：{GetFileSizeText(_sourceEnglishDataPath)}；" +
            $"运行时 eng.traineddata 存在：{File.Exists(_runtimeEnglishDataPath)}，大小：{GetFileSizeText(_runtimeEnglishDataPath)}。";
    }

    private static string GetFileSizeText(string path)
    {
        return File.Exists(path) ? new FileInfo(path).Length.ToString() : "missing";
    }

    private static string BuildExceptionMessage(Exception exception)
    {
        if (exception.InnerException is null)
        {
            return exception.Message;
        }

        return $"{exception.Message} InnerException: {exception.InnerException.Message}";
    }

    private static Bitmap CreateOcrBitmap(Bitmap source)
    {
        using var scaled = ScaleBitmap(source, 3);
        var bitmap = new Bitmap(scaled.Width, scaled.Height, PixelFormat.Format24bppRgb);

        for (var y = 0; y < scaled.Height; y++)
        {
            for (var x = 0; x < scaled.Width; x++)
            {
                var pixel = scaled.GetPixel(x, y);
                var maxChannel = Math.Max(pixel.R, Math.Max(pixel.G, pixel.B));
                var minChannel = Math.Min(pixel.R, Math.Min(pixel.G, pixel.B));
                var grayscale = (int)((pixel.R * 0.299) + (pixel.G * 0.587) + (pixel.B * 0.114));
                var contrast = Math.Clamp(((grayscale - 128) * 2) + 128, 0, 255);
                var redTextCandidate = pixel.R >= 80 && pixel.R > pixel.G * 1.25 && pixel.R > pixel.B * 1.25;
                var brightTextCandidate = maxChannel >= 145 && maxChannel - minChannel <= 120;
                var threshold = redTextCandidate || brightTextCandidate || contrast >= 150;
                bitmap.SetPixel(x, y, threshold ? Color.White : Color.Black);
            }
        }

        return bitmap;
    }

    private static Bitmap ScaleBitmap(Bitmap source, int scale)
    {
        var width = Math.Max(1, source.Width * scale);
        var height = Math.Max(1, source.Height * scale);
        var bitmap = new Bitmap(width, height, PixelFormat.Format24bppRgb);

        using var graphics = Graphics.FromImage(bitmap);
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        graphics.SmoothingMode = SmoothingMode.None;
        graphics.DrawImage(source, 0, 0, width, height);

        return bitmap;
    }
}
