using System;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using SimpleRawEditor.Services;
using SimpleRawEditor.Views.Adjustments;

namespace SimpleRawEditor.ViewModels.Editor;

public partial class VignetteAdjustment : AdjustmentStep
{
    public override string Name => "Vignette";
    public override UserControl View => new VignetteView { DataContext = this };

    [ObservableProperty] 
    private double _intensity;

    [ObservableProperty] 
    private double _spread = 50;

    protected override void ApplyCore(byte[] pixels, int width, int height, int stride)
    {
        if (Math.Abs(Intensity) < 0.001) return;

        float intensity = (float)(Intensity / 100.0);
        float spread = (float)(Spread / 100.0);

        float centerX = width * 0.5f;
        float centerY = height * 0.5f;
        float maxDist = MathF.Sqrt(2.0f);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int index = (y * stride) + (x * 4);

                float dx = (x - centerX) / centerX;
                float dy = (y - centerY) / centerY;
                float dist = MathF.Sqrt(dx * dx + dy * dy) / maxDist;

                float clampedSpread = Math.Clamp(spread, 0.1f, 1.0f);
                float falloff = ImageProcessor.Smoothstep(clampedSpread, 1.0f, dist);

                float r = pixels[index + 2];
                float g = pixels[index + 1];
                float b = pixels[index];

                if (intensity > 0)
                {
                    float factor = 1.0f - (float)intensity * 0.8f * falloff;
                    r *= factor;
                    g *= factor;
                    b *= factor;
                }
                else
                {
                    float factor = 1.0f + (float)(-intensity) * 0.5f * falloff;
                    r *= factor;
                    g *= factor;
                    b *= factor;
                }

                pixels[index] = ImageProcessor.ClampByte(b);
                pixels[index + 1] = ImageProcessor.ClampByte(g);
                pixels[index + 2] = ImageProcessor.ClampByte(r);
            }
        }
    }

    partial void OnIntensityChanged(double _) => NotifyChanged();
    partial void OnSpreadChanged(double _) => NotifyChanged();
}
