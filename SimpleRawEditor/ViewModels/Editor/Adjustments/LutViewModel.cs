using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SimpleRawEditor.Models;
using SimpleRawEditor.Services.Core;
using SimpleRawEditor.Services.Processing;
using SimpleRawEditor.Views.Adjustments;

namespace SimpleRawEditor.ViewModels.Editor.Adjustments;

public partial class LutViewModel : ObservableObject, IAdjustmentStep
{
    private readonly ILutService _lutService;

    public string Name => "LUT";
    public UserControl View => new LutView { DataContext = this };
    public event Action? RemoveRequested;

    [ObservableProperty]
    private bool _isExpanded = true;

    [ObservableProperty]
    private bool _isEnabled;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLut))]
    [NotifyPropertyChangedFor(nameof(CanAdjust))]
    private CubeLut? _activeLut;

    [ObservableProperty]
    private string? _lutFileName;

    [ObservableProperty]
    private double _intensity = 100;

    [ObservableProperty]
    private string? _selectedPresetName;

    [ObservableProperty]
    private ObservableCollection<string> _availablePresets = new();

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public bool HasLut => ActiveLut != null;
    public bool CanAdjust => IsEnabled;

    public void Remove() => RemoveRequested?.Invoke();

    public LutViewModel(ILutService lutService)
    {
        _lutService = lutService;
        LoadAvailablePresets();
    }

    private void LoadAvailablePresets()
    {
        AvailablePresets.Clear();
        foreach (var lutName in _lutService.GetAvailableLuts())
        {
            AvailablePresets.Add(lutName);
        }
    }

    public void Apply(byte[] pixels, int width, int height, int stride)
    {
        if (!IsEnabled || ActiveLut == null || Intensity < 0.001) return;

        float intensity = (float)(Intensity / 100.0);
        const float inv255 = 1.0f / 255.0f;

        var precomputed = new PrecomputedAdjustments(
            0, 0, 0, 0, 0, 0, 0,
            false, false, false, false,
            false, false,
            ActiveLut, intensity, true
        );

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int index = (y * stride) + (x * 4);

                float r = pixels[index] * inv255;
                float g = pixels[index + 1] * inv255;
                float b = pixels[index + 2] * inv255;

                LutApplicationHandler.ApplyLutInline(ref r, ref g, ref b, in precomputed);

                pixels[index] = ToneAdjustmentHandler.ClampByte(r * 255.0f);
                pixels[index + 1] = ToneAdjustmentHandler.ClampByte(g * 255.0f);
                pixels[index + 2] = ToneAdjustmentHandler.ClampByte(b * 255.0f);
            }
        }
    }

    partial void OnIsEnabledChanged(bool value)
    {
        IsExpanded = value;
    }

    partial void OnSelectedPresetNameChanged(string? value)
    {
        if (value == null) return;

        try
        {
            var lut = _lutService.LoadLut(value);
            if (lut != null)
            {
                ActiveLut = lut;
                LutFileName = value;
                Intensity = 100;
                IsEnabled = true;
                StatusMessage = $"LUT chargée: {value} (taille: {lut.Size})";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Erreur chargement LUT: {ex.Message}";
        }
    }

    [RelayCommand]
    public Task LoadExternalLutAsync()
    {
        return Task.CompletedTask;
    }

    [RelayCommand]
    public void Clear()
    {
        ActiveLut = null;
        LutFileName = null;
        Intensity = 100;
        SelectedPresetName = null;
        IsEnabled = false;
        StatusMessage = "LUT supprimée";
    }

    [RelayCommand]
    public void Toggle() => IsEnabled = !IsEnabled;

    [RelayCommand]
    public void Expand() => IsExpanded = true;

    [RelayCommand]
    public void Collapse() => IsExpanded = false;
}
