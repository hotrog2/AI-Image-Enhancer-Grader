using ColorGrader.Core.Models;
using SixLabors.ImageSharp;

namespace ColorGrader.Imaging.Services;

public static class ImageTransformMath
{
    public static Rectangle CalculateCropRectangle(int width, int height, CropStraightenSettings settings)
    {
        var zoom = Math.Clamp(settings.Zoom, 0.0, 0.75);
        var scale = Math.Clamp(1.0 - zoom, 0.25, 1.0);

        var cropWidth = Math.Max(1, (int)Math.Round(width * scale));
        var cropHeight = Math.Max(1, (int)Math.Round(height * scale));
        var maxOffsetX = (width - cropWidth) / 2.0;
        var maxOffsetY = (height - cropHeight) / 2.0;

        var centerX = (width / 2.0) + (Math.Clamp(settings.OffsetX, -1.0, 1.0) * maxOffsetX);
        var centerY = (height / 2.0) + (Math.Clamp(settings.OffsetY, -1.0, 1.0) * maxOffsetY);
        var left = (int)Math.Round(centerX - (cropWidth / 2.0));
        var top = (int)Math.Round(centerY - (cropHeight / 2.0));

        left = Math.Clamp(left, 0, Math.Max(0, width - cropWidth));
        top = Math.Clamp(top, 0, Math.Max(0, height - cropHeight));

        return new Rectangle(left, top, cropWidth, cropHeight);
    }
}
