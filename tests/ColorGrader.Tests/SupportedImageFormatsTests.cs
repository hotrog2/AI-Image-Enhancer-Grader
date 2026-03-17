using ColorGrader.Core;
using ColorGrader.Core.Models;

namespace ColorGrader.Tests;

public sealed class SupportedImageFormatsTests
{
    [Theory]
    [InlineData(".jpg", AssetKind.Jpeg)]
    [InlineData(".jpeg", AssetKind.Jpeg)]
    [InlineData(".png", AssetKind.Png)]
    [InlineData(".cr3", AssetKind.Raw)]
    [InlineData(".arw", AssetKind.Raw)]
    public void GetAssetKind_ReturnsExpectedKind(string extension, AssetKind expectedKind)
    {
        var actual = SupportedImageFormats.GetAssetKind(extension);
        Assert.Equal(expectedKind, actual);
    }

    [Fact]
    public void IsSupported_RejectsUnknownExtensions()
    {
        Assert.False(SupportedImageFormats.IsSupported(".gif"));
    }
}
