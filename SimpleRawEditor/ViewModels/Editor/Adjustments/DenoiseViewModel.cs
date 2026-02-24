using System;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SimpleRawEditor.Services.Processing;
using SimpleRawEditor.Services.Processing.Denoising;
using SimpleRawEditor.Views.Adjustments;

namespace SimpleRawEditor.ViewModels.Editor.Adjustments;

public partial class DenoiseViewModel : ObservableObject, IAdjustmentStep
{
    private readonly DenoisingHandler _denoisingHandler;
    private byte[]? _currentPixels;
    private int _currentWidth;
    private int _currentHeight;
    private int _currentStride;

    public string Name => "Denoise";
    public UserControl View => new DenoiseView { DataContext = this };
    public event Action? RemoveRequested;

    [ObservableProperty]
    private bool _isExpanded = true;

    [ObservableProperty]
    private bool _isEnabled;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanAdjust))]
    private double _amount;

    public bool CanAdjust => IsEnabled;

    public void Remove() => RemoveRequested?.Invoke();

    public DenoiseViewModel()
    {
        _denoisingHandler = new DenoisingHandler();
    }

    public void Apply(byte[] pixels, int width, int height, int stride)
    {
        if (!IsEnabled || Amount < 0.001) return;

        float strength = (float)(Amount * 2.5);

        _denoisingHandler.InitializeCache(pixels, width, height, stride);

        var denoised = _denoisingHandler.GetDenoisedPixels(strength);

        if (denoised != null)
        {
            float blend = 1.0f;
            float invBlend = 0f;

            for (int i = 0; i < denoised.Length; i += 4)
            {
                pixels[i] = (byte)(pixels[i] * invBlend + denoised[i] * blend);
                pixels[i + 1] = (byte)(pixels[i + 1] * invBlend + denoised[i + 1] * blend);
                pixels[i + 2] = (byte)(pixels[i + 2] * invBlend + denoised[i + 2] * blend);
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
