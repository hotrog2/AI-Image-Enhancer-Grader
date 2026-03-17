namespace ColorGrader.Imaging.Services;

public sealed record QualityRestorationInferenceOptions(
    string ModelPath,
    int MaxInputLongEdge = 768);
