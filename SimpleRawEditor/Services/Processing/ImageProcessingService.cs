using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using SimpleRawEditor.Models;
using SimpleRawEditor.Services.Processing;
using SimpleRawEditor.ViewModels.Editor.Adjustments;

namespace SimpleRawEditor.Services.Processing;

public class ImageProcessingService : IDisposable
{
    private WriteableBitmap? _previewBitmap;
    private readonly int _previewDivisor = 4;

    private readonly DenoisingHandler _denoisingHandler;

    public ImageProcessingService(DenoisingHandler? denoisingHandler = null)
    {
        _denoisingHandler = denoisingHandler ?? new DenoisingHandler();
    }

    public void InitializeCache(WriteableBitmap source)
    {
        var pixelSize = source.PixelSize;
        int width = pixelSize.Width;
        int height = pixelSize.Height;

        using var srcBuffer = source.Lock();
        int stride = srcBuffer.RowBytes;
        int totalBytes = height * stride;

        byte[] pixels = new byte[totalBytes];
        System.Runtime.InteropServices.Marshal.Copy(srcBuffer.Address, pixels, 0, totalBytes);

        _denoisingHandler.InitializeCache(pixels, width, height, stride);

        Console.WriteLine($"[ImageProcessingService] Cache initialized - size: {width}x{height}, stride: {stride}");
    }

    public void InvalidateDenoiseCache()
    {
        _denoisingHandler.InvalidateCache();
        Console.WriteLine("[ImageProcessingService] Denoise cache invalidated");
    }

    public unsafe Bitmap ApplySteps(IEnumerable<IAdjustmentStep> steps, Bitmap source)
    {
        Console.WriteLine("[ImageProcessingService] ApplySteps START");

        if (source is not WriteableBitmap writeableSource)
        {
            Console.WriteLine("[ImageProcessingService] Source is not WriteableBitmap, returning as-is");
            return source;
        }

        var pixelSize = writeableSource.PixelSize;
        int width = pixelSize.Width;
        int height = pixelSize.Height;

        using var srcBuffer = writeableSource.Lock();
        int stride = srcBuffer.RowBytes;
        int totalBytes = height * stride;

        byte[] pixels = new byte[totalBytes];
        System.Runtime.InteropServices.Marshal.Copy(srcBuffer.Address, pixels, 0, totalBytes);

        foreach (var step in steps)
        {
            if (step.IsEnabled)
            {
                Console.WriteLine($"[ImageProcessingService] Applying step: {step.Name}");
                step.Apply(pixels, width, height, stride);
            }
        }

        var result = new WriteableBitmap(
            pixelSize,
            writeableSource.Dpi,
            writeableSource.Format,
            writeableSource.AlphaFormat);

        using var destBuffer = result.Lock();
        System.Runtime.InteropServices.Marshal.Copy(pixels, 0, destBuffer.Address, pixels.Length);

        Console.WriteLine("[ImageProcessingService] ApplySteps completed");
        return result;
    }

