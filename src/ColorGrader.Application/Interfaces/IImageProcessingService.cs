using ColorGrader.Core.Models;

namespace ColorGrader.Application.Interfaces;

public interface IImageProcessingService
{
    bool CanRender(string filePath);
    Task<ImageAnalysis?> AnalyzeAsync(string filePath, CancellationToken cancellationToken);
    Task<ImagePreview?> LoadPreviewAsync(string filePath, CancellationToken cancellationToken);
    Task<ImagePreview?> LoadThumbnailAsync(string filePath, int maxDimension, CancellationToken cancellationToken);
    Task<ImagePreview?> RenderPreviewAsync(string filePath, EnhancementSuggestion suggestion, CancellationToken cancellationToken);
    Task<ExportResult> ExportAsync(ExportRequest request, CancellationToken cancellationToken);
}
