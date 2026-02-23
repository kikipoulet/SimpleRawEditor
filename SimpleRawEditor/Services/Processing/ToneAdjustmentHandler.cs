using System;
using System.Runtime.CompilerServices;

namespace SimpleRawEditor.Services.Processing;

public static class ToneAdjustmentHandler
{
    private const float Inv255 = 1.0f / 255.0f;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float ApplyToneCurve(float normalizedValue, float luminance, in PrecomputedAdjustments p)
    {
        float value = normalizedValue;

        if (p.HasExposure)
            value = ApplyExposure(value, p.ExposureFactor);

        if (p.HasHighlights)
            value = ApplyHighlights(value, luminance, p.HighlightsAmount);

        if (p.HasShadows)
            value = ApplyShadows(value, luminance, p.ShadowsAmount);

        if (p.HasContrast)
            value = ApplyContrastSCurve(value, p.ContrastAmount);

        return value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float ApplyExposure(float value, float factor)
    {
        float exposed = value * factor;
        return SoftRolloff(exposed);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float ApplyHighlights(float value, float luminance, float amount)
    {
        if (luminance < 0.5f)
            return value;

        float t = Smoothstep(0.5f, 1.0f, luminance);
        float effect = amount * 0.6f * t;

        if (amount > 0)
        {
            float compression = 1.0f - effect * (1.0f - value);
            return value * compression;
        }
        else
        {
            float recovery = -effect * value;
            float maxRecovery = 1.0f - value;
            return value + Math.Min(recovery * 0.3f, maxRecovery * 0.5f);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float ApplyShadows(float value, float luminance, float amount)
    {
        if (Math.Abs(amount) < 0.001f)
            return value;

        float x = value;

        if (amount > 0)
        {
            float lift = amount * 0.3f;
            float result = x + lift * x * (1.0f - x) * 4.0f;
            return Math.Min(result, 1.0f);
        }
        else
        {
            float crush = -amount * 0.3f;
            float result = x * (1.0f - crush * (1.0f - x));
            return Math.Max(result, 0.0f);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float ApplyContrastSCurve(float value, float amount)
    {
        if (Math.Abs(amount) < 0.001f)
            return value;

        float x = value;
        float contrast = amount * 0.11f;
        float result = x + contrast * (x - 0.5f) * (1.0f - x) * 4.0f;

        return Math.Clamp(result, 0.0f, 1.0f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Smoothstep(float edge0, float edge1, float x)
    {
        float t = Math.Clamp((x - edge0) / (edge1 - edge0), 0.0f, 1.0f);
        return t * t * (3.0f - 2.0f * t);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float SoftRolloff(float x)
    {
        if (x <= 0.0f) return 0.0f;
        if (x >= 1.0f)
        {
            float excess = x - 1.0f;
            return 1.0f + excess / (1.0f + excess * 2.0f) * 0.5f;
        }
        return x;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte ClampByte(float value)
    {
        if (value >= 255.0f) return 255;
        if (value <= 0.0f) return 0;
        return (byte)value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Lerp(float a, float b, float t) => a + (b - a) * t;
}
