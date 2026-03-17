namespace ColorGrader.Core.Models;

[Flags]
public enum EnhancementFeature
{
    None = 0,
    AutoExposure = 1 << 0,
    WhiteBalance = 1 << 1,
    Contrast = 1 << 2,
    ToneCurve = 1 << 3,
    SkinTone = 1 << 4,
    Denoise = 1 << 5,
    Sharpen = 1 << 6,
    Upscale = 1 << 7,
    StyleLearning = 1 << 8,
    QualityRestore = 1 << 9
}
