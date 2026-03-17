using ColorGrader.Core.Models;
using SixLabors.ImageSharp;

namespace ColorGrader.Imaging.Services;

public static class ImageTransformMath
{
    public static Rectangle CalculateCropRectangle(int width, int height, CropStraightenSettings settings)
    {
        var minWidth = 1.0 / Math.Max(1, width);
        var minHeight = 1.0 / Math.Max(1, height);

        var normalizedWidth = Math.Clamp(settings.CropWidth, minWidth, 1.0);
        var normalizedHeight = Math.Clamp(settings.CropHeight, minHeight, 1.0);
        var normalizedLeft = Math.Clamp(settings.CropLeft, 0.0, 1.0 - normalizedWidth);
        var normalizedTop = Math.Clamp(settings.CropTop, 0.0, 1.0 - normalizedHeight);

        var left = Math.Clamp((int)Math.Floor(normalizedLeft * width), 0, Math.Max(0, width - 1));
        var top = Math.Clamp((int)Math.Floor(normalizedTop * height), 0, Math.Max(0, height - 1));
        var right = Math.Clamp((int)Math.Ceiling((normalizedLeft + normalizedWidth) * width), left + 1, width);
        var bottom = Math.Clamp((int)Math.Ceiling((normalizedTop + normalizedHeight) * height), top + 1, height);

        return new Rectangle(left, top, right - left, bottom - top);
    }
}
