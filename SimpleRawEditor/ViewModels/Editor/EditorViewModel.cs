using System;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SimpleRawEditor.Models;
using SimpleRawEditor.Services.Core;
using SimpleRawEditor.ViewModels.Editor.Adjustments;

namespace SimpleRawEditor.ViewModels.Editor;

public partial class EditorViewModel : ObservableObject
{
    private readonly ILutService _lutService;
    private ImageAdjustments? _adjustments;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanEdit))]
    private Bitmap? _currentImage;

    [ObservableProperty]
    private Bitmap? _displayedImage;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusMessage = "Aucune image sélectionnée";

    public bool CanEdit => CurrentImage != null;

    public ObservableCollection<IAdjustmentStep> ActiveAdjustments { get; } = new();

    public EditorViewModel(ILutService lutService)
    {
        _lutService = lutService;
        
        var basic = new BasicAdjustmentsViewModel();
        ActiveAdjustments.Add(basic);
    }

    public EditorViewModel(ImageAdjustments adjustments, ILutService lutService) : this(lutService)
    {
        _adjustments = adjustments;
    }

    [RelayCommand]
    private void AddDenoise()
    {
        var denoise = new DenoiseViewModel();
        denoise.RemoveRequested += () => RemoveStep(denoise);
        denoise.IsEnabled = true;
        
        var insertIndex = FindDenoiseInsertIndex();
        ActiveAdjustments.Insert(insertIndex, denoise);
    }

    [RelayCommand]
    private void AddLut()
    {
        var lut = new LutViewModel(_lutService);
        lut.RemoveRequested += () => RemoveStep(lut);
        lut.IsEnabled = true;
        
        ActiveAdjustments.Add(lut);
    }

    [RelayCommand]
    private void AddVignette()
    {
        var vignette = new VignetteViewModel();
        vignette.RemoveRequested += () => RemoveStep(vignette);
        vignette.IsEnabled = true;
        
        ActiveAdjustments.Add(vignette);
    }

    private int FindDenoiseInsertIndex()
    {
        for (int i = 1; i < ActiveAdjustments.Count; i++)
        {
            if (ActiveAdjustments[i] is LutViewModel || ActiveAdjustments[i] is VignetteViewModel)
            {
                return i;
            }
        }
        return ActiveAdjustments.Count;
    }

    [RelayCommand]
    private void Remove(IAdjustmentStep? step)
    {
        if (step == null) return;
        RemoveStep(step);
    }

    private void RemoveStep(IAdjustmentStep step)
    {
        if (step is BasicAdjustmentsViewModel) return;
        ActiveAdjustments.Remove(step);
    }

    public void SetImage(Bitmap image)
    {
        CurrentImage = image;
        DisplayedImage = image;
        StatusMessage = "Image chargée - Prête à l'édition";
    }

    public void UpdateDisplayedImage(Bitmap image)
    {
        DisplayedImage = image;
    }

    public void Reset()
    {
        ActiveAdjustments.Clear();
        
        var basic = new BasicAdjustmentsViewModel();
        ActiveAdjustments.Add(basic);
    }

    public void SetAdjustments(ImageAdjustments adjustments)
    {
        _adjustments = adjustments;
    }

    public ImageAdjustments? GetAdjustments()
    {
        return _adjustments;
    }

    public System.Collections.Generic.IEnumerable<IAdjustmentStep> GetAdjustmentSteps()
    {
        return ActiveAdjustments;
    }
}
