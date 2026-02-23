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
using SimpleRawEditor.Services.Core;
using SimpleRawEditor.Services.Processing;
using SimpleRawEditor.ViewModels.Editor;
using SimpleRawEditor.ViewModels.Main.Thumbnails;

namespace SimpleRawEditor.ViewModels.Main;

public partial class MainViewModel : ObservableObject
{
    private readonly IRawImageService _rawImageService;
    private readonly IImageProcessor _imageProcessor;
    private readonly ILutService _lutService;
    private RawImageData? _currentRawImage;
    private bool _isDraggingSlider;
    private ImageAdjustments? _currentAdjustments;

    [ObservableProperty]
    private ThumbnailListViewModel _thumbnailList = new();

    [ObservableProperty]
    private EditorViewModel _editor;

    [ObservableProperty]
    private ObservableCollection<string> _availableLuts = new();

    [ObservableProperty]
    private string _statusMessage = "Prêt";

    [ObservableProperty]
    private bool _isLoading;

    public MainViewModel() : this(new RawImageService(), new DebouncedImageProcessor(), new LutService())
    {
    }

    public MainViewModel(IRawImageService rawImageService, IImageProcessor imageProcessor, ILutService lutService)
    {
        _rawImageService = rawImageService;
        _imageProcessor = imageProcessor;
        _lutService = lutService;

        Editor = new EditorViewModel(new ImageAdjustments(), _lutService);

        LoadAvailableLuts();

        _imageProcessor.ImageProcessed += (_, image) =>
        {
            Editor.UpdateDisplayedImage(image);
        };

        _imageProcessor.ProcessingError += (_, error) =>
        {
            StatusMessage = $"Erreur: {error}";
        };

        ThumbnailList.ThumbnailSelected += async (s, image) =>
        {
            if (image != null)
            {
                await SelectImageAsync(image);
            }
        };

        Editor.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(Editor.DisplayedImage))
            {
                Editor.CurrentImage = Editor.DisplayedImage;
            }
        };
    }

    private void LoadAvailableLuts()
    {
        AvailableLuts.Clear();
        foreach (var lutName in _lutService.GetAvailableLuts())
        {
            AvailableLuts.Add(lutName);
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
            StatusMessage = $"{ThumbnailList.Thumbnails.Count} image(s) chargée(s)";
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
                ThumbnailList.AddThumbnail(loadedImage);
            });
        }
        catch (Exception ex)
        {
            StatusMessage = $"Erreur chargement {filePath}: {ex.Message}";
        }
    }

    private async Task SelectImageAsync(LoadedImageViewModel image)
    {
        IsLoading = true;
        StatusMessage = $"Chargement de {image.FileName}...";

        _currentRawImage?.Dispose();

        _currentRawImage = await _rawImageService.LoadRawImageAsync(image.FilePath);

        if (_currentRawImage?.OriginalBitmap != null)
        {
            Editor.SetImage(_currentRawImage.OriginalBitmap);
            
            Editor.SetAdjustments(image.Adjustments);
            _currentAdjustments = image.Adjustments;
            
            _imageProcessor.SetOriginalBitmap(_currentRawImage.OriginalBitmap);
            SubscribeToAdjustments(image.Adjustments);
            _imageProcessor.RequestProcessing(image.Adjustments, false);
            
            StatusMessage = $"{image.FileName} chargé ({_currentRawImage.Width}x{_currentRawImage.Height})";
        }
        else
        {
            StatusMessage = $"Erreur chargement {image.FileName}";
        }

        IsLoading = false;
    }

    private void SubscribeToAdjustments(ImageAdjustments adjustments)
    {
        adjustments.PropertyChanged += OnAdjustmentPropertyChanged;
    }

    private void OnAdjustmentPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(ImageAdjustments.NeedsUpdate))
        {
            if (e.PropertyName == nameof(ImageAdjustments.DenoiseAmount) ||
                e.PropertyName == nameof(ImageAdjustments.IsDenoiseEnabled))
            {
                _imageProcessor.InvalidateDenoiseCache();
            }
            _imageProcessor.RequestProcessing(_currentAdjustments, _isDraggingSlider);
        }
    }

    [RelayCommand]
    private void ResetAdjustments()
    {
        Editor.Reset();
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
        if (_currentAdjustments != null)
        {
            _imageProcessor.RequestProcessing(_currentAdjustments, false);
        }
    }

    [RelayCommand]
    private async Task LoadExternalLutAsync()
    {
        var window = GetWindow();
        if (window == null) return;

        var options = new FilePickerOpenOptions
        {
            Title = "Charger une LUT externe",
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
                var lut = await Task.Run(() => _lutService.LoadFromPath(filePath));

                var adjustments = Editor.GetAdjustments();
                if (adjustments != null)
                {
                    adjustments.ActiveLut = lut;
                    adjustments.LutFileName = Path.GetFileName(filePath);
                    adjustments.LutIntensity = 100;
                    adjustments.IsLutEnabled = true;
                    Editor.Lut.ActiveLut = lut;
                    Editor.Lut.LutFileName = Path.GetFileName(filePath);
                    Editor.Lut.Intensity = 100;
                    Editor.Lut.IsEnabled = true;
                    StatusMessage = $"LUT chargée: {adjustments.LutFileName} (taille: {lut.Size})";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Erreur chargement LUT: {ex.Message}";
            }
        }
    }

    private static Window? GetWindow()
    {
        return App.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.MainWindow
            : null;
    }
}
