namespace ColorGrader.Core.Models;

public sealed record EnhancementSuggestion(
    EnhancementFeature EnabledFeatures,
    EnhancementSettings Settings,
    string Rationale,
    double Confidence);
