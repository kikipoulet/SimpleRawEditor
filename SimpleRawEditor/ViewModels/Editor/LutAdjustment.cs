using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SimpleRawEditor.Models;
using SimpleRawEditor.Services;
using SimpleRawEditor.Views.Adjustments;

namespace SimpleRawEditor.ViewModels.Editor;

public partial class LutAdjustment : AdjustmentStep
{
    private readonly LutService _lutService;

    public override string Name => "LUT";
    public override UserControl View => new LutView { DataContext = this };

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLut))]
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

    public LutAdjustment(LutService lutService)
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

    public override void Apply(byte[] pixels, int width, int height, int stride)
    {
        if (!IsEnabled || ActiveLut == null || Intensity < 0.001) return;

        float intensity = (float)(Intensity / 100.0);
        const float inv255 = 1.0f / 255.0f;
        var lut = ActiveLut;
        int size = lut.Size;
        float[] data = lut.Data;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int index = (y * stride) + (x * 4);

                float r = pixels[index + 2] * inv255;
                float g = pixels[index + 1] * inv255;
                float b = pixels[index] * inv255;

                r = ImageProcessor.Clamp01(r);
                g = ImageProcessor.Clamp01(g);
                b = ImageProcessor.Clamp01(b);

                var (lutR, lutG, lutB) = SampleLutNearest(data, size, r, g, b);

                float oneMinus = 1.0f - intensity;
                r = r * oneMinus + lutR * intensity;
                g = g * oneMinus + lutG * intensity;
                b = b * oneMinus + lutB * intensity;

                pixels[index] = ImageProcessor.ClampByte(b * 255.0f);
                pixels[index + 1] = ImageProcessor.ClampByte(g * 255.0f);
                pixels[index + 2] = ImageProcessor.ClampByte(r * 255.0f);
            }
        }
    }

    private static (float r, float g, float b) SampleLutNearest(float[] data, int size, float r, float g, float b)
    {
        int rIdx = Math.Clamp((int)(r * (size - 1)), 0, size - 1);
        int gIdx = Math.Clamp((int)(g * (size - 1)), 0, size - 1);
        int bIdx = Math.Clamp((int)(b * (size - 1)), 0, size - 1);

        int idx = (bIdx * size * size + gIdx * size + rIdx) * 3;

        float outR = Math.Clamp(data[idx], 0f, 1f);
        float outG = Math.Clamp(data[idx + 1], 0f, 1f);
        float outB = Math.Clamp(data[idx + 2], 0f, 1f);

        return (outR, outG, outB);
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
                StatusMessage = $"LUT: {value} (size: {lut.Size})";
                NotifyChanged();
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
    }

    [RelayCommand]
    public void Clear()
    {
        ActiveLut = null;
        LutFileName = null;
        Intensity = 100;
        SelectedPresetName = null;
        IsEnabled = false;
        StatusMessage = "LUT cleared";
        NotifyChanged();
    }

    partial void OnIntensityChanged(double _) => NotifyChanged();
}
