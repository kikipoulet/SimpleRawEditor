using System;
using Avalonia.Media.Imaging;

namespace SimpleRawEditor.Models;

public class RawImageData : IDisposable
{
    public string FilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public Bitmap? OriginalBitmap { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    
    public ImageMetadata ? Metadata { get; set; }

    public void Dispose()
    {
        OriginalBitmap?.Dispose();
    }
}
