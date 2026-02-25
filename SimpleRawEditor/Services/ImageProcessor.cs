using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using SimpleRawEditor.ViewModels.Editor;

namespace SimpleRawEditor.Services;

public class ImageProcessor : IDisposable
{
    private WriteableBitmap? _source;
    private WriteableBitmap? _previewBuffer;
    private CancellationTokenSource? _cts;
    private int _pendingRequests;
    private readonly object _lock = new();

    private byte[]? _sourcePixels;
    private Dictionary<int, byte[]> _stepCaches = new();
    private int _cachedWidth;
    private int _cachedHeight;
    private int _cachedStride;

    public event Action<Bitmap>? ImageProcessed;
    public event Action<string>? ProcessingError;

    public void SetSource(WriteableBitmap source)
    {
        if (!ReferenceEquals(_source, source))
        {
            _source = source;
            ClearCaches();
        }
    }

    public void ClearCaches()
    {
        _sourcePixels = null;
        _stepCaches.Clear();
    }

    public void Cancel()
    {
        _cts?.Cancel();
    }

    public void RequestProcessing(IReadOnlyList<AdjustmentStep> steps, bool isPreview)
    {
        RequestProcessingFrom(null, steps, isPreview);
    }

    public void RequestProcessingFrom(AdjustmentStep? changedStep, IReadOnlyList<AdjustmentStep> steps, bool isPreview)
    {
        Interlocked.Increment(ref _pendingRequests);
        
        Task.Run(async () =>
        {
            await Task.Delay(16);
            
            if (Interlocked.Decrement(ref _pendingRequests) > 0)
                return;

            lock (_lock)
            {
                _cts?.Cancel();
                _cts = new CancellationTokenSource();
                
                try
                {
                    var result = ProcessInternalFrom(changedStep, steps, isPreview, _cts.Token);
                    
                    if (result != null)
                    {
                        Dispatcher.UIThread.Post(() => ImageProcessed?.Invoke(result));
                    }
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception ex)
                {
                    Dispatcher.UIThread.Post(() => ProcessingError?.Invoke(ex.Message));
                }
            }
        });
    }

    private WriteableBitmap? ProcessInternalFrom(AdjustmentStep? changedStep, IReadOnlyList<AdjustmentStep> steps, bool isPreview, CancellationToken ct)
    {
        if (_source == null) return null;

        ct.ThrowIfCancellationRequested();

        if (isPreview)
        {
            return ProcessPreview(_source, steps, ct);
        }
        else
        {
            return ProcessFullResolutionFrom(changedStep, steps, ct);
        }
    }

    private WriteableBitmap ProcessFullResolutionFrom(AdjustmentStep? changedStep, IReadOnlyList<AdjustmentStep> steps, CancellationToken ct)
    {
        var pixelSize = _source!.PixelSize;
        int width = pixelSize.Width;
        int height = pixelSize.Height;

        using var srcBuffer = _source.Lock();
        int stride = srcBuffer.RowBytes;
        int totalBytes = height * stride;

        bool dimensionChanged = width != _cachedWidth || height != _cachedHeight || stride != _cachedStride;
        if (dimensionChanged)
        {
            ClearCaches();
            _cachedWidth = width;
            _cachedHeight = height;
            _cachedStride = stride;
        }

        if (_sourcePixels == null)
        {
            _sourcePixels = new byte[totalBytes];
            Marshal.Copy(srcBuffer.Address, _sourcePixels, 0, totalBytes);
        }

        int startIndex = 0;
        if (changedStep != null)
        {
            for (int i = 0; i < steps.Count; i++)
            {
                if (steps[i] == changedStep)
                {
                    startIndex = i;
                    break;
                }
            }
        }

        byte[] pixels;
        if (startIndex == 0)
        {
            pixels = new byte[totalBytes];
            Array.Copy(_sourcePixels, pixels, totalBytes);
            _stepCaches.Clear();
        }
        else if (_stepCaches.TryGetValue(startIndex - 1, out var cachedPixels))
        {
            pixels = new byte[totalBytes];
            Array.Copy(cachedPixels, pixels, totalBytes);
            
            for (int i = startIndex; i < steps.Count; i++)
            {
                _stepCaches.Remove(i);
            }
        }
        else
        {
            pixels = new byte[totalBytes];
            Array.Copy(_sourcePixels, pixels, totalBytes);
            startIndex = 0;
            _stepCaches.Clear();
        }

        for (int i = startIndex; i < steps.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var step = steps[i];
            if (step.IsEnabled)
            {
                step.Apply(pixels, width, height, stride);
            }
            
            var cacheCopy = new byte[totalBytes];
            Array.Copy(pixels, cacheCopy, totalBytes);
            _stepCaches[i] = cacheCopy;
        }

        var result = new WriteableBitmap(pixelSize, _source.Dpi, _source.Format, _source.AlphaFormat);
        using var destBuffer = result.Lock();
        Marshal.Copy(pixels, 0, destBuffer.Address, pixels.Length);

        return result;
    }