    public unsafe Bitmap ApplyStepsFast(IEnumerable<IAdjustmentStep> steps, WriteableBitmap source, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();

        var pixelSize = source.PixelSize;
        int previewWidth = Math.Max(pixelSize.Width / _previewDivisor, 100);
        int previewHeight = Math.Max(pixelSize.Height / _previewDivisor, 100);

        if (_previewBitmap == null ||
            _previewBitmap.PixelSize.Width != previewWidth ||
            _previewBitmap.PixelSize.Height != previewHeight)
        {
            _previewBitmap?.Dispose();
            _previewBitmap = new WriteableBitmap(
                new PixelSize(previewWidth, previewHeight),
                source.Dpi,
                source.Format,
                source.AlphaFormat);
        }

        using var srcBuffer = source.Lock();
        using var destBuffer = _previewBitmap.Lock();

        byte* srcPtr = (byte*)srcBuffer.Address;
        byte* destPtr = (byte*)destBuffer.Address;
        int srcStride = srcBuffer.RowBytes;
        int destStride = destBuffer.RowBytes;
        int srcWidth = pixelSize.Width;
        int srcHeight = pixelSize.Height;

        byte[] srcPixels = new byte[srcHeight * srcStride];
        for (int y = 0; y < srcHeight; y++)
        {
            int rowStart = y * srcStride;
            for (int x = 0; x < srcWidth * 4; x++)
            {
                srcPixels[rowStart + x] = srcPtr[rowStart + x];
            }
        }

        var stepsList = new List<IAdjustmentStep>(steps);
        
        for (int y = 0; y < previewHeight; y++)
        {
            int srcY = y * _previewDivisor;
            if (srcY >= srcHeight) break;

            int destRowStart = y * destStride;
            int srcRowStart = srcY * srcStride;

            for (int x = 0; x < previewWidth; x++)
            {
                int srcX = x * _previewDivisor;
                if (srcX >= srcWidth) break;

                int destIndex = destRowStart + x * 4;
                int srcIndex = srcRowStart + srcX * 4;

                float b = srcPixels[srcIndex];
                float g = srcPixels[srcIndex + 1];
                float r = srcPixels[srcIndex + 2];

                byte[] pixelBytes = new byte[] { (byte)b, (byte)g, (byte)r, srcPixels[srcIndex + 3] };
                
                foreach (var step in stepsList)
                {
                    if (step.IsEnabled)
                    {
                        var smallPixels = new byte[4 * previewHeight * previewWidth];
                        int smallIndex = (y * previewWidth + x) * 4;
                        smallPixels[smallIndex] = (byte)b;
                        smallPixels[smallIndex + 1] = (byte)g;
                        smallPixels[smallIndex + 2] = (byte)r;
                        smallPixels[smallIndex + 3] = srcPixels[srcIndex + 3];
                        
                        step.Apply(smallPixels, previewWidth, previewHeight, previewWidth * 4);
                        
                        b = smallPixels[smallIndex];
                        g = smallPixels[smallIndex + 1];
                        r = smallPixels[smallIndex + 2];
                    }
                }

                destPtr[destIndex] = (byte)b;
                destPtr[destIndex + 1] = (byte)g;
                destPtr[destIndex + 2] = (byte)r;
                destPtr[destIndex + 3] = srcPixels[srcIndex + 3];
            }
        }

        return _previewBitmap;
    }

    public unsafe Bitmap ApplyAdjustments(Bitmap source, ImageAdjustments adjustments)
    {
        Console.WriteLine("[ImageProcessingService] ApplyAdjustments START");

        if (source is not WriteableBitmap writeableSource)
        {
            Console.WriteLine("[ImageProcessingService] Source is not WriteableBitmap, returning as-is");
            return source;
        }

        var p = AdjustmentPrecompute.FromImageAdjustments(adjustments);

        var pixelSize = writeableSource.PixelSize;
        int width = pixelSize.Width;
        int height = pixelSize.Height;

        byte[] workingPixels;
        int stride;

        var (cachedWidth, cachedHeight, cachedStride) = _denoisingHandler.GetCachedDimensions();
        var originalPixels = _denoisingHandler.GetOriginalPixels();

        if (originalPixels != null && cachedWidth == width && cachedHeight == height)
        {
            stride = cachedStride;

            if (p.HasDenoise)
            {
                Console.WriteLine($"[ImageProcessingService] Using denoise cache system - DenoiseStrength: {p.DenoiseStrength}");
                workingPixels = _denoisingHandler.GetDenoisedPixels(p.DenoiseStrength);
            }
            else
            {
                Console.WriteLine("[ImageProcessingService] No denoise, using original from cache");
                workingPixels = originalPixels;
            }
        }
        else
        {
            Console.WriteLine("[ImageProcessingService] Cache not available, extracting pixels");

            using (var srcBuffer = writeableSource.Lock())
            {
                stride = srcBuffer.RowBytes;
                int totalBytes = height * stride;
                workingPixels = new byte[totalBytes];
                System.Runtime.InteropServices.Marshal.Copy(srcBuffer.Address, workingPixels, 0, totalBytes);
            }
        }

        byte[] resultPixels = new byte[workingPixels.Length];
        Array.Copy(workingPixels, resultPixels, workingPixels.Length);

        Console.WriteLine("[ImageProcessingService] Applying tone adjustments...");
        ApplyToneAdjustments(resultPixels, width, height, stride, p);

        Console.WriteLine("[ImageProcessingService] Creating result bitmap...");
        var result = new WriteableBitmap(
            pixelSize,
            writeableSource.Dpi,
            writeableSource.Format,
            writeableSource.AlphaFormat);

        using (var destBuffer = result.Lock())
        {
            System.Runtime.InteropServices.Marshal.Copy(resultPixels, 0, destBuffer.Address, resultPixels.Length);
        }

        Console.WriteLine("[ImageProcessingService] ApplyAdjustments completed");
        return result;
    }

