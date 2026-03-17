using ColorGrader.Application.Interfaces;
using ColorGrader.Application.Models;
using ColorGrader.Core.Models;

namespace ColorGrader.Application.Services;

public sealed class EditorWorkflowService(
    ICatalogService catalogService,
    IImageProcessingService imageProcessingService,
    IStyleLearningService styleLearningService,
    IThumbnailCacheService thumbnailCacheService)
{
    public Task InitializeAsync(CancellationToken cancellationToken) =>
        catalogService.InitializeAsync(cancellationToken);

    public Task<IReadOnlyList<CatalogFolder>> GetFoldersAsync(CancellationToken cancellationToken) =>
        catalogService.GetFoldersAsync(cancellationToken);

    public Task<IReadOnlyList<CatalogAsset>> GetAssetsAsync(CancellationToken cancellationToken) =>
        catalogService.GetAssetsAsync(cancellationToken);

    public async Task<IReadOnlyList<CatalogAssetListItem>> GetLibraryItemsAsync(CancellationToken cancellationToken)
    {
        var assets = await catalogService.GetAssetsAsync(cancellationToken);
        var items = new List<CatalogAssetListItem>(assets.Count);

        foreach (var asset in assets)
        {
            var thumbnailPath = await thumbnailCacheService.GetThumbnailPathAsync(asset, 220, cancellationToken);
            items.Add(new CatalogAssetListItem(asset, thumbnailPath));
        }

        return items;
    }

    public Task<IReadOnlyList<ExportHistoryEntry>> GetRecentExportHistoryAsync(int take, CancellationToken cancellationToken) =>
        catalogService.GetRecentExportHistoryAsync(take, cancellationToken);

    public Task<IReadOnlyList<SavedPreset>> GetPresetsAsync(CancellationToken cancellationToken) =>
        catalogService.GetPresetsAsync(cancellationToken);

    public Task SavePresetAsync(SavedPreset preset, CancellationToken cancellationToken) =>
        catalogService.SavePresetAsync(preset, cancellationToken);

    public Task DeletePresetAsync(long presetId, CancellationToken cancellationToken) =>
        catalogService.DeletePresetAsync(presetId, cancellationToken);

    public Task<IReadOnlyList<StyleProfile>> GetStyleProfilesAsync(CancellationToken cancellationToken) =>
        catalogService.GetStyleProfilesAsync(cancellationToken);

    public Task SaveStyleProfileAsync(StyleProfile profile, CancellationToken cancellationToken) =>
        catalogService.SaveStyleProfileAsync(profile, cancellationToken);

    public Task DeleteStyleProfileAsync(long styleProfileId, CancellationToken cancellationToken) =>
        catalogService.DeleteStyleProfileAsync(styleProfileId, cancellationToken);

    public Task<int> ImportFolderAsync(string folderPath, CancellationToken cancellationToken) =>
        catalogService.ImportFolderAsync(folderPath, recursive: true, cancellationToken);

    public async Task<EditorDocument> BuildEditorDocumentAsync(
        CatalogAsset asset,
        EnhancementFeature enabledFeatures,
        double strength,
        ManualEnhancementAdjustments manualAdjustments,
        long? styleProfileId,
        CancellationToken cancellationToken)
    {
        var suggestion = await BuildSuggestionAsync(asset, enabledFeatures, strength, manualAdjustments, styleProfileId, cancellationToken);
        var originalPreview = await imageProcessingService.LoadPreviewAsync(asset.FilePath, cancellationToken);

        ImagePreview? enhancedPreview = null;
        var notice = string.Empty;

        if (imageProcessingService.CanRender(asset.FilePath))
        {
            enhancedPreview = await imageProcessingService.RenderPreviewAsync(asset.FilePath, suggestion, cancellationToken);
        }

        if (originalPreview is null)
        {
            notice = asset.Kind == AssetKind.Raw
                ? "RAW file imported, but the current WIC and LibRaw decoder chain could not open a preview for this file yet."
                : "Preview unavailable for this file.";
        }
        else if (asset.Kind == AssetKind.Raw && enhancedPreview is not null)
        {
            notice = "RAW preview opened locally through the WIC and LibRaw decoder chain.";
        }
        else if (enhancedPreview is null)
        {
            notice = "Preview loaded, but enhancement rendering was unavailable for this file.";
        }

        return new EditorDocument(asset, originalPreview, enhancedPreview, suggestion, notice);
    }

    public async Task<ExportResult> ExportAssetAsync(
        CatalogAsset asset,
        EnhancementFeature enabledFeatures,
        double strength,
        ManualEnhancementAdjustments manualAdjustments,
        ExportPreset preset,
        long? styleProfileId,
        CancellationToken cancellationToken)
    {
        var suggestion = await BuildSuggestionAsync(asset, enabledFeatures, strength, manualAdjustments, styleProfileId, cancellationToken);
        var request = new ExportRequest(asset, suggestion, preset);
        var result = await imageProcessingService.ExportAsync(request, cancellationToken);

        await catalogService.SaveExportHistoryAsync(
            new ExportHistoryEntry(
                0,
                asset.Id,
                asset.FileName,
                result.OutputPath,
                result.Status,
                result.Message,
                DateTimeOffset.UtcNow),
            cancellationToken);

        return result;
    }

    public Task SaveFeedbackAsync(
        CatalogAsset asset,
        FeedbackDisposition disposition,
        EnhancementSuggestion suggestion,
        long? styleProfileId,
        CancellationToken cancellationToken)
    {
        var feedback = new EnhancementFeedback(
            asset.Id,
            disposition,
            suggestion.Settings,
            DateTimeOffset.UtcNow,
            styleProfileId);

        return catalogService.SaveFeedbackAsync(feedback, cancellationToken);
    }

    private async Task<EnhancementSuggestion> BuildSuggestionAsync(
        CatalogAsset asset,
        EnhancementFeature enabledFeatures,
        double strength,
        ManualEnhancementAdjustments manualAdjustments,
        long? styleProfileId,
        CancellationToken cancellationToken)
    {
        var analysis = await imageProcessingService.AnalyzeAsync(asset.FilePath, cancellationToken);
        var suggestion = await styleLearningService.BuildSuggestionAsync(asset, enabledFeatures, analysis, styleProfileId, cancellationToken);
        var appliedSettings = suggestion.Settings
            .ApplyStrength(strength)
            .ApplyManualAdjustments(manualAdjustments);

        var rationale = $"{suggestion.Rationale} Strength {Math.Round(Math.Clamp(strength, 0.0, 1.0) * 100)}%.";
        if (manualAdjustments.HasAdjustments)
        {
            rationale += " Manual fine-tune adjustments applied.";
        }

        return suggestion with
        {
            Settings = appliedSettings,
            Rationale = rationale
        };
    }
}
