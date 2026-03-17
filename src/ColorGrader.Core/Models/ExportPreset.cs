namespace ColorGrader.Core.Models;

public sealed record ExportPreset(
    string OutputDirectory,
    ExportFileFormat Format,
    int JpegQuality,
    int LongEdgePixels);
