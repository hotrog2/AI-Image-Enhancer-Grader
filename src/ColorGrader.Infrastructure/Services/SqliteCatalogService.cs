using ColorGrader.Application.Interfaces;
using ColorGrader.Core;
using ColorGrader.Core.Models;
using Microsoft.Data.Sqlite;

namespace ColorGrader.Infrastructure.Services;

public sealed class SqliteCatalogService : ICatalogService
{
    private readonly AppDataPaths _paths;

    public SqliteCatalogService(AppDataPaths paths)
    {
        _paths = paths;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            PRAGMA journal_mode = WAL;
            CREATE TABLE IF NOT EXISTS Folders (
                Id TEXT NOT NULL PRIMARY KEY,
                FolderPath TEXT NOT NULL UNIQUE,
                ImportedAt TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS Assets (
                Id TEXT NOT NULL PRIMARY KEY,
                FolderId TEXT NOT NULL,
                FilePath TEXT NOT NULL UNIQUE,
                FileName TEXT NOT NULL,
                Extension TEXT NOT NULL,
                Kind INTEGER NOT NULL,
                ImportedAt TEXT NOT NULL,
                LastModifiedAt TEXT NOT NULL,
                FileSizeBytes INTEGER NOT NULL,
                CanPreview INTEGER NOT NULL,
                Width INTEGER NULL,
                Height INTEGER NULL
            );

            CREATE TABLE IF NOT EXISTS Feedback (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                AssetId TEXT NOT NULL,
                Outcome INTEGER NOT NULL,
                Exposure REAL NOT NULL,
                Contrast REAL NOT NULL,
                Vibrance REAL NOT NULL,
                Warmth REAL NOT NULL,
                Saturation REAL NOT NULL,
                HighlightRecovery REAL NOT NULL,
                ShadowLift REAL NOT NULL,
                SkinSoftening REAL NOT NULL,
                Denoise REAL NOT NULL,
                Sharpen REAL NOT NULL,
                UpscaleFactor REAL NOT NULL,
                DetailRecovery REAL NOT NULL DEFAULT 0,
                Deblur REAL NOT NULL DEFAULT 0,
                ArtifactReduction REAL NOT NULL DEFAULT 0,
                RealismBoost REAL NOT NULL DEFAULT 0,
                CreatedAt TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS ExportHistory (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                AssetId TEXT NOT NULL,
                FileName TEXT NOT NULL,
                OutputPath TEXT NULL,
                Status INTEGER NOT NULL,
                Message TEXT NOT NULL,
                CreatedAt TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS ThumbnailCache (
                AssetId TEXT NOT NULL PRIMARY KEY,
                ThumbnailPath TEXT NOT NULL,
                SourceLastModifiedAt TEXT NOT NULL,
                PixelWidth INTEGER NOT NULL,
                PixelHeight INTEGER NOT NULL,
                GeneratedAt TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS Presets (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL UNIQUE,
                FeatureMask INTEGER NOT NULL,
                Strength REAL NOT NULL,
                ManualExposure REAL NOT NULL,
                ManualContrast REAL NOT NULL,
                ManualWarmth REAL NOT NULL,
                ManualSaturation REAL NOT NULL,
                ManualVibrance REAL NOT NULL,
                ManualHighlightRecovery REAL NOT NULL,
                ManualShadowLift REAL NOT NULL,
                ManualSkinSoftening REAL NOT NULL,
                ManualDenoise REAL NOT NULL,
                ManualSharpen REAL NOT NULL,
                ManualDetailRecovery REAL NOT NULL DEFAULT 0,
                ManualDeblur REAL NOT NULL DEFAULT 0,
                ManualArtifactReduction REAL NOT NULL DEFAULT 0,
                ManualRealismBoost REAL NOT NULL DEFAULT 0,
                CropRotationDegrees REAL NOT NULL DEFAULT 0,
                CropZoom REAL NOT NULL DEFAULT 0,
                CropOffsetX REAL NOT NULL DEFAULT 0,
                CropOffsetY REAL NOT NULL DEFAULT 0,
                CropLeft REAL NOT NULL DEFAULT 0,
                CropTop REAL NOT NULL DEFAULT 0,
                CropWidth REAL NOT NULL DEFAULT 1,
                CropHeight REAL NOT NULL DEFAULT 1,
                LocalizedMaskEnabled INTEGER NOT NULL DEFAULT 0,
                LocalizedMaskKind INTEGER NOT NULL DEFAULT 1,
                LocalizedMaskCenterX REAL NOT NULL DEFAULT 0.5,
                LocalizedMaskCenterY REAL NOT NULL DEFAULT 0.5,
                LocalizedMaskWidth REAL NOT NULL DEFAULT 0.55,
                LocalizedMaskHeight REAL NOT NULL DEFAULT 0.55,
                LocalizedMaskFeather REAL NOT NULL DEFAULT 0.25,
                LocalizedMaskAngleDegrees REAL NOT NULL DEFAULT 0,
                LocalizedMaskInvert INTEGER NOT NULL DEFAULT 0,
                LocalizedMaskIntensity REAL NOT NULL DEFAULT 0.85,
                LocalizedExposure REAL NOT NULL DEFAULT 0,
                LocalizedContrast REAL NOT NULL DEFAULT 0,
                LocalizedWarmth REAL NOT NULL DEFAULT 0,
                LocalizedSaturation REAL NOT NULL DEFAULT 0,
                LocalizedVibrance REAL NOT NULL DEFAULT 0,
                LocalizedSkinSoftening REAL NOT NULL DEFAULT 0,
                LocalizedDenoise REAL NOT NULL DEFAULT 0,
                LocalizedSharpen REAL NOT NULL DEFAULT 0,
                LocalizedDetailRecovery REAL NOT NULL DEFAULT 0,
                LocalizedDeblur REAL NOT NULL DEFAULT 0,
                LocalizedArtifactReduction REAL NOT NULL DEFAULT 0,
                LocalizedRealismBoost REAL NOT NULL DEFAULT 0,
                CreatedAt TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS StyleProfiles (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL UNIQUE,
                CreatedAt TEXT NOT NULL
            );
            """;

        await command.ExecuteNonQueryAsync(cancellationToken);
        await EnsureFeedbackColumnsAsync(connection, cancellationToken);
        await EnsurePresetColumnsAsync(connection, cancellationToken);
    }

    public async Task<IReadOnlyList<CatalogFolder>> GetFoldersAsync(CancellationToken cancellationToken)
    {
        var folders = new List<CatalogFolder>();

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, FolderPath, ImportedAt FROM Folders ORDER BY ImportedAt DESC;";

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            folders.Add(new CatalogFolder(
                Guid.Parse(reader.GetString(0)),
                reader.GetString(1),
                DateTimeOffset.Parse(reader.GetString(2))));
        }

        return folders;
    }

    public async Task<IReadOnlyList<CatalogAsset>> GetAssetsAsync(CancellationToken cancellationToken)
    {
        var assets = new List<CatalogAsset>();

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT Id, FolderId, FilePath, FileName, Extension, Kind, ImportedAt, LastModifiedAt, FileSizeBytes, CanPreview, Width, Height
            FROM Assets
            ORDER BY ImportedAt DESC, FileName ASC;
            """;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            assets.Add(new CatalogAsset(
                Guid.Parse(reader.GetString(0)),
                Guid.Parse(reader.GetString(1)),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                (AssetKind)reader.GetInt32(5),
                DateTimeOffset.Parse(reader.GetString(6)),
                DateTimeOffset.Parse(reader.GetString(7)),
                reader.GetInt64(8),
                reader.GetInt64(9) == 1,
                reader.IsDBNull(10) ? null : reader.GetInt32(10),
                reader.IsDBNull(11) ? null : reader.GetInt32(11)));
        }

        return assets;
    }

    public async Task DeleteAssetAsync(Guid assetId, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);

        string? thumbnailPath = null;
        string? folderId = null;

        await using (var selectAssetCommand = connection.CreateCommand())
        {
            selectAssetCommand.Transaction = transaction;
            selectAssetCommand.CommandText = "SELECT FolderId FROM Assets WHERE Id = $assetId;";
            selectAssetCommand.Parameters.AddWithValue("$assetId", assetId.ToString());
            folderId = await selectAssetCommand.ExecuteScalarAsync(cancellationToken) as string;
        }

        if (folderId is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return;
        }

        await using (var selectThumbnailCommand = connection.CreateCommand())
        {
            selectThumbnailCommand.Transaction = transaction;
            selectThumbnailCommand.CommandText = "SELECT ThumbnailPath FROM ThumbnailCache WHERE AssetId = $assetId;";
            selectThumbnailCommand.Parameters.AddWithValue("$assetId", assetId.ToString());
            thumbnailPath = await selectThumbnailCommand.ExecuteScalarAsync(cancellationToken) as string;
        }

        foreach (var statement in new[]
                 {
                     "DELETE FROM ThumbnailCache WHERE AssetId = $assetId;",
                     "DELETE FROM ExportHistory WHERE AssetId = $assetId;",
                     "DELETE FROM Feedback WHERE AssetId = $assetId;",
                     "DELETE FROM Assets WHERE Id = $assetId;"
                 })
        {
            await using var deleteCommand = connection.CreateCommand();
            deleteCommand.Transaction = transaction;
            deleteCommand.CommandText = statement;
            deleteCommand.Parameters.AddWithValue("$assetId", assetId.ToString());
            await deleteCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var countFolderAssetsCommand = connection.CreateCommand())
        {
            countFolderAssetsCommand.Transaction = transaction;
            countFolderAssetsCommand.CommandText = "SELECT COUNT(*) FROM Assets WHERE FolderId = $folderId;";
            countFolderAssetsCommand.Parameters.AddWithValue("$folderId", folderId);
            var remainingAssets = Convert.ToInt32(await countFolderAssetsCommand.ExecuteScalarAsync(cancellationToken));

            if (remainingAssets == 0)
            {
                await using var deleteFolderCommand = connection.CreateCommand();
                deleteFolderCommand.Transaction = transaction;
                deleteFolderCommand.CommandText = "DELETE FROM Folders WHERE Id = $folderId;";
                deleteFolderCommand.Parameters.AddWithValue("$folderId", folderId);
                await deleteFolderCommand.ExecuteNonQueryAsync(cancellationToken);
            }
        }

        await transaction.CommitAsync(cancellationToken);

        TryDeleteThumbnailFile(thumbnailPath);
    }

    public async Task<int> ImportFolderAsync(string folderPath, bool recursive, CancellationToken cancellationToken)
    {
        if (!Directory.Exists(folderPath))
        {
            return 0;
        }

        var folderId = await UpsertFolderAsync(folderPath, cancellationToken);
        var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        var imported = 0;

        foreach (var filePath in Directory.EnumerateFiles(folderPath, "*.*", searchOption))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var extension = Path.GetExtension(filePath);
            if (!SupportedImageFormats.IsSupported(extension))
            {
                continue;
            }

            var file = new FileInfo(filePath);
            var canPreview =
                SupportedImageFormats.JpegExtensions.Contains(extension) ||
                SupportedImageFormats.PngExtensions.Contains(extension) ||
                HasRenderProxy(file.FullName);

            await using var connection = await OpenConnectionAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                INSERT INTO Assets (Id, FolderId, FilePath, FileName, Extension, Kind, ImportedAt, LastModifiedAt, FileSizeBytes, CanPreview, Width, Height)
                VALUES ($id, $folderId, $filePath, $fileName, $extension, $kind, $importedAt, $lastModifiedAt, $fileSizeBytes, $canPreview, NULL, NULL)
                ON CONFLICT(FilePath) DO UPDATE SET
                    FolderId = excluded.FolderId,
                    FileName = excluded.FileName,
                    Extension = excluded.Extension,
                    Kind = excluded.Kind,
                    ImportedAt = excluded.ImportedAt,
                    LastModifiedAt = excluded.LastModifiedAt,
                    FileSizeBytes = excluded.FileSizeBytes,
                    CanPreview = excluded.CanPreview;
                """;

            command.Parameters.AddWithValue("$id", Guid.NewGuid().ToString());
            command.Parameters.AddWithValue("$folderId", folderId.ToString());
            command.Parameters.AddWithValue("$filePath", file.FullName);
            command.Parameters.AddWithValue("$fileName", file.Name);
            command.Parameters.AddWithValue("$extension", extension);
            command.Parameters.AddWithValue("$kind", (int)SupportedImageFormats.GetAssetKind(extension));
            command.Parameters.AddWithValue("$importedAt", DateTimeOffset.UtcNow.ToString("O"));
            command.Parameters.AddWithValue("$lastModifiedAt", file.LastWriteTimeUtc.ToString("O"));
            command.Parameters.AddWithValue("$fileSizeBytes", file.Length);
            command.Parameters.AddWithValue("$canPreview", canPreview ? 1 : 0);

            imported += await command.ExecuteNonQueryAsync(cancellationToken) > 0 ? 1 : 0;
        }

        return imported;
    }

    public async Task SaveFeedbackAsync(EnhancementFeedback feedback, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO Feedback (
                AssetId, Outcome, Exposure, Contrast, Vibrance, Warmth, Saturation,
                HighlightRecovery, ShadowLift, SkinSoftening, Denoise, Sharpen, UpscaleFactor,
                DetailRecovery, Deblur, ArtifactReduction, RealismBoost, CreatedAt, StyleProfileId)
            VALUES (
                $assetId, $outcome, $exposure, $contrast, $vibrance, $warmth, $saturation,
                $highlightRecovery, $shadowLift, $skinSoftening, $denoise, $sharpen, $upscaleFactor,
                $detailRecovery, $deblur, $artifactReduction, $realismBoost, $createdAt, $styleProfileId);
            """;

        command.Parameters.AddWithValue("$assetId", feedback.AssetId.ToString());
        command.Parameters.AddWithValue("$outcome", (int)feedback.Outcome);
        command.Parameters.AddWithValue("$exposure", feedback.Settings.Exposure);
        command.Parameters.AddWithValue("$contrast", feedback.Settings.Contrast);
        command.Parameters.AddWithValue("$vibrance", feedback.Settings.Vibrance);
        command.Parameters.AddWithValue("$warmth", feedback.Settings.Warmth);
        command.Parameters.AddWithValue("$saturation", feedback.Settings.Saturation);
        command.Parameters.AddWithValue("$highlightRecovery", feedback.Settings.HighlightRecovery);
        command.Parameters.AddWithValue("$shadowLift", feedback.Settings.ShadowLift);
        command.Parameters.AddWithValue("$skinSoftening", feedback.Settings.SkinSoftening);
        command.Parameters.AddWithValue("$denoise", feedback.Settings.Denoise);
        command.Parameters.AddWithValue("$sharpen", feedback.Settings.Sharpen);
        command.Parameters.AddWithValue("$upscaleFactor", feedback.Settings.UpscaleFactor);
        command.Parameters.AddWithValue("$detailRecovery", feedback.Settings.DetailRecovery);
        command.Parameters.AddWithValue("$deblur", feedback.Settings.Deblur);
        command.Parameters.AddWithValue("$artifactReduction", feedback.Settings.ArtifactReduction);
        command.Parameters.AddWithValue("$realismBoost", feedback.Settings.RealismBoost);
        command.Parameters.AddWithValue("$createdAt", feedback.CreatedAt.ToString("O"));
        command.Parameters.AddWithValue("$styleProfileId", (object?)feedback.StyleProfileId ?? DBNull.Value);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<EnhancementFeedback>> GetFeedbackAsync(CancellationToken cancellationToken)
    {
        var feedback = new List<EnhancementFeedback>();

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT AssetId, Outcome, Exposure, Contrast, Vibrance, Warmth, Saturation,
                   HighlightRecovery, ShadowLift, SkinSoftening, Denoise, Sharpen, UpscaleFactor,
                   DetailRecovery, Deblur, ArtifactReduction, RealismBoost, CreatedAt, StyleProfileId
            FROM Feedback
            ORDER BY CreatedAt DESC;
            """;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            feedback.Add(new EnhancementFeedback(
                Guid.Parse(reader.GetString(0)),
                (FeedbackDisposition)reader.GetInt32(1),
                new EnhancementSettings(
                    reader.GetDouble(2),
                    reader.GetDouble(3),
                    reader.GetDouble(4),
                    reader.GetDouble(5),
                    reader.GetDouble(6),
                    reader.GetDouble(7),
                    reader.GetDouble(8),
                    reader.GetDouble(9),
                    reader.GetDouble(10),
                    reader.GetDouble(11),
                    reader.GetDouble(12),
                    reader.GetDouble(13),
                    reader.GetDouble(14),
                    reader.GetDouble(15),
                    reader.GetDouble(16)),
                DateTimeOffset.Parse(reader.GetString(17)),
                reader.IsDBNull(18) ? null : reader.GetInt64(18)));
        }

        return feedback;
    }

    public async Task SaveExportHistoryAsync(ExportHistoryEntry entry, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO ExportHistory (AssetId, FileName, OutputPath, Status, Message, CreatedAt)
            VALUES ($assetId, $fileName, $outputPath, $status, $message, $createdAt);
            """;

        command.Parameters.AddWithValue("$assetId", entry.AssetId.ToString());
        command.Parameters.AddWithValue("$fileName", entry.FileName);
        command.Parameters.AddWithValue("$outputPath", (object?)entry.OutputPath ?? DBNull.Value);
        command.Parameters.AddWithValue("$status", (int)entry.Status);
        command.Parameters.AddWithValue("$message", entry.Message);
        command.Parameters.AddWithValue("$createdAt", entry.CreatedAt.ToString("O"));

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ExportHistoryEntry>> GetRecentExportHistoryAsync(int take, CancellationToken cancellationToken)
    {
        var entries = new List<ExportHistoryEntry>();

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT Id, AssetId, FileName, OutputPath, Status, Message, CreatedAt
            FROM ExportHistory
            ORDER BY CreatedAt DESC
            LIMIT $take;
            """;
        command.Parameters.AddWithValue("$take", take);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            entries.Add(new ExportHistoryEntry(
                reader.GetInt64(0),
                Guid.Parse(reader.GetString(1)),
                reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetString(3),
                (ExportJobStatus)reader.GetInt32(4),
                reader.GetString(5),
                DateTimeOffset.Parse(reader.GetString(6))));
        }

        return entries;
    }

    public async Task<ThumbnailCacheEntry?> GetThumbnailCacheEntryAsync(Guid assetId, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT AssetId, ThumbnailPath, SourceLastModifiedAt, PixelWidth, PixelHeight, GeneratedAt
            FROM ThumbnailCache
            WHERE AssetId = $assetId;
            """;
        command.Parameters.AddWithValue("$assetId", assetId.ToString());

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new ThumbnailCacheEntry(
            Guid.Parse(reader.GetString(0)),
            reader.GetString(1),
            DateTimeOffset.Parse(reader.GetString(2)),
            reader.GetInt32(3),
            reader.GetInt32(4),
            DateTimeOffset.Parse(reader.GetString(5)));
    }

    public async Task SaveThumbnailCacheEntryAsync(ThumbnailCacheEntry entry, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO ThumbnailCache (AssetId, ThumbnailPath, SourceLastModifiedAt, PixelWidth, PixelHeight, GeneratedAt)
            VALUES ($assetId, $thumbnailPath, $sourceLastModifiedAt, $pixelWidth, $pixelHeight, $generatedAt)
            ON CONFLICT(AssetId) DO UPDATE SET
                ThumbnailPath = excluded.ThumbnailPath,
                SourceLastModifiedAt = excluded.SourceLastModifiedAt,
                PixelWidth = excluded.PixelWidth,
                PixelHeight = excluded.PixelHeight,
                GeneratedAt = excluded.GeneratedAt;
            """;

        command.Parameters.AddWithValue("$assetId", entry.AssetId.ToString());
        command.Parameters.AddWithValue("$thumbnailPath", entry.ThumbnailPath);
        command.Parameters.AddWithValue("$sourceLastModifiedAt", entry.SourceLastModifiedAt.ToString("O"));
        command.Parameters.AddWithValue("$pixelWidth", entry.PixelWidth);
        command.Parameters.AddWithValue("$pixelHeight", entry.PixelHeight);
        command.Parameters.AddWithValue("$generatedAt", entry.GeneratedAt.ToString("O"));

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task SavePresetAsync(SavedPreset preset, CancellationToken cancellationToken)
    {
        var legacyCrop = ToLegacyCropValues(preset.CropStraighten);

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO Presets (
                Name, FeatureMask, Strength, ManualExposure, ManualContrast, ManualWarmth, ManualSaturation,
                ManualVibrance, ManualHighlightRecovery, ManualShadowLift, ManualSkinSoftening, ManualDenoise,
                ManualSharpen, ManualDetailRecovery, ManualDeblur, ManualArtifactReduction, ManualRealismBoost,
                CropRotationDegrees, CropZoom, CropOffsetX, CropOffsetY, CropLeft, CropTop, CropWidth, CropHeight,
                LocalizedMaskEnabled, LocalizedMaskKind, LocalizedMaskCenterX, LocalizedMaskCenterY,
                LocalizedMaskWidth, LocalizedMaskHeight, LocalizedMaskFeather, LocalizedMaskAngleDegrees,
                LocalizedMaskInvert, LocalizedMaskIntensity, LocalizedExposure, LocalizedContrast,
                LocalizedWarmth, LocalizedSaturation, LocalizedVibrance, LocalizedSkinSoftening,
                LocalizedDenoise, LocalizedSharpen, LocalizedDetailRecovery, LocalizedDeblur,
                LocalizedArtifactReduction, LocalizedRealismBoost, CreatedAt)
            VALUES (
                $name, $featureMask, $strength, $manualExposure, $manualContrast, $manualWarmth, $manualSaturation,
                $manualVibrance, $manualHighlightRecovery, $manualShadowLift, $manualSkinSoftening, $manualDenoise,
                $manualSharpen, $manualDetailRecovery, $manualDeblur, $manualArtifactReduction, $manualRealismBoost,
                $cropRotationDegrees, $cropZoom, $cropOffsetX, $cropOffsetY, $cropLeft, $cropTop, $cropWidth, $cropHeight,
                $localizedMaskEnabled, $localizedMaskKind, $localizedMaskCenterX, $localizedMaskCenterY,
                $localizedMaskWidth, $localizedMaskHeight, $localizedMaskFeather, $localizedMaskAngleDegrees,
                $localizedMaskInvert, $localizedMaskIntensity, $localizedExposure, $localizedContrast,
                $localizedWarmth, $localizedSaturation, $localizedVibrance, $localizedSkinSoftening,
                $localizedDenoise, $localizedSharpen, $localizedDetailRecovery, $localizedDeblur,
                $localizedArtifactReduction, $localizedRealismBoost, $createdAt)
            ON CONFLICT(Name) DO UPDATE SET
                FeatureMask = excluded.FeatureMask,
                Strength = excluded.Strength,
                ManualExposure = excluded.ManualExposure,
                ManualContrast = excluded.ManualContrast,
                ManualWarmth = excluded.ManualWarmth,
                ManualSaturation = excluded.ManualSaturation,
                ManualVibrance = excluded.ManualVibrance,
                ManualHighlightRecovery = excluded.ManualHighlightRecovery,
                ManualShadowLift = excluded.ManualShadowLift,
                ManualSkinSoftening = excluded.ManualSkinSoftening,
                ManualDenoise = excluded.ManualDenoise,
                ManualSharpen = excluded.ManualSharpen,
                ManualDetailRecovery = excluded.ManualDetailRecovery,
                ManualDeblur = excluded.ManualDeblur,
                ManualArtifactReduction = excluded.ManualArtifactReduction,
                ManualRealismBoost = excluded.ManualRealismBoost,
                CropRotationDegrees = excluded.CropRotationDegrees,
                CropZoom = excluded.CropZoom,
                CropOffsetX = excluded.CropOffsetX,
                CropOffsetY = excluded.CropOffsetY,
                CropLeft = excluded.CropLeft,
                CropTop = excluded.CropTop,
                CropWidth = excluded.CropWidth,
                CropHeight = excluded.CropHeight,
                LocalizedMaskEnabled = excluded.LocalizedMaskEnabled,
                LocalizedMaskKind = excluded.LocalizedMaskKind,
                LocalizedMaskCenterX = excluded.LocalizedMaskCenterX,
                LocalizedMaskCenterY = excluded.LocalizedMaskCenterY,
                LocalizedMaskWidth = excluded.LocalizedMaskWidth,
                LocalizedMaskHeight = excluded.LocalizedMaskHeight,
                LocalizedMaskFeather = excluded.LocalizedMaskFeather,
                LocalizedMaskAngleDegrees = excluded.LocalizedMaskAngleDegrees,
                LocalizedMaskInvert = excluded.LocalizedMaskInvert,
                LocalizedMaskIntensity = excluded.LocalizedMaskIntensity,
                LocalizedExposure = excluded.LocalizedExposure,
                LocalizedContrast = excluded.LocalizedContrast,
                LocalizedWarmth = excluded.LocalizedWarmth,
                LocalizedSaturation = excluded.LocalizedSaturation,
                LocalizedVibrance = excluded.LocalizedVibrance,
                LocalizedSkinSoftening = excluded.LocalizedSkinSoftening,
                LocalizedDenoise = excluded.LocalizedDenoise,
                LocalizedSharpen = excluded.LocalizedSharpen,
                LocalizedDetailRecovery = excluded.LocalizedDetailRecovery,
                LocalizedDeblur = excluded.LocalizedDeblur,
                LocalizedArtifactReduction = excluded.LocalizedArtifactReduction,
                LocalizedRealismBoost = excluded.LocalizedRealismBoost,
                CreatedAt = excluded.CreatedAt;
            """;

        command.Parameters.AddWithValue("$name", preset.Name);
        command.Parameters.AddWithValue("$featureMask", (int)preset.FeatureMask);
        command.Parameters.AddWithValue("$strength", preset.Strength);
        command.Parameters.AddWithValue("$manualExposure", preset.ManualAdjustments.Exposure);
        command.Parameters.AddWithValue("$manualContrast", preset.ManualAdjustments.Contrast);
        command.Parameters.AddWithValue("$manualWarmth", preset.ManualAdjustments.Warmth);
        command.Parameters.AddWithValue("$manualSaturation", preset.ManualAdjustments.Saturation);
        command.Parameters.AddWithValue("$manualVibrance", preset.ManualAdjustments.Vibrance);
        command.Parameters.AddWithValue("$manualHighlightRecovery", preset.ManualAdjustments.HighlightRecovery);
        command.Parameters.AddWithValue("$manualShadowLift", preset.ManualAdjustments.ShadowLift);
        command.Parameters.AddWithValue("$manualSkinSoftening", preset.ManualAdjustments.SkinSoftening);
        command.Parameters.AddWithValue("$manualDenoise", preset.ManualAdjustments.Denoise);
        command.Parameters.AddWithValue("$manualSharpen", preset.ManualAdjustments.Sharpen);
        command.Parameters.AddWithValue("$manualDetailRecovery", preset.ManualAdjustments.DetailRecovery);
        command.Parameters.AddWithValue("$manualDeblur", preset.ManualAdjustments.Deblur);
        command.Parameters.AddWithValue("$manualArtifactReduction", preset.ManualAdjustments.ArtifactReduction);
        command.Parameters.AddWithValue("$manualRealismBoost", preset.ManualAdjustments.RealismBoost);
        command.Parameters.AddWithValue("$cropRotationDegrees", preset.CropStraighten.RotationDegrees);
        command.Parameters.AddWithValue("$cropZoom", legacyCrop.Zoom);
        command.Parameters.AddWithValue("$cropOffsetX", legacyCrop.OffsetX);
        command.Parameters.AddWithValue("$cropOffsetY", legacyCrop.OffsetY);
        command.Parameters.AddWithValue("$cropLeft", preset.CropStraighten.CropLeft);
        command.Parameters.AddWithValue("$cropTop", preset.CropStraighten.CropTop);
        command.Parameters.AddWithValue("$cropWidth", preset.CropStraighten.CropWidth);
        command.Parameters.AddWithValue("$cropHeight", preset.CropStraighten.CropHeight);
        command.Parameters.AddWithValue("$localizedMaskEnabled", preset.LocalizedMask.IsEnabled ? 1 : 0);
        command.Parameters.AddWithValue("$localizedMaskKind", (int)preset.LocalizedMask.Kind);
        command.Parameters.AddWithValue("$localizedMaskCenterX", preset.LocalizedMask.CenterX);
        command.Parameters.AddWithValue("$localizedMaskCenterY", preset.LocalizedMask.CenterY);
        command.Parameters.AddWithValue("$localizedMaskWidth", preset.LocalizedMask.Width);
        command.Parameters.AddWithValue("$localizedMaskHeight", preset.LocalizedMask.Height);
        command.Parameters.AddWithValue("$localizedMaskFeather", preset.LocalizedMask.Feather);
        command.Parameters.AddWithValue("$localizedMaskAngleDegrees", preset.LocalizedMask.AngleDegrees);
        command.Parameters.AddWithValue("$localizedMaskInvert", preset.LocalizedMask.Invert ? 1 : 0);
        command.Parameters.AddWithValue("$localizedMaskIntensity", preset.LocalizedMask.Intensity);
        command.Parameters.AddWithValue("$localizedExposure", preset.LocalizedMask.Adjustments.Exposure);
        command.Parameters.AddWithValue("$localizedContrast", preset.LocalizedMask.Adjustments.Contrast);
        command.Parameters.AddWithValue("$localizedWarmth", preset.LocalizedMask.Adjustments.Warmth);
        command.Parameters.AddWithValue("$localizedSaturation", preset.LocalizedMask.Adjustments.Saturation);
        command.Parameters.AddWithValue("$localizedVibrance", preset.LocalizedMask.Adjustments.Vibrance);
        command.Parameters.AddWithValue("$localizedSkinSoftening", preset.LocalizedMask.Adjustments.SkinSoftening);
        command.Parameters.AddWithValue("$localizedDenoise", preset.LocalizedMask.Adjustments.Denoise);
        command.Parameters.AddWithValue("$localizedSharpen", preset.LocalizedMask.Adjustments.Sharpen);
        command.Parameters.AddWithValue("$localizedDetailRecovery", preset.LocalizedMask.Adjustments.DetailRecovery);
        command.Parameters.AddWithValue("$localizedDeblur", preset.LocalizedMask.Adjustments.Deblur);
        command.Parameters.AddWithValue("$localizedArtifactReduction", preset.LocalizedMask.Adjustments.ArtifactReduction);
        command.Parameters.AddWithValue("$localizedRealismBoost", preset.LocalizedMask.Adjustments.RealismBoost);
        command.Parameters.AddWithValue("$createdAt", preset.CreatedAt.ToString("O"));

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SavedPreset>> GetPresetsAsync(CancellationToken cancellationToken)
    {
        var presets = new List<SavedPreset>();

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT Id, Name, FeatureMask, Strength, ManualExposure, ManualContrast, ManualWarmth, ManualSaturation,
                   ManualVibrance, ManualHighlightRecovery, ManualShadowLift, ManualSkinSoftening, ManualDenoise,
                   ManualSharpen, ManualDetailRecovery, ManualDeblur, ManualArtifactReduction, ManualRealismBoost,
                   CropRotationDegrees, CropZoom, CropOffsetX, CropOffsetY, CropLeft, CropTop, CropWidth, CropHeight,
                   LocalizedMaskEnabled,
                   LocalizedMaskKind, LocalizedMaskCenterX, LocalizedMaskCenterY, LocalizedMaskWidth, LocalizedMaskHeight,
                   LocalizedMaskFeather, LocalizedMaskAngleDegrees, LocalizedMaskInvert, LocalizedMaskIntensity,
                   LocalizedExposure, LocalizedContrast, LocalizedWarmth, LocalizedSaturation, LocalizedVibrance,
                   LocalizedSkinSoftening, LocalizedDenoise, LocalizedSharpen, LocalizedDetailRecovery, LocalizedDeblur,
                   LocalizedArtifactReduction, LocalizedRealismBoost, CreatedAt
            FROM Presets
            ORDER BY Name COLLATE NOCASE ASC;
            """;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            presets.Add(new SavedPreset(
                reader.GetInt64(0),
                reader.GetString(1),
                (EnhancementFeature)reader.GetInt32(2),
                reader.GetDouble(3),
                new ManualEnhancementAdjustments(
                    reader.GetDouble(4),
                    reader.GetDouble(5),
                    reader.GetDouble(6),
                    reader.GetDouble(7),
                    reader.GetDouble(8),
                    reader.GetDouble(9),
                    reader.GetDouble(10),
                    reader.GetDouble(11),
                    reader.GetDouble(12),
                    reader.GetDouble(13),
                    reader.GetDouble(14),
                    reader.GetDouble(15),
                    reader.GetDouble(16),
                    reader.GetDouble(17)),
                ReadCropStraightenSettings(
                    reader.GetDouble(18),
                    reader.GetDouble(19),
                    reader.GetDouble(20),
                    reader.GetDouble(21),
                    reader.GetDouble(22),
                    reader.GetDouble(23),
                    reader.GetDouble(24),
                    reader.GetDouble(25)),
                new LocalizedMaskSettings(
                    reader.GetInt64(26) == 1,
                    (LocalizedMaskKind)reader.GetInt32(27),
                    reader.GetDouble(28),
                    reader.GetDouble(29),
                    reader.GetDouble(30),
                    reader.GetDouble(31),
                    reader.GetDouble(32),
                    reader.GetDouble(33),
                    reader.GetInt64(34) == 1,
                    reader.GetDouble(35),
                    new ManualEnhancementAdjustments(
                        reader.GetDouble(36),
                        reader.GetDouble(37),
                        reader.GetDouble(38),
                        reader.GetDouble(39),
                        reader.GetDouble(40),
                        0,
                        0,
                        reader.GetDouble(41),
                        reader.GetDouble(42),
                        reader.GetDouble(43),
                        reader.GetDouble(44),
                        reader.GetDouble(45),
                        reader.GetDouble(46),
                        reader.GetDouble(47))),
                DateTimeOffset.Parse(reader.GetString(48))));
        }

        return presets;
    }

    public async Task DeletePresetAsync(long presetId, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Presets WHERE Id = $presetId;";
        command.Parameters.AddWithValue("$presetId", presetId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task SaveStyleProfileAsync(StyleProfile profile, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO StyleProfiles (Name, CreatedAt)
            VALUES ($name, $createdAt)
            ON CONFLICT(Name) DO NOTHING;
            """;
        command.Parameters.AddWithValue("$name", profile.Name);
        command.Parameters.AddWithValue("$createdAt", profile.CreatedAt.ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<StyleProfile>> GetStyleProfilesAsync(CancellationToken cancellationToken)
    {
        var profiles = new List<StyleProfile>();

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                profiles.Id,
                profiles.Name,
                profiles.CreatedAt,
                COALESCE(SUM(CASE WHEN feedback.Outcome IN (1, 3) THEN 1 ELSE 0 END), 0) AS AcceptedCount,
                COALESCE(SUM(CASE WHEN feedback.Outcome = 2 THEN 1 ELSE 0 END), 0) AS DeclinedCount,
                MAX(feedback.CreatedAt) AS LastFeedbackAt
            FROM StyleProfiles AS profiles
            LEFT JOIN Feedback AS feedback
                ON feedback.StyleProfileId = profiles.Id
            GROUP BY profiles.Id, profiles.Name, profiles.CreatedAt
            ORDER BY profiles.Name COLLATE NOCASE ASC;
            """;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            profiles.Add(new StyleProfile(
                reader.GetInt64(0),
                reader.GetString(1),
                reader.GetInt32(3),
                reader.GetInt32(4),
                DateTimeOffset.Parse(reader.GetString(2)),
                reader.IsDBNull(5) ? null : DateTimeOffset.Parse(reader.GetString(5))));
        }

        return profiles;
    }

    public async Task DeleteStyleProfileAsync(long styleProfileId, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);

        await using (var clearFeedbackCommand = connection.CreateCommand())
        {
            clearFeedbackCommand.Transaction = transaction;
            clearFeedbackCommand.CommandText = "UPDATE Feedback SET StyleProfileId = NULL WHERE StyleProfileId = $styleProfileId;";
            clearFeedbackCommand.Parameters.AddWithValue("$styleProfileId", styleProfileId);
            await clearFeedbackCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var deleteProfileCommand = connection.CreateCommand())
        {
            deleteProfileCommand.Transaction = transaction;
            deleteProfileCommand.CommandText = "DELETE FROM StyleProfiles WHERE Id = $styleProfileId;";
            deleteProfileCommand.Parameters.AddWithValue("$styleProfileId", styleProfileId);
            await deleteProfileCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    private async Task<Guid> UpsertFolderAsync(string folderPath, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var selectCommand = connection.CreateCommand();
        selectCommand.CommandText = "SELECT Id FROM Folders WHERE FolderPath = $folderPath;";
        selectCommand.Parameters.AddWithValue("$folderPath", folderPath);

        var existing = await selectCommand.ExecuteScalarAsync(cancellationToken);
        if (existing is string existingId && Guid.TryParse(existingId, out var parsedId))
        {
            return parsedId;
        }

        var folderId = Guid.NewGuid();

        await using var insertCommand = connection.CreateCommand();
        insertCommand.CommandText =
            """
            INSERT INTO Folders (Id, FolderPath, ImportedAt)
            VALUES ($id, $folderPath, $importedAt);
            """;
        insertCommand.Parameters.AddWithValue("$id", folderId.ToString());
        insertCommand.Parameters.AddWithValue("$folderPath", folderPath);
        insertCommand.Parameters.AddWithValue("$importedAt", DateTimeOffset.UtcNow.ToString("O"));

        await insertCommand.ExecuteNonQueryAsync(cancellationToken);
        return folderId;
    }

    private async Task<SqliteConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = _paths.DatabasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        };

        var connection = new SqliteConnection(builder.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    private static bool HasRenderProxy(string filePath)
    {
        var directory = Path.GetDirectoryName(filePath);
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(filePath);
        if (string.IsNullOrWhiteSpace(directory) || string.IsNullOrWhiteSpace(fileNameWithoutExtension))
        {
            return false;
        }

        foreach (var extension in new[] { ".jpg", ".jpeg", ".png" })
        {
            if (File.Exists(Path.Combine(directory, fileNameWithoutExtension + extension)))
            {
                return true;
            }
        }

        return false;
    }

    private static void TryDeleteThumbnailFile(string? thumbnailPath)
    {
        if (string.IsNullOrWhiteSpace(thumbnailPath) || !File.Exists(thumbnailPath))
        {
            return;
        }

        try
        {
            File.Delete(thumbnailPath);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static async Task EnsureFeedbackColumnsAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        var columns = await GetTableColumnsAsync(connection, "Feedback", cancellationToken);

        await EnsureColumnAsync(connection, columns, "StyleProfileId", "ALTER TABLE Feedback ADD COLUMN StyleProfileId INTEGER NULL;", cancellationToken);
        await EnsureColumnAsync(connection, columns, "DetailRecovery", "ALTER TABLE Feedback ADD COLUMN DetailRecovery REAL NOT NULL DEFAULT 0;", cancellationToken);
        await EnsureColumnAsync(connection, columns, "Deblur", "ALTER TABLE Feedback ADD COLUMN Deblur REAL NOT NULL DEFAULT 0;", cancellationToken);
        await EnsureColumnAsync(connection, columns, "ArtifactReduction", "ALTER TABLE Feedback ADD COLUMN ArtifactReduction REAL NOT NULL DEFAULT 0;", cancellationToken);
        await EnsureColumnAsync(connection, columns, "RealismBoost", "ALTER TABLE Feedback ADD COLUMN RealismBoost REAL NOT NULL DEFAULT 0;", cancellationToken);
    }

    private static async Task EnsurePresetColumnsAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        var columns = await GetTableColumnsAsync(connection, "Presets", cancellationToken);

        await EnsureColumnAsync(connection, columns, "ManualDetailRecovery", "ALTER TABLE Presets ADD COLUMN ManualDetailRecovery REAL NOT NULL DEFAULT 0;", cancellationToken);
        await EnsureColumnAsync(connection, columns, "ManualDeblur", "ALTER TABLE Presets ADD COLUMN ManualDeblur REAL NOT NULL DEFAULT 0;", cancellationToken);
        await EnsureColumnAsync(connection, columns, "ManualArtifactReduction", "ALTER TABLE Presets ADD COLUMN ManualArtifactReduction REAL NOT NULL DEFAULT 0;", cancellationToken);
        await EnsureColumnAsync(connection, columns, "ManualRealismBoost", "ALTER TABLE Presets ADD COLUMN ManualRealismBoost REAL NOT NULL DEFAULT 0;", cancellationToken);
        await EnsureColumnAsync(connection, columns, "CropRotationDegrees", "ALTER TABLE Presets ADD COLUMN CropRotationDegrees REAL NOT NULL DEFAULT 0;", cancellationToken);
        await EnsureColumnAsync(connection, columns, "CropZoom", "ALTER TABLE Presets ADD COLUMN CropZoom REAL NOT NULL DEFAULT 0;", cancellationToken);
        await EnsureColumnAsync(connection, columns, "CropOffsetX", "ALTER TABLE Presets ADD COLUMN CropOffsetX REAL NOT NULL DEFAULT 0;", cancellationToken);
        await EnsureColumnAsync(connection, columns, "CropOffsetY", "ALTER TABLE Presets ADD COLUMN CropOffsetY REAL NOT NULL DEFAULT 0;", cancellationToken);
        await EnsureColumnAsync(connection, columns, "CropLeft", "ALTER TABLE Presets ADD COLUMN CropLeft REAL NOT NULL DEFAULT 0;", cancellationToken);
        await EnsureColumnAsync(connection, columns, "CropTop", "ALTER TABLE Presets ADD COLUMN CropTop REAL NOT NULL DEFAULT 0;", cancellationToken);
        await EnsureColumnAsync(connection, columns, "CropWidth", "ALTER TABLE Presets ADD COLUMN CropWidth REAL NOT NULL DEFAULT 1;", cancellationToken);
        await EnsureColumnAsync(connection, columns, "CropHeight", "ALTER TABLE Presets ADD COLUMN CropHeight REAL NOT NULL DEFAULT 1;", cancellationToken);
        await EnsureColumnAsync(connection, columns, "LocalizedMaskEnabled", "ALTER TABLE Presets ADD COLUMN LocalizedMaskEnabled INTEGER NOT NULL DEFAULT 0;", cancellationToken);
        await EnsureColumnAsync(connection, columns, "LocalizedMaskKind", "ALTER TABLE Presets ADD COLUMN LocalizedMaskKind INTEGER NOT NULL DEFAULT 1;", cancellationToken);
        await EnsureColumnAsync(connection, columns, "LocalizedMaskCenterX", "ALTER TABLE Presets ADD COLUMN LocalizedMaskCenterX REAL NOT NULL DEFAULT 0.5;", cancellationToken);
        await EnsureColumnAsync(connection, columns, "LocalizedMaskCenterY", "ALTER TABLE Presets ADD COLUMN LocalizedMaskCenterY REAL NOT NULL DEFAULT 0.5;", cancellationToken);
        await EnsureColumnAsync(connection, columns, "LocalizedMaskWidth", "ALTER TABLE Presets ADD COLUMN LocalizedMaskWidth REAL NOT NULL DEFAULT 0.55;", cancellationToken);
        await EnsureColumnAsync(connection, columns, "LocalizedMaskHeight", "ALTER TABLE Presets ADD COLUMN LocalizedMaskHeight REAL NOT NULL DEFAULT 0.55;", cancellationToken);
        await EnsureColumnAsync(connection, columns, "LocalizedMaskFeather", "ALTER TABLE Presets ADD COLUMN LocalizedMaskFeather REAL NOT NULL DEFAULT 0.25;", cancellationToken);
        await EnsureColumnAsync(connection, columns, "LocalizedMaskAngleDegrees", "ALTER TABLE Presets ADD COLUMN LocalizedMaskAngleDegrees REAL NOT NULL DEFAULT 0;", cancellationToken);
        await EnsureColumnAsync(connection, columns, "LocalizedMaskInvert", "ALTER TABLE Presets ADD COLUMN LocalizedMaskInvert INTEGER NOT NULL DEFAULT 0;", cancellationToken);
        await EnsureColumnAsync(connection, columns, "LocalizedMaskIntensity", "ALTER TABLE Presets ADD COLUMN LocalizedMaskIntensity REAL NOT NULL DEFAULT 0.85;", cancellationToken);
        await EnsureColumnAsync(connection, columns, "LocalizedExposure", "ALTER TABLE Presets ADD COLUMN LocalizedExposure REAL NOT NULL DEFAULT 0;", cancellationToken);
        await EnsureColumnAsync(connection, columns, "LocalizedContrast", "ALTER TABLE Presets ADD COLUMN LocalizedContrast REAL NOT NULL DEFAULT 0;", cancellationToken);
        await EnsureColumnAsync(connection, columns, "LocalizedWarmth", "ALTER TABLE Presets ADD COLUMN LocalizedWarmth REAL NOT NULL DEFAULT 0;", cancellationToken);
        await EnsureColumnAsync(connection, columns, "LocalizedSaturation", "ALTER TABLE Presets ADD COLUMN LocalizedSaturation REAL NOT NULL DEFAULT 0;", cancellationToken);
        await EnsureColumnAsync(connection, columns, "LocalizedVibrance", "ALTER TABLE Presets ADD COLUMN LocalizedVibrance REAL NOT NULL DEFAULT 0;", cancellationToken);
        await EnsureColumnAsync(connection, columns, "LocalizedSkinSoftening", "ALTER TABLE Presets ADD COLUMN LocalizedSkinSoftening REAL NOT NULL DEFAULT 0;", cancellationToken);
        await EnsureColumnAsync(connection, columns, "LocalizedDenoise", "ALTER TABLE Presets ADD COLUMN LocalizedDenoise REAL NOT NULL DEFAULT 0;", cancellationToken);
        await EnsureColumnAsync(connection, columns, "LocalizedSharpen", "ALTER TABLE Presets ADD COLUMN LocalizedSharpen REAL NOT NULL DEFAULT 0;", cancellationToken);
        await EnsureColumnAsync(connection, columns, "LocalizedDetailRecovery", "ALTER TABLE Presets ADD COLUMN LocalizedDetailRecovery REAL NOT NULL DEFAULT 0;", cancellationToken);
        await EnsureColumnAsync(connection, columns, "LocalizedDeblur", "ALTER TABLE Presets ADD COLUMN LocalizedDeblur REAL NOT NULL DEFAULT 0;", cancellationToken);
        await EnsureColumnAsync(connection, columns, "LocalizedArtifactReduction", "ALTER TABLE Presets ADD COLUMN LocalizedArtifactReduction REAL NOT NULL DEFAULT 0;", cancellationToken);
        await EnsureColumnAsync(connection, columns, "LocalizedRealismBoost", "ALTER TABLE Presets ADD COLUMN LocalizedRealismBoost REAL NOT NULL DEFAULT 0;", cancellationToken);
    }

    private static CropStraightenSettings ReadCropStraightenSettings(
        double rotationDegrees,
        double legacyZoom,
        double legacyOffsetX,
        double legacyOffsetY,
        double cropLeft,
        double cropTop,
        double cropWidth,
        double cropHeight)
    {
        var normalizedCrop = NormalizeCrop(new CropStraightenSettings(
            rotationDegrees,
            cropLeft,
            cropTop,
            cropWidth,
            cropHeight));

        if (!normalizedCrop.IsIdentity)
        {
            return normalizedCrop;
        }

        return FromLegacyCropValues(rotationDegrees, legacyZoom, legacyOffsetX, legacyOffsetY);
    }

    private static CropStraightenSettings FromLegacyCropValues(
        double rotationDegrees,
        double zoom,
        double offsetX,
        double offsetY)
    {
        var clampedZoom = Math.Clamp(zoom, 0.0, 0.75);
        if (clampedZoom <= 0.0001)
        {
            return new CropStraightenSettings(rotationDegrees, 0, 0, 1, 1);
        }

        var scale = Math.Clamp(1.0 - clampedZoom, 0.25, 1.0);
        var maxOffsetFactor = clampedZoom / 2.0;
        var centerX = 0.5 + (Math.Clamp(offsetX, -1.0, 1.0) * maxOffsetFactor);
        var centerY = 0.5 + (Math.Clamp(offsetY, -1.0, 1.0) * maxOffsetFactor);

        return NormalizeCrop(new CropStraightenSettings(
            rotationDegrees,
            centerX - (scale / 2.0),
            centerY - (scale / 2.0),
            scale,
            scale));
    }

    private static CropStraightenSettings NormalizeCrop(CropStraightenSettings crop)
    {
        const double minimumSize = 0.03;

        var normalizedWidth = Math.Clamp(crop.CropWidth, minimumSize, 1.0);
        var normalizedHeight = Math.Clamp(crop.CropHeight, minimumSize, 1.0);
        var normalizedLeft = Math.Clamp(crop.CropLeft, 0.0, 1.0 - normalizedWidth);
        var normalizedTop = Math.Clamp(crop.CropTop, 0.0, 1.0 - normalizedHeight);

        return new CropStraightenSettings(
            crop.RotationDegrees,
            normalizedLeft,
            normalizedTop,
            normalizedWidth,
            normalizedHeight);
    }

    private static (double Zoom, double OffsetX, double OffsetY) ToLegacyCropValues(CropStraightenSettings crop)
    {
        if (crop.IsIdentity)
        {
            return (0, 0, 0);
        }

        var scale = Math.Clamp((crop.CropWidth + crop.CropHeight) / 2.0, 0.25, 1.0);
        var zoom = Math.Clamp(1.0 - scale, 0.0, 0.75);
        if (zoom <= 0.0001)
        {
            return (0, 0, 0);
        }

        var centerX = crop.CropLeft + (crop.CropWidth / 2.0);
        var centerY = crop.CropTop + (crop.CropHeight / 2.0);
        var maxOffsetFactor = zoom / 2.0;

        return (
            zoom,
            Math.Clamp((centerX - 0.5) / maxOffsetFactor, -1.0, 1.0),
            Math.Clamp((centerY - 0.5) / maxOffsetFactor, -1.0, 1.0));
    }

    private static async Task<HashSet<string>> GetTableColumnsAsync(SqliteConnection connection, string tableName, CancellationToken cancellationToken)
    {
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using var pragmaCommand = connection.CreateCommand();
        pragmaCommand.CommandText = $"PRAGMA table_info({tableName});";

        await using var reader = await pragmaCommand.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            columns.Add(reader.GetString(1));
        }

        return columns;
    }

    private static async Task EnsureColumnAsync(
        SqliteConnection connection,
        ISet<string> existingColumns,
        string columnName,
        string alterStatement,
        CancellationToken cancellationToken)
    {
        if (existingColumns.Contains(columnName))
        {
            return;
        }

        await using var alterCommand = connection.CreateCommand();
        alterCommand.CommandText = alterStatement;
        await alterCommand.ExecuteNonQueryAsync(cancellationToken);
        existingColumns.Add(columnName);
    }
}
