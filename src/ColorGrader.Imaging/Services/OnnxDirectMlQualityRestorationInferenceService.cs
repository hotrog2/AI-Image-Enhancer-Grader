using System.IO;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace ColorGrader.Imaging.Services;

public sealed class OnnxDirectMlQualityRestorationInferenceService : IQualityRestorationInferenceService, IDisposable
{
    private readonly QualityRestorationInferenceOptions _options;
    private readonly object _gate = new();
    private InferenceSession? _session;
    private string _statusSummary;
    private bool _initialized;
    private bool _disposed;

    public OnnxDirectMlQualityRestorationInferenceService(QualityRestorationInferenceOptions options)
    {
        _options = options;
        _statusSummary = File.Exists(_options.ModelPath)
            ? $"AI quality restoration model detected at {_options.ModelPath}. Session will initialize on first use."
            : $"AI quality restoration model unavailable. Place a model at {_options.ModelPath}. Deterministic restoration fallback remains available.";
    }

    public string StatusSummary => _statusSummary;

    public Task<Image<Rgba32>?> RestoreAsync(Image<Rgba32> image, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var session = EnsureSession();
        if (session is null)
        {
            return Task.FromResult<Image<Rgba32>?>(null);
        }

        var inputMetadata = session.InputMetadata.First().Value;
        var inputDimensions = inputMetadata.Dimensions.ToArray();
        var inputChannels = inputDimensions.Length >= 2 && inputDimensions[1] > 0
            ? inputDimensions[1]
            : 3;
        var (inputWidth, inputHeight) = CalculateWorkingSize(image.Width, image.Height, _options.MaxInputLongEdge, inputDimensions);
        using var resized = image.Clone(context => context.Resize(inputWidth, inputHeight));
        var tensor = new DenseTensor<float>(new[] { 1, inputChannels, inputHeight, inputWidth });

        resized.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (var x = 0; x < accessor.Width; x++)
                {
                    var pixel = row[x];
                    if (inputChannels == 1)
                    {
                        tensor[0, 0, y, x] = ToLuma(pixel);
                    }
                    else
                    {
                        tensor[0, 0, y, x] = pixel.R / 255f;
                        tensor[0, 1, y, x] = pixel.G / 255f;
                        tensor[0, 2, y, x] = pixel.B / 255f;
                    }
                }
            }
        });

        var inputName = session.InputMetadata.Keys.First();
        using var results = session.Run(
            [
                NamedOnnxValue.CreateFromTensor(inputName, tensor)
            ]);

        var output = results.First().AsTensor<float>();
        var restored = inputChannels == 1
            ? ToColorImageFromLuma(output, resized)
            : ToImage(output);
        if (restored.Width != image.Width || restored.Height != image.Height)
        {
            restored.Mutate(context => context.Resize(image.Width, image.Height));
        }

        return Task.FromResult<Image<Rgba32>?>(restored);
    }

    private InferenceSession? EnsureSession()
    {
        lock (_gate)
        {
            if (_initialized)
            {
                return _session;
            }

            _initialized = true;
            if (!File.Exists(_options.ModelPath))
            {
                _statusSummary = $"AI quality restoration model unavailable. Place a model at {_options.ModelPath}. Deterministic restoration fallback remains available.";
                return null;
            }

            try
            {
                var dmlOptions = new SessionOptions();
                dmlOptions.AppendExecutionProvider_DML(0);
                dmlOptions.GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_EXTENDED;
                _session = new InferenceSession(_options.ModelPath, dmlOptions);
                _statusSummary = $"AI quality restoration active via ONNX Runtime + DirectML using {_options.ModelPath}.";
                return _session;
            }
            catch (Exception directMlException)
            {
                try
                {
                    var cpuOptions = new SessionOptions
                    {
                        GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_EXTENDED
                    };

                    _session = new InferenceSession(_options.ModelPath, cpuOptions);
                    _statusSummary = $"AI quality restoration model loaded on CPU fallback because DirectML initialization failed: {directMlException.Message}";
                    return _session;
                }
                catch (Exception cpuException)
                {
                    _statusSummary = $"AI quality restoration model could not be loaded. DirectML error: {directMlException.Message}. CPU fallback error: {cpuException.Message}. Deterministic restoration fallback remains available.";
                    return null;
                }
            }
        }
    }

    private static (int Width, int Height) CalculateWorkingSize(int sourceWidth, int sourceHeight, int maxLongEdge, int[]? metadataDimensions = null)
    {
        if (sourceWidth <= 0 || sourceHeight <= 0)
        {
            return (256, 256);
        }

        if (metadataDimensions is { Length: >= 4 } && metadataDimensions[2] > 0 && metadataDimensions[3] > 0)
        {
            return (metadataDimensions[3], metadataDimensions[2]);
        }

        var scale = Math.Min(1.0, maxLongEdge / (double)Math.Max(sourceWidth, sourceHeight));
        var width = Math.Max(64, (int)Math.Round(sourceWidth * scale));
        var height = Math.Max(64, (int)Math.Round(sourceHeight * scale));

        width = Math.Max(64, width - (width % 8));
        height = Math.Max(64, height - (height % 8));
        return (width, height);
    }

    private static Image<Rgba32> ToColorImageFromLuma(Tensor<float> tensor, Image<Rgba32> referenceColorImage)
    {
        var dimensions = tensor.Dimensions.ToArray();
        var (height, width) = dimensions.Length switch
        {
            4 => (dimensions[2], dimensions[3]),
            3 => (dimensions[1], dimensions[2]),
            _ => (referenceColorImage.Height, referenceColorImage.Width)
        };

        using var colorReference = referenceColorImage.Clone(context => context.Resize(width, height));
        var image = new Image<Rgba32>(width, height);

        image.ProcessPixelRows(colorReference, (targetAccessor, referenceAccessor) =>
        {
            for (var y = 0; y < targetAccessor.Height; y++)
            {
                var targetRow = targetAccessor.GetRowSpan(y);
                var referenceRow = referenceAccessor.GetRowSpan(y);
                for (var x = 0; x < targetRow.Length; x++)
                {
                    var targetLuma = ReadChannel(tensor, dimensions.Length, 0, y, x);
                    var referencePixel = referenceRow[x];
                    var referenceLuma = Math.Max(0.001f, ToLuma(referencePixel));
                    var ratio = Math.Clamp(targetLuma < 0f ? (targetLuma + 1f) * 0.5f : targetLuma, 0f, 1f) / referenceLuma;

                    targetRow[x] = new Rgba32(
                        ToByte((referencePixel.R / 255f) * ratio),
                        ToByte((referencePixel.G / 255f) * ratio),
                        ToByte((referencePixel.B / 255f) * ratio));
                }
            }
        });

        return image;
    }

    private static Image<Rgba32> ToImage(Tensor<float> tensor)
    {
        var dimensions = tensor.Dimensions.ToArray();
        var (channels, height, width) = dimensions.Length switch
        {
            4 => (dimensions[1], dimensions[2], dimensions[3]),
            3 => (dimensions[0], dimensions[1], dimensions[2]),
            _ => (3, (int)Math.Sqrt(tensor.Length / 3.0), (int)Math.Sqrt(tensor.Length / 3.0))
        };

        var image = new Image<Rgba32>(width, height);
        image.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (var x = 0; x < accessor.Width; x++)
                {
                    var r = channels > 0 ? ReadChannel(tensor, dimensions.Length, 0, y, x) : 0f;
                    var g = channels > 1 ? ReadChannel(tensor, dimensions.Length, 1, y, x) : r;
                    var b = channels > 2 ? ReadChannel(tensor, dimensions.Length, 2, y, x) : g;
                    row[x] = new Rgba32(ToByte(r), ToByte(g), ToByte(b));
                }
            }
        });

        return image;
    }

    private static float ReadChannel(Tensor<float> tensor, int rank, int channel, int y, int x) =>
        rank == 4 ? tensor[0, channel, y, x] : tensor[channel, y, x];

    private static float ToLuma(Rgba32 pixel) =>
        ((0.299f * pixel.R) + (0.587f * pixel.G) + (0.114f * pixel.B)) / 255f;

    private static byte ToByte(float value)
    {
        var normalized = value < 0f ? (value + 1f) * 0.5f : value;
        return (byte)Math.Clamp((int)Math.Round(Math.Clamp(normalized, 0f, 1f) * 255f), 0, 255);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _session?.Dispose();
    }
}
