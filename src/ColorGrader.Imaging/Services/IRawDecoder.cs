using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace ColorGrader.Imaging.Services;

public interface IRawDecoder
{
    Task<Image<Rgba32>?> DecodeAsync(string filePath, CancellationToken cancellationToken);
}
