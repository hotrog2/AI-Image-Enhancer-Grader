using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace ColorGrader.Imaging.Services;

public interface IQualityRestorationInferenceService
{
    string StatusSummary { get; }

    Task<Image<Rgba32>?> RestoreAsync(Image<Rgba32> image, CancellationToken cancellationToken);
}
