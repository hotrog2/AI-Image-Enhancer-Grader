using ColorGrader.Core.Models;

namespace ColorGrader.Application.Interfaces;

public interface ICatalogService
{
    Task InitializeAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<CatalogFolder>> GetFoldersAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<CatalogAsset>> GetAssetsAsync(CancellationToken cancellationToken);
    Task<int> ImportFolderAsync(string folderPath, bool recursive, CancellationToken cancellationToken);
    Task SaveFeedbackAsync(EnhancementFeedback feedback, CancellationToken cancellationToken);
    Task<IReadOnlyList<EnhancementFeedback>> GetFeedbackAsync(CancellationToken cancellationToken);
    Task SaveExportHistoryAsync(ExportHistoryEntry entry, CancellationToken cancellationToken);
    Task<IReadOnlyList<ExportHistoryEntry>> GetRecentExportHistoryAsync(int take, CancellationToken cancellationToken);
    Task<ThumbnailCacheEntry?> GetThumbnailCacheEntryAsync(Guid assetId, CancellationToken cancellationToken);
    Task SaveThumbnailCacheEntryAsync(ThumbnailCacheEntry entry, CancellationToken cancellationToken);
    Task SavePresetAsync(SavedPreset preset, CancellationToken cancellationToken);
    Task<IReadOnlyList<SavedPreset>> GetPresetsAsync(CancellationToken cancellationToken);
    Task DeletePresetAsync(long presetId, CancellationToken cancellationToken);
    Task SaveStyleProfileAsync(StyleProfile profile, CancellationToken cancellationToken);
    Task<IReadOnlyList<StyleProfile>> GetStyleProfilesAsync(CancellationToken cancellationToken);
    Task DeleteStyleProfileAsync(long styleProfileId, CancellationToken cancellationToken);
}
