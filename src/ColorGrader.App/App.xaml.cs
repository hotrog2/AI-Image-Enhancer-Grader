using System.IO;
using System.Windows;
using ColorGrader.AI.Services;
using ColorGrader.App.Services;
using ColorGrader.App.ViewModels;
using ColorGrader.Application.Interfaces;
using ColorGrader.Application.Services;
using ColorGrader.Imaging.Services;
using ColorGrader.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ColorGrader.App;

public partial class App : System.Windows.Application
{
    private readonly IHost _host;

    public App()
    {
        _host = Host
            .CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton<AppDataPaths>();
                services.AddSingleton<ICatalogService, SqliteCatalogService>();
                services.AddSingleton<IThumbnailCacheService, ThumbnailCacheService>();
                services.AddSingleton<WicRawDecoder>();
                services.AddSingleton<LibRawRawDecoder>();
                services.AddSingleton(serviceProvider =>
                {
                    var paths = serviceProvider.GetRequiredService<AppDataPaths>();
                    return new SubjectMaskInferenceOptions(Path.Combine(paths.ModelsFolder, "subject-mask.onnx"));
                });
                services.AddSingleton<ISubjectMaskInferenceService, OnnxDirectMlSubjectMaskInferenceService>();
                services.AddSingleton<IRawDecoder>(serviceProvider => new CompositeRawDecoder(
                    [
                        serviceProvider.GetRequiredService<WicRawDecoder>(),
                        serviceProvider.GetRequiredService<LibRawRawDecoder>()
                    ]));
                services.AddSingleton<IImageProcessingService, ImageProcessingService>();
                services.AddSingleton<IStyleLearningService, PreferenceStyleLearningService>();
                services.AddSingleton<IFolderPickerService, FolderPickerService>();
                services.AddSingleton<EditorWorkflowService>();
                services.AddSingleton<ShellViewModel>();
                services.AddSingleton<MainWindow>();
            })
            .Build();
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        await _host.StartAsync();

        var mainWindow = _host.Services.GetRequiredService<MainWindow>();
        mainWindow.Show();

        base.OnStartup(e);
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        await _host.StopAsync(TimeSpan.FromSeconds(2));
        _host.Dispose();
        base.OnExit(e);
    }
}
