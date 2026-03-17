using System.Windows.Media.Imaging;
using System.IO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace ColorGrader.Imaging.Services;

public sealed class WicRawDecoder : IRawDecoder
{
    public Task<Image<Rgba32>?> DecodeAsync(string filePath, CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            try
            {
                using var stream = File.OpenRead(filePath);
                var decoder = BitmapDecoder.Create(
                    stream,
                    BitmapCreateOptions.PreservePixelFormat,
                    BitmapCacheOption.OnLoad);

                var source = decoder.Preview
                    ?? decoder.Thumbnail
                    ?? decoder.Frames.FirstOrDefault(frame => frame.Thumbnail is not null)?.Thumbnail
                    ?? decoder.Frames.FirstOrDefault();

                return source is null ? null : ImagingConversion.LoadIntoImageSharp(source);
            }
            catch
            {
                return null;
            }
        }, cancellationToken);
    }
}
