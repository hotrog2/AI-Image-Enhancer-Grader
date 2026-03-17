using System.IO;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace ColorGrader.Imaging.Services;

public sealed class OnnxDirectMlSubjectMaskInferenceService : ISubjectMaskInferenceService, IDisposable
{
    private readonly SubjectMaskInferenceOptions _options;
    private readonly object _gate = new();
    private InferenceSession? _session;
    private string _statusSummary;
    private bool _initialized;
    private bool _disposed;

    public OnnxDirectMlSubjectMaskInferenceService(SubjectMaskInferenceOptions options)
    {
        _options = options;
        _statusSummary = File.Exists(_options.ModelPath)
            ? $"AI subject mask model detected at {_options.ModelPath}. Session will initialize on first use."
            : $"AI subject mask unavailable. Place a model at {_options.ModelPath}.";
    }

    public string StatusSummary => _statusSummary;

    public Task<SubjectMaskPrediction?> PredictMaskAsync(Image<Rgba32> image, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var session = EnsureSession();
        if (session is null)
        {
            return Task.FromResult<SubjectMaskPrediction?>(null);
        }

        using var resized = image.Clone(context => context.Resize(_options.InputSize, _options.InputSize));
        var tensor = new DenseTensor<float>(new[] { 1, 3, _options.InputSize, _options.InputSize });

        resized.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (var x = 0; x < accessor.Width; x++)
                {
                    var pixel = row[x];
                    tensor[0, 0, y, x] = pixel.R / 255f;
                    tensor[0, 1, y, x] = pixel.G / 255f;
                    tensor[0, 2, y, x] = pixel.B / 255f;
                }
            }
        });

        var inputName = session.InputMetadata.Keys.First();
        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor(inputName, tensor)
        };

        using var results = session.Run(inputs);
        var output = results.First().AsTensor<float>();
        var prediction = ToPrediction(output);
        return Task.FromResult<SubjectMaskPrediction?>(prediction);
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
                _statusSummary = $"AI subject mask unavailable. Place a model at {_options.ModelPath}.";
                return null;
            }

            try
            {
                var dmlOptions = new SessionOptions();
                dmlOptions.AppendExecutionProvider_DML(0);
                dmlOptions.GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_EXTENDED;
                _session = new InferenceSession(_options.ModelPath, dmlOptions);
                _statusSummary = $"AI subject mask active via ONNX Runtime + DirectML using {_options.ModelPath}.";
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
                    _statusSummary = $"AI subject mask model loaded on CPU fallback because DirectML initialization failed: {directMlException.Message}";
                    return _session;
                }
                catch (Exception cpuException)
                {
                    _statusSummary = $"AI subject mask model could not be loaded. DirectML error: {directMlException.Message}. CPU fallback error: {cpuException.Message}";
                    return null;
                }
            }
        }
    }

    private static SubjectMaskPrediction ToPrediction(Tensor<float> tensor)
    {
        var dimensions = tensor.Dimensions.ToArray();

        if (dimensions.Length == 4)
        {
            var channels = dimensions[1];
            var height = dimensions[2];
            var width = dimensions[3];
            var values = new float[width * height];

            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var value = channels <= 1
                        ? tensor[0, 0, y, x]
                        : MaxForegroundChannel(tensor, channels, y, x);
                    values[(y * width) + x] = Sigmoid(value);
                }
            }

            return new SubjectMaskPrediction(width, height, values);
        }

        if (dimensions.Length == 3)
        {
            var height = dimensions[1];
            var width = dimensions[2];
            var values = new float[width * height];
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    values[(y * width) + x] = Sigmoid(tensor[0, y, x]);
                }
            }

            return new SubjectMaskPrediction(width, height, values);
        }

        var fallbackValues = tensor.ToArray().Select(Sigmoid).ToArray();
        var side = (int)Math.Sqrt(fallbackValues.Length);
        return new SubjectMaskPrediction(side, side, fallbackValues);
    }

    private static float MaxForegroundChannel(Tensor<float> tensor, int channels, int y, int x)
    {
        var best = tensor[0, 0, y, x];
        for (var channel = 1; channel < channels; channel++)
        {
            best = Math.Max(best, tensor[0, channel, y, x]);
        }

        return best;
    }

    private static float Sigmoid(float value)
    {
        var clamped = Math.Clamp(value, -12f, 12f);
        return 1f / (1f + MathF.Exp(-clamped));
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
