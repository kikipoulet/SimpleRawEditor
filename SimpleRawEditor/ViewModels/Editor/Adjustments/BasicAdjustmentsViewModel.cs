using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SimpleRawEditor.Services.Processing;
using SimpleRawEditor.Views.Adjustments;

namespace SimpleRawEditor.ViewModels.Editor.Adjustments;

public partial class BasicAdjustmentsViewModel : ObservableObject, IAdjustmentStep
{
    public string Name => "Basic Adjustments";
    public UserControl View => new BasicAdjustmentsView { DataContext = this };

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanAdjust))]
    private bool _isEnabled = true;

    [ObservableProperty]
    private bool _isExpanded = true;

    [ObservableProperty]
    private double _exposure;

    [ObservableProperty]
    private double _highlights;

    [ObservableProperty]
    private double _contrast;

    [ObservableProperty]
    private double _shadows;

    public bool CanAdjust => IsEnabled;

    public event Action? RemoveRequested;

    public void Remove() => RemoveRequested?.Invoke();

    public BasicAdjustmentsViewModel()
    {
    }

    public void Apply(byte[] pixels, int width, int height, int stride)
    {
        if (!IsEnabled) return;
        if (Math.Abs(Exposure) < 0.001 && Math.Abs(Contrast) < 0.001 && 
            Math.Abs(Highlights) < 0.001 && Math.Abs(Shadows) < 0.001) return;

        var precomputed = new PrecomputedAdjustments(
            (float)Math.Pow(2, (Exposure / 100.0) * 0.5),
            (float)(Contrast / 100.0),
            (float)(Shadows / 100.0),
            (float)(Highlights / 100.0),
            0, 0, 0,
            Math.Abs(Exposure) > 0.001,
            Math.Abs(Contrast) > 0.001,
            Math.Abs(Shadows) > 0.001,
            Math.Abs(Highlights) > 0.001,
            false, false,
            null, 0, false
        );

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

                r = ToneAdjustmentHandler.ApplyToneCurve(r * inv255, luminance, in precomputed) * 255.0f;
                g = ToneAdjustmentHandler.ApplyToneCurve(g * inv255, luminance, in precomputed) * 255.0f;
                b = ToneAdjustmentHandler.ApplyToneCurve(b * inv255, luminance, in precomputed) * 255.0f;

                pixels[index] = ToneAdjustmentHandler.ClampByte(b);
                pixels[index + 1] = ToneAdjustmentHandler.ClampByte(g);
                pixels[index + 2] = ToneAdjustmentHandler.ClampByte(r);
            }
        });
    }

    partial void OnIsEnabledChanged(bool value)
    {
        if (!value)
        {
            Exposure = 0;
            Highlights = 0;
            Contrast = 0;
            Shadows = 0;
        }
        IsExpanded = value;
    }

    [RelayCommand]
    public void Toggle() => IsEnabled = !IsEnabled;

    [RelayCommand]
    public void Expand() => IsExpanded = true;

    [RelayCommand]
    public void Collapse() => IsExpanded = false;
}
