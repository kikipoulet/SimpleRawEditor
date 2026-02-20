using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SimpleRawEditor.Models;
using SimpleRawEditor.Services;

namespace SimpleRawEditor.ViewModels;

public partial class MainWindowViewModel : ObservableObject, IDisposable
{
    private readonly RawImageService _rawImageService;
    private readonly DebouncedImageProcessor _imageProcessor;
    private RawImageData? _currentRawImage;
    private Bitmap? _originalBitmap;
    private bool _isDraggingSlider;
    private ImageAdjustments? _subscribedAdjustments;

    [ObservableProperty]
    private ObservableCollection<LoadedImageViewModel> _loadedImages = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanProcess))]
    [NotifyPropertyChangedFor(nameof(CurrentAdjustments))]
    private LoadedImageViewModel? _selectedImage;

    [ObservableProperty]
    private Bitmap? _displayedImage;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusMessage = "Prêt";

    public bool CanProcess => SelectedImage != null;
    public ImageAdjustments? CurrentAdjustments => SelectedImage?.Adjustments;

    public MainWindowViewModel()
    {
        _rawImageService = new RawImageService();
        _imageProcessor = new DebouncedImageProcessor();

        _imageProcessor.ImageProcessed += (s, image) =>
        {
            DisplayedImage = image;
        };

        _imageProcessor.ProcessingError += (s, error) =>
        {
            Console.WriteLine(error);
        };
    }

    private void SubscribeToAdjustments(ImageAdjustments? adjustments)
    {
        if (_subscribedAdjustments != null)
        {
            _subscribedAdjustments.PropertyChanged -= OnAdjustmentsPropertyChanged;
        }

        _subscribedAdjustments = adjustments;

        if (adjustments != null)
        {
            adjustments.PropertyChanged += OnAdjustmentsPropertyChanged;
        }
    }

    private void OnAdjustmentsPropertyChanged(object? s, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (_subscribedAdjustments == null) return;

        if (e.PropertyName != nameof(ImageAdjustments.NeedsUpdate))
        {
            if (e.PropertyName == nameof(ImageAdjustments.DenoiseAmount) ||
                e.PropertyName == nameof(ImageAdjustments.IsDenoiseEnabled))
            {
                _imageProcessor.InvalidateDenoiseCache();
            }
            _imageProcessor.RequestProcessing(_subscribedAdjustments, _isDraggingSlider);
        }
    }

    partial void OnSelectedImageChanged(LoadedImageViewModel? value)
    {
        foreach (var img in LoadedImages)
        {
            img.IsSelected = (img == value);
        }

        SubscribeToAdjustments(value?.Adjustments);

        if (value != null)
        {
            _ = SelectImageAsync(value);
        }
    }

    [RelayCommand]
    private async Task OpenImagesAsync()
    {
        var window = GetWindow();
        if (window == null) return;

        var options = new FilePickerOpenOptions
        {
            Title = "Sélectionner des images RAW",
            AllowMultiple = true,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Images RAW")
                {
                    Patterns = new[] { "*.raw", "*.cr2", "*.cr3", "*.nef", "*.arw", "*.dng", "*.raf", "*.orf", "*.rw2", "*.pef" }
                },
                new FilePickerFileType("Tous les fichiers")
                {
                    Patterns = new[] { "*" }
                }
            }
        };

        var result = await window.StorageProvider.OpenFilePickerAsync(options);

        if (result.Count > 0)
        {
            IsLoading = true;
            StatusMessage = $"Chargement de {result.Count} image(s)...";

            foreach (var file in result)
            {
                await LoadImageAsync(file.Path.LocalPath);
            }

            IsLoading = false;
            StatusMessage = $"{LoadedImages.Count} image(s) chargée(s)";
        }
    }

    private async Task LoadImageAsync(string filePath)
    {
        try
        {
            var thumbnail = await _rawImageService.LoadThumbnailAsync(filePath);

            var loadedImage = new LoadedImageViewModel
            {
                FilePath = filePath,
                FileName = Path.GetFileName(filePath),
                Thumbnail = thumbnail
            };

            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                LoadedImages.Add(loadedImage);
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erreur chargement {filePath}: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task SelectImageAsync(LoadedImageViewModel? image)
    {
        if (image == null) return;

        IsLoading = true;
        StatusMessage = $"Chargement de {image.FileName}...";

        _currentRawImage?.Dispose();
        _originalBitmap?.Dispose();

        _currentRawImage = await _rawImageService.LoadRawImageAsync(image.FilePath);

        if (_currentRawImage?.OriginalBitmap != null)
        {
            _originalBitmap = _currentRawImage.OriginalBitmap;
            _imageProcessor.SetOriginalBitmap(_originalBitmap);
            _imageProcessor.RequestProcessing(image.Adjustments, false);
            DisplayedImage = _originalBitmap;
            StatusMessage = $"{image.FileName} chargé ({_currentRawImage.Width}x{_currentRawImage.Height})";
        }
        else
        {
            StatusMessage = $"Erreur chargement {image.FileName}";
        }

        IsLoading = false;
    }

    [RelayCommand]
    private void ResetAdjustments()
    {
        CurrentAdjustments?.Reset();
    }

    [RelayCommand]
    private void SliderDragStarted()
    {
        _isDraggingSlider = true;
    }

    [RelayCommand]
    private void SliderDragCompleted()
    {
        _isDraggingSlider = false;
        if (_subscribedAdjustments != null)
            _imageProcessor.RequestProcessing(_subscribedAdjustments, false);
    }

    [RelayCommand]
    private async Task LoadLutAsync()
    {
        var window = GetWindow();
        if (window == null) return;

        var options = new FilePickerOpenOptions
        {
            Title = "Charger une LUT",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Fichiers LUT")
                {
                    Patterns = new[] { "*.cube" }
                },
                new FilePickerFileType("Tous les fichiers")
                {
                    Patterns = new[] { "*" }
                }
            }
        };

        var result = await window.StorageProvider.OpenFilePickerAsync(options);

        if (result.Count > 0)
        {
            try
            {
                var filePath = result[0].Path.LocalPath;
                var lut = await Task.Run(() => CubeLutParser.Parse(filePath));

                if (CurrentAdjustments != null)
                {
                    CurrentAdjustments.ActiveLut = lut;
                    CurrentAdjustments.LutFileName = Path.GetFileName(filePath);
                    CurrentAdjustments.LutIntensity = 100;
                    StatusMessage = $"LUT chargée: {CurrentAdjustments.LutFileName} (taille: {lut.Size})";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Erreur chargement LUT: {ex.Message}";
            }
        }
    }

    [RelayCommand]
    private void ClearLut()
    {
        CurrentAdjustments?.ClearLut();
        StatusMessage = "LUT supprimée";
    }

    private static Window? GetWindow()
    {
        return App.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.MainWindow
            : null;
    }

    public void Dispose()
    {
        _imageProcessor.Dispose();
        _currentRawImage?.Dispose();
        _originalBitmap?.Dispose();
        DisplayedImage?.Dispose();
    }
}

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

    public Avalonia.Media.IBrush BorderBrush => IsSelected
        ? Avalonia.Media.Brushes.Orange
        : Avalonia.Media.Brushes.Transparent;
}
