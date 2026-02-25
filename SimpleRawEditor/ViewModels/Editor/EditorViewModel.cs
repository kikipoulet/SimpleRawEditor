using System;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SimpleRawEditor.Services;

namespace SimpleRawEditor.ViewModels.Editor;

public partial class EditorViewModel : ObservableObject
{
    private readonly LutService _lutService;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanEdit))]
    private Bitmap? _currentImage;

    [ObservableProperty]
    private Bitmap? _displayedImage;

    public bool CanEdit => CurrentImage != null;

    public ObservableCollection<AdjustmentStep> Adjustments { get; } = new();

    public event Action<AdjustmentStep?>? AdjustmentsChanged;

    public EditorViewModel(LutService lutService)
    {
        _lutService = lutService;
        
        var basic = new BasicAdjustment();
        basic.Changed += step => AdjustmentsChanged?.Invoke(step);
        Adjustments.Add(basic);
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

    public void SetImage(Bitmap image)
    {
        CurrentImage = image;
        DisplayedImage = image;
    }

    public void UpdateDisplayedImage(Bitmap image)
    {
        DisplayedImage = image;
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

    public System.Collections.Generic.IReadOnlyList<AdjustmentStep> GetAdjustmentSteps()
    {
        return Adjustments;
    }
}
