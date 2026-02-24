using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using SimpleRawEditor.Models;
using SimpleRawEditor.Services.Core;
using SimpleRawEditor.ViewModels.Editor.Adjustments;

namespace SimpleRawEditor.Services.Processing;

public class DebouncedImageProcessor : IImageProcessor
{
    private readonly ImageProcessingService _processingService;
    private CancellationTokenSource? _cancellationTokenSource;
    private readonly Timer _debounceTimer;
    private readonly object _lockObject = new();

    private Bitmap? _originalBitmap;
    private ImageAdjustments? _pendingAdjustments;
    private IEnumerable<IAdjustmentStep>? _pendingSteps;
    private bool _isDragging;

    public event EventHandler<Bitmap>? ImageProcessed;
    public event EventHandler<string>? ProcessingError;

    private const int DebounceDelayMs = 50;

    public DebouncedImageProcessor(ImageProcessingService? processingService = null)
    {
        _processingService = processingService ?? new ImageProcessingService();
        _debounceTimer = new Timer(OnDebounceElapsed, null, Timeout.Infinite, Timeout.Infinite);
    }

    public void SetOriginalBitmap(Bitmap? bitmap)
    {
        lock (_lockObject)
        {
            _originalBitmap?.Dispose();
            _originalBitmap = bitmap;
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource = null;

            if (bitmap is WriteableBitmap writeableBitmap)
            {
                _processingService.InitializeCache(writeableBitmap);
            }
        }
    }

    public void InvalidateDenoiseCache()
    {
        _processingService.InvalidateDenoiseCache();
    }

    public void RequestProcessing(ImageAdjustments adjustments, bool isDragging)
    {
        lock (_lockObject)
        {
            _isDragging = isDragging;
            _pendingAdjustments = new ImageAdjustments
            {
                Exposure = adjustments.Exposure,
                Highlights = adjustments.Highlights,
                Contrast = adjustments.Contrast,
                Shadows = adjustments.Shadows,
                IsDenoiseEnabled = adjustments.IsDenoiseEnabled,
                DenoiseAmount = adjustments.DenoiseAmount,
                IsLutEnabled = adjustments.IsLutEnabled,
                ActiveLut = adjustments.ActiveLut,
                LutIntensity = adjustments.LutIntensity,
                IsVignetteEnabled = adjustments.IsVignetteEnabled,
                VignetteIntensity = adjustments.VignetteIntensity,
                VignetteSpread = adjustments.VignetteSpread
            };

            Console.WriteLine($"[DebouncedImageProcessor] RequestProcessing - DenoiseAmount: {adjustments.DenoiseAmount}, IsDragging: {isDragging}");

            _debounceTimer.Change(Timeout.Infinite, Timeout.Infinite);
            _debounceTimer.Change(DebounceDelayMs, Timeout.Infinite);
        }
    }

    public void RequestProcessingWithSteps(IEnumerable<IAdjustmentStep> steps, bool isDragging)
    {
        lock (_lockObject)
        {
            _isDragging = isDragging;
            _pendingSteps = steps;

            Console.WriteLine($"[DebouncedImageProcessor] RequestProcessingWithSteps - IsDragging: {isDragging}");

            _debounceTimer.Change(Timeout.Infinite, Timeout.Infinite);
            _debounceTimer.Change(DebounceDelayMs, Timeout.Infinite);
        }
    }

    private void OnDebounceElapsed(object? state)
    {
        lock (_lockObject)
        {
            Console.WriteLine($"[DebouncedImageProcessor] OnDebounceElapsed - HasBitmap: {_originalBitmap != null}, HasAdjustments: {_pendingAdjustments != null}, HasSteps: {_pendingSteps != null}");

            if (_originalBitmap == null)
                return;

            if (_pendingSteps != null)
            {
                _cancellationTokenSource?.Cancel();
                _cancellationTokenSource = new CancellationTokenSource();
                var token = _cancellationTokenSource.Token;

                var steps = _pendingSteps;
                var isDragging = _isDragging;
                var originalBitmap = _originalBitmap;

                Task.Run(() => ProcessImageWithStepsAsync(originalBitmap, steps, isDragging, token), token);
            }
            else if (_pendingAdjustments != null)
            {
                _cancellationTokenSource?.Cancel();
                _cancellationTokenSource = new CancellationTokenSource();
                var token = _cancellationTokenSource.Token;

                var adjustments = _pendingAdjustments;
                var isDragging = _isDragging;
                var originalBitmap = _originalBitmap;

                Task.Run(() => ProcessImageAsync(originalBitmap, adjustments, isDragging, token), token);
            }
        }
    }

