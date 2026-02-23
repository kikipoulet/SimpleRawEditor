using System;
using System.Threading.Tasks;
using SimpleRawEditor.Services.Denoising;

namespace SimpleRawEditor.Services.Processing;

public class DenoisingHandler
{
    private readonly IDenoisingAlgorithm _denoiser;

    private byte[]? _originalPixels;
    private byte[]? _denoisedPixelsCache;
    private int _cachedWidth;
    private int _cachedHeight;
    private int _cachedStride;
    private float _cachedDenoiseAmount = -1;

    private readonly object _cacheLock = new();

    public DenoisingHandler(IDenoisingAlgorithm? denoiser = null)
    {
        _denoiser = denoiser ?? new BilateralFilter();
    }

    public void InitializeCache(byte[] originalPixels, int width, int height, int stride)
    {
        lock (_cacheLock)
        {
            _cachedWidth = width;
            _cachedHeight = height;
            _cachedStride = stride;

            int totalBytes = height * stride;
            _originalPixels = new byte[totalBytes];
            Array.Copy(originalPixels, _originalPixels, totalBytes);

            _denoisedPixelsCache = null;
            _cachedDenoiseAmount = -1;

            Console.WriteLine($"[DenoisingHandler] Cache initialized - size: {width}x{height}, stride: {stride}");
        }
    }

    public void InvalidateCache()
    {
        lock (_cacheLock)
        {
            _denoisedPixelsCache = null;
            _cachedDenoiseAmount = -1;
            Console.WriteLine("[DenoisingHandler] Cache invalidated");
        }
    }

    public byte[]? GetOriginalPixels()
    {
        lock (_cacheLock)
        {
            return _originalPixels;
        }
    }

    public (int width, int height, int stride) GetCachedDimensions()
    {
        lock (_cacheLock)
        {
            return (_cachedWidth, _cachedHeight, _cachedStride);
        }
    }

    public byte[] GetDenoisedPixels(float denoiseStrength)
    {
        lock (_cacheLock)
        {
            if (_denoisedPixelsCache != null &&
                Math.Abs(_cachedDenoiseAmount - denoiseStrength) < 0.01f)
            {
                Console.WriteLine("[DenoisingHandler] Using cached denoised pixels");
                return _denoisedPixelsCache;
            }

            Console.WriteLine($"[DenoisingHandler] Computing denoising - strength: {denoiseStrength}");

            if (_originalPixels == null)
            {
                Console.WriteLine("[DenoisingHandler] No original pixels in cache!");
                return Array.Empty<byte>();
            }

            float blendFactor = Math.Clamp(denoiseStrength / 100.0f, 0.0f, 1.0f);

            if (blendFactor < 0.01f)
            {
                Console.WriteLine("[DenoisingHandler] Blend factor too low, returning original");
                return _originalPixels;
            }

            Console.WriteLine($"[DenoisingHandler] Using algorithm: {_denoiser.Name}");

            var denoised = _denoiser.Process(_originalPixels, _cachedWidth, _cachedHeight, _cachedStride, denoiseStrength);

            if (denoised != null)
            {
                if (blendFactor < 0.99f)
                {
                    Console.WriteLine($"[DenoisingHandler] Applying blend factor: {blendFactor:F2}");
                    BlendWithOriginal(_originalPixels, denoised, blendFactor);
                }

                _denoisedPixelsCache = denoised;
                _cachedDenoiseAmount = denoiseStrength;

                Console.WriteLine("[DenoisingHandler] Denoising completed and cached");
                return _denoisedPixelsCache;
            }
            else
            {
                Console.WriteLine("[DenoisingHandler] Denoising returned null, using original");
                return _originalPixels;
            }
        }
    }

    private static void BlendWithOriginal(byte[] original, byte[] denoised, float blendFactor)
    {
        float oneMinus = 1.0f - blendFactor;

        Parallel.For(0, denoised.Length, i =>
        {
            denoised[i] = (byte)(original[i] * oneMinus + denoised[i] * blendFactor);
        });
    }
}
