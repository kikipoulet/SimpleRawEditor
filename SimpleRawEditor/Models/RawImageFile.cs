using System;
using Avalonia.Media.Imaging;

namespace SimpleRawEditor.Models;

public class RawImageFile
{
    public string FilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public Bitmap? Thumbnail { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public string CameraModel { get; set; } = string.Empty;
    public DateTime CaptureDate { get; set; }
}
