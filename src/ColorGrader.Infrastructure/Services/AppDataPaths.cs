namespace ColorGrader.Infrastructure.Services;

public sealed class AppDataPaths
{
    public AppDataPaths()
    {
        AppFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ColorGrader");

        Directory.CreateDirectory(AppFolder);
        DatabasePath = Path.Combine(AppFolder, "catalog.db");
        ThumbnailFolder = Path.Combine(AppFolder, "thumbs");
        ModelsFolder = Path.Combine(AppFolder, "models");
        Directory.CreateDirectory(ThumbnailFolder);
        Directory.CreateDirectory(ModelsFolder);
    }

    public string AppFolder { get; }

    public string DatabasePath { get; }

    public string ThumbnailFolder { get; }

    public string ModelsFolder { get; }
}
