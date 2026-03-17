using System.Windows.Media.Imaging;
using System.IO;
using HurlbertVisionLab.LibRawWrapper;
using HurlbertVisionLab.LibRawWrapper.Native;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace ColorGrader.Imaging.Services;

public sealed class LibRawRawDecoder : IRawDecoder
{
    public Task<Image<Rgba32>?> DecodeAsync(string filePath, CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            try
            {
                using var processor = new LibRawProcessor();
                processor.Open(Path.GetFullPath(filePath));

                var output = processor.OutputParameters;
                output.UseCameraWhiteBalance = true;
                output.UseAutomaticWhiteBalance = false;
                output.UseCameraMatrix = UseCameraMatrix.Always;
                output.NoAutoBrightness = true;
                output.Brightness = 1.0f;
                output.OutputBitsPerPixel = 16;
                output.UserQuality = Interpolation.DCB;
                output.DcbEnhance = true;
                output.DcbIterations = 2;
                output.HighlightMode = HighlightMode.Rebuild;
                output.HighlightRebuildFactor = 6;
                output.SetGammaTosRGB();

                var bitmap = processor.GetProcessedBitmap();
                return bitmap is null ? null : ImagingConversion.LoadIntoImageSharp(bitmap);
            }
            catch
            {
                return null;
            }
        }, cancellationToken);
    }
}
