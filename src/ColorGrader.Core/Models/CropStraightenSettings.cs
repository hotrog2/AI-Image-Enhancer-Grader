namespace ColorGrader.Core.Models;

public sealed record CropStraightenSettings(
    double RotationDegrees,
    double CropLeft,
    double CropTop,
    double CropWidth,
    double CropHeight)
{
    public static CropStraightenSettings Default { get; } = new(0, 0, 0, 1, 1);

    public bool IsIdentity =>
        Math.Abs(RotationDegrees) < 0.001 &&
        Math.Abs(CropLeft) < 0.001 &&
        Math.Abs(CropTop) < 0.001 &&
        Math.Abs(CropWidth - 1.0) < 0.001 &&
        Math.Abs(CropHeight - 1.0) < 0.001;
}
