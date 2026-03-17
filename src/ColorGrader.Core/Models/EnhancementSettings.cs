namespace ColorGrader.Core.Models;

public sealed record EnhancementSettings(
    double Exposure,
    double Contrast,
    double Vibrance,
    double Warmth,
    double Saturation,
    double HighlightRecovery,
    double ShadowLift,
    double SkinSoftening,
    double Denoise,
    double Sharpen,
    double UpscaleFactor,
    double DetailRecovery,
    double Deblur,
    double ArtifactReduction,
    double RealismBoost)
{
    public static EnhancementSettings Default { get; } = new(
        Exposure: 0.10,
        Contrast: 0.12,
        Vibrance: 0.16,
        Warmth: 0.03,
        Saturation: 0.06,
        HighlightRecovery: 0.10,
        ShadowLift: 0.10,
        SkinSoftening: 0.06,
        Denoise: 0.12,
        Sharpen: 0.18,
        UpscaleFactor: 1.0,
        DetailRecovery: 0.20,
        Deblur: 0.18,
        ArtifactReduction: 0.16,
        RealismBoost: 0.12);

    public EnhancementSettings ApplyFeatureMask(EnhancementFeature featureMask)
    {
        return this with
        {
            Exposure = featureMask.HasFlag(EnhancementFeature.AutoExposure) ? Exposure : 0,
            Warmth = featureMask.HasFlag(EnhancementFeature.WhiteBalance) ? Warmth : 0,
            Contrast = featureMask.HasFlag(EnhancementFeature.Contrast) ? Contrast : 0,
            Vibrance = featureMask.HasFlag(EnhancementFeature.ToneCurve) ? Vibrance : 0,
            Saturation = featureMask.HasFlag(EnhancementFeature.ToneCurve) ? Saturation : 0,
            HighlightRecovery = featureMask.HasFlag(EnhancementFeature.ToneCurve) ? HighlightRecovery : 0,
            ShadowLift = featureMask.HasFlag(EnhancementFeature.ToneCurve) ? ShadowLift : 0,
            SkinSoftening = featureMask.HasFlag(EnhancementFeature.SkinTone) ? SkinSoftening : 0,
            Denoise = featureMask.HasFlag(EnhancementFeature.Denoise) ? Denoise : 0,
            Sharpen = featureMask.HasFlag(EnhancementFeature.Sharpen) ? Sharpen : 0,
            UpscaleFactor = featureMask.HasFlag(EnhancementFeature.Upscale) ? UpscaleFactor : 1.0,
            DetailRecovery = featureMask.HasFlag(EnhancementFeature.QualityRestore) ? DetailRecovery : 0,
            Deblur = featureMask.HasFlag(EnhancementFeature.QualityRestore) ? Deblur : 0,
            ArtifactReduction = featureMask.HasFlag(EnhancementFeature.QualityRestore) ? ArtifactReduction : 0,
            RealismBoost = featureMask.HasFlag(EnhancementFeature.QualityRestore) ? RealismBoost : 0
        };
    }

    public EnhancementSettings ApplyStrength(double strength)
    {
        var normalized = Math.Clamp(strength, 0.0, 1.0);

        return this with
        {
            Exposure = Exposure * normalized,
            Contrast = Contrast * normalized,
            Vibrance = Vibrance * normalized,
            Warmth = Warmth * normalized,
            Saturation = Saturation * normalized,
            HighlightRecovery = HighlightRecovery * normalized,
            ShadowLift = ShadowLift * normalized,
            SkinSoftening = SkinSoftening * normalized,
            Denoise = Denoise * normalized,
            Sharpen = Sharpen * normalized,
            UpscaleFactor = 1.0 + ((UpscaleFactor - 1.0) * normalized),
            DetailRecovery = DetailRecovery * normalized,
            Deblur = Deblur * normalized,
            ArtifactReduction = ArtifactReduction * normalized,
            RealismBoost = RealismBoost * normalized
        };
    }

    public EnhancementSettings ApplyManualAdjustments(ManualEnhancementAdjustments adjustments)
    {
        return this with
        {
            Exposure = Math.Clamp(Exposure + adjustments.Exposure, -1.0, 1.5),
            Contrast = Math.Clamp(Contrast + adjustments.Contrast, -0.5, 1.0),
            Vibrance = Math.Clamp(Vibrance + adjustments.Vibrance, -0.5, 1.0),
            Warmth = Math.Clamp(Warmth + adjustments.Warmth, -0.35, 0.35),
            Saturation = Math.Clamp(Saturation + adjustments.Saturation, -0.5, 1.0),
            HighlightRecovery = Math.Clamp(HighlightRecovery + adjustments.HighlightRecovery, 0.0, 1.0),
            ShadowLift = Math.Clamp(ShadowLift + adjustments.ShadowLift, -0.5, 1.0),
            SkinSoftening = Math.Clamp(SkinSoftening + adjustments.SkinSoftening, 0.0, 1.0),
            Denoise = Math.Clamp(Denoise + adjustments.Denoise, 0.0, 1.0),
            Sharpen = Math.Clamp(Sharpen + adjustments.Sharpen, 0.0, 1.0),
            DetailRecovery = Math.Clamp(DetailRecovery + adjustments.DetailRecovery, 0.0, 1.0),
            Deblur = Math.Clamp(Deblur + adjustments.Deblur, 0.0, 1.0),
            ArtifactReduction = Math.Clamp(ArtifactReduction + adjustments.ArtifactReduction, 0.0, 1.0),
            RealismBoost = Math.Clamp(RealismBoost + adjustments.RealismBoost, 0.0, 1.0)
        };
    }
}
