using ColorGrader.Core.Models;

namespace ColorGrader.Imaging.Services;

public static class LocalizedMaskMath
{
    public static float GetMaskWeight(
        int x,
        int y,
        int width,
        int height,
        LocalizedMaskSettings settings,
        SubjectMaskPrediction? subjectMask)
    {
        if (!settings.HasVisibleEffect || width <= 0 || height <= 0)
        {
            return 0f;
        }

        var normalizedX = width == 1 ? 0.5 : x / (double)(width - 1);
        var normalizedY = height == 1 ? 0.5 : y / (double)(height - 1);

        var baseWeight = settings.Kind switch
        {
            LocalizedMaskKind.Radial => GetRadialWeight(normalizedX, normalizedY, settings),
            LocalizedMaskKind.Linear => GetLinearWeight(normalizedX, normalizedY, settings),
            LocalizedMaskKind.Subject => GetSubjectWeight(normalizedX, normalizedY, subjectMask),
            _ => 0.0
        };

        if (settings.Invert)
        {
            baseWeight = 1.0 - baseWeight;
        }

        return (float)Math.Clamp(baseWeight * Math.Clamp(settings.Intensity, 0.0, 1.0), 0.0, 1.0);
    }

    private static double GetRadialWeight(double x, double y, LocalizedMaskSettings settings)
    {
        var halfWidth = Math.Max(0.05, settings.Width / 2.0);
        var halfHeight = Math.Max(0.05, settings.Height / 2.0);
        var dx = (x - settings.CenterX) / halfWidth;
        var dy = (y - settings.CenterY) / halfHeight;
        var distance = Math.Sqrt((dx * dx) + (dy * dy));
        return 1.0 - SmoothEdge(distance, Math.Clamp(settings.Feather, 0.01, 0.95));
    }

    private static double GetLinearWeight(double x, double y, LocalizedMaskSettings settings)
    {
        var radians = settings.AngleDegrees * (Math.PI / 180.0);
        var translatedX = x - settings.CenterX;
        var translatedY = y - settings.CenterY;
        var projected = (translatedX * Math.Cos(radians)) + (translatedY * Math.Sin(radians));
        var bandWidth = Math.Max(0.05, settings.Width);
        var feather = Math.Clamp(settings.Feather, 0.01, 0.95);
        var normalized = ((projected / bandWidth) + 1.0) / 2.0;
        return SmoothStep(0.5 - feather, 0.5 + feather, normalized);
    }

    private static double GetSubjectWeight(double x, double y, SubjectMaskPrediction? subjectMask)
    {
        if (subjectMask is null || subjectMask.Width <= 0 || subjectMask.Height <= 0 || subjectMask.Values.Length == 0)
        {
            return 0.0;
        }

        var sampleX = Math.Clamp((int)Math.Round(x * (subjectMask.Width - 1)), 0, subjectMask.Width - 1);
        var sampleY = Math.Clamp((int)Math.Round(y * (subjectMask.Height - 1)), 0, subjectMask.Height - 1);
        var index = (sampleY * subjectMask.Width) + sampleX;
        return Math.Clamp(subjectMask.Values[index], 0f, 1f);
    }

    private static double SmoothEdge(double distance, double feather)
    {
        var edgeStart = Math.Max(0.01, 1.0 - feather);
        return SmoothStep(edgeStart, 1.0, distance);
    }

    private static double SmoothStep(double edge0, double edge1, double value)
    {
        if (edge0 >= edge1)
        {
            return value >= edge1 ? 1.0 : 0.0;
        }

        var t = Math.Clamp((value - edge0) / (edge1 - edge0), 0.0, 1.0);
        return t * t * (3.0 - (2.0 * t));
    }
}
