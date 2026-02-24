using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using SimpleRawEditor.Services;
using SimpleRawEditor.Views.Adjustments;

namespace SimpleRawEditor.ViewModels.Editor;

public partial class BasicAdjustment : AdjustmentStep
{
    public override string Name => "Basic";
    public override UserControl View => new BasicAdjustmentsView { DataContext = this };

    [ObservableProperty] 
    private double _exposure;

    [ObservableProperty] 
    private double _highlights;

    [ObservableProperty] 
    private double _contrast;

    [ObservableProperty] 
    private double _shadows;

    public BasicAdjustment()
    {
        IsEnabled = true;
    }

    public override void Apply(byte[] pixels, int width, int height, int stride)
    {
        if (!IsEnabled) return;
        if (Math.Abs(Exposure) < 0.001 && Math.Abs(Contrast) < 0.001 &&
            Math.Abs(Highlights) < 0.001 && Math.Abs(Shadows) < 0.001) return;

        float exposureFactor = (float)Math.Pow(2, (Exposure / 100.0) * 0.5);
        float contrastAmount = (float)(Contrast / 100.0);
        float shadowsAmount = (float)(Shadows / 100.0);
        float highlightsAmount = (float)(Highlights / 100.0);

        bool hasExposure = Math.Abs(Exposure) > 0.001;
        bool hasContrast = Math.Abs(Contrast) > 0.001;
        bool hasShadows = Math.Abs(Shadows) > 0.001;
        bool hasHighlights = Math.Abs(Highlights) > 0.001;

        const float inv255 = 1.0f / 255.0f;

        Parallel.For(0, height, y =>
        {
            int rowStart = y * stride;

            for (int x = 0; x < width; x++)
            {
                int index = rowStart + x * 4;

                float b = pixels[index];
                float g = pixels[index + 1];
                float r = pixels[index + 2];

                float luminance = (0.299f * r + 0.587f * g + 0.114f * b) * inv255;

                float normR = r * inv255;
                float normG = g * inv255;
                float normB = b * inv255;

                if (hasExposure)
                {
                    normR = ApplyExposure(normR, exposureFactor);
                    normG = ApplyExposure(normG, exposureFactor);
                    normB = ApplyExposure(normB, exposureFactor);
                }

                if (hasHighlights)
                {
                    normR = ApplyHighlights(normR, luminance, highlightsAmount);
                    normG = ApplyHighlights(normG, luminance, highlightsAmount);
                    normB = ApplyHighlights(normB, luminance, highlightsAmount);
                }

                if (hasShadows)
                {
                    normR = ApplyShadows(normR, shadowsAmount);
                    normG = ApplyShadows(normG, shadowsAmount);
                    normB = ApplyShadows(normB, shadowsAmount);
                }

                if (hasContrast)
                {
                    normR = ApplyContrast(normR, contrastAmount);
                    normG = ApplyContrast(normG, contrastAmount);
                    normB = ApplyContrast(normB, contrastAmount);
                }

                pixels[index] = ImageProcessor.ClampByte(normB * 255.0f);
                pixels[index + 1] = ImageProcessor.ClampByte(normG * 255.0f);
                pixels[index + 2] = ImageProcessor.ClampByte(normR * 255.0f);
            }
        });
    }

    private static float ApplyExposure(float value, float factor)
    {
        float exposed = value * factor;
        return SoftRolloff(exposed);
    }

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

    private static float ApplyHighlights(float value, float luminance, float amount)
    {
        if (luminance < 0.5f) return value;

        float t = ImageProcessor.Smoothstep(0.5f, 1.0f, luminance);
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

    private static float ApplyShadows(float value, float amount)
    {
        if (Math.Abs(amount) < 0.001f) return value;

        if (amount > 0)
        {
            float lift = amount * 0.3f;
            float result = value + lift * value * (1.0f - value) * 4.0f;
            return Math.Min(result, 1.0f);
        }
        else
        {
            float crush = -amount * 0.3f;
            float result = value * (1.0f - crush * (1.0f - value));
            return Math.Max(result, 0.0f);
        }
    }

    private static float ApplyContrast(float value, float amount)
    {
        if (Math.Abs(amount) < 0.001f) return value;

        float contrast = amount * 0.11f;
        float result = value + contrast * (value - 0.5f) * (1.0f - value) * 4.0f;

        return Math.Clamp(result, 0.0f, 1.0f);
    }

    protected override void OnIsEnabledChangedCore(bool value)
    {
        if (!value)
        {
            Exposure = 0;
            Highlights = 0;
            Contrast = 0;
            Shadows = 0;
        }
        base.OnIsEnabledChangedCore(value);
    }
}
