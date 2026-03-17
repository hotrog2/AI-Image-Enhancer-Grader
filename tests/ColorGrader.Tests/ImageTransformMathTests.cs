using ColorGrader.Core.Models;
using ColorGrader.Imaging.Services;

namespace ColorGrader.Tests;

public sealed class ImageTransformMathTests
{
    [Fact]
    public void CalculateCropRectangle_RespectsZoomAndOffsets()
    {
        var rectangle = ImageTransformMath.CalculateCropRectangle(
            1000,
            800,
            new CropStraightenSettings(0, 0.25, 1.0, -1.0));

        Assert.Equal(750, rectangle.Width);
        Assert.Equal(600, rectangle.Height);
        Assert.Equal(250, rectangle.X);
        Assert.Equal(0, rectangle.Y);
    }
}