    private WriteableBitmap ProcessPreview(WriteableBitmap source, IReadOnlyList<AdjustmentStep> steps, CancellationToken ct)
    {
        var pixelSize = source.PixelSize;
        int divisor = 4;
        int previewWidth = Math.Max(pixelSize.Width / divisor, 100);
        int previewHeight = Math.Max(pixelSize.Height / divisor, 100);

        EnsurePreviewBuffer(previewWidth, previewHeight, source);

        using var srcBuffer = source.Lock();
        using var destBuffer = _previewBuffer!.Lock();

        int srcStride = srcBuffer.RowBytes;
        int destStride = destBuffer.RowBytes;
        int srcWidth = pixelSize.Width;
        int srcHeight = pixelSize.Height;

        byte[] srcPixels = new byte[srcHeight * srcStride];
        Marshal.Copy(srcBuffer.Address, srcPixels, 0, srcPixels.Length);

        var stepsList = new List<AdjustmentStep>(steps);

        unsafe
        {
            byte* destPtr = (byte*)destBuffer.Address;

            Parallel.For(0, previewHeight, y =>
            {
                ct.ThrowIfCancellationRequested();

                int srcY = y * divisor;
                if (srcY >= srcHeight) return;

                int destRowStart = y * destStride;
                int srcRowStart = srcY * srcStride;

                for (int x = 0; x < previewWidth; x++)
                {
                    int srcX = x * divisor;
                    if (srcX >= srcWidth) break;

                    int destIndex = destRowStart + x * 4;
                    int srcIndex = srcRowStart + srcX * 4;

                    float b = srcPixels[srcIndex];
                    float g = srcPixels[srcIndex + 1];
                    float r = srcPixels[srcIndex + 2];
                    byte a = srcPixels[srcIndex + 3];

                    byte[] pixelBuffer = new byte[] { (byte)b, (byte)g, (byte)r, a };

                    foreach (var step in stepsList)
                    {
                        if (step.IsEnabled)
                        {
                            step.Apply(pixelBuffer, 1, 1, 4);
                            b = pixelBuffer[0];
                            g = pixelBuffer[1];
                            r = pixelBuffer[2];
                        }
                    }

                    destPtr[destIndex] = ClampByte(b);
                    destPtr[destIndex + 1] = ClampByte(g);
                    destPtr[destIndex + 2] = ClampByte(r);
                    destPtr[destIndex + 3] = a;
                }
            });
        }

        return _previewBuffer;
    }

    private void EnsurePreviewBuffer(int width, int height, WriteableBitmap source)
    {
        if (_previewBuffer == null ||
            _previewBuffer.PixelSize.Width != width ||
            _previewBuffer.PixelSize.Height != height)
        {
            _previewBuffer?.Dispose();
            _previewBuffer = new WriteableBitmap(
                new PixelSize(width, height),
                source.Dpi,
                source.Format,
                source.AlphaFormat);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte ClampByte(float value)
    {
        if (value >= 255f) return 255;
        if (value <= 0f) return 0;
        return (byte)(value + 0.5f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Smoothstep(float edge0, float edge1, float x)
    {
        float t = Math.Clamp((x - edge0) / (edge1 - edge0), 0.0f, 1.0f);
        return t * t * (3.0f - 2.0f * t);
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _previewBuffer?.Dispose();
    }
}
