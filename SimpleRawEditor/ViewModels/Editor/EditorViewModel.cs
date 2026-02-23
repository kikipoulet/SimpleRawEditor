using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SimpleRawEditor.Models;
using SimpleRawEditor.Services.Core;
using SimpleRawEditor.ViewModels.Editor.Adjustments;

namespace SimpleRawEditor.ViewModels.Editor;

public partial class EditorViewModel : ObservableObject
{
    private ImageAdjustments _adjustments;
    private readonly ILutService _lutService;

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

    public BasicAdjustmentsViewModel BasicAdjustments { get; private set; }
    public DenoiseViewModel Denoise { get; private set; }
    public LutViewModel Lut { get; private set; }
    public VignetteViewModel Vignette { get; private set; }

    public EditorViewModel(ImageAdjustments adjustments, ILutService lutService)
    {
        _lutService = lutService;
        _adjustments = adjustments;
        
        BasicAdjustments = new BasicAdjustmentsViewModel(adjustments);
        Denoise = new DenoiseViewModel(adjustments);
        Lut = new LutViewModel(adjustments, lutService);
        Vignette = new VignetteViewModel(adjustments);
    }

    public void SetAdjustments(ImageAdjustments adjustments)
    {
        _adjustments = adjustments;
        
        BasicAdjustments = new BasicAdjustmentsViewModel(adjustments);
        Denoise = new DenoiseViewModel(adjustments);
        Lut = new LutViewModel(adjustments, _lutService);
        Vignette = new VignetteViewModel(adjustments);
        
        OnPropertyChanged(nameof(BasicAdjustments));
        OnPropertyChanged(nameof(Denoise));
        OnPropertyChanged(nameof(Lut));
        OnPropertyChanged(nameof(Vignette));
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
        _adjustments.Reset();
        BasicAdjustments.UpdateFromAdjustments();
        Denoise.Amount = 0;
        Denoise.IsEnabled = false;
        Vignette.Intensity = 0;
        Vignette.Spread = 50;
        Vignette.IsEnabled = false;
        Lut.Clear();
    }

    public ImageAdjustments GetAdjustments()
    {
        return _adjustments;
    }
}
