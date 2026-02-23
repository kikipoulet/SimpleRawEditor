using System;
using System.Runtime.CompilerServices;

namespace SimpleRawEditor.Services.Processing;

public readonly struct PrecomputedAdjustments
{
    public readonly float ExposureFactor;
    public readonly float ContrastAmount;
    public readonly float ShadowsAmount;
    public readonly float HighlightsAmount;
    public readonly float DenoiseStrength;
    public readonly float VignetteIntensity;
    public readonly float VignetteSpread;
    public readonly bool HasExposure;
    public readonly bool HasContrast;
    public readonly bool HasShadows;
    public readonly bool HasHighlights;
    public readonly bool HasDenoise;
    public readonly bool HasVignette;
    public readonly Models.CubeLut? Lut;
    public readonly float LutIntensity;
    public readonly bool HasLut;

    public PrecomputedAdjustments(
        float exposureFactor, float contrastAmount,
        float shadowsAmount, float highlightsAmount, float denoiseStrength,
        float vignetteIntensity, float vignetteSpread,
        bool hasExposure, bool hasContrast, bool hasShadows, bool hasHighlights,
        bool hasDenoise, bool hasVignette,
        Models.CubeLut? lut, float lutIntensity, bool hasLut)
    {
        ExposureFactor = exposureFactor;
        ContrastAmount = contrastAmount;
        ShadowsAmount = shadowsAmount;
        HighlightsAmount = highlightsAmount;
        DenoiseStrength = denoiseStrength;
        VignetteIntensity = vignetteIntensity;
        VignetteSpread = vignetteSpread;
        HasExposure = hasExposure;
        HasContrast = hasContrast;
        HasShadows = hasShadows;
        HasHighlights = hasHighlights;
        HasDenoise = hasDenoise;
        HasVignette = hasVignette;
        Lut = lut;
        LutIntensity = lutIntensity;
        HasLut = hasLut;
    }
}

public static class AdjustmentPrecompute
{
    private const float Inv255 = 1.0f / 255.0f;
    
    public static PrecomputedAdjustments FromImageAdjustments(Models.ImageAdjustments adjustments)
    {
        float exposureEV = (float)(adjustments.Exposure / 100.0);
        float exposureFactor = (float)Math.Pow(2, exposureEV * 0.5f);
        float contrast = (float)(adjustments.Contrast / 100.0);
        float shadows = (float)(adjustments.Shadows / 100.0);
        float highlights = (float)(adjustments.Highlights / 100.0);
        float denoiseAmount = (float)adjustments.DenoiseAmount;
        float denoiseStrength = denoiseAmount * 2.5f;
        float vignetteIntensity = (float)(adjustments.VignetteIntensity / 100.0);
        float vignetteSpread = (float)(adjustments.VignetteSpread / 100.0);

        var lut = adjustments.ActiveLut;
        float lutIntensity = (float)adjustments.LutIntensity;
        bool hasLut = adjustments.IsLutEnabled && lut != null && lutIntensity > 0.001f;
        bool hasDenoise = adjustments.IsDenoiseEnabled && denoiseAmount > 0.001f;
        bool hasVignette = adjustments.IsVignetteEnabled && Math.Abs(vignetteIntensity) > 0.001f;

        return new PrecomputedAdjustments(
            exposureFactor,
            contrast,
            shadows,
            highlights,
            denoiseStrength,
            vignetteIntensity,
            vignetteSpread,
            Math.Abs(exposureEV) > 0.001f,
            Math.Abs(contrast) > 0.001f,
            Math.Abs(shadows) > 0.001f,
            Math.Abs(highlights) > 0.001f,
            hasDenoise,
            hasVignette,
            lut,
            lutIntensity,
            hasLut
        );
    }
}
