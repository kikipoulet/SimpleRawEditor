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

    public bool CanAdjust => IsEnabled;

    public event Action? RemoveRequested;
    public event Action? Changed;

    public abstract void Apply(byte[] pixels, int width, int height, int stride);

    public void RequestRemove() => RemoveRequested?.Invoke();
    
    protected void NotifyChanged() => Changed?.Invoke();

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
        
        if (e.PropertyName != nameof(IsExpanded))
        {
            NotifyChanged();
        }
    }

    protected virtual void OnIsEnabledChangedCore(bool value)
    {
        IsExpanded = value;
    }
}
