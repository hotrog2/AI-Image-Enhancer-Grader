using ColorGrader.Core.Models;

namespace ColorGrader.Core;

public static class SupportedImageFormats
{
    public static readonly IReadOnlySet<string> JpegExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg",
        ".jpeg"
    };

    public static readonly IReadOnlySet<string> PngExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".png"
    };

    public static readonly IReadOnlySet<string> RawExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".cr2",
        ".cr3",
        ".nef",
        ".arw",
        ".dng",
        ".rw2",
        ".orf"
    };

    public static bool IsSupported(string extension) =>
        JpegExtensions.Contains(extension) ||
        PngExtensions.Contains(extension) ||
        RawExtensions.Contains(extension);

    public static AssetKind GetAssetKind(string extension)
    {
        if (JpegExtensions.Contains(extension))
        {
            return AssetKind.Jpeg;
        }

        if (PngExtensions.Contains(extension))
        {
            return AssetKind.Png;
        }

        return AssetKind.Raw;
    }
}
