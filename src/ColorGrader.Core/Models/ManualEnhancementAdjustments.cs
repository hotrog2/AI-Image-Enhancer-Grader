namespace ColorGrader.Core.Models;

public sealed record ManualEnhancementAdjustments(
    double Exposure,
    double Contrast,
    double Warmth,
    double Saturation,
    double Vibrance,
    double HighlightRecovery,
    double ShadowLift,
    double SkinSoftening,
    double Denoise,
    double Sharpen,
    double DetailRecovery,
    double Deblur,
    double ArtifactReduction,
    double RealismBoost)
{
    public static ManualEnhancementAdjustments None { get; } = new(
        Exposure: 0,
        Contrast: 0,
        Warmth: 0,
        Saturation: 0,
        Vibrance: 0,
        HighlightRecovery: 0,
        ShadowLift: 0,
        SkinSoftening: 0,
        Denoise: 0,
        Sharpen: 0,
        DetailRecovery: 0,
        Deblur: 0,
        ArtifactReduction: 0,
        RealismBoost: 0);

    public bool HasAdjustments =>
        Exposure != 0 ||
        Contrast != 0 ||
        Warmth != 0 ||
        Saturation != 0 ||
        Vibrance != 0 ||
        HighlightRecovery != 0 ||
        ShadowLift != 0 ||
        SkinSoftening != 0 ||
        Denoise != 0 ||
        Sharpen != 0 ||
        DetailRecovery != 0 ||
        Deblur != 0 ||
        ArtifactReduction != 0 ||
        RealismBoost != 0;
}
