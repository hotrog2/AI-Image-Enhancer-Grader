using ColorGrader.Application.Interfaces;
using ColorGrader.Core.Models;

namespace ColorGrader.AI.Services;

public sealed class PreferenceStyleLearningService(ICatalogService catalogService) : IStyleLearningService
{
    public async Task<EnhancementSuggestion> BuildSuggestionAsync(
        CatalogAsset asset,
        EnhancementFeature enabledFeatures,
        ImageAnalysis? analysis,
        long? styleProfileId,
        CancellationToken cancellationToken)
    {
        var feedback = await catalogService.GetFeedbackAsync(cancellationToken);
        var relevantFeedback = styleProfileId is null
            ? feedback.Where(item => item.StyleProfileId is null)
            : feedback.Where(item => item.StyleProfileId == styleProfileId);

        var accepted = relevantFeedback
            .Where(item => item.Outcome == FeedbackDisposition.Accepted || item.Outcome == FeedbackDisposition.ModifiedAfterAccept)
            .Take(12)
            .ToList();

        var baseSettings = BuildBaseSettings(analysis);
        var learnedSettings = !enabledFeatures.HasFlag(EnhancementFeature.StyleLearning) || accepted.Count == 0
            ? baseSettings
            : Blend(baseSettings, Average(accepted.Select(item => item.Settings).ToList()), 0.45);
        var maskedSettings = learnedSettings.ApplyFeatureMask(enabledFeatures);

        var rationale = accepted.Count == 0
            ? styleProfileId is null
                ? $"Balanced quality-first pass for {asset.Kind} with room for skin finish and tone shaping."
                : $"Balanced quality-first pass for {asset.Kind}; the selected style profile does not have accepted examples yet."
            : styleProfileId is null
                ? $"Blended scene analysis with {accepted.Count} accepted edits from your catalog history."
                : $"Blended scene analysis with {accepted.Count} accepted edits from the selected style profile.";

        var confidence = accepted.Count == 0
            ? 0.62
            : Math.Min(styleProfileId is null ? 0.90 : 0.93, 0.65 + (accepted.Count * 0.02));
        return new EnhancementSuggestion(enabledFeatures, maskedSettings, rationale, confidence);
    }

    private static EnhancementSettings BuildBaseSettings(ImageAnalysis? analysis)
    {
        if (analysis is null)
        {
            return EnhancementSettings.Default;
        }

        var exposure = Math.Clamp((0.56 - analysis.AverageLuminance) * 1.4, -0.2, 0.8);
        var contrast = Math.Clamp(0.10 + ((0.28 - analysis.AverageSaturation) * 0.25), 0.04, 0.22);
        var warmth = Math.Clamp(-analysis.WarmthBias * 0.20, -0.10, 0.10);
        var saturation = Math.Clamp(0.08 + ((0.24 - analysis.AverageSaturation) * 0.4), 0.03, 0.18);
        var vibrance = Math.Clamp(0.12 + ((0.30 - analysis.AverageSaturation) * 0.3), 0.06, 0.24);
        var denoise = analysis.AverageLuminance < 0.35 ? 0.16 : 0.10;
        var sharpen = analysis.AverageLuminance > 0.55 ? 0.16 : 0.22;

        return new EnhancementSettings(
            Exposure: exposure,
            Contrast: contrast,
            Vibrance: vibrance,
            Warmth: warmth,
            Saturation: saturation,
            HighlightRecovery: 0.12,
            ShadowLift: Math.Clamp(0.08 + ((0.50 - analysis.AverageLuminance) * 0.25), 0.04, 0.18),
            SkinSoftening: 0.06,
            Denoise: denoise,
            Sharpen: sharpen,
            UpscaleFactor: 1.0);
    }

    private static EnhancementSettings Average(IReadOnlyList<EnhancementSettings> values)
    {
        if (values.Count == 0)
        {
            return EnhancementSettings.Default;
        }

        return new EnhancementSettings(
            values.Average(item => item.Exposure),
            values.Average(item => item.Contrast),
            values.Average(item => item.Vibrance),
            values.Average(item => item.Warmth),
            values.Average(item => item.Saturation),
            values.Average(item => item.HighlightRecovery),
            values.Average(item => item.ShadowLift),
            values.Average(item => item.SkinSoftening),
            values.Average(item => item.Denoise),
            values.Average(item => item.Sharpen),
            values.Average(item => item.UpscaleFactor));
    }

    private static EnhancementSettings Blend(EnhancementSettings first, EnhancementSettings second, double weightOfSecond)
    {
        var firstWeight = 1.0 - weightOfSecond;

        return new EnhancementSettings(
            Exposure: (first.Exposure * firstWeight) + (second.Exposure * weightOfSecond),
            Contrast: (first.Contrast * firstWeight) + (second.Contrast * weightOfSecond),
            Vibrance: (first.Vibrance * firstWeight) + (second.Vibrance * weightOfSecond),
            Warmth: (first.Warmth * firstWeight) + (second.Warmth * weightOfSecond),
            Saturation: (first.Saturation * firstWeight) + (second.Saturation * weightOfSecond),
            HighlightRecovery: (first.HighlightRecovery * firstWeight) + (second.HighlightRecovery * weightOfSecond),
            ShadowLift: (first.ShadowLift * firstWeight) + (second.ShadowLift * weightOfSecond),
            SkinSoftening: (first.SkinSoftening * firstWeight) + (second.SkinSoftening * weightOfSecond),
            Denoise: (first.Denoise * firstWeight) + (second.Denoise * weightOfSecond),
            Sharpen: (first.Sharpen * firstWeight) + (second.Sharpen * weightOfSecond),
            UpscaleFactor: (first.UpscaleFactor * firstWeight) + (second.UpscaleFactor * weightOfSecond));
    }
}
