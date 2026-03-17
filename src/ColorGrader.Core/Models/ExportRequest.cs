namespace ColorGrader.Core.Models;

public sealed record ExportRequest(
    CatalogAsset Asset,
    EnhancementSuggestion Suggestion,
    ExportPreset Preset,
    CropStraightenSettings CropStraighten,
    LocalizedMaskSettings LocalizedMask);
