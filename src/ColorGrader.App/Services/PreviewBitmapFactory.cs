using System.IO;
using System.Windows.Media.Imaging;
using ColorGrader.Core.Models;

namespace ColorGrader.App.Services;

public static class PreviewBitmapFactory
{
    public static BitmapSource? Create(ImagePreview? preview)
    {
        if (preview is null || preview.PngBytes.Length == 0)
        {
            return null;
        }

        using var stream = new MemoryStream(preview.PngBytes);
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = stream;
        image.EndInit();
        image.Freeze();
        return image;
    }
}
