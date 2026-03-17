using ColorGrader.Core.Models;

namespace ColorGrader.Application.Models;

public sealed record CatalogAssetListItem(
    CatalogAsset Asset,
    string? ThumbnailPath)
{
    public string FileName => Asset.FileName;
    public string FilePath => Asset.FilePath;
    public AssetKind Kind => Asset.Kind;
}
