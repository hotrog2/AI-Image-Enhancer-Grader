namespace ColorGrader.Imaging.Services;

public sealed record SubjectMaskInferenceOptions(
    string ModelPath,
    int InputSize = 512);
