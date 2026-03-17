namespace ColorGrader.Core.Models;

public sealed record CatalogAsset(
    Guid Id,
    Guid FolderId,
    string FilePath,
    string FileName,
    string Extension,
    AssetKind Kind,
    DateTimeOffset ImportedAt,
    DateTimeOffset LastModifiedAt,
    long FileSizeBytes,
    bool CanPreview,
    int? Width,
    int? Height);
