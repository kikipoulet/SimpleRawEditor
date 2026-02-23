using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SimpleRawEditor.Models;

namespace SimpleRawEditor.ViewModels.Editor.Adjustments;

public partial class DenoiseViewModel : ObservableObject
{
    private readonly ImageAdjustments _adjustments;

    [ObservableProperty]
    private bool _isExpanded = true;

    [ObservableProperty]
    private bool _isEnabled;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanAdjust))]
    private double _amount;

    public bool CanAdjust => IsEnabled;

    public DenoiseViewModel(ImageAdjustments adjustments)
    {
        _adjustments = adjustments;
        IsEnabled = adjustments.IsDenoiseEnabled;
        Amount = adjustments.DenoiseAmount;

        _adjustments.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(ImageAdjustments.IsDenoiseEnabled))
                IsEnabled = _adjustments.IsDenoiseEnabled;
            else if (e.PropertyName == nameof(ImageAdjustments.DenoiseAmount))
                Amount = _adjustments.DenoiseAmount;
        };
    }

    partial void OnIsEnabledChanged(bool value)
    {
        _adjustments.IsDenoiseEnabled = value;
        IsExpanded = value;
    }

    partial void OnAmountChanged(double value)
    {
        _adjustments.DenoiseAmount = value;
    }

    [RelayCommand]
    public void Toggle() => IsEnabled = !IsEnabled;

    [RelayCommand]
    public void Expand() => IsExpanded = true;

    [RelayCommand]
    public void Collapse() => IsExpanded = false;
}
