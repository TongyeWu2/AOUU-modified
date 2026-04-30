using System;
using System.IO;
using AOUU.Models;

namespace AOUU.Services;

public sealed class DeathTriggerService
{
    private readonly ScreenCaptureService _screenCaptureService;
    private readonly TemplateMatcher _templateMatcher;

    public DeathTriggerService(
        ScreenCaptureService screenCaptureService,
        TemplateMatcher templateMatcher,
        HealthBarAnalyzerService healthBarAnalyzerService,
        CircleLocatorService circleLocatorService)
    {
        _screenCaptureService = screenCaptureService;
        _templateMatcher = templateMatcher;
    }

    public DeathTriggerEvaluation Evaluate(DeathTriggerConfig config)
    {
        if (!config.Enabled)
        {
            return DeathTriggerEvaluation.NotMatched("死亡触发未启用。");
        }

        if (config.DeathTextRegion is null || config.DeathTextRegion.Width <= 0 || config.DeathTextRegion.Height <= 0)
        {
            return DeathTriggerEvaluation.NotMatched("死亡触发缺少 YOU DIED 区域。");
        }

        if (string.IsNullOrWhiteSpace(config.DeathTemplateImagePath) || !File.Exists(config.DeathTemplateImagePath))
        {
            return DeathTriggerEvaluation.NotMatched($"死亡触发模板不存在：{config.DeathTemplateImagePath}");
        }

        using var textFrame = _screenCaptureService.Capture(config.DeathTextRegion.ToRectangle());
        if (_screenCaptureService.LooksBlankOrBlack(textFrame))
        {
            return new DeathTriggerEvaluation(false, false, 0, "死亡触发文字截图接近黑屏。");
        }

        var match = _templateMatcher.FindBestMatch(textFrame, config.DeathTemplateImagePath);
        var templateMatched = match.Score >= Math.Clamp(config.TemplateSimilarityThreshold, 0.1, 1.0);
        var message = templateMatched
            ? $"死亡触发命中：模板相似度 {match.Score:0.###}。"
            : $"死亡触发未命中：模板相似度 {match.Score:0.###}。";

        return new DeathTriggerEvaluation(templateMatched, templateMatched, match.Score, message);
    }
}

public sealed record DeathTriggerEvaluation(
    bool Matched,
    bool TemplateMatched,
    double TemplateScore,
    string Message)
{
    public static DeathTriggerEvaluation NotMatched(string message)
    {
        return new DeathTriggerEvaluation(false, false, 0, message);
    }
}
