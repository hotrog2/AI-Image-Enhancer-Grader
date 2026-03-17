namespace ColorGrader.Imaging.Services;

public sealed record SubjectMaskPrediction(
    int Width,
    int Height,
    float[] Values);
