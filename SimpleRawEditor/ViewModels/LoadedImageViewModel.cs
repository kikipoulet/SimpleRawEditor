using System;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SimpleRawEditor.Models;

namespace SimpleRawEditor.ViewModels;

public partial class LoadedImageViewModel : ObservableObject
{
    [ObservableProperty]
    private string _filePath = string.Empty;

    [ObservableProperty]
    private string _fileName = string.Empty;

    [ObservableProperty]
    private Bitmap? _thumbnail;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BorderBrush))]
    private bool _isSelected;

    public ImageAdjustments Adjustments { get; } = new();

    public IBrush BorderBrush => IsSelected
        ? Brushes.Orange
        : Brushes.Transparent;

    public event EventHandler? Selected;

    [RelayCommand]
    public void Select()
    {
        Selected?.Invoke(this, EventArgs.Empty);
    }
}
