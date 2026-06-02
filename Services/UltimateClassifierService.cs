using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace AOUU.Services;

public sealed class UltimateClassifierService : IDisposable
{
    private const int InputSize = 224;
    private static readonly float[] Mean = [0.485f, 0.456f, 0.406f];
    private static readonly float[] Std = [0.229f, 0.224f, 0.225f];

    private readonly InferenceSession _session;
    private readonly string _inputName;
    private readonly List<string> _labels;

    public UltimateClassifierService(string modelPath, string labelsPath)
    {
        if (!File.Exists(modelPath))
        {
            throw new FileNotFoundException("Ultimate classifier model file was not found.", modelPath);
        }

        if (!File.Exists(labelsPath))
        {
            throw new FileNotFoundException("Ultimate classifier labels file was not found.", labelsPath);
        }

        _session = new InferenceSession(modelPath);
        _inputName = _session.InputMetadata.Keys.First();
        _labels = File.ReadAllLines(labelsPath)
            .Select(label => label.Trim())
            .Where(label => !string.IsNullOrWhiteSpace(label))
            .ToList();

        if (_labels.Count == 0)
        {
            throw new InvalidOperationException("Ultimate classifier labels file is empty.");
        }
    }

    public UltimateClassifierPrediction Predict(Bitmap bitmap)
    {
        using var resized = ResizeAndCenterCrop(bitmap);
        var tensor = new DenseTensor<float>([1, 3, InputSize, InputSize]);

        for (var y = 0; y < InputSize; y++)
        {
            for (var x = 0; x < InputSize; x++)
            {
                var pixel = resized.GetPixel(x, y);
                tensor[0, 0, y, x] = ((pixel.R / 255f) - Mean[0]) / Std[0];
                tensor[0, 1, y, x] = ((pixel.G / 255f) - Mean[1]) / Std[1];
                tensor[0, 2, y, x] = ((pixel.B / 255f) - Mean[2]) / Std[2];
            }
        }

        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor(_inputName, tensor)
        };
        using var results = _session.Run(inputs);
        var logits = results.First().AsEnumerable<float>().ToArray();
        var probabilities = Softmax(logits);
        var bestIndex = 0;
        for (var i = 1; i < probabilities.Length; i++)
        {
            if (probabilities[i] > probabilities[bestIndex])
            {
                bestIndex = i;
            }
        }

        var label = bestIndex < _labels.Count ? _labels[bestIndex] : $"class_{bestIndex}";
        return new UltimateClassifierPrediction(bestIndex, label, probabilities[bestIndex]);
    }

    public void Dispose()
    {
        _session.Dispose();
    }

    private static Bitmap ResizeAndCenterCrop(Bitmap source)
    {
        var scale = 256.0 / Math.Min(source.Width, source.Height);
        var resizedWidth = Math.Max(InputSize, (int)Math.Round(source.Width * scale));
        var resizedHeight = Math.Max(InputSize, (int)Math.Round(source.Height * scale));

        using var resized = new Bitmap(resizedWidth, resizedHeight);
        using (var graphics = Graphics.FromImage(resized))
        {
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            graphics.DrawImage(source, 0, 0, resizedWidth, resizedHeight);
        }

        var cropX = Math.Max(0, (resizedWidth - InputSize) / 2);
        var cropY = Math.Max(0, (resizedHeight - InputSize) / 2);
        return resized.Clone(new Rectangle(cropX, cropY, InputSize, InputSize), resized.PixelFormat);
    }

    private static float[] Softmax(float[] logits)
    {
        var max = logits.Max();
        var exps = logits.Select(value => MathF.Exp(value - max)).ToArray();
        var sum = exps.Sum();
        return exps.Select(value => value / sum).ToArray();
    }
}

public sealed record UltimateClassifierPrediction(int Index, string ClassName, float Confidence);
