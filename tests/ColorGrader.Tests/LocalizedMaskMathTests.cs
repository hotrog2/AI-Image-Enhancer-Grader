using ColorGrader.Core.Models;
using ColorGrader.Imaging.Services;

namespace ColorGrader.Tests;

public sealed class LocalizedMaskMathTests
{
    [Fact]
    public void GetMaskWeight_RadialMaskPeaksNearCenter()
    {
        var settings = LocalizedMaskSettings.Default with
        {
            IsEnabled = true,
            Kind = LocalizedMaskKind.Radial,
            Adjustments = new ManualEnhancementAdjustments(0.2, 0, 0, 0, 0, 0, 0, 0, 0, 0)
        };

        var centerWeight = LocalizedMaskMath.GetMaskWeight(50, 50, 100, 100, settings, null);
        var cornerWeight = LocalizedMaskMath.GetMaskWeight(0, 0, 100, 100, settings, null);

        Assert.True(centerWeight > 0.7f);
        Assert.True(cornerWeight < 0.05f);
    }

    [Fact]
    public void GetMaskWeight_SubjectMaskUsesPredictionValues()
    {
        var settings = LocalizedMaskSettings.Default with
        {
            IsEnabled = true,
            Kind = LocalizedMaskKind.Subject,
            Intensity = 1.0,
            Adjustments = new ManualEnhancementAdjustments(0.2, 0, 0, 0, 0, 0, 0, 0, 0, 0)
        };

        var prediction = new SubjectMaskPrediction(2, 2, [0f, 1f, 0f, 0f]);
        var weight = LocalizedMaskMath.GetMaskWeight(1, 0, 2, 2, settings, prediction);

        Assert.Equal(1f, weight);
    }
}
