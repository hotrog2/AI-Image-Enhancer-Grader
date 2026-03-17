using ColorGrader.Application.Interfaces;
using ColorGrader.Core;
using ColorGrader.Core.Models;
using System.IO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace ColorGrader.Imaging.Services;

public sealed class ImageProcessingService : IImageProcessingService
{
    private const int PreviewMaxDimension = 1400;
    private readonly IRawDecoder _rawDecoder;
    private readonly ISubjectMaskInferenceService _subjectMaskInferenceService;

    public ImageProcessingService(IRawDecoder rawDecoder, ISubjectMaskInferenceService subjectMaskInferenceService)
    {
        _rawDecoder = rawDecoder;
        _subjectMaskInferenceService = subjectMaskInferenceService;
    }

    public string InferenceStatusSummary => _subjectMaskInferenceService.StatusSummary;

    public bool CanRender(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return false;
        }

        var extension = Path.GetExtension(filePath);
        return SupportedImageFormats.IsSupported(extension) || ResolveProxySourcePath(filePath) is not null;
    }

    public async Task<ImageAnalysis?> AnalyzeAsync(string filePath, CancellationToken cancellationToken)
    {
        var image = await LoadWorkingImageAsync(filePath, cancellationToken);
        if (image is null)
        {
            return null;
        }

        using (image)
        {
            return Analyze(image);
        }
    }

    public async Task<ImagePreview?> LoadPreviewAsync(string filePath, CropStraightenSettings cropStraighten, CancellationToken cancellationToken)
    {
        var image = await LoadWorkingImageAsync(filePath, cancellationToken);
        if (image is null)
        {
            return null;
        }

        using (image)
        {
            ApplyCropAndStraighten(image, cropStraighten);
            ResizeForPreview(image);
            return await EncodePreviewAsync(image, cancellationToken);
        }
    }

    public async Task<ImagePreview?> LoadThumbnailAsync(string filePath, int maxDimension, CancellationToken cancellationToken)
    {
        var image = await LoadWorkingImageAsync(filePath, cancellationToken);
        if (image is null)
        {
            return null;
        }

        using (image)
        {
            ResizeToLongEdge(image, maxDimension);
            return await EncodePreviewAsync(image, cancellationToken);
        }
    }

    public async Task<ImagePreview?> RenderPreviewAsync(
        string filePath,
        EnhancementSuggestion suggestion,
        CropStraightenSettings cropStraighten,
        LocalizedMaskSettings localizedMask,
        CancellationToken cancellationToken)
    {
        var image = await LoadWorkingImageAsync(filePath, cancellationToken);
        if (image is null)
        {
            return null;
        }

        using (image)
        {
            ApplyCropAndStraighten(image, cropStraighten);
            ResizeForPreview(image);

            var settings = suggestion.Settings.ApplyFeatureMask(suggestion.EnabledFeatures);
            ApplyAdjustments(image, settings);
            await ApplyLocalizedMaskAsync(image, localizedMask, cancellationToken);

            return await EncodePreviewAsync(image, cancellationToken);
        }
    }

    public async Task<ExportResult> ExportAsync(ExportRequest request, CancellationToken cancellationToken)
    {
        var image = await LoadWorkingImageAsync(request.Asset.FilePath, cancellationToken);
        if (image is null)
        {
            return new ExportResult(request.Asset.Id, ExportJobStatus.Skipped, null, "No RAW preview codec or matching JPG/PNG proxy was available for export.");
        }

        using (image)
        {
            Directory.CreateDirectory(request.Preset.OutputDirectory);

            ApplyCropAndStraighten(image, request.CropStraighten);

            var settings = request.Suggestion.Settings.ApplyFeatureMask(request.Suggestion.EnabledFeatures);
            ApplyAdjustments(image, settings);
            await ApplyLocalizedMaskAsync(image, request.LocalizedMask, cancellationToken);
            ResizeForExport(image, request.Preset.LongEdgePixels);

            var outputPath = CreateUniqueOutputPath(request.Asset, request.Preset);
            await using var outputStream = File.Create(outputPath);

            if (request.Preset.Format == ExportFileFormat.Jpeg)
            {
                var encoder = new JpegEncoder
                {
                    Quality = Math.Clamp(request.Preset.JpegQuality, 50, 100)
                };

                await image.SaveAsJpegAsync(outputStream, encoder, cancellationToken);
            }
            else
            {
                await image.SaveAsPngAsync(outputStream, new PngEncoder(), cancellationToken);
            }

            return new ExportResult(request.Asset.Id, ExportJobStatus.Completed, outputPath, "Export complete.");
        }
    }

    private static void ResizeForPreview(Image<Rgba32> image)
    {
        ResizeToLongEdge(image, PreviewMaxDimension);
    }

    private static void ResizeToLongEdge(Image<Rgba32> image, int maxDimensionTarget)
    {
        image.Mutate(context =>
        {
            var maxDimension = Math.Max(image.Width, image.Height);
            if (maxDimension > maxDimensionTarget)
            {
                var scale = maxDimensionTarget / (double)maxDimension;
                context.Resize(
                    Math.Max(1, (int)Math.Round(image.Width * scale)),
                    Math.Max(1, (int)Math.Round(image.Height * scale)));
            }
        });
    }

    private static void ResizeForExport(Image<Rgba32> image, int longEdgePixels)
    {
        if (longEdgePixels <= 0)
        {
            return;
        }

        var maxDimension = Math.Max(image.Width, image.Height);
        if (maxDimension <= longEdgePixels)
        {
            return;
        }

        image.Mutate(context =>
        {
            var scale = longEdgePixels / (double)maxDimension;
            context.Resize(
                Math.Max(1, (int)Math.Round(image.Width * scale)),
                Math.Max(1, (int)Math.Round(image.Height * scale)));
        });
    }

    private static void ApplyAdjustments(Image<Rgba32> image, EnhancementSettings settings)
    {
        image.Mutate(context =>
        {
            var brightness = (float)Math.Clamp(1.0 + (settings.Exposure * 0.35) + (settings.ShadowLift * 0.15), 0.6, 1.6);
            var contrast = (float)Math.Clamp(1.0 + settings.Contrast + (settings.HighlightRecovery * 0.1), 0.7, 1.6);
            var saturation = (float)Math.Clamp(1.0 + settings.Saturation + (settings.Vibrance * 0.8), 0.7, 1.8);

            context.Brightness(brightness);
            context.Contrast(contrast);
            context.Saturate(saturation);

            if (settings.Denoise > 0.01)
            {
                context.GaussianBlur((float)Math.Clamp(settings.Denoise * 0.8, 0.1, 1.5));
            }

            if (settings.SkinSoftening > 0.01)
            {
                context.GaussianBlur((float)Math.Clamp(settings.SkinSoftening * 0.3, 0.05, 0.7));
            }

            if (settings.Sharpen > 0.01)
            {
                context.GaussianSharpen((float)Math.Clamp(settings.Sharpen * 1.8, 0.1, 2.0));
            }

            if (settings.UpscaleFactor > 1.0)
            {
                context.Resize(
                    Math.Max(1, (int)Math.Round(image.Width * settings.UpscaleFactor)),
                    Math.Max(1, (int)Math.Round(image.Height * settings.UpscaleFactor)));
            }
        });

        if (Math.Abs(settings.Warmth) > 0.001)
        {
            ApplyWarmth(image, settings.Warmth);
        }
    }

    private static void ApplyCropAndStraighten(Image<Rgba32> image, CropStraightenSettings cropStraighten)
    {
        image.Mutate(context => context.AutoOrient());

        if (Math.Abs(cropStraighten.RotationDegrees) > 0.001)
        {
            image.Mutate(context => context.Rotate((float)Math.Clamp(cropStraighten.RotationDegrees, -20.0, 20.0)));
        }

        if (cropStraighten.IsIdentity)
        {
            return;
        }

        var cropRectangle = ImageTransformMath.CalculateCropRectangle(image.Width, image.Height, cropStraighten);
        image.Mutate(context => context.Crop(cropRectangle));
    }

    private async Task ApplyLocalizedMaskAsync(Image<Rgba32> image, LocalizedMaskSettings localizedMask, CancellationToken cancellationToken)
    {
        if (!localizedMask.HasVisibleEffect)
        {
            return;
        }

        SubjectMaskPrediction? subjectMask = null;
        if (localizedMask.Kind == LocalizedMaskKind.Subject)
        {
            subjectMask = await _subjectMaskInferenceService.PredictMaskAsync(image, cancellationToken);
            if (subjectMask is null)
            {
                return;
            }
        }

        using var localizedImage = image.Clone();
        ApplyAdjustments(localizedImage, ToLocalizedSettings(localizedMask.Adjustments));
        BlendLocalizedMask(image, localizedImage, localizedMask, subjectMask);
    }

    private static void BlendLocalizedMask(
        Image<Rgba32> baseImage,
        Image<Rgba32> adjustedImage,
        LocalizedMaskSettings localizedMask,
        SubjectMaskPrediction? subjectMask)
    {
        baseImage.ProcessPixelRows(adjustedImage, (baseAccessor, adjustedAccessor) =>
        {
            for (var y = 0; y < baseAccessor.Height; y++)
            {
                var baseRow = baseAccessor.GetRowSpan(y);
                var adjustedRow = adjustedAccessor.GetRowSpan(y);

                for (var x = 0; x < baseRow.Length; x++)
                {
                    var weight = LocalizedMaskMath.GetMaskWeight(x, y, baseAccessor.Width, baseAccessor.Height, localizedMask, subjectMask);
                    if (weight <= 0.001f)
                    {
                        continue;
                    }

                    ref var destination = ref baseRow[x];
                    ref var source = ref adjustedRow[x];

                    destination.R = Lerp(destination.R, source.R, weight);
                    destination.G = Lerp(destination.G, source.G, weight);
                    destination.B = Lerp(destination.B, source.B, weight);
                }
            }
        });
    }

    private static EnhancementSettings ToLocalizedSettings(ManualEnhancementAdjustments adjustments) => new(
        Exposure: adjustments.Exposure,
        Contrast: adjustments.Contrast,
        Vibrance: adjustments.Vibrance,
        Warmth: adjustments.Warmth,
        Saturation: adjustments.Saturation,
        HighlightRecovery: Math.Max(0.0, adjustments.HighlightRecovery),
        ShadowLift: adjustments.ShadowLift,
        SkinSoftening: Math.Max(0.0, adjustments.SkinSoftening),
        Denoise: Math.Max(0.0, adjustments.Denoise),
        Sharpen: Math.Max(0.0, adjustments.Sharpen),
        UpscaleFactor: 1.0);

    private static void ApplyWarmth(Image<Rgba32> image, double warmth)
    {
        var redLift = (float)Math.Clamp(warmth * 18.0, -12.0, 12.0);
        var blueLift = (float)Math.Clamp(warmth * -14.0, -10.0, 10.0);

        image.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (var x = 0; x < row.Length; x++)
                {
                    ref var pixel = ref row[x];
                    pixel.R = ClampToByte(pixel.R + redLift);
                    pixel.B = ClampToByte(pixel.B + blueLift);
                }
            }
        });
    }

    private static ImageAnalysis Analyze(Image<Rgba32> image)
    {
        var sampleStep = Math.Max(1, Math.Min(image.Width, image.Height) / 150);
        double luminanceSum = 0;
        double saturationSum = 0;
        double warmthSum = 0;
        var sampleCount = 0;

        image.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < accessor.Height; y += sampleStep)
            {
                var row = accessor.GetRowSpan(y);
                for (var x = 0; x < row.Length; x += sampleStep)
                {
                    var pixel = row[x];
                    var r = pixel.R / 255.0;
                    var g = pixel.G / 255.0;
                    var b = pixel.B / 255.0;

                    var max = Math.Max(r, Math.Max(g, b));
                    var min = Math.Min(r, Math.Min(g, b));

                    luminanceSum += (0.2126 * r) + (0.7152 * g) + (0.0722 * b);
                    saturationSum += max <= 0 ? 0 : (max - min) / max;
                    warmthSum += r - b;
                    sampleCount++;
                }
            }
        });

        if (sampleCount == 0)
        {
            return new ImageAnalysis(0.5, 0.2, 0);
        }

        return new ImageAnalysis(
            luminanceSum / sampleCount,
            saturationSum / sampleCount,
            warmthSum / sampleCount);
    }

    private static async Task<ImagePreview> EncodePreviewAsync(Image<Rgba32> image, CancellationToken cancellationToken)
    {
        await using var stream = new MemoryStream();
        await image.SaveAsPngAsync(stream, new PngEncoder(), cancellationToken);
        return new ImagePreview(stream.ToArray(), image.Width, image.Height);
    }

    private static byte ClampToByte(float value) => (byte)Math.Clamp((int)Math.Round(value), 0, 255);

    private static byte Lerp(byte from, byte to, float weight) =>
        (byte)Math.Clamp((int)Math.Round(from + ((to - from) * weight)), 0, 255);

    private async Task<Image<Rgba32>?> LoadWorkingImageAsync(string filePath, CancellationToken cancellationToken)
    {
        if (!File.Exists(filePath))
        {
            return null;
        }

        var extension = Path.GetExtension(filePath);
        if (SupportedImageFormats.JpegExtensions.Contains(extension) || SupportedImageFormats.PngExtensions.Contains(extension))
        {
            return await Image.LoadAsync<Rgba32>(filePath, cancellationToken);
        }

        if (SupportedImageFormats.RawExtensions.Contains(extension))
        {
            var rawImage = await _rawDecoder.DecodeAsync(filePath, cancellationToken);
            if (rawImage is not null)
            {
                return rawImage;
            }
        }

        var fallbackPath = ResolveProxySourcePath(filePath);
        return fallbackPath is null ? null : await Image.LoadAsync<Rgba32>(fallbackPath, cancellationToken);
    }

    private static string? ResolveProxySourcePath(string filePath)
    {
        var directory = Path.GetDirectoryName(filePath);
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(filePath);
        if (string.IsNullOrWhiteSpace(directory) || string.IsNullOrWhiteSpace(fileNameWithoutExtension))
        {
            return null;
        }

        foreach (var fallbackExtension in new[] { ".jpg", ".jpeg", ".png" })
        {
            var fallbackPath = Path.Combine(directory, fileNameWithoutExtension + fallbackExtension);
            if (File.Exists(fallbackPath))
            {
                return fallbackPath;
            }
        }

        return null;
    }

    private static string CreateUniqueOutputPath(CatalogAsset asset, ExportPreset preset)
    {
        var extension = preset.Format == ExportFileFormat.Jpeg ? ".jpg" : ".png";
        var baseFileName = $"{Path.GetFileNameWithoutExtension(asset.FileName)}_graded";
        var candidatePath = Path.Combine(preset.OutputDirectory, baseFileName + extension);
        var suffix = 1;

        while (File.Exists(candidatePath))
        {
            candidatePath = Path.Combine(preset.OutputDirectory, $"{baseFileName}_{suffix}{extension}");
            suffix++;
        }

        return candidatePath;
    }
}
