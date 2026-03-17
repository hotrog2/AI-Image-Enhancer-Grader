namespace ColorGrader.Core.Models;

public sealed record SavedPreset(
    long Id,
    string Name,
    EnhancementFeature FeatureMask,
    double Strength,
    ManualEnhancementAdjustments ManualAdjustments,
    CropStraightenSettings CropStraighten,
    LocalizedMaskSettings LocalizedMask,
    DateTimeOffset CreatedAt);
