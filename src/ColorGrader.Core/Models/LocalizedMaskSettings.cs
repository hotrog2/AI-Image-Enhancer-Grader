namespace ColorGrader.Core.Models;

public sealed record LocalizedMaskSettings(
    bool IsEnabled,
    LocalizedMaskKind Kind,
    double CenterX,
    double CenterY,
    double Width,
    double Height,
    double Feather,
    double AngleDegrees,
    bool Invert,
    double Intensity,
    ManualEnhancementAdjustments Adjustments)
{
    public static LocalizedMaskSettings Default { get; } = new(
        IsEnabled: false,
        Kind: LocalizedMaskKind.Radial,
        CenterX: 0.5,
        CenterY: 0.5,
        Width: 0.55,
        Height: 0.55,
        Feather: 0.25,
        AngleDegrees: 0,
        Invert: false,
        Intensity: 0.85,
        Adjustments: ManualEnhancementAdjustments.None);

    public bool HasVisibleEffect =>
        IsEnabled &&
        Kind != LocalizedMaskKind.None &&
        Intensity > 0.001 &&
        Adjustments.HasAdjustments;
}
