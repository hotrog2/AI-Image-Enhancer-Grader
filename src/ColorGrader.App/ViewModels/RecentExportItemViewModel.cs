using ColorGrader.Core.Models;

namespace ColorGrader.App.ViewModels;

public sealed record RecentExportItemViewModel(
    string FileName,
    ExportJobStatus Status,
    string Detail,
    string TimestampText);
