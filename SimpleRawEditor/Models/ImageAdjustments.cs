using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SimpleRawEditor.Models;

public partial class ImageAdjustments : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NeedsUpdate))]
    private bool _isBasicAdjustmentsEnabled = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NeedsUpdate))]
    private double _exposure;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NeedsUpdate))]
    private double _highlights;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NeedsUpdate))]
    private double _contrast;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NeedsUpdate))]
    private double _shadows;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NeedsUpdate))]
    private bool _isDenoiseEnabled;

    [ObservableProperty]
    private bool _isDenoiseExpanded = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NeedsUpdate))]
    private double _denoiseAmount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NeedsUpdate))]
    private bool _isVignetteEnabled;

    [ObservableProperty]
    private bool _isVignetteExpanded = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NeedsUpdate))]
    [NotifyPropertyChangedFor(nameof(HasVignette))]
    private double _vignetteIntensity;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NeedsUpdate))]
    [NotifyPropertyChangedFor(nameof(HasVignette))]
    private double _vignetteSpread = 50;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NeedsUpdate))]
    private bool _isLutEnabled;

    [ObservableProperty]
    private bool _isLutExpanded = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NeedsUpdate))]
    private double _lutIntensity = 100;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLut))]
    [NotifyPropertyChangedFor(nameof(NeedsUpdate))]
    private CubeLut? _activeLut;

    [ObservableProperty]
    private string? _lutFileName;

    public bool HasVignette => Math.Abs(VignetteIntensity) > 0.001;
    public bool HasLut => ActiveLut != null;
    public bool NeedsUpdate => true;

    public void Reset()
    {
        IsBasicAdjustmentsEnabled = true;
        Exposure = 0;
        Highlights = 0;
        Contrast = 0;
        Shadows = 0;
        IsDenoiseEnabled = false;
        DenoiseAmount = 0;
        IsVignetteEnabled = false;
        VignetteIntensity = 0;
        VignetteSpread = 50;
        IsLutEnabled = false;
        LutIntensity = 100;
    }

    public void ClearLut()
    {
        ActiveLut = null;
        LutFileName = null;
        LutIntensity = 100;
        IsLutEnabled = false;
    }
}
