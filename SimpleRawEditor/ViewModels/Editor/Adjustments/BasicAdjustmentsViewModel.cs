using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SimpleRawEditor.Models;

namespace SimpleRawEditor.ViewModels.Editor.Adjustments;

public partial class BasicAdjustmentsViewModel : ObservableObject
{
    private readonly ImageAdjustments _adjustments;

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

    public BasicAdjustmentsViewModel(ImageAdjustments adjustments)
    {
        _adjustments = adjustments;
        _isEnabled = adjustments.IsBasicAdjustmentsEnabled;
        _exposure = adjustments.Exposure;
        _highlights = adjustments.Highlights;
        _contrast = adjustments.Contrast;
        _shadows = adjustments.Shadows;

        _adjustments.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(ImageAdjustments.Exposure))
                Exposure = _adjustments.Exposure;
            else if (e.PropertyName == nameof(ImageAdjustments.Highlights))
                Highlights = _adjustments.Highlights;
            else if (e.PropertyName == nameof(ImageAdjustments.Contrast))
                Contrast = _adjustments.Contrast;
            else if (e.PropertyName == nameof(ImageAdjustments.Shadows))
                Shadows = _adjustments.Shadows;
            else if (e.PropertyName == nameof(ImageAdjustments.IsBasicAdjustmentsEnabled))
                IsEnabled = _adjustments.IsBasicAdjustmentsEnabled;
        };
    }

    partial void OnIsEnabledChanged(bool value)
    {
        _adjustments.IsBasicAdjustmentsEnabled = value;
        if (!value)
        {
            Exposure = 0;
            Highlights = 0;
            Contrast = 0;
            Shadows = 0;
        }
        IsExpanded = value;
    }

    partial void OnExposureChanged(double value)
    {
        if (IsEnabled) _adjustments.Exposure = value;
    }

    partial void OnHighlightsChanged(double value)
    {
        if (IsEnabled) _adjustments.Highlights = value;
    }

    partial void OnContrastChanged(double value)
    {
        if (IsEnabled) _adjustments.Contrast = value;
    }

    partial void OnShadowsChanged(double value)
    {
        if (IsEnabled) _adjustments.Shadows = value;
    }

    public void UpdateFromAdjustments()
    {
        IsEnabled = _adjustments.IsBasicAdjustmentsEnabled;
        Exposure = _adjustments.Exposure;
        Highlights = _adjustments.Highlights;
        Contrast = _adjustments.Contrast;
        Shadows = _adjustments.Shadows;
    }

    [RelayCommand]
    public void Toggle() => IsEnabled = !IsEnabled;

    [RelayCommand]
    public void Expand() => IsExpanded = true;

    [RelayCommand]
    public void Collapse() => IsExpanded = false;
}
