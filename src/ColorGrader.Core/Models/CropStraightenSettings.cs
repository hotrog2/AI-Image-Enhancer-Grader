namespace ColorGrader.Core.Models;

public sealed record CropStraightenSettings(
    double RotationDegrees,
    double Zoom,
    double OffsetX,
    double OffsetY)
{
    public static CropStraightenSettings Default { get; } = new(0, 0, 0, 0);

    public bool IsIdentity =>
        Math.Abs(RotationDegrees) < 0.001 &&
        Zoom <= 0.0001 &&
        Math.Abs(OffsetX) < 0.001 &&
        Math.Abs(OffsetY) < 0.001;
}
