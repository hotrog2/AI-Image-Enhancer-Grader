namespace ColorGrader.Core.Models;

public sealed record ThumbnailCacheEntry(
    Guid AssetId,
    string ThumbnailPath,
    DateTimeOffset SourceLastModifiedAt,
    int PixelWidth,
    int PixelHeight,
    DateTimeOffset GeneratedAt);
