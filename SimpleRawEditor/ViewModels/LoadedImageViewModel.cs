using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SimpleRawEditor.Models;
using SimpleRawEditor.Services;
using SimpleRawEditor.ViewModels.Editor;

namespace SimpleRawEditor.ViewModels;

public partial class LoadedImageViewModel : ObservableObject
{
    private readonly LutService _lutService;

    [ObservableProperty]
    private string _filePath = string.Empty;

    [ObservableProperty]
    private string _fileName = string.Empty;

    [ObservableProperty]
    private Bitmap? _thumbnail;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BorderBrush))]
    private bool _isSelected;

    [ObservableProperty]
    private Bitmap? _originalBitmap;

    [ObservableProperty]
    private Bitmap? _displayedBitmap;

    [ObservableProperty]
    private ImageMetadata? _metadata;

    public IBrush BorderBrush => IsSelected
        ? Brushes.Orange
        : Brushes.Transparent;

    public ObservableCollection<AdjustmentStep> Adjustments { get; }

    public event Action<AdjustmentStep?>? AdjustmentsChanged;
    public event EventHandler? Selected;

    public LoadedImageViewModel(LutService lutService)
    {
        _lutService = lutService;
        Adjustments = new ObservableCollection<AdjustmentStep>();

        var basic = new BasicAdjustment();
        basic.Changed += step => AdjustmentsChanged?.Invoke(step);
        Adjustments.Add(basic);
    }

    [RelayCommand]
    public void Select()
    {
        Selected?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void AddDenoise()
    {
        var denoise = new DenoiseAdjustment();
        denoise.RemoveRequested += () => RemoveStep(denoise);
        denoise.Changed += step => AdjustmentsChanged?.Invoke(step);

        var insertIndex = FindDenoiseInsertIndex();
        Adjustments.Insert(insertIndex, denoise);
    }

    [RelayCommand]
    private void AddLut()
    {
        var lut = new LutAdjustment(_lutService);
        lut.RemoveRequested += () => RemoveStep(lut);
        lut.Changed += step => AdjustmentsChanged?.Invoke(step);

        Adjustments.Add(lut);
    }

    [RelayCommand]
    private void AddVignette()
    {
        var vignette = new VignetteAdjustment();
        vignette.RemoveRequested += () => RemoveStep(vignette);
        vignette.Changed += step => AdjustmentsChanged?.Invoke(step);

        Adjustments.Add(vignette);
    }

    private int FindDenoiseInsertIndex()
    {
        for (int i = 1; i < Adjustments.Count; i++)
        {
            if (Adjustments[i] is LutAdjustment or VignetteAdjustment)
            {
                return i;
            }
        }
        return Adjustments.Count;
    }

    [RelayCommand]
    private void Remove(AdjustmentStep? step)
    {
        if (step == null) return;
        RemoveStep(step);
    }

    private void RemoveStep(AdjustmentStep step)
    {
        if (step is BasicAdjustment) return;
        Adjustments.Remove(step);
        AdjustmentsChanged?.Invoke(null);
    }

    public void SetImage(Bitmap image, ImageMetadata? metadata = null)
    {
        OriginalBitmap = image;
        DisplayedBitmap = image;
        Metadata = metadata;
    }

    public void UpdateDisplayedBitmap(Bitmap image)
    {
        DisplayedBitmap = image;
    }

    public void Reset()
    {
        foreach (var step in Adjustments.ToList())
        {
            if (step is not BasicAdjustment)
            {
                Adjustments.Remove(step);
            }
        }

        if (Adjustments.Count == 0 || Adjustments[0] is not BasicAdjustment)
        {
            Adjustments.Clear();
            var basic = new BasicAdjustment();
            basic.Changed += step => AdjustmentsChanged?.Invoke(step);
            Adjustments.Add(basic);
        }
        else
        {
            ((BasicAdjustment)Adjustments[0]).Exposure = 0;
            ((BasicAdjustment)Adjustments[0]).Highlights = 0;
            ((BasicAdjustment)Adjustments[0]).Contrast = 0;
            ((BasicAdjustment)Adjustments[0]).Shadows = 0;
        }

        AdjustmentsChanged?.Invoke(null);
    }

    public IReadOnlyList<AdjustmentStep> GetAdjustmentSteps()
    {
        return Adjustments;
    }
}
