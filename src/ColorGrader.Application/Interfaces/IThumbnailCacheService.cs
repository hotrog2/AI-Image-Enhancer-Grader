using ColorGrader.Core.Models;

namespace ColorGrader.Application.Interfaces;

public interface IThumbnailCacheService
{
    Task<string?> GetThumbnailPathAsync(CatalogAsset asset, int maxDimension, CancellationToken cancellationToken);
}
