namespace ColorGrader.Core.Models;

public sealed record StyleProfile(
    long Id,
    string Name,
    int AcceptedCount,
    int DeclinedCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastFeedbackAt)
{
    public int TotalExamples => AcceptedCount + DeclinedCount;
}
