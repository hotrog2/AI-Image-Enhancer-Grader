using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Media.Imaging;
using ColorGrader.App.Services;
using ColorGrader.Application.Models;
using ColorGrader.Application.Services;
using ColorGrader.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ColorGrader.App.ViewModels;

public partial class ShellViewModel(
    EditorWorkflowService workflowService,
    IFolderPickerService folderPickerService) : ObservableObject
{
    private const string NeonClubReferencePresetName = "Reference Look - Neon Club Glow";
    private CancellationTokenSource? _editorCancellationSource;
    private CancellationTokenSource? _exportCancellationSource;
    private bool _suppressPreviewRefresh;

    public ObservableCollection<CatalogFolder> Folders { get; } = [];
    public ObservableCollection<CatalogAssetListItem> LibraryItems { get; } = [];
    public ObservableCollection<ExportQueueItemViewModel> ExportQueueItems { get; } = [];
    public ObservableCollection<RecentExportItemViewModel> RecentExports { get; } = [];
    public ObservableCollection<SavedPreset> Presets { get; } = [];
    public ObservableCollection<StyleProfile> StyleProfiles { get; } = [];
    public IReadOnlyList<ExportFileFormat> ExportFormats { get; } = Enum.GetValues<ExportFileFormat>();
    public IReadOnlyList<EditorCompareMode> CompareModes { get; } = Enum.GetValues<EditorCompareMode>();
    public IReadOnlyList<LocalizedMaskKind> LocalizedMaskKinds { get; } = Enum.GetValues<LocalizedMaskKind>();
    public IReadOnlyList<CanvasEditorTool> CanvasEditorTools { get; } = Enum.GetValues<CanvasEditorTool>();

    [ObservableProperty]
    private CatalogAssetListItem? selectedLibraryItem;

    [ObservableProperty]
    private CatalogAsset? selectedAsset;

    [ObservableProperty]
    private BitmapSource? originalPreview;

    [ObservableProperty]
    private BitmapSource? enhancedPreview;

    [ObservableProperty]
    private string statusText = "Import a folder to start building your catalog.";

    [ObservableProperty]
    private string previewNotice = "No asset selected.";

    [ObservableProperty]
    private string suggestionRationale = "Enhancement suggestions show up here after you pick an image.";

    [ObservableProperty]
    private string confidenceText = "Confidence --";

    [ObservableProperty]
    private string inferenceStatusText = "AI subject mask unavailable until a subject segmentation ONNX model is installed.";

    [ObservableProperty]
    private string selectedAssetSummary = "Choose a photo from the library";

    [ObservableProperty]
    private string catalogSummary = "Catalog is empty";

    [ObservableProperty]
    private string exportFolder = "No export folder selected";

    [ObservableProperty]
    private string exportProgressText = "No export jobs have run yet.";

    [ObservableProperty]
    private string newPresetName = string.Empty;

    [ObservableProperty]
    private string newStyleProfileName = string.Empty;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private bool isExporting;

    [ObservableProperty]
    private ExportFileFormat selectedExportFormat = ExportFileFormat.Jpeg;

    [ObservableProperty]
    private int exportLongEdgePixels = 3200;

    [ObservableProperty]
    private int jpegQuality = 92;

    [ObservableProperty]
    private double enhancementStrength = 1.0;

    [ObservableProperty]
    private EditorCompareMode selectedCompareMode = EditorCompareMode.Compare;

    [ObservableProperty]
    private bool autoExposure = true;

    [ObservableProperty]
    private bool whiteBalance = true;

    [ObservableProperty]
    private bool contrast = true;

    [ObservableProperty]
    private bool toneCurve = true;

    [ObservableProperty]
    private bool skinTone = true;

    [ObservableProperty]
    private bool denoise = true;

    [ObservableProperty]
    private bool sharpen = true;

    [ObservableProperty]
    private bool upscale;

    [ObservableProperty]
    private bool styleLearning = true;

    [ObservableProperty]
    private bool qualityRestore = true;

    [ObservableProperty]
    private double manualExposure;

    [ObservableProperty]
    private double manualContrast;

    [ObservableProperty]
    private double manualWarmth;

    [ObservableProperty]
    private double manualSaturation;

    [ObservableProperty]
    private double manualVibrance;

    [ObservableProperty]
    private double manualHighlightRecovery;

    [ObservableProperty]
    private double manualShadowLift;

    [ObservableProperty]
    private double manualSkinSoftening;

    [ObservableProperty]
    private double manualDenoise;

    [ObservableProperty]
    private double manualSharpen;

    [ObservableProperty]
    private double manualDetailRecovery;

    [ObservableProperty]
    private double manualDeblur;

    [ObservableProperty]
    private double manualArtifactReduction;

    [ObservableProperty]
    private double manualRealismBoost;

    [ObservableProperty]
    private double straightenAngle;

    [ObservableProperty]
    private double cropLeft;

    [ObservableProperty]
    private double cropTop;

    [ObservableProperty]
    private double cropWidth = 1.0;

    [ObservableProperty]
    private double cropHeight = 1.0;

    [ObservableProperty]
    private bool localizedMaskEnabled;

    [ObservableProperty]
    private LocalizedMaskKind selectedLocalizedMaskKind = LocalizedMaskKind.Radial;

    [ObservableProperty]
    private double localizedMaskCenterX = 0.5;

    [ObservableProperty]
    private double localizedMaskCenterY = 0.5;

    [ObservableProperty]
    private double localizedMaskWidth = 0.55;

    [ObservableProperty]
    private double localizedMaskHeight = 0.55;

    [ObservableProperty]
    private double localizedMaskFeather = 0.25;

    [ObservableProperty]
    private double localizedMaskAngle;

    [ObservableProperty]
    private bool invertLocalizedMask;

    [ObservableProperty]
    private double localizedMaskIntensity = 0.85;

    [ObservableProperty]
    private double localizedExposure;

    [ObservableProperty]
    private double localizedContrast;

    [ObservableProperty]
    private double localizedWarmth;

    [ObservableProperty]
    private double localizedSaturation;

    [ObservableProperty]
    private double localizedVibrance;

    [ObservableProperty]
    private double localizedSkinSoftening;

    [ObservableProperty]
    private double localizedDenoise;

    [ObservableProperty]
    private double localizedSharpen;

    [ObservableProperty]
    private CanvasEditorTool selectedCanvasEditorTool = CanvasEditorTool.Crop;

    [ObservableProperty]
    private SavedPreset? selectedPreset;

    [ObservableProperty]
    private StyleProfile? selectedStyleProfile;

    public EnhancementSuggestion? CurrentSuggestion { get; private set; }

    [RelayCommand]
    private async Task InitializeAsync()
    {
        await workflowService.InitializeAsync(CancellationToken.None);
        await EnsureBuiltInPresetsAsync();
        await RefreshLibraryAsync();
        await LoadRecentExportsAsync();
        await LoadPresetsAsync();
        await LoadStyleProfilesAsync();
    }

    [RelayCommand]
    private async Task RefreshLibraryAsync()
    {
        IsBusy = true;
        try
        {
            var selectedAssetId = SelectedAsset?.Id;

            Folders.Clear();
            foreach (var folder in await workflowService.GetFoldersAsync(CancellationToken.None))
            {
                Folders.Add(folder);
            }

            LibraryItems.Clear();
            foreach (var item in await workflowService.GetLibraryItemsAsync(CancellationToken.None))
            {
                LibraryItems.Add(item);
            }

            CatalogSummary = LibraryItems.Count == 0
                ? "Catalog is empty"
                : $"{LibraryItems.Count} assets across {Folders.Count} imported folders";

            SelectedLibraryItem = selectedAssetId is null
                ? LibraryItems.FirstOrDefault()
                : LibraryItems.FirstOrDefault(item => item.Asset.Id == selectedAssetId) ?? LibraryItems.FirstOrDefault();

            if (SelectedLibraryItem is null && LibraryItems.Count > 0)
            {
                SelectedLibraryItem = LibraryItems[0];
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ImportFolderAsync()
    {
        var folder = folderPickerService.PickFolder("Choose a folder to import into the Color Grader catalog");
        if (string.IsNullOrWhiteSpace(folder))
        {
            return;
        }

        IsBusy = true;
        StatusText = $"Importing {folder}...";

        try
        {
            var importedCount = await workflowService.ImportFolderAsync(folder, CancellationToken.None);
            StatusText = importedCount == 0
                ? "No supported JPG, PNG, or RAW files were found in that folder."
                : $"Imported or refreshed {importedCount} assets from {folder}.";

            await RefreshLibraryAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private Task EnhanceAsync() => LoadSelectedAssetAsync();

    [RelayCommand]
    private void ResetFineTune()
    {
        _suppressPreviewRefresh = true;
        ManualExposure = 0;
        ManualContrast = 0;
        ManualWarmth = 0;
        ManualSaturation = 0;
        ManualVibrance = 0;
        ManualHighlightRecovery = 0;
        ManualShadowLift = 0;
        ManualSkinSoftening = 0;
        ManualDenoise = 0;
        ManualSharpen = 0;
        ManualDetailRecovery = 0;
        ManualDeblur = 0;
        ManualArtifactReduction = 0;
        ManualRealismBoost = 0;
        _suppressPreviewRefresh = false;
        _ = LoadSelectedAssetAsync();
    }

    [RelayCommand]
    private void ResetCropStraighten()
    {
        _suppressPreviewRefresh = true;
        StraightenAngle = 0;
        CropLeft = 0;
        CropTop = 0;
        CropWidth = 1;
        CropHeight = 1;
        _suppressPreviewRefresh = false;
        _ = LoadSelectedAssetAsync();
    }

    [RelayCommand]
    private void ResetLocalizedMask()
    {
        _suppressPreviewRefresh = true;
        LocalizedMaskEnabled = false;
        SelectedLocalizedMaskKind = LocalizedMaskKind.Radial;
        LocalizedMaskCenterX = 0.5;
        LocalizedMaskCenterY = 0.5;
        LocalizedMaskWidth = 0.55;
        LocalizedMaskHeight = 0.55;
        LocalizedMaskFeather = 0.25;
        LocalizedMaskAngle = 0;
        InvertLocalizedMask = false;
        LocalizedMaskIntensity = 0.85;
        LocalizedExposure = 0;
        LocalizedContrast = 0;
        LocalizedWarmth = 0;
        LocalizedSaturation = 0;
        LocalizedVibrance = 0;
        LocalizedSkinSoftening = 0;
        LocalizedDenoise = 0;
        LocalizedSharpen = 0;
        _suppressPreviewRefresh = false;
        _ = LoadSelectedAssetAsync();
    }

    [RelayCommand]
    private async Task SavePresetAsync()
    {
        var normalizedName = string.IsNullOrWhiteSpace(NewPresetName)
            ? BuildDefaultPresetName()
            : NewPresetName.Trim();

        var preset = new SavedPreset(
            SelectedPreset?.Name.Equals(normalizedName, StringComparison.OrdinalIgnoreCase) == true
                ? SelectedPreset.Id
                : 0,
            normalizedName,
            BuildFeatureMask(),
            Math.Clamp(EnhancementStrength, 0.0, 1.0),
            BuildManualAdjustments(),
            BuildCropStraightenSettings(),
            BuildLocalizedMaskSettings(),
            DateTimeOffset.UtcNow);

        await workflowService.SavePresetAsync(preset, CancellationToken.None);
        NewPresetName = normalizedName;
        StatusText = $"Saved preset \"{normalizedName}\".";
        await LoadPresetsAsync(normalizedName);
    }

    [RelayCommand]
    private void ApplySelectedPreset()
    {
        if (SelectedPreset is null)
        {
            StatusText = "Choose a saved preset before applying it.";
            return;
        }

        ApplyPresetToEditor(SelectedPreset);
        NewPresetName = SelectedPreset.Name;
        StatusText = $"Applied preset \"{SelectedPreset.Name}\".";
        _ = LoadSelectedAssetAsync();
    }

    [RelayCommand]
    private async Task DeleteSelectedPresetAsync()
    {
        if (SelectedPreset is null)
        {
            StatusText = "Choose a saved preset before deleting it.";
            return;
        }

        var presetName = SelectedPreset.Name;
        await workflowService.DeletePresetAsync(SelectedPreset.Id, CancellationToken.None);
        StatusText = $"Deleted preset \"{presetName}\".";
        SelectedPreset = null;

        if (string.Equals(NewPresetName, presetName, StringComparison.OrdinalIgnoreCase))
        {
            NewPresetName = string.Empty;
        }

        await LoadPresetsAsync();
    }

    [RelayCommand]
    private async Task SaveStyleProfileAsync()
    {
        var normalizedName = string.IsNullOrWhiteSpace(NewStyleProfileName)
            ? BuildDefaultStyleProfileName()
            : NewStyleProfileName.Trim();

        await workflowService.SaveStyleProfileAsync(
            new StyleProfile(0, normalizedName, 0, 0, DateTimeOffset.UtcNow, null),
            CancellationToken.None);

        NewStyleProfileName = normalizedName;
        StatusText = $"Saved style profile \"{normalizedName}\".";
        await LoadStyleProfilesAsync(normalizedName);
    }

    [RelayCommand]
    private void UseCatalogStyleHistory()
    {
        SelectedStyleProfile = null;
        StatusText = "Using overall catalog history for style learning.";
    }

    [RelayCommand]
    private async Task DeleteSelectedStyleProfileAsync()
    {
        if (SelectedStyleProfile is null)
        {
            StatusText = "Choose a style profile before deleting it.";
            return;
        }

        var profileName = SelectedStyleProfile.Name;
        await workflowService.DeleteStyleProfileAsync(SelectedStyleProfile.Id, CancellationToken.None);
        SelectedStyleProfile = null;

        if (string.Equals(NewStyleProfileName, profileName, StringComparison.OrdinalIgnoreCase))
        {
            NewStyleProfileName = string.Empty;
        }

        StatusText = $"Deleted style profile \"{profileName}\". Feedback examples remain in catalog history.";
        await LoadStyleProfilesAsync();
    }

    [RelayCommand]
    private async Task AcceptAsync()
    {
        if (SelectedAsset is null || CurrentSuggestion is null)
        {
            return;
        }

        await workflowService.SaveFeedbackAsync(
            SelectedAsset,
            FeedbackDisposition.Accepted,
            CurrentSuggestion,
            SelectedStyleProfile?.Id,
            CancellationToken.None);

        await LoadStyleProfilesAsync(SelectedStyleProfile?.Name);
        StatusText = SelectedStyleProfile is null
            ? "Saved this enhancement as an accepted catalog style example."
            : $"Saved this enhancement as an accepted example for style profile \"{SelectedStyleProfile.Name}\".";
    }

    [RelayCommand]
    private async Task DeclineAsync()
    {
        if (SelectedAsset is null || CurrentSuggestion is null)
        {
            return;
        }

        await workflowService.SaveFeedbackAsync(
            SelectedAsset,
            FeedbackDisposition.Declined,
            CurrentSuggestion,
            SelectedStyleProfile?.Id,
            CancellationToken.None);

        await LoadStyleProfilesAsync(SelectedStyleProfile?.Name);
        StatusText = SelectedStyleProfile is null
            ? "Saved this enhancement as a declined catalog style example."
            : $"Saved this enhancement as a declined example for style profile \"{SelectedStyleProfile.Name}\".";
    }

    [RelayCommand]
    private void ChooseExportFolder()
    {
        var folder = folderPickerService.PickFolder("Choose an export folder");
        if (!string.IsNullOrWhiteSpace(folder))
        {
            ExportFolder = folder;
            StatusText = $"Export folder set to {folder}";
        }
    }

    [RelayCommand]
    private async Task ExportSelectedAsync()
    {
        if (SelectedAsset is null)
        {
            StatusText = "Select a photo first if you want to export only one item.";
            return;
        }

        await RunExportQueueAsync([SelectedAsset]);
    }

    [RelayCommand]
    private async Task ExportAllAsync()
    {
        if (LibraryItems.Count == 0)
        {
            StatusText = "The catalog is empty. Import a folder before exporting.";
            return;
        }

        await RunExportQueueAsync(LibraryItems.Select(item => item.Asset).ToList());
    }

    [RelayCommand]
    private void CancelExports()
    {
        _exportCancellationSource?.Cancel();
        ExportProgressText = "Cancelling export queue...";
    }

    partial void OnSelectedLibraryItemChanged(CatalogAssetListItem? value)
    {
        SelectedAsset = value?.Asset;
    }

    partial void OnSelectedAssetChanged(CatalogAsset? value)
    {
        _ = LoadSelectedAssetAsync();
    }

    partial void OnSelectedPresetChanged(SavedPreset? value)
    {
        if (value is not null)
        {
            NewPresetName = value.Name;
        }
    }

    partial void OnSelectedStyleProfileChanged(StyleProfile? value)
    {
        if (value is not null)
        {
            NewStyleProfileName = value.Name;
        }

        TriggerPreviewRefresh();
    }

    private async Task LoadSelectedAssetAsync()
    {
        if (SelectedAsset is null)
        {
            return;
        }

        _editorCancellationSource?.Cancel();
        _editorCancellationSource?.Dispose();
        _editorCancellationSource = new CancellationTokenSource();

        IsBusy = true;
        PreviewNotice = "Rendering preview...";
        SelectedAssetSummary = $"{SelectedAsset.Kind} | {SelectedAsset.FileName}";

        try
        {
            var document = await workflowService.BuildEditorDocumentAsync(
                SelectedAsset,
                BuildFeatureMask(),
                EnhancementStrength,
                BuildManualAdjustments(),
                BuildCropStraightenSettings(),
                BuildLocalizedMaskSettings(),
                SelectedStyleProfile?.Id,
                _editorCancellationSource.Token);

            OriginalPreview = PreviewBitmapFactory.Create(document.OriginalPreview);
            EnhancedPreview = PreviewBitmapFactory.Create(document.EnhancedPreview);
            PreviewNotice = string.IsNullOrWhiteSpace(document.Notice)
                ? "Preview rendered locally on your machine."
                : document.Notice;
            SuggestionRationale = document.Suggestion.Rationale;
            ConfidenceText = $"Confidence {document.Suggestion.Confidence:P0}";
            InferenceStatusText = document.InferenceStatus;
            CurrentSuggestion = document.Suggestion;
            StatusText = $"Ready: {SelectedAsset.FileName}";
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task RunExportQueueAsync(IReadOnlyList<CatalogAsset> assetsToExport)
    {
        if (!EnsureExportFolder())
        {
            return;
        }

        ExportQueueItems.Clear();
        foreach (var asset in assetsToExport)
        {
            ExportQueueItems.Add(new ExportQueueItemViewModel(asset.FileName));
        }

        _exportCancellationSource?.Cancel();
        _exportCancellationSource?.Dispose();
        _exportCancellationSource = new CancellationTokenSource();

        IsExporting = true;
        StatusText = $"Starting export queue for {assetsToExport.Count} item(s)...";

        try
        {
            var preset = new ExportPreset(
                ExportFolder,
                SelectedExportFormat,
                Math.Clamp(JpegQuality, 50, 100),
                Math.Clamp(ExportLongEdgePixels, 1200, 8000));

            for (var index = 0; index < assetsToExport.Count; index++)
            {
                _exportCancellationSource.Token.ThrowIfCancellationRequested();

                var asset = assetsToExport[index];
                var queueItem = ExportQueueItems[index];
                queueItem.Status = "Rendering";
                queueItem.Detail = "Applying enhancement and writing output file...";

                var result = await workflowService.ExportAssetAsync(
                    asset,
                    BuildFeatureMask(),
                    EnhancementStrength,
                    BuildManualAdjustments(),
                    BuildCropStraightenSettings(),
                    BuildLocalizedMaskSettings(),
                    preset,
                    SelectedStyleProfile?.Id,
                    _exportCancellationSource.Token);

                queueItem.Status = result.Status.ToString();
                queueItem.Detail = result.Succeeded
                    ? result.OutputPath ?? result.Message
                    : result.Message;

                ExportProgressText = $"{index + 1}/{assetsToExport.Count} processed";
            }

            StatusText = $"Finished export queue for {assetsToExport.Count} item(s).";
            await LoadRecentExportsAsync();
        }
        catch (OperationCanceledException)
        {
            StatusText = "Export queue cancelled.";
            ExportProgressText = "Cancelled";
            await LoadRecentExportsAsync();
        }
        finally
        {
            IsExporting = false;
            _exportCancellationSource?.Dispose();
            _exportCancellationSource = null;
        }
    }

    private async Task LoadRecentExportsAsync()
    {
        RecentExports.Clear();
        foreach (var entry in await workflowService.GetRecentExportHistoryAsync(12, CancellationToken.None))
        {
            RecentExports.Add(new RecentExportItemViewModel(
                entry.FileName,
                entry.Status,
                entry.OutputPath ?? entry.Message,
                entry.CreatedAt.LocalDateTime.ToString("g")));
        }
    }

    private async Task LoadPresetsAsync(string? preferredPresetName = null)
    {
        var previouslySelectedPresetId = SelectedPreset?.Id;

        Presets.Clear();
        foreach (var preset in await workflowService.GetPresetsAsync(CancellationToken.None))
        {
            Presets.Add(preset);
        }

        SelectedPreset = preferredPresetName is null
            ? previouslySelectedPresetId is null
                ? Presets.FirstOrDefault()
                : Presets.FirstOrDefault(preset => preset.Id == previouslySelectedPresetId) ?? Presets.FirstOrDefault()
            : Presets.FirstOrDefault(preset => string.Equals(preset.Name, preferredPresetName, StringComparison.OrdinalIgnoreCase))
              ?? Presets.FirstOrDefault();
    }

    private async Task LoadStyleProfilesAsync(string? preferredProfileName = null)
    {
        var previouslySelectedProfileId = SelectedStyleProfile?.Id;

        StyleProfiles.Clear();
        foreach (var profile in await workflowService.GetStyleProfilesAsync(CancellationToken.None))
        {
            StyleProfiles.Add(profile);
        }

        SelectedStyleProfile = preferredProfileName is null
            ? previouslySelectedProfileId is null
                ? null
                : StyleProfiles.FirstOrDefault(profile => profile.Id == previouslySelectedProfileId)
            : StyleProfiles.FirstOrDefault(profile => string.Equals(profile.Name, preferredProfileName, StringComparison.OrdinalIgnoreCase));
    }

    private bool EnsureExportFolder()
    {
        if (Directory.Exists(ExportFolder))
        {
            return true;
        }

        var folder = folderPickerService.PickFolder("Choose an export folder");
        if (string.IsNullOrWhiteSpace(folder))
        {
            StatusText = "Export cancelled because no export folder was selected.";
            return false;
        }

        ExportFolder = folder;
        return true;
    }

    private EnhancementFeature BuildFeatureMask()
    {
        var features = EnhancementFeature.None;

        if (AutoExposure) features |= EnhancementFeature.AutoExposure;
        if (WhiteBalance) features |= EnhancementFeature.WhiteBalance;
        if (Contrast) features |= EnhancementFeature.Contrast;
        if (ToneCurve) features |= EnhancementFeature.ToneCurve;
        if (SkinTone) features |= EnhancementFeature.SkinTone;
        if (Denoise) features |= EnhancementFeature.Denoise;
        if (Sharpen) features |= EnhancementFeature.Sharpen;
        if (Upscale) features |= EnhancementFeature.Upscale;
        if (StyleLearning) features |= EnhancementFeature.StyleLearning;
        if (QualityRestore) features |= EnhancementFeature.QualityRestore;

        return features;
    }

    private string BuildDefaultPresetName() => $"Preset {DateTime.Now:yyyy-MM-dd HHmm}";

    private string BuildDefaultStyleProfileName() => $"Style {DateTime.Now:yyyy-MM-dd HHmm}";

    private async Task EnsureBuiltInPresetsAsync()
    {
        var existingPresets = await workflowService.GetPresetsAsync(CancellationToken.None);
        if (existingPresets.Any(preset => string.Equals(preset.Name, NeonClubReferencePresetName, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        await workflowService.SavePresetAsync(BuildNeonClubReferencePreset(), CancellationToken.None);
    }

    private static SavedPreset BuildNeonClubReferencePreset()
    {
        var featureMask =
            EnhancementFeature.AutoExposure |
            EnhancementFeature.WhiteBalance |
            EnhancementFeature.Contrast |
            EnhancementFeature.ToneCurve |
            EnhancementFeature.SkinTone |
            EnhancementFeature.Denoise |
            EnhancementFeature.Sharpen |
            EnhancementFeature.StyleLearning |
            EnhancementFeature.QualityRestore;

        return new SavedPreset(
            Id: 0,
            Name: NeonClubReferencePresetName,
            FeatureMask: featureMask,
            Strength: 1.0,
            ManualAdjustments: new ManualEnhancementAdjustments(
                Exposure: 0.18,
                Contrast: 0.16,
                Warmth: 0.11,
                Saturation: 0.10,
                Vibrance: 0.16,
                HighlightRecovery: 0.08,
                ShadowLift: -0.04,
                SkinSoftening: 0.04,
                Denoise: 0.08,
                Sharpen: 0.10,
                DetailRecovery: 0.14,
                Deblur: 0.10,
                ArtifactReduction: 0.06,
                RealismBoost: 0.14),
            CropStraighten: CropStraightenSettings.Default,
            LocalizedMask: new LocalizedMaskSettings(
                IsEnabled: true,
                Kind: LocalizedMaskKind.Subject,
                CenterX: 0.5,
                CenterY: 0.5,
                Width: 0.70,
                Height: 0.88,
                Feather: 0.22,
                AngleDegrees: 0,
                Invert: false,
                Intensity: 0.88,
                Adjustments: new ManualEnhancementAdjustments(
                    Exposure: 0.12,
                    Contrast: 0.06,
                    Warmth: 0.05,
                    Saturation: 0.03,
                    Vibrance: 0.07,
                    HighlightRecovery: 0,
                    ShadowLift: 0,
                    SkinSoftening: 0.03,
                    Denoise: 0.03,
                    Sharpen: 0.05,
                    DetailRecovery: 0,
                    Deblur: 0,
                    ArtifactReduction: 0,
                    RealismBoost: 0),
            CreatedAt: DateTimeOffset.UtcNow);
    }

    public void BeginInteractiveCanvasEdit() => _suppressPreviewRefresh = true;

    public void EndInteractiveCanvasEdit(bool refreshPreview = true)
    {
        _suppressPreviewRefresh = false;
        if (refreshPreview)
        {
            TriggerPreviewRefresh();
        }
    }

    private CropStraightenSettings BuildCropStraightenSettings() => new(
        RotationDegrees: StraightenAngle,
        CropLeft: CropLeft,
        CropTop: CropTop,
        CropWidth: CropWidth,
        CropHeight: CropHeight);

    public CropStraightenSettings GetCropStraightenSettings() => BuildCropStraightenSettings();

    public bool IsPointInsideCrop(double normalizedX, double normalizedY)
    {
        var crop = BuildCropStraightenSettings();
        if (crop.IsIdentity)
        {
            return false;
        }

        return normalizedX >= crop.CropLeft &&
               normalizedX <= crop.CropLeft + crop.CropWidth &&
               normalizedY >= crop.CropTop &&
               normalizedY <= crop.CropTop + crop.CropHeight;
    }

    public void SetCropRectangleFromCanvas(double startX, double startY, double endX, double endY)
    {
        const double minimumSize = 0.03;

        var normalizedStartX = Math.Clamp(startX, 0.0, 1.0);
        var normalizedStartY = Math.Clamp(startY, 0.0, 1.0);
        var normalizedEndX = Math.Clamp(endX, 0.0, 1.0);
        var normalizedEndY = Math.Clamp(endY, 0.0, 1.0);

        var left = Math.Min(normalizedStartX, normalizedEndX);
        var top = Math.Min(normalizedStartY, normalizedEndY);
        var width = Math.Max(minimumSize, Math.Abs(normalizedEndX - normalizedStartX));
        var height = Math.Max(minimumSize, Math.Abs(normalizedEndY - normalizedStartY));

        CropWidth = Math.Clamp(width, minimumSize, 1.0);
        CropHeight = Math.Clamp(height, minimumSize, 1.0);
        CropLeft = Math.Clamp(left, 0.0, 1.0 - CropWidth);
        CropTop = Math.Clamp(top, 0.0, 1.0 - CropHeight);
    }

    public void MoveCropRectangleFromCanvas(double baseLeft, double baseTop, double deltaX, double deltaY)
    {
        var clampedWidth = Math.Clamp(CropWidth, 0.03, 1.0);
        var clampedHeight = Math.Clamp(CropHeight, 0.03, 1.0);
        CropLeft = Math.Clamp(baseLeft + deltaX, 0.0, 1.0 - clampedWidth);
        CropTop = Math.Clamp(baseTop + deltaY, 0.0, 1.0 - clampedHeight);
    }

    private ManualEnhancementAdjustments BuildManualAdjustments() => new(
        Exposure: ManualExposure,
        Contrast: ManualContrast,
        Warmth: ManualWarmth,
        Saturation: ManualSaturation,
        Vibrance: ManualVibrance,
        HighlightRecovery: ManualHighlightRecovery,
        ShadowLift: ManualShadowLift,
        SkinSoftening: ManualSkinSoftening,
        Denoise: ManualDenoise,
        Sharpen: ManualSharpen,
        DetailRecovery: ManualDetailRecovery,
        Deblur: ManualDeblur,
        ArtifactReduction: ManualArtifactReduction,
        RealismBoost: ManualRealismBoost);

    private LocalizedMaskSettings BuildLocalizedMaskSettings() => new(
        IsEnabled: LocalizedMaskEnabled,
        Kind: SelectedLocalizedMaskKind,
        CenterX: LocalizedMaskCenterX,
        CenterY: LocalizedMaskCenterY,
        Width: LocalizedMaskWidth,
        Height: LocalizedMaskHeight,
        Feather: LocalizedMaskFeather,
        AngleDegrees: LocalizedMaskAngle,
        Invert: InvertLocalizedMask,
        Intensity: LocalizedMaskIntensity,
        Adjustments: new ManualEnhancementAdjustments(
            Exposure: LocalizedExposure,
            Contrast: LocalizedContrast,
            Warmth: LocalizedWarmth,
            Saturation: LocalizedSaturation,
            Vibrance: LocalizedVibrance,
            HighlightRecovery: 0,
            ShadowLift: 0,
            SkinSoftening: LocalizedSkinSoftening,
            Denoise: LocalizedDenoise,
            Sharpen: LocalizedSharpen,
            DetailRecovery: 0,
            Deblur: 0,
            ArtifactReduction: 0,
            RealismBoost: 0));

    public void MoveLocalizedMaskCenterFromCanvas(double normalizedX, double normalizedY)
    {
        LocalizedMaskCenterX = Math.Clamp(normalizedX, 0.0, 1.0);
        LocalizedMaskCenterY = Math.Clamp(normalizedY, 0.0, 1.0);
    }

    public void AdjustLocalizedMaskSize(double delta)
    {
        LocalizedMaskWidth = Math.Clamp(LocalizedMaskWidth + delta, 0.15, 1.0);
        LocalizedMaskHeight = Math.Clamp(LocalizedMaskHeight + delta, 0.15, 1.0);
    }

    private void ApplyPresetToEditor(SavedPreset preset)
    {
        _suppressPreviewRefresh = true;

        AutoExposure = preset.FeatureMask.HasFlag(EnhancementFeature.AutoExposure);
        WhiteBalance = preset.FeatureMask.HasFlag(EnhancementFeature.WhiteBalance);
        Contrast = preset.FeatureMask.HasFlag(EnhancementFeature.Contrast);
        ToneCurve = preset.FeatureMask.HasFlag(EnhancementFeature.ToneCurve);
        SkinTone = preset.FeatureMask.HasFlag(EnhancementFeature.SkinTone);
        Denoise = preset.FeatureMask.HasFlag(EnhancementFeature.Denoise);
        Sharpen = preset.FeatureMask.HasFlag(EnhancementFeature.Sharpen);
        Upscale = preset.FeatureMask.HasFlag(EnhancementFeature.Upscale);
        StyleLearning = preset.FeatureMask.HasFlag(EnhancementFeature.StyleLearning);
        QualityRestore = preset.FeatureMask.HasFlag(EnhancementFeature.QualityRestore);
        EnhancementStrength = preset.Strength;
        ManualExposure = preset.ManualAdjustments.Exposure;
        ManualContrast = preset.ManualAdjustments.Contrast;
        ManualWarmth = preset.ManualAdjustments.Warmth;
        ManualSaturation = preset.ManualAdjustments.Saturation;
        ManualVibrance = preset.ManualAdjustments.Vibrance;
        ManualHighlightRecovery = preset.ManualAdjustments.HighlightRecovery;
        ManualShadowLift = preset.ManualAdjustments.ShadowLift;
        ManualSkinSoftening = preset.ManualAdjustments.SkinSoftening;
        ManualDenoise = preset.ManualAdjustments.Denoise;
        ManualSharpen = preset.ManualAdjustments.Sharpen;
        ManualDetailRecovery = preset.ManualAdjustments.DetailRecovery;
        ManualDeblur = preset.ManualAdjustments.Deblur;
        ManualArtifactReduction = preset.ManualAdjustments.ArtifactReduction;
        ManualRealismBoost = preset.ManualAdjustments.RealismBoost;
        StraightenAngle = preset.CropStraighten.RotationDegrees;
        CropLeft = preset.CropStraighten.CropLeft;
        CropTop = preset.CropStraighten.CropTop;
        CropWidth = preset.CropStraighten.CropWidth;
        CropHeight = preset.CropStraighten.CropHeight;
        LocalizedMaskEnabled = preset.LocalizedMask.IsEnabled;
        SelectedLocalizedMaskKind = preset.LocalizedMask.Kind;
        LocalizedMaskCenterX = preset.LocalizedMask.CenterX;
        LocalizedMaskCenterY = preset.LocalizedMask.CenterY;
        LocalizedMaskWidth = preset.LocalizedMask.Width;
        LocalizedMaskHeight = preset.LocalizedMask.Height;
        LocalizedMaskFeather = preset.LocalizedMask.Feather;
        LocalizedMaskAngle = preset.LocalizedMask.AngleDegrees;
        InvertLocalizedMask = preset.LocalizedMask.Invert;
        LocalizedMaskIntensity = preset.LocalizedMask.Intensity;
        LocalizedExposure = preset.LocalizedMask.Adjustments.Exposure;
        LocalizedContrast = preset.LocalizedMask.Adjustments.Contrast;
        LocalizedWarmth = preset.LocalizedMask.Adjustments.Warmth;
        LocalizedSaturation = preset.LocalizedMask.Adjustments.Saturation;
        LocalizedVibrance = preset.LocalizedMask.Adjustments.Vibrance;
        LocalizedSkinSoftening = preset.LocalizedMask.Adjustments.SkinSoftening;
        LocalizedDenoise = preset.LocalizedMask.Adjustments.Denoise;
        LocalizedSharpen = preset.LocalizedMask.Adjustments.Sharpen;

        _suppressPreviewRefresh = false;
    }

    partial void OnAutoExposureChanged(bool value) => TriggerPreviewRefresh();
    partial void OnWhiteBalanceChanged(bool value) => TriggerPreviewRefresh();
    partial void OnContrastChanged(bool value) => TriggerPreviewRefresh();
    partial void OnToneCurveChanged(bool value) => TriggerPreviewRefresh();
    partial void OnSkinToneChanged(bool value) => TriggerPreviewRefresh();
    partial void OnDenoiseChanged(bool value) => TriggerPreviewRefresh();
    partial void OnSharpenChanged(bool value) => TriggerPreviewRefresh();
    partial void OnUpscaleChanged(bool value) => TriggerPreviewRefresh();
    partial void OnStyleLearningChanged(bool value) => TriggerPreviewRefresh();
    partial void OnQualityRestoreChanged(bool value) => TriggerPreviewRefresh();
    partial void OnEnhancementStrengthChanged(double value) => TriggerPreviewRefresh();
    partial void OnManualExposureChanged(double value) => TriggerPreviewRefresh();
    partial void OnManualContrastChanged(double value) => TriggerPreviewRefresh();
    partial void OnManualWarmthChanged(double value) => TriggerPreviewRefresh();
    partial void OnManualSaturationChanged(double value) => TriggerPreviewRefresh();
    partial void OnManualVibranceChanged(double value) => TriggerPreviewRefresh();
    partial void OnManualHighlightRecoveryChanged(double value) => TriggerPreviewRefresh();
    partial void OnManualShadowLiftChanged(double value) => TriggerPreviewRefresh();
    partial void OnManualSkinSofteningChanged(double value) => TriggerPreviewRefresh();
    partial void OnManualDenoiseChanged(double value) => TriggerPreviewRefresh();
    partial void OnManualSharpenChanged(double value) => TriggerPreviewRefresh();
    partial void OnManualDetailRecoveryChanged(double value) => TriggerPreviewRefresh();
    partial void OnManualDeblurChanged(double value) => TriggerPreviewRefresh();
    partial void OnManualArtifactReductionChanged(double value) => TriggerPreviewRefresh();
    partial void OnManualRealismBoostChanged(double value) => TriggerPreviewRefresh();
    partial void OnStraightenAngleChanged(double value) => TriggerPreviewRefresh();
    partial void OnCropLeftChanged(double value) => TriggerPreviewRefresh();
    partial void OnCropTopChanged(double value) => TriggerPreviewRefresh();
    partial void OnCropWidthChanged(double value) => TriggerPreviewRefresh();
    partial void OnCropHeightChanged(double value) => TriggerPreviewRefresh();
    partial void OnLocalizedMaskEnabledChanged(bool value) => TriggerPreviewRefresh();
    partial void OnSelectedLocalizedMaskKindChanged(LocalizedMaskKind value) => TriggerPreviewRefresh();
    partial void OnLocalizedMaskCenterXChanged(double value) => TriggerPreviewRefresh();
    partial void OnLocalizedMaskCenterYChanged(double value) => TriggerPreviewRefresh();
    partial void OnLocalizedMaskWidthChanged(double value) => TriggerPreviewRefresh();
    partial void OnLocalizedMaskHeightChanged(double value) => TriggerPreviewRefresh();
    partial void OnLocalizedMaskFeatherChanged(double value) => TriggerPreviewRefresh();
    partial void OnLocalizedMaskAngleChanged(double value) => TriggerPreviewRefresh();
    partial void OnInvertLocalizedMaskChanged(bool value) => TriggerPreviewRefresh();
    partial void OnLocalizedMaskIntensityChanged(double value) => TriggerPreviewRefresh();
    partial void OnLocalizedExposureChanged(double value) => TriggerPreviewRefresh();
    partial void OnLocalizedContrastChanged(double value) => TriggerPreviewRefresh();
    partial void OnLocalizedWarmthChanged(double value) => TriggerPreviewRefresh();
    partial void OnLocalizedSaturationChanged(double value) => TriggerPreviewRefresh();
    partial void OnLocalizedVibranceChanged(double value) => TriggerPreviewRefresh();
    partial void OnLocalizedSkinSofteningChanged(double value) => TriggerPreviewRefresh();
    partial void OnLocalizedDenoiseChanged(double value) => TriggerPreviewRefresh();
    partial void OnLocalizedSharpenChanged(double value) => TriggerPreviewRefresh();

    private void TriggerPreviewRefresh()
    {
        if (_suppressPreviewRefresh)
        {
            return;
        }

        if (SelectedAsset is not null && !IsExporting)
        {
            _ = LoadSelectedAssetAsync();
        }
    }
}
