using System;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SimpleRawEditor.Services.Processing;
using SimpleRawEditor.Views.Adjustments;

namespace SimpleRawEditor.ViewModels.Editor.Adjustments;

public partial class VignetteViewModel : ObservableObject, IAdjustmentStep
{
    public string Name => "Vignette";
    public UserControl View => new VignetteView { DataContext = this };
    public event Action? RemoveRequested;

    [ObservableProperty]
    private bool _isExpanded = true;

    [ObservableProperty]
    private bool _isEnabled;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanAdjust))]
    private double _intensity;

    [ObservableProperty]
    private double _spread = 50;

    public bool CanAdjust => IsEnabled;

    public void Remove() => RemoveRequested?.Invoke();

    public VignetteViewModel()
    {
    }

    public void Apply(byte[] pixels, int width, int height, int stride)
    {
        if (!IsEnabled || Math.Abs(Intensity) < 0.001) return;

        float intensity = (float)(Intensity / 100.0);
        float spread = (float)(Spread / 100.0);

        var precomputed = new PrecomputedAdjustments(
            0, 0, 0, 0, 0,
            intensity, spread,
            false, false, false, false,
            false, true,
            null, 0, false
        );

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int index = (y * stride) + (x * 4);

                float r = pixels[index];
                float g = pixels[index + 1];
                float b = pixels[index + 2];

                VignetteHandler.ApplyVignette(ref r, ref g, ref b, x, y, width, height, in precomputed);

                pixels[index] = ToneAdjustmentHandler.ClampByte(r);
                pixels[index + 1] = ToneAdjustmentHandler.ClampByte(g);
                pixels[index + 2] = ToneAdjustmentHandler.ClampByte(b);
            }
        }
    }

    partial void OnIsEnabledChanged(bool value)
    {
        IsExpanded = value;
    }

    [RelayCommand]
    public void Toggle() => IsEnabled = !IsEnabled;

    [RelayCommand]
    public void Expand() => IsExpanded = true;

    [RelayCommand]
    public void Collapse() => IsExpanded = false;
}
