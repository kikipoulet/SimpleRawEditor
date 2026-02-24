using System;
using System.Collections.Generic;
using Avalonia.Media.Imaging;
using SimpleRawEditor.Models;
using SimpleRawEditor.ViewModels.Editor.Adjustments;

namespace SimpleRawEditor.Services.Core;

public interface IImageProcessor : IDisposable
{
    event EventHandler<Bitmap>? ImageProcessed;
    event EventHandler<string>? ProcessingError;

    void SetOriginalBitmap(Bitmap? bitmap);
    void InvalidateDenoiseCache();
    void RequestProcessing(ImageAdjustments adjustments, bool isDragging);
    void RequestProcessingWithSteps(IEnumerable<IAdjustmentStep> steps, bool isDragging);
}
