namespace ColorGrader.Core.Models;

public sealed record ImageAnalysis(
    double AverageLuminance,
    double AverageSaturation,
    double WarmthBias);
