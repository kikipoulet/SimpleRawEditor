using System;
using System.ComponentModel;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace SimpleRawEditor.ViewModels.Editor;

public abstract partial class AdjustmentStep : ObservableObject
{
    public abstract string Name { get; }
    public abstract UserControl View { get; }

    [ObservableProperty] 
    protected bool _isEnabled = true;

    [ObservableProperty] 
    protected bool _isExpanded = true;

    [ObservableProperty]
    protected bool _isComputing;

    public event Action? RemoveRequested;
    public event Action<AdjustmentStep>? Changed;

    public void Apply(byte[] pixels, int width, int height, int stride)
    {
        if (!IsEnabled) return;
        IsComputing = true;
        try
        {
            ApplyCore(pixels, width, height, stride);
        }
        finally
        {
            IsComputing = false;
        }
    }

    protected abstract void ApplyCore(byte[] pixels, int width, int height, int stride);

    public void RequestRemove() => RemoveRequested?.Invoke();
    
    protected void NotifyChanged() => Changed?.Invoke(this);

    [RelayCommand]
    public void Toggle() => IsEnabled = !IsEnabled;

    [RelayCommand]
    public void Expand() => IsExpanded = true;

    [RelayCommand]
    public void Collapse() => IsExpanded = false;

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        
        if (e.PropertyName == nameof(IsEnabled))
        {
            OnIsEnabledChangedCore(IsEnabled);
        }
        
        if (e.PropertyName != nameof(IsExpanded) && e.PropertyName != nameof(IsComputing))
        {
            NotifyChanged();
        }
    }

    protected virtual void OnIsEnabledChangedCore(bool value)
    {
        IsExpanded = value;
    }
}
