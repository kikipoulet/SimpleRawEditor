using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SimpleRawEditor.Models;
using SimpleRawEditor.Services;
using SimpleRawEditor.ViewModels.Editor;
using SimpleRawEditor.ViewModels.Main.Thumbnails;

namespace SimpleRawEditor.ViewModels.Main;

public partial class MainViewModel : ObservableObject
{
    private readonly RawImageService _rawService;
    private readonly LutService _lutService;
    private readonly ImageProcessor _processor;
    private bool _isDraggingSlider;

    [ObservableProperty]
    private RawImageData? _currentRawImage;

    [ObservableProperty]
    private ThumbnailListViewModel _thumbnailList = new();

    [ObservableProperty]
    private EditorViewModel _editor;

    [ObservableProperty]
    private string _statusMessage = "Ready";

    [ObservableProperty]
    private bool _isLoading;

    public MainViewModel()
    {
        _rawService = new RawImageService();
        _lutService = new LutService();
        _processor = new ImageProcessor();
        
        _editor = new EditorViewModel(_lutService);

        _processor.ImageProcessed += image =>
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                _editor.UpdateDisplayedImage(image);
            });
        };

        _processor.ProcessingError += error =>
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                StatusMessage = $"Error: {error}";
            });
        };

        _thumbnailList.ThumbnailSelected += async (_, image) =>
        {
            if (image != null)
            {
                await SelectImageAsync(image);
            }
        };

        _editor.AdjustmentsChanged += () =>
        {
            RequestProcessing();
        };
    }

    private void RequestProcessing()
    {
        if (CurrentRawImage?.OriginalBitmap is WriteableBitmap wb)
        {
            _processor.SetSource(wb);
            _processor.RequestProcessing(_editor.GetAdjustmentSteps(), _isDraggingSlider);
        }
    }

    [RelayCommand]
    private async Task OpenImagesAsync()
    {
        var window = GetWindow();
        if (window == null) return;

        var options = new FilePickerOpenOptions
        {
            Title = "Select RAW images",
            AllowMultiple = true,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("RAW Images")
                {
                    Patterns = new[] { "*.raw", "*.cr2", "*.cr3", "*.nef", "*.arw", "*.dng", "*.raf", "*.orf", "*.rw2", "*.pef" }
                },
                new FilePickerFileType("All Files")
                {
                    Patterns = new[] { "*" }
                }
            }
        };

        var result = await window.StorageProvider.OpenFilePickerAsync(options);

        if (result.Count > 0)
        {
            IsLoading = true;
            StatusMessage = $"Loading {result.Count} image(s)...";

            foreach (var file in result)
            {
                await LoadImageAsync(file.Path.LocalPath);
            }

            IsLoading = false;
            StatusMessage = $"{ThumbnailList.Thumbnails.Count} image(s) loaded";
        }
    }

    private async Task LoadImageAsync(string filePath)
    {
        try
        {
            var thumbnail = await _rawService.LoadThumbnailAsync(filePath);

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
            StatusMessage = $"Error loading {filePath}: {ex.Message}";
        }
    }

    private async Task SelectImageAsync(LoadedImageViewModel image)
    {
        IsLoading = true;
        StatusMessage = $"Loading {image.FileName}...";

        CurrentRawImage?.Dispose();

        CurrentRawImage = await _rawService.LoadRawImageAsync(image.FilePath);

        if (CurrentRawImage?.OriginalBitmap != null)
        {
            _editor.SetImage(CurrentRawImage.OriginalBitmap);
            RequestProcessing();
            StatusMessage = $"{image.FileName} loaded ({CurrentRawImage.Width}x{CurrentRawImage.Height})";
        }
        else
        {
            StatusMessage = $"Error loading {image.FileName}";
        }

        IsLoading = false;
    }

    [RelayCommand]
    private void ResetAdjustments()
    {
        _editor.Reset();
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
        RequestProcessing();
    }

    [RelayCommand]
    private async Task LoadExternalLutAsync()
    {
        var window = GetWindow();
        if (window == null) return;

        var options = new FilePickerOpenOptions
        {
            Title = "Load external LUT",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("LUT Files")
                {
                    Patterns = new[] { "*.cube" }
                },
                new FilePickerFileType("All Files")
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
                var lut = _lutService.LoadFromPath(filePath);

                _editor.AddLutCommand.Execute(null);
                
                StatusMessage = $"LUT loaded: {Path.GetFileName(filePath)} (size: {lut.Size})";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error loading LUT: {ex.Message}";
            }
        }
    }

    private static Window? GetWindow()
    {
        return App.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.MainWindow
            : null;
    }
}
