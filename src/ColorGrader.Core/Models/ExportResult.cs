namespace ColorGrader.Core.Models;

public sealed record ExportResult(
    Guid AssetId,
    ExportJobStatus Status,
    string? OutputPath,
    string Message)
{
    public bool Succeeded => Status == ExportJobStatus.Completed;
}
