using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace ColorGrader.Imaging.Services;

public sealed class CompositeRawDecoder(IEnumerable<IRawDecoder> decoders) : IRawDecoder
{
    private readonly IReadOnlyList<IRawDecoder> _decoders = decoders.ToList();

    public async Task<Image<Rgba32>?> DecodeAsync(string filePath, CancellationToken cancellationToken)
    {
        foreach (var decoder in _decoders)
        {
            var image = await decoder.DecodeAsync(filePath, cancellationToken);
            if (image is not null)
            {
                return image;
            }
        }

        return null;
    }
}
