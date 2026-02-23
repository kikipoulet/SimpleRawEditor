using System;
using System.Runtime.CompilerServices;
using SimpleRawEditor.Models;

namespace SimpleRawEditor.Services.Processing;

public static class LutApplicationHandler
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ApplyLutInline(ref float r, ref float g, ref float b, in PrecomputedAdjustments p)
    {
        var lut = p.Lut!;
        int size = lut.Size;
        float[] data = lut.Data;

        float nR = ToneAdjustmentHandler.Clamp01(r / 255.0f);
        float nG = ToneAdjustmentHandler.Clamp01(g / 255.0f);
        float nB = ToneAdjustmentHandler.Clamp01(b / 255.0f);

        var (lutR, lutG, lutB) = SampleLutNearest(data, size, nR, nG, nB);

        float intensityNorm = p.LutIntensity / 100.0f;
        float oneMinus = 1.0f - intensityNorm;

        r = r * oneMinus + lutR * 255.0f * intensityNorm;
        g = g * oneMinus + lutG * 255.0f * intensityNorm;
        b = b * oneMinus + lutB * 255.0f * intensityNorm;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static (float r, float g, float b) SampleLutNearest(
        float[] data, int size, float r, float g, float b)
    {
        int rIdx = Math.Clamp((int)(r * (size - 1)), 0, size - 1);
        int gIdx = Math.Clamp((int)(g * (size - 1)), 0, size - 1);
        int bIdx = Math.Clamp((int)(b * (size - 1)), 0, size - 1);

        int idx = (bIdx * size * size + gIdx * size + rIdx) * 3;

        float outR = Math.Clamp(data[idx], 0f, 1f);
        float outG = Math.Clamp(data[idx + 1], 0f, 1f);
        float outB = Math.Clamp(data[idx + 2], 0f, 1f);

        return (outR, outG, outB);
    }
}
