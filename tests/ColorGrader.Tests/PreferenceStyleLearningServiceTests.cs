using ColorGrader.AI.Services;
using ColorGrader.Application.Interfaces;
using ColorGrader.Core.Models;

namespace ColorGrader.Tests;

public sealed class PreferenceStyleLearningServiceTests
{
    [Fact]
    public async Task BuildSuggestionAsync_UsesSelectedProfileFeedbackInsteadOfGlobalHistory()
    {
        var service = new PreferenceStyleLearningService(new FakeCatalogService(
        [
            new EnhancementFeedback(Guid.NewGuid(), FeedbackDisposition.Accepted, EnhancementSettings.Default with { Exposure = 0.70 }, DateTimeOffset.UtcNow),
            new EnhancementFeedback(Guid.NewGuid(), FeedbackDisposition.Accepted, EnhancementSettings.Default with { Exposure = -0.10 }, DateTimeOffset.UtcNow, 7)
        ]));

        var suggestion = await service.BuildSuggestionAsync(
            new CatalogAsset(Guid.NewGuid(), Guid.NewGuid(), @"C:\photo.jpg", "photo.jpg", ".jpg", AssetKind.Jpeg, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, 10, true, null, null),
            EnhancementFeature.AutoExposure | EnhancementFeature.StyleLearning,
            analysis: null,
            retryMode: RetryMode.Auto,
            retryVariantIndex: 0,
            styleProfileId: 7,
            CancellationToken.None);

        Assert.InRange(suggestion.Settings.Exposure, 0.009, 0.011);
        Assert.Contains("selected style profile", suggestion.Rationale, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task BuildSuggestionAsync_AppliesReferenceLookBiasForLowLightNeonPortraits()
    {
        var service = new PreferenceStyleLearningService(new FakeCatalogService([]));

        var suggestion = await service.BuildSuggestionAsync(
            new CatalogAsset(Guid.NewGuid(), Guid.NewGuid(), @"C:\photo.jpg", "photo.jpg", ".jpg", AssetKind.Jpeg, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, 10, true, null, null),
            EnhancementFeature.AutoExposure |
            EnhancementFeature.WhiteBalance |
            EnhancementFeature.Contrast |
            EnhancementFeature.ToneCurve |
            EnhancementFeature.QualityRestore,
            new ImageAnalysis(0.24, 0.16, -0.03),
            retryMode: RetryMode.Auto,
            retryVariantIndex: 0,
            styleProfileId: null,
            CancellationToken.None);

        Assert.Contains("reference look", suggestion.Rationale, StringComparison.OrdinalIgnoreCase);
        Assert.True(suggestion.Settings.Warmth >= 0.05);
        Assert.True(suggestion.Settings.Vibrance >= 0.20);
        Assert.True(suggestion.Settings.DetailRecovery >= 0.24);
    }

    [Fact]
    public async Task BuildSuggestionAsync_UsesDeclinedFeedbackToGenerateAlternateRetryPass()
    {
        var service = new PreferenceStyleLearningService(new FakeCatalogService(
        [
            new EnhancementFeedback(Guid.NewGuid(), FeedbackDisposition.Declined, EnhancementSettings.Default, DateTimeOffset.UtcNow)
        ]));

        var suggestion = await service.BuildSuggestionAsync(
            new CatalogAsset(Guid.NewGuid(), Guid.NewGuid(), @"C:\photo.jpg", "photo.jpg", ".jpg", AssetKind.Jpeg, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, 10, true, null, null),
            EnhancementFeature.AutoExposure |
            EnhancementFeature.Contrast |
            EnhancementFeature.WhiteBalance |
            EnhancementFeature.ToneCurve |
            EnhancementFeature.Denoise |
            EnhancementFeature.Sharpen |
            EnhancementFeature.QualityRestore |
            EnhancementFeature.StyleLearning,
            analysis: null,
            retryMode: RetryMode.Auto,
            retryVariantIndex: 1,
            styleProfileId: null,
            CancellationToken.None);

        Assert.Contains("retry", suggestion.Rationale, StringComparison.OrdinalIgnoreCase);
        Assert.NotEqual(EnhancementSettings.Default.Exposure, suggestion.Settings.Exposure);
        Assert.NotEqual(EnhancementSettings.Default.DetailRecovery, suggestion.Settings.DetailRecovery);
    }

    [Fact]
    public async Task BuildSuggestionAsync_UsesSelectedRetryModeBias()
    {
        var service = new PreferenceStyleLearningService(new FakeCatalogService([]));

        var suggestion = await service.BuildSuggestionAsync(
            new CatalogAsset(Guid.NewGuid(), Guid.NewGuid(), @"C:\photo.jpg", "photo.jpg", ".jpg", AssetKind.Jpeg, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, 10, true, null, null),
            EnhancementFeature.AutoExposure |
            EnhancementFeature.Contrast |
            EnhancementFeature.WhiteBalance |
            EnhancementFeature.ToneCurve |
            EnhancementFeature.Denoise |
            EnhancementFeature.Sharpen |
            EnhancementFeature.QualityRestore,
            analysis: null,
            retryMode: RetryMode.Brighter,
            retryVariantIndex: 1,
            styleProfileId: null,
            CancellationToken.None);

        Assert.Contains("Retry mode: Brighter", suggestion.Rationale, StringComparison.OrdinalIgnoreCase);
        Assert.True(suggestion.Settings.Exposure > EnhancementSettings.Default.Exposure);
        Assert.True(suggestion.Settings.ShadowLift > EnhancementSettings.Default.ShadowLift);
    }

    private sealed class FakeCatalogService(IReadOnlyList<EnhancementFeedback> feedback) : ICatalogService
    {
        public Task InitializeAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<IReadOnlyList<CatalogFolder>> GetFoldersAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<CatalogFolder>>([]);
        public Task<IReadOnlyList<CatalogAsset>> GetAssetsAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<CatalogAsset>>([]);
        public Task DeleteAssetAsync(Guid assetId, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<int> ImportFolderAsync(string folderPath, bool recursive, CancellationToken cancellationToken) => Task.FromResult(0);
        public Task SaveFeedbackAsync(EnhancementFeedback feedback, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<IReadOnlyList<EnhancementFeedback>> GetFeedbackAsync(CancellationToken cancellationToken) => Task.FromResult(feedback);
        public Task SaveExportHistoryAsync(ExportHistoryEntry entry, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<IReadOnlyList<ExportHistoryEntry>> GetRecentExportHistoryAsync(int take, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<ExportHistoryEntry>>([]);
        public Task<ThumbnailCacheEntry?> GetThumbnailCacheEntryAsync(Guid assetId, CancellationToken cancellationToken) => Task.FromResult<ThumbnailCacheEntry?>(null);
        public Task SaveThumbnailCacheEntryAsync(ThumbnailCacheEntry entry, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task SavePresetAsync(SavedPreset preset, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<IReadOnlyList<SavedPreset>> GetPresetsAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<SavedPreset>>([]);
        public Task DeletePresetAsync(long presetId, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task SaveStyleProfileAsync(StyleProfile profile, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<IReadOnlyList<StyleProfile>> GetStyleProfilesAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<StyleProfile>>([]);
        public Task DeleteStyleProfileAsync(long styleProfileId, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
