using ColorGrader.Core.Models;

namespace ColorGrader.Tests;

public sealed class EnhancementSettingsTests
{
    [Fact]
    public void ApplyFeatureMask_DisablesUnselectedFeatureValues()
    {
        var settings = new EnhancementSettings(0.2, 0.1, 0.1, 0.05, 0.06, 0.07, 0.08, 0.09, 0.1, 0.11, 2.0);
        var masked = settings.ApplyFeatureMask(EnhancementFeature.AutoExposure | EnhancementFeature.Sharpen);

        Assert.Equal(0.2, masked.Exposure);
        Assert.Equal(0.11, masked.Sharpen);
        Assert.Equal(0, masked.Contrast);
        Assert.Equal(0, masked.Denoise);
        Assert.Equal(1.0, masked.UpscaleFactor);
    }
}
