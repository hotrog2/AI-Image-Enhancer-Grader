using ColorGrader.Core.Models;

namespace ColorGrader.Application.Interfaces;

public interface IImageProcessingService
{
    string InferenceStatusSummary { get; }
    bool CanRender(string filePath);
    Task<ImageAnalysis?> AnalyzeAsync(string filePath, CancellationToken cancellationToken);
    Task<ImagePreview?> LoadPreviewAsync(string filePath, CropStraightenSettings cropStraighten, CancellationToken cancellationToken);
    Task<ImagePreview?> LoadThumbnailAsync(string filePath, int maxDimension, CancellationToken cancellationToken);
    Task<ImagePreview?> RenderPreviewAsync(
        string filePath,
        EnhancementSuggestion suggestion,
        CropStraightenSettings cropStraighten,
        LocalizedMaskSettings localizedMask,
        CancellationToken cancellationToken);
    Task<ExportResult> ExportAsync(ExportRequest request, CancellationToken cancellationToken);
}
