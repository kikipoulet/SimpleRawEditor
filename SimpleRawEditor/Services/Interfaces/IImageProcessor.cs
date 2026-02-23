using System;
using Avalonia.Media.Imaging;
using SimpleRawEditor.Models;

namespace SimpleRawEditor.Services.Interfaces;

public interface IImageProcessor : IDisposable
{
    event EventHandler<Bitmap>? ImageProcessed;
    event EventHandler<string>? ProcessingError;

    void SetOriginalBitmap(Bitmap? bitmap);
    void InvalidateDenoiseCache();
    void RequestProcessing(ImageAdjustments adjustments, bool isDragging);
}