    private async Task ProcessImageWithStepsAsync(Bitmap original, IEnumerable<IAdjustmentStep> steps, bool isPreview, CancellationToken token)
    {
        try
        {
            Console.WriteLine("[DebouncedImageProcessor] ProcessImageWithStepsAsync START");
            token.ThrowIfCancellationRequested();

            Bitmap? processedImage = null;

            if (isPreview && original is WriteableBitmap writeableOriginal)
            {
                Console.WriteLine("[DebouncedImageProcessor] Calling ApplyStepsFast");
                processedImage = await Task.Run(() =>
                    _processingService.ApplyStepsFast(steps, writeableOriginal, token), token);
                Console.WriteLine($"[DebouncedImageProcessor] ApplyStepsFast returned: {processedImage != null}");
            }
            else
            {
                Console.WriteLine("[DebouncedImageProcessor] Calling ApplySteps");
                processedImage = await Task.Run(() =>
                    _processingService.ApplySteps(steps, original), token);
                Console.WriteLine($"[DebouncedImageProcessor] ApplySteps returned: {processedImage != null}");
            }

            token.ThrowIfCancellationRequested();

            if (processedImage != null)
            {
                Console.WriteLine("[DebouncedImageProcessor] Notifying UI with processed image");
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    ImageProcessed?.Invoke(this, processedImage);
                });
            }
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("[DebouncedImageProcessor] Processing cancelled");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DebouncedImageProcessor] Exception: {ex}");
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                ProcessingError?.Invoke(this, $"Erreur traitement: {ex.Message}");
            });
        }
        Console.WriteLine("[DebouncedImageProcessor] ProcessImageWithStepsAsync END");
    }

    private async Task ProcessImageAsync(Bitmap original, ImageAdjustments adjustments, bool isPreview, CancellationToken token)
    {
        try
        {
            Console.WriteLine("[DebouncedImageProcessor] ProcessImageAsync START");
            token.ThrowIfCancellationRequested();

            Bitmap? processedImage = null;

            if (isPreview && original is WriteableBitmap writeableOriginal)
            {
                Console.WriteLine("[DebouncedImageProcessor] Calling ApplyAdjustmentsFast");
                processedImage = await Task.Run(() =>
                    _processingService.ApplyAdjustmentsFast(writeableOriginal, adjustments, token), token);
                Console.WriteLine($"[DebouncedImageProcessor] ApplyAdjustmentsFast returned: {processedImage != null}");
            }
            else
            {
                Console.WriteLine("[DebouncedImageProcessor] Calling ApplyAdjustments");
                processedImage = await Task.Run(() =>
                    _processingService.ApplyAdjustments(original, adjustments), token);
                Console.WriteLine($"[DebouncedImageProcessor] ApplyAdjustments returned: {processedImage != null}");
            }

            token.ThrowIfCancellationRequested();

            if (processedImage != null)
            {
                Console.WriteLine("[DebouncedImageProcessor] Notifying UI with processed image");
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    ImageProcessed?.Invoke(this, processedImage);
                });
            }
            else
            {
                Console.WriteLine("[DebouncedImageProcessor] processedImage is NULL!");
            }
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("[DebouncedImageProcessor] Processing cancelled");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DebouncedImageProcessor] Exception: {ex}");
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                ProcessingError?.Invoke(this, $"Erreur traitement: {ex.Message}");
            });
        }
        Console.WriteLine("[DebouncedImageProcessor] ProcessImageAsync END");
    }

    public void Dispose()
    {
        _debounceTimer.Dispose();
        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource?.Dispose();
        _processingService.Dispose();
        lock (_lockObject)
        {
            _originalBitmap?.Dispose();
        }
    }
}