    public unsafe Bitmap? ApplyAdjustmentsFast(WriteableBitmap source, ImageAdjustments adjustments, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();

        var pixelSize = source.PixelSize;
        int previewWidth = Math.Max(pixelSize.Width / _previewDivisor, 100);
        int previewHeight = Math.Max(pixelSize.Height / _previewDivisor, 100);

        if (_previewBitmap == null ||
            _previewBitmap.PixelSize.Width != previewWidth ||
            _previewBitmap.PixelSize.Height != previewHeight)
        {
            _previewBitmap?.Dispose();
            _previewBitmap = new WriteableBitmap(
                new PixelSize(previewWidth, previewHeight),
                source.Dpi,
                source.Format,
                source.AlphaFormat);
        }

        using var srcBuffer = source.Lock();
        using var destBuffer = _previewBitmap.Lock();

        byte* srcPtr = (byte*)srcBuffer.Address;
        byte* destPtr = (byte*)destBuffer.Address;
        int srcStride = srcBuffer.RowBytes;
        int destStride = destBuffer.RowBytes;
        int srcWidth = pixelSize.Width;
        int srcHeight = pixelSize.Height;

        var p = AdjustmentPrecompute.FromImageAdjustments(adjustments);

        Parallel.For(0, previewHeight, y =>
        {
            token.ThrowIfCancellationRequested();

            int srcY = y * _previewDivisor;
            if (srcY >= srcHeight) return;

            int destRowStart = y * destStride;
            int srcRowStart = srcY * srcStride;

            for (int x = 0; x < previewWidth; x++)
            {
                int srcX = x * _previewDivisor;
                if (srcX >= srcWidth) break;

                int destIndex = destRowStart + x * 4;
                int srcIndex = srcRowStart + srcX * 4;

                float b = srcPtr[srcIndex];
                float g = srcPtr[srcIndex + 1];
                float r = srcPtr[srcIndex + 2];

                float luminance = (0.299f * r + 0.587f * g + 0.114f * b) / 255.0f;

                r = ToneAdjustmentHandler.ApplyToneCurve(r / 255.0f, luminance, in p) * 255.0f;
                g = ToneAdjustmentHandler.ApplyToneCurve(g / 255.0f, luminance, in p) * 255.0f;
                b = ToneAdjustmentHandler.ApplyToneCurve(b / 255.0f, luminance, in p) * 255.0f;

                if (p.HasLut)
                    LutApplicationHandler.ApplyLutInline(ref r, ref g, ref b, in p);

                if (p.HasVignette)
                    VignetteHandler.ApplyVignette(ref r, ref g, ref b, srcX, srcY, srcWidth, srcHeight, in p);

                destPtr[destIndex] = ToneAdjustmentHandler.ClampByte(b);
                destPtr[destIndex + 1] = ToneAdjustmentHandler.ClampByte(g);
                destPtr[destIndex + 2] = ToneAdjustmentHandler.ClampByte(r);
                destPtr[destIndex + 3] = srcPtr[srcIndex + 3];
            }
        });

        return _previewBitmap;
    }

    private static void ApplyToneAdjustments(byte[] pixels, int width, int height, int stride, PrecomputedAdjustments p)
    {
        const float inv255 = 1.0f / 255.0f;

        Parallel.For(0, height, y =>
        {
            int rowStart = y * stride;

            for (int x = 0; x < width; x++)
            {
                int index = rowStart + x * 4;

                float b = pixels[index];
                float g = pixels[index + 1];
                float r = pixels[index + 2];

                float luminance = (0.299f * r + 0.587f * g + 0.114f * b) * inv255;

                r = ToneAdjustmentHandler.ApplyToneCurve(r * inv255, luminance, in p) * 255.0f;
                g = ToneAdjustmentHandler.ApplyToneCurve(g * inv255, luminance, in p) * 255.0f;
                b = ToneAdjustmentHandler.ApplyToneCurve(b * inv255, luminance, in p) * 255.0f;

                if (p.HasLut)
                    LutApplicationHandler.ApplyLutInline(ref r, ref g, ref b, in p);

                if (p.HasVignette)
                    VignetteHandler.ApplyVignette(ref r, ref g, ref b, x, y, width, height, in p);

                pixels[index] = ToneAdjustmentHandler.ClampByte(b);
                pixels[index + 1] = ToneAdjustmentHandler.ClampByte(g);
                pixels[index + 2] = ToneAdjustmentHandler.ClampByte(r);
            }
        });
    }

    public void Dispose()
    {
        _previewBitmap?.Dispose();
    }
}
