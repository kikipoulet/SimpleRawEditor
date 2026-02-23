using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SimpleRawEditor.Models;

namespace SimpleRawEditor.ViewModels.Editor.Adjustments;

public partial class VignetteViewModel : ObservableObject
{
    private readonly ImageAdjustments _adjustments;

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

    public VignetteViewModel(ImageAdjustments adjustments)
    {
        _adjustments = adjustments;
        IsEnabled = adjustments.IsVignetteEnabled;
        Intensity = adjustments.VignetteIntensity;
        Spread = adjustments.VignetteSpread;

        _adjustments.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(ImageAdjustments.IsVignetteEnabled))
                IsEnabled = _adjustments.IsVignetteEnabled;
            else if (e.PropertyName == nameof(ImageAdjustments.VignetteIntensity))
                Intensity = _adjustments.VignetteIntensity;
            else if (e.PropertyName == nameof(ImageAdjustments.VignetteSpread))
                Spread = _adjustments.VignetteSpread;
        };
    }

    partial void OnIsEnabledChanged(bool value)
    {
        _adjustments.IsVignetteEnabled = value;
        IsExpanded = value;
    }

    partial void OnIntensityChanged(double value)
    {
        _adjustments.VignetteIntensity = value;
    }

    partial void OnSpreadChanged(double value)
    {
        _adjustments.VignetteSpread = value;
    }

    [RelayCommand]
    public void Toggle() => IsEnabled = !IsEnabled;

    [RelayCommand]
    public void Expand() => IsExpanded = true;

    [RelayCommand]
    public void Collapse() => IsExpanded = false;
}
