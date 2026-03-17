using ColorGrader.Core.Models;

namespace ColorGrader.Application.Models;

public sealed record EditorDocument(
    CatalogAsset Asset,
    ImagePreview? OriginalPreview,
    ImagePreview? EnhancedPreview,
    EnhancementSuggestion Suggestion,
    string Notice);
