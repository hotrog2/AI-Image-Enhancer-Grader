using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace ColorGrader.Imaging.Services;

public interface ISubjectMaskInferenceService
{
    string StatusSummary { get; }
    Task<SubjectMaskPrediction?> PredictMaskAsync(Image<Rgba32> image, CancellationToken cancellationToken);
}
