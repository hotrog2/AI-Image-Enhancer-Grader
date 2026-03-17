namespace ColorGrader.Core.Models;

public sealed record EnhancementFeedback(
    Guid AssetId,
    FeedbackDisposition Outcome,
    EnhancementSettings Settings,
    DateTimeOffset CreatedAt,
    long? StyleProfileId = null);
