namespace ColorGrader.Core.Models;

public sealed record CatalogFolder(
    Guid Id,
    string FolderPath,
    DateTimeOffset ImportedAt);
