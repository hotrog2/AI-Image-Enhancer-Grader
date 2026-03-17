namespace ColorGrader.Core.Models;

public sealed record ExportHistoryEntry(
    long Id,
    Guid AssetId,
    string FileName,
    string? OutputPath,
    ExportJobStatus Status,
    string Message,
    DateTimeOffset CreatedAt);
