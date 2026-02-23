using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SimpleRawEditor.Models;
using SimpleRawEditor.Services.Core;

namespace SimpleRawEditor.ViewModels.Editor.Adjustments;

public partial class LutViewModel : ObservableObject
{
    private readonly ImageAdjustments _adjustments;
    private readonly ILutService _lutService;

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

    public LutViewModel(ImageAdjustments adjustments, ILutService lutService)
    {
        _adjustments = adjustments;
        _lutService = lutService;
        IsEnabled = adjustments.IsLutEnabled;
        ActiveLut = adjustments.ActiveLut;
        LutFileName = adjustments.LutFileName;
        Intensity = adjustments.LutIntensity;

        LoadAvailablePresets();

        _adjustments.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(ImageAdjustments.IsLutEnabled))
                IsEnabled = _adjustments.IsLutEnabled;
            else if (e.PropertyName == nameof(ImageAdjustments.ActiveLut))
                ActiveLut = _adjustments.ActiveLut;
            else if (e.PropertyName == nameof(ImageAdjustments.LutFileName))
                LutFileName = _adjustments.LutFileName;
            else if (e.PropertyName == nameof(ImageAdjustments.LutIntensity))
                Intensity = _adjustments.LutIntensity;
        };
    }

    private void LoadAvailablePresets()
    {
        AvailablePresets.Clear();
        foreach (var lutName in _lutService.GetAvailableLuts())
        {
            AvailablePresets.Add(lutName);
        }
    }

    partial void OnIsEnabledChanged(bool value)
    {
        _adjustments.IsLutEnabled = value;
        IsExpanded = value;
    }

    partial void OnActiveLutChanged(CubeLut? value)
    {
        _adjustments.ActiveLut = value;
    }

    partial void OnLutFileNameChanged(string? value)
    {
        _adjustments.LutFileName = value;
    }

    partial void OnIntensityChanged(double value)
    {
        _adjustments.LutIntensity = value;
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
