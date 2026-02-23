using System;
using System.Runtime.CompilerServices;

namespace SimpleRawEditor.Services.Processing;

public static class VignetteHandler
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ApplyVignette(
        ref float r, ref float g, ref float b,
        int x, int y, int width, int height,
        in PrecomputedAdjustments p)
    {
        float centerX = width * 0.5f;
        float centerY = height * 0.5f;
        float dx = (x - centerX) / centerX;
        float dy = (y - centerY) / centerY;
        float dist = MathF.Sqrt(dx * dx + dy * dy);

        float maxDist = MathF.Sqrt(2.0f);
        dist = dist / maxDist;

        float spread = Math.Clamp(p.VignetteSpread, 0.1f, 1.0f);
        float falloff = ToneAdjustmentHandler.Smoothstep(spread, 1.0f, dist);

        if (p.VignetteIntensity > 0)
        {
            float factor = 1.0f - p.VignetteIntensity * 0.8f * falloff;
            r *= factor;
            g *= factor;
            b *= factor;
        }
        else if (p.VignetteIntensity < 0)
        {
            float intensity = -p.VignetteIntensity;
            float factor = 1.0f + intensity * 0.5f * falloff;
            r *= factor;
            g *= factor;
            b *= factor;
        }
    }
}
