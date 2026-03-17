namespace ColorGrader.Core.Models;

public sealed record ImagePreview(
    byte[] PngBytes,
    int PixelWidth,
    int PixelHeight);
