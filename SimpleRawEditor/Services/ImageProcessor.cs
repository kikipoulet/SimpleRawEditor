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

    private byte[]? _denoiseCache;
    private float _cachedDenoiseStrength = -1;

    public event Action<Bitmap>? ImageProcessed;
    public event Action<string>? ProcessingError;

    public void SetSource(WriteableBitmap source)
    {
        _source = source;
        InvalidateDenoiseCache();
    }

    public void Cancel()
    {
        _cts?.Cancel();
    }

    public void RequestProcessing(IReadOnlyList<AdjustmentStep> steps, bool isPreview)
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
                    var result = ProcessInternal(steps, isPreview, _cts.Token);
                    
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

    private WriteableBitmap? ProcessInternal(IReadOnlyList<AdjustmentStep> steps, bool isPreview, CancellationToken ct)
    {
        if (_source == null) return null;

        ct.ThrowIfCancellationRequested();

        if (isPreview)
        {
            return ProcessPreview(_source, steps, ct);
        }
        else
        {
            return ProcessFullResolution(_source, steps, ct);
        }
    }

    private WriteableBitmap ProcessFullResolution(WriteableBitmap source, IReadOnlyList<AdjustmentStep> steps, CancellationToken ct)
    {
        var pixelSize = source.PixelSize;
        int width = pixelSize.Width;
        int height = pixelSize.Height;

        using var srcBuffer = source.Lock();
        int stride = srcBuffer.RowBytes;
        int totalBytes = height * stride;

        byte[] pixels = new byte[totalBytes];
        Marshal.Copy(srcBuffer.Address, pixels, 0, totalBytes);

        foreach (var step in steps)
        {
            ct.ThrowIfCancellationRequested();
            if (step.IsEnabled)
            {
                step.Apply(pixels, width, height, stride);
            }
        }

        var result = new WriteableBitmap(pixelSize, source.Dpi, source.Format, source.AlphaFormat);
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

    public void InvalidateDenoiseCache()
    {
        _denoiseCache = null;
        _cachedDenoiseStrength = -1;
    }

    public byte[]? GetDenoisedPixels(byte[] originalPixels, int width, int height, int stride, float strength)
    {
        if (Math.Abs(_cachedDenoiseStrength - strength) < 0.01f && _denoiseCache != null)
        {
            return _denoiseCache;
        }

        var denoised = ApplyBilateralFilter(originalPixels, width, height, stride, strength);
        
        if (denoised != null)
        {
            _denoiseCache = denoised;
            _cachedDenoiseStrength = strength;
        }

        return denoised;
    }

    private static byte[]? ApplyBilateralFilter(byte[] sourcePixels, int width, int height, int stride, float strength)
    {
        if (strength < 0.5f) return null;
        if (width < 8 || height < 8) return null;

        int radius = Math.Clamp((int)(2 + (strength / 100f) * 6), 2, 8);
        float spatialSigma = radius / 2.0f;
        float rangeSigma = 15f + (strength / 100f) * 45f;

        int kernelSize = radius * 2 + 1;
        float[] spatialWeights = new float[kernelSize * kernelSize];

        for (int ky = -radius, idx = 0; ky <= radius; ky++)
        {
            for (int kx = -radius; kx <= radius; kx++, idx++)
            {
                float dist = kx * kx + ky * ky;
                spatialWeights[idx] = MathF.Exp(-dist / (2 * spatialSigma * spatialSigma));
            }
        }

        byte[] result = new byte[sourcePixels.Length];
        float rangeCoeff = 1.0f / (2 * rangeSigma * rangeSigma);

        Parallel.For(0, height, y =>
        {
            int rowStart = y * stride;

            for (int x = 0; x < width; x++)
            {
                int centerIdx = rowStart + x * 4;
                float centerR = sourcePixels[centerIdx + 2];
                float centerG = sourcePixels[centerIdx + 1];
                float centerB = sourcePixels[centerIdx];

                float sumR = 0, sumG = 0, sumB = 0;
                float weightSum = 0;

                int kyStart = Math.Max(-radius, -y);
                int kyEnd = Math.Min(radius, height - 1 - y);
                int kxStart = Math.Max(-radius, -x);
                int kxEnd = Math.Min(radius, width - 1 - x);

                for (int ky = kyStart; ky <= kyEnd; ky++)
                {
                    int ny = y + ky;
                    int neighborRowStart = ny * stride;

                    for (int kx = kxStart; kx <= kxEnd; kx++)
                    {
                        int nx = x + kx;
                        int neighborIdx = neighborRowStart + nx * 4;

                        float neighborR = sourcePixels[neighborIdx + 2];
                        float neighborG = sourcePixels[neighborIdx + 1];
                        float neighborB = sourcePixels[neighborIdx];

                        float diffR = centerR - neighborR;
                        float diffG = centerG - neighborG;
                        float diffB = centerB - neighborB;
                        float colorDist = diffR * diffR + diffG * diffG + diffB * diffB;

                        float rangeWeight = MathF.Exp(-colorDist * rangeCoeff);
                        int kernelIdx = (ky + radius) * kernelSize + (kx + radius);
                        float weight = spatialWeights[kernelIdx] * rangeWeight;

                        sumR += neighborR * weight;
                        sumG += neighborG * weight;
                        sumB += neighborB * weight;
                        weightSum += weight;
                    }
                }

                float invWeight = 1.0f / weightSum;
                result[centerIdx + 2] = ClampByte(sumR * invWeight);
                result[centerIdx + 1] = ClampByte(sumG * invWeight);
                result[centerIdx] = ClampByte(sumB * invWeight);
                result[centerIdx + 3] = sourcePixels[centerIdx + 3];
            }
        });

        return result;
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
