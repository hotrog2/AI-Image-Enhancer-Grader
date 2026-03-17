using ColorGrader.Application.Interfaces;
using ColorGrader.Core.Models;

namespace ColorGrader.Infrastructure.Services;

public sealed class ThumbnailCacheService(
    AppDataPaths paths,
    ICatalogService catalogService,
    IImageProcessingService imageProcessingService) : IThumbnailCacheService
{
    public async Task<string?> GetThumbnailPathAsync(CatalogAsset asset, int maxDimension, CancellationToken cancellationToken)
    {
        var cached = await catalogService.GetThumbnailCacheEntryAsync(asset.Id, cancellationToken);
        if (cached is not null &&
            File.Exists(cached.ThumbnailPath) &&
            cached.SourceLastModifiedAt >= asset.LastModifiedAt)
        {
            return cached.ThumbnailPath;
        }

        var thumbnail = await imageProcessingService.LoadThumbnailAsync(asset.FilePath, maxDimension, cancellationToken);
        if (thumbnail is null)
        {
            return cached is not null && File.Exists(cached.ThumbnailPath)
                ? cached.ThumbnailPath
                : null;
        }

        var thumbnailPath = Path.Combine(paths.ThumbnailFolder, $"{asset.Id}.png");
        await File.WriteAllBytesAsync(thumbnailPath, thumbnail.PngBytes, cancellationToken);

        await catalogService.SaveThumbnailCacheEntryAsync(
            new ThumbnailCacheEntry(
                asset.Id,
                thumbnailPath,
                asset.LastModifiedAt,
                thumbnail.PixelWidth,
                thumbnail.PixelHeight,
                DateTimeOffset.UtcNow),
            cancellationToken);

        return thumbnailPath;
    }
}
