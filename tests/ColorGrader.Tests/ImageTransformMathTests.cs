using ColorGrader.Core.Models;
using ColorGrader.Imaging.Services;

namespace ColorGrader.Tests;

public sealed class ImageTransformMathTests
{
    [Fact]
    public void CalculateCropRectangle_UsesNormalizedRectangle()
    {
        var rectangle = ImageTransformMath.CalculateCropRectangle(
            1000,
            800,
            new CropStraightenSettings(0, 0.25, 0.10, 0.50, 0.60));

        Assert.Equal(250, rectangle.X);
        Assert.Equal(80, rectangle.Y);
        Assert.Equal(500, rectangle.Width);
        Assert.Equal(480, rectangle.Height);
    }

    [Fact]
    public void CalculateCropRectangle_ClampsRectangleWithinImageBounds()
    {
        var rectangle = ImageTransformMath.CalculateCropRectangle(
            1000,
            800,
            new CropStraightenSettings(0, 0.90, 0.90, 0.30, 0.30));

        Assert.Equal(700, rectangle.X);
        Assert.Equal(560, rectangle.Y);
        Assert.Equal(300, rectangle.Width);
        Assert.Equal(240, rectangle.Height);
    }
}
