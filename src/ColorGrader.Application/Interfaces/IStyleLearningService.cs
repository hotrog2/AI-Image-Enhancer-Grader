using ColorGrader.Core.Models;

namespace ColorGrader.Application.Interfaces;

public interface IStyleLearningService
{
    Task<EnhancementSuggestion> BuildSuggestionAsync(
        CatalogAsset asset,
        EnhancementFeature enabledFeatures,
        ImageAnalysis? analysis,
        long? styleProfileId,
        CancellationToken cancellationToken);
}
