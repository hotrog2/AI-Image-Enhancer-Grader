namespace ColorGrader.Core.Models;

public enum ExportJobStatus
{
    Queued = 1,
    Running = 2,
    Completed = 3,
    Failed = 4,
    Skipped = 5,
    Canceled = 6
}
