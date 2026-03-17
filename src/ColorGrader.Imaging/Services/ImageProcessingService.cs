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
    private readonly IQualityRestorationInferenceService _qualityRestorationInferenceService;

    public ImageProcessingService(
        IRawDecoder rawDecoder,
        ISubjectMaskInferenceService subjectMaskInferenceService,
        IQualityRestorationInferenceService qualityRestorationInferenceService)
    {
        _rawDecoder = rawDecoder;
        _subjectMaskInferenceService = subjectMaskInferenceService;
        _qualityRestorationInferenceService = qualityRestorationInferenceService;
    }

    public string InferenceStatusSummary =>
        $"{_subjectMaskInferenceService.StatusSummary}{Environment.NewLine}{_qualityRestorationInferenceService.StatusSummary}";

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
            await ApplyQualityRestorationAsync(image, settings, cancellationToken);
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
            await ApplyQualityRestorationAsync(image, settings, cancellationToken);
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

    private async Task ApplyQualityRestorationAsync(Image<Rgba32> image, EnhancementSettings settings, CancellationToken cancellationToken)
    {
        if (!HasQualityRestoration(settings))
        {
            return;
        }

        var aiBlend = (float)Math.Clamp(
            ((settings.DetailRecovery * 0.35) +
             (settings.Deblur * 0.35) +
             (settings.ArtifactReduction * 0.20) +
             (settings.RealismBoost * 0.10)),
            0.0,
            0.7);

        if (aiBlend > 0.05)
        {
            using var aiRestored = await _qualityRestorationInferenceService.RestoreAsync(image, cancellationToken);
            if (aiRestored is not null)
            {
                BlendImages(image, aiRestored, aiBlend);
            }
        }

        ApplyArtifactReduction(image, settings.ArtifactReduction);
        ApplyDetailRecovery(image, settings.DetailRecovery, settings.Deblur);
        ApplyRealismBoost(image, settings.RealismBoost);
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
        UpscaleFactor: 1.0,
        DetailRecovery: Math.Max(0.0, adjustments.DetailRecovery),
        Deblur: Math.Max(0.0, adjustments.Deblur),
        ArtifactReduction: Math.Max(0.0, adjustments.ArtifactReduction),
        RealismBoost: Math.Max(0.0, adjustments.RealismBoost));

    private static bool HasQualityRestoration(EnhancementSettings settings) =>
        settings.DetailRecovery > 0.001 ||
        settings.Deblur > 0.001 ||
        settings.ArtifactReduction > 0.001 ||
        settings.RealismBoost > 0.001;

    private static void ApplyArtifactReduction(Image<Rgba32> image, double amount)
    {
        if (amount <= 0.01)
        {
            return;
        }

        using var softened = image.Clone(context => context.GaussianBlur((float)Math.Clamp(0.45 + (amount * 1.2), 0.4, 1.8)));
        var blendWeight = (float)Math.Clamp(amount * 0.32, 0.04, 0.30);
        BlendImages(image, softened, blendWeight);
    }

    private static void ApplyDetailRecovery(Image<Rgba32> image, double detailRecovery, double deblur)
    {
        var total = Math.Clamp((detailRecovery * 0.9) + (deblur * 1.1), 0.0, 1.0);
        if (total <= 0.01)
        {
            return;
        }

        using var blurred = image.Clone(context => context.GaussianBlur((float)Math.Clamp(0.7 + ((1.0 - total) * 0.4), 0.5, 1.2)));
        var gain = (float)Math.Clamp(0.55 + (total * 0.95), 0.55, 1.45);

        image.ProcessPixelRows(blurred, (baseAccessor, blurredAccessor) =>
        {
            for (var y = 0; y < baseAccessor.Height; y++)
            {
                var baseRow = baseAccessor.GetRowSpan(y);
                var blurredRow = blurredAccessor.GetRowSpan(y);
                for (var x = 0; x < baseRow.Length; x++)
                {
                    ref var destination = ref baseRow[x];
                    ref var softened = ref blurredRow[x];

                    destination.R = ClampToByte(destination.R + ((destination.R - softened.R) * gain));
                    destination.G = ClampToByte(destination.G + ((destination.G - softened.G) * gain));
                    destination.B = ClampToByte(destination.B + ((destination.B - softened.B) * gain));
                }
            }
        });
    }

    private static void ApplyRealismBoost(Image<Rgba32> image, double amount)
    {
        if (amount <= 0.01)
        {
            return;
        }

        var contrast = 1.0 + (amount * 0.12);
        var saturation = 1.0 + (amount * 0.06);
        var gamma = 1.0 - (amount * 0.05);

        image.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (var x = 0; x < row.Length; x++)
                {
                    ref var pixel = ref row[x];
                    var r = pixel.R / 255.0;
                    var g = pixel.G / 255.0;
                    var b = pixel.B / 255.0;

                    r = Math.Pow(Math.Clamp(((r - 0.5) * contrast) + 0.5, 0.0, 1.0), gamma);
                    g = Math.Pow(Math.Clamp(((g - 0.5) * contrast) + 0.5, 0.0, 1.0), gamma);
                    b = Math.Pow(Math.Clamp(((b - 0.5) * contrast) + 0.5, 0.0, 1.0), gamma);

                    var luma = (0.2126 * r) + (0.7152 * g) + (0.0722 * b);
                    r = Math.Clamp(luma + ((r - luma) * saturation), 0.0, 1.0);
                    g = Math.Clamp(luma + ((g - luma) * saturation), 0.0, 1.0);
                    b = Math.Clamp(luma + ((b - luma) * saturation), 0.0, 1.0);

                    pixel.R = (byte)Math.Round(r * 255.0);
                    pixel.G = (byte)Math.Round(g * 255.0);
                    pixel.B = (byte)Math.Round(b * 255.0);
                }
            }
        });
    }

    private static void BlendImages(Image<Rgba32> baseImage, Image<Rgba32> blendImage, float weight)
    {
        if (weight <= 0.001f)
        {
            return;
        }

        baseImage.ProcessPixelRows(blendImage, (baseAccessor, blendAccessor) =>
        {
            for (var y = 0; y < baseAccessor.Height; y++)
            {
                var baseRow = baseAccessor.GetRowSpan(y);
                var blendRow = blendAccessor.GetRowSpan(y);
                for (var x = 0; x < baseRow.Length; x++)
                {
                    ref var destination = ref baseRow[x];
                    ref var source = ref blendRow[x];
                    destination.R = Lerp(destination.R, source.R, weight);
                    destination.G = Lerp(destination.G, source.G, weight);
                    destination.B = Lerp(destination.B, source.B, weight);
                }
            }
        });
    }

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
