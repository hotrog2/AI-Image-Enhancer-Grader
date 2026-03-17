using System.Windows.Media.Imaging;
using System.IO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace ColorGrader.Imaging.Services;

internal static class ImagingConversion
{
    public static Image<Rgba32> LoadIntoImageSharp(BitmapSource source)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(source));

        using var memoryStream = new MemoryStream();
        encoder.Save(memoryStream);
        memoryStream.Position = 0;

        return Image.Load<Rgba32>(memoryStream);
    }
}
