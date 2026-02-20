using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using SimpleRawEditor.Models;
using SimpleRawEditor.Services.Denoising;

namespace SimpleRawEditor.Services;

public class ImageProcessingService : IDisposable
{
    private WriteableBitmap? _previewBitmap;
    private int _previewDivisor = 4;

    private const float Inv255 = 1.0f / 255.0f;
    private const float Gray18Percent = 0.18f;
    private const float DeepBlack = 0.05f;
    
    // Débruiteur BM3D
    private readonly IDenoisingAlgorithm _denoiser;
    
    // Cache pour le débruitage
    private byte[]? _originalPixels;
    private byte[]? _denoisedPixelsCache;
    private int _cachedWidth;
    private int _cachedHeight;
    private int _cachedStride;
    private float _cachedDenoiseAmount = -1;  // -1 = pas de cache
    
    // Verrou pour les opérations de cache
    private readonly object _cacheLock = new();

    public ImageProcessingService()
    {
        _denoiser = new BM3DDenoising();
    }
    
    /// <summary>
    /// Initialise le cache avec les pixels de l'image originale.
    /// Doit être appelé quand une nouvelle image est chargée.
    /// </summary>
    public void InitializeCache(WriteableBitmap source)
    {
        lock (_cacheLock)
        {
            var pixelSize = source.PixelSize;
            _cachedWidth = pixelSize.Width;
            _cachedHeight = pixelSize.Height;
            
            using var srcBuffer = source.Lock();
            _cachedStride = srcBuffer.RowBytes;
            int totalBytes = _cachedHeight * _cachedStride;
            
            _originalPixels = new byte[totalBytes];
            System.Runtime.InteropServices.Marshal.Copy(srcBuffer.Address, _originalPixels, 0, totalBytes);
            
            // Invalider le cache débruité
            _denoisedPixelsCache = null;
            _cachedDenoiseAmount = -1;
            
            Console.WriteLine($"[DEBUG] Cache initialized - size: {_cachedWidth}x{_cachedHeight}, stride: {_cachedStride}");
        }
    }
    
    /// <summary>
    /// Invalide le cache de débruitage (quand DenoiseAmount change).
    /// </summary>
    public void InvalidateDenoiseCache()
    {
        lock (_cacheLock)
        {
            _denoisedPixelsCache = null;
            _cachedDenoiseAmount = -1;
            Console.WriteLine("[DEBUG] Denoise cache invalidated");
        }
    }
    
    /// <summary>
    /// Obtient les pixels débruités (depuis le cache ou en les calculant).
    /// </summary>
    private byte[] GetDenoisedPixels(float denoiseStrength)
    {
        lock (_cacheLock)
        {
            // Vérifier si le cache est valide
            if (_denoisedPixelsCache != null && 
                Math.Abs(_cachedDenoiseAmount - denoiseStrength) < 0.01f)
            {
                Console.WriteLine("[DEBUG] Using cached denoised pixels");
                return _denoisedPixelsCache;
            }
            
            // Calculer le débruitage
            Console.WriteLine($"[DEBUG] Computing denoising - strength: {denoiseStrength}");
            
            if (_originalPixels == null)
            {
                Console.WriteLine("[DEBUG] No original pixels in cache!");
                return new byte[0];
            }
            
            // Blend factor basé sur la strength (0-100 -> 0-1)
            float blendFactor = Math.Clamp(denoiseStrength / 100.0f, 0.0f, 1.0f);
            
            // Si blend factor très bas, pas besoin de débruitage
            if (blendFactor < 0.01f)
            {
                Console.WriteLine("[DEBUG] Blend factor too low, returning original");
                return _originalPixels;
            }
            
            Console.WriteLine($"[DEBUG] Using algorithm: {_denoiser.Name}");
            
            var denoised = _denoiser.Process(_originalPixels, _cachedWidth, _cachedHeight, _cachedStride, denoiseStrength);
            
            if (denoised != null)
            {
                // Appliquer le blending si nécessaire
                if (blendFactor < 0.99f)
                {
                    Console.WriteLine($"[DEBUG] Applying blend factor: {blendFactor:F2}");
                    BlendWithOriginal(_originalPixels, denoised, blendFactor);
                }
                
                // Mettre en cache
                _denoisedPixelsCache = denoised;
                _cachedDenoiseAmount = denoiseStrength;
                
                Console.WriteLine("[DEBUG] Denoising completed and cached");
                return _denoisedPixelsCache;
            }
            else
            {
                Console.WriteLine("[DEBUG] Denoising returned null, using original");
                return _originalPixels;
            }
        }
    }
    
    /// <summary>
    /// Blend les pixels débruités avec l'original selon le facteur.
    /// </summary>
    private static void BlendWithOriginal(byte[] original, byte[] denoised, float blendFactor)
    {
        float oneMinus = 1.0f - blendFactor;
        
        Parallel.For(0, denoised.Length, i =>
        {
            denoised[i] = (byte)(original[i] * oneMinus + denoised[i] * blendFactor);
        });
    }

    public unsafe Bitmap ApplyAdjustments(Bitmap source, ImageAdjustments adjustments)
    {
        Console.WriteLine("[DEBUG] ApplyAdjustments START");
        
        if (source is not WriteableBitmap writeableSource)
        {
            Console.WriteLine("[DEBUG] Source is not WriteableBitmap, returning as-is");
            return source;
        }

        var p = PrecomputeAdjustments(adjustments);
        
        var pixelSize = writeableSource.PixelSize;
        int width = pixelSize.Width;
        int height = pixelSize.Height;
        
        // Utiliser le cache si disponible
        byte[] workingPixels;
        int stride;
        
        if (_originalPixels != null && _cachedWidth == width && _cachedHeight == height)
        {
            // Utiliser le cache
            stride = _cachedStride;
            
            if (p.HasDenoise)
            {
                Console.WriteLine($"[DEBUG] Using denoise cache system - DenoiseStrength: {p.DenoiseStrength}");
                workingPixels = GetDenoisedPixels(p.DenoiseStrength);
            }
            else
            {
                Console.WriteLine("[DEBUG] No denoise, using original from cache");
                workingPixels = _originalPixels;
            }
        }
        else
        {
            // Fallback: extraire les pixels (premier chargement)
            Console.WriteLine("[DEBUG] Cache not available, extracting pixels");
            
            using (var srcBuffer = writeableSource.Lock())
            {
                stride = srcBuffer.RowBytes;
                int totalBytes = height * stride;
                workingPixels = new byte[totalBytes];
                System.Runtime.InteropServices.Marshal.Copy(srcBuffer.Address, workingPixels, 0, totalBytes);
            }
            
            // Appliquer débruitage si nécessaire
            if (p.HasDenoise)
            {
                try
                {
                    var denoised = _denoiser.Process(workingPixels, width, height, stride, p.DenoiseStrength);
                    if (denoised != null)
                        workingPixels = denoised;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[DEBUG] Denoising error: {ex.Message}");
                }
            }
        }
        
        // Copier les pixels pour les modifier (le cache ne doit pas être modifié)
        byte[] resultPixels = new byte[workingPixels.Length];
        Array.Copy(workingPixels, resultPixels, workingPixels.Length);
        
        // Appliquer les ajustements de ton
        Console.WriteLine("[DEBUG] Applying tone adjustments...");
        ApplyToneAdjustments(resultPixels, width, height, stride, p);
        
        // Créer le bitmap résultat
        Console.WriteLine("[DEBUG] Creating result bitmap...");
        var result = new WriteableBitmap(
            pixelSize,
            writeableSource.Dpi,
            writeableSource.Format,
            writeableSource.AlphaFormat);
        
        using (var destBuffer = result.Lock())
        {
            System.Runtime.InteropServices.Marshal.Copy(resultPixels, 0, destBuffer.Address, resultPixels.Length);
        }
        
        Console.WriteLine("[DEBUG] ApplyAdjustments completed");
        return result;
    }
    
    private static void ApplyToneAdjustments(byte[] pixels, int width, int height, int stride, PrecomputedAdjustments p)
    {
        Parallel.For(0, height, y =>
        {
            int rowStart = y * stride;
            
            for (int x = 0; x < width; x++)
            {
                int index = rowStart + x * 4;

                float b = pixels[index];
                float g = pixels[index + 1];
                float r = pixels[index + 2];

                float luminance = (0.299f * r + 0.587f * g + 0.114f * b) * Inv255;

                r = ApplyToneCurve(r * Inv255, luminance, in p) * 255.0f;
                g = ApplyToneCurve(g * Inv255, luminance, in p) * 255.0f;
                b = ApplyToneCurve(b * Inv255, luminance, in p) * 255.0f;

                if (p.HasLut)
                    ApplyLutInline(ref r, ref g, ref b, in p);

                if (p.HasVignette)
                    ApplyVignette(ref r, ref g, ref b, x, y, width, height, in p);

                pixels[index] = ClampByte(b);
                pixels[index + 1] = ClampByte(g);
                pixels[index + 2] = ClampByte(r);
            }
        });
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

        var p = PrecomputeAdjustments(adjustments);

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

                float luminance = (0.299f * r + 0.587f * g + 0.114f * b) * Inv255;

                r = ApplyToneCurve(r * Inv255, luminance, in p) * 255.0f;
                g = ApplyToneCurve(g * Inv255, luminance, in p) * 255.0f;
                b = ApplyToneCurve(b * Inv255, luminance, in p) * 255.0f;

                if (p.HasLut)
                    ApplyLutInline(ref r, ref g, ref b, in p);

                if (p.HasVignette)
                    ApplyVignette(ref r, ref g, ref b, srcX, srcY, srcWidth, srcHeight, in p);

                destPtr[destIndex] = ClampByte(b);
                destPtr[destIndex + 1] = ClampByte(g);
                destPtr[destIndex + 2] = ClampByte(r);
                destPtr[destIndex + 3] = srcPtr[srcIndex + 3];
            }
        });

        return _previewBitmap;
    }

    private readonly struct PrecomputedAdjustments
    {
        public readonly float ExposureFactor;
        public readonly float ContrastAmount;
        public readonly float ShadowsAmount;
        public readonly float HighlightsAmount;
        public readonly float DenoiseStrength;
        public readonly float VignetteIntensity;
        public readonly float VignetteSpread;
        public readonly bool HasExposure;
        public readonly bool HasContrast;
        public readonly bool HasShadows;
        public readonly bool HasHighlights;
        public readonly bool HasDenoise;
        public readonly bool HasVignette;
        public readonly CubeLut? Lut;
        public readonly float LutIntensity;
        public readonly bool HasLut;

        public PrecomputedAdjustments(float exposureFactor, float contrastAmount,
            float shadowsAmount, float highlightsAmount, float denoiseStrength,
            float vignetteIntensity, float vignetteSpread,
            bool hasExposure, bool hasContrast, bool hasShadows, bool hasHighlights, bool hasDenoise, bool hasVignette,
            CubeLut? lut, float lutIntensity, bool hasLut)
        {
            ExposureFactor = exposureFactor;
            ContrastAmount = contrastAmount;
            ShadowsAmount = shadowsAmount;
            HighlightsAmount = highlightsAmount;
            DenoiseStrength = denoiseStrength;
            VignetteIntensity = vignetteIntensity;
            VignetteSpread = vignetteSpread;
            HasExposure = hasExposure;
            HasContrast = hasContrast;
            HasShadows = hasShadows;
            HasHighlights = hasHighlights;
            HasDenoise = hasDenoise;
            HasVignette = hasVignette;
            Lut = lut;
            LutIntensity = lutIntensity;
            HasLut = hasLut;
        }
    }

    private static PrecomputedAdjustments PrecomputeAdjustments(ImageAdjustments adjustments)
    {
        float exposureEV = (float)(adjustments.Exposure / 100.0);
        float exposureFactor = (float)Math.Pow(2, exposureEV * 0.5f);
        float contrast = (float)(adjustments.Contrast / 100.0);
        float shadows = (float)(adjustments.Shadows / 100.0);
        float highlights = (float)(adjustments.Highlights / 100.0);
        float denoiseAmount = (float)adjustments.DenoiseAmount;
        float denoiseStrength = denoiseAmount * 2.5f;
        float vignetteIntensity = (float)(adjustments.VignetteIntensity / 100.0);
        float vignetteSpread = (float)(adjustments.VignetteSpread / 100.0);
        
        var lut = adjustments.ActiveLut;
        float lutIntensity = (float)adjustments.LutIntensity;
        bool hasLut = adjustments.IsLutEnabled && lut != null && lutIntensity > 0.001f;
        bool hasDenoise = adjustments.IsDenoiseEnabled && denoiseAmount > 0.001f;
        bool hasVignette = adjustments.IsVignetteEnabled && Math.Abs(vignetteIntensity) > 0.001f;

        return new PrecomputedAdjustments(
            exposureFactor,
            contrast,
            shadows,
            highlights,
            denoiseStrength,
            vignetteIntensity,
            vignetteSpread,
            Math.Abs(exposureEV) > 0.001f,
            Math.Abs(contrast) > 0.001f,
            Math.Abs(shadows) > 0.001f,
            Math.Abs(highlights) > 0.001f,
            hasDenoise,
            hasVignette,
            lut,
            lutIntensity,
            hasLut
        );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float ApplyToneCurve(float normalizedValue, float luminance, in PrecomputedAdjustments p)
    {
        float value = normalizedValue;

        if (p.HasExposure)
            value = ApplyExposure(value, p.ExposureFactor);

        if (p.HasHighlights)
            value = ApplyHighlights(value, luminance, p.HighlightsAmount);

        if (p.HasShadows)
            value = ApplyShadows(value, luminance, p.ShadowsAmount);

        if (p.HasContrast)
            value = ApplyContrastSCurve(value, p.ContrastAmount);

        return value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float ApplyExposure(float value, float factor)
    {
        float exposed = value * factor;
        return SoftRolloff(exposed);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float ApplyHighlights(float value, float luminance, float amount)
    {
        if (luminance < 0.5f)
            return value;

        float t = Smoothstep(0.5f, 1.0f, luminance);
        float effect = amount * 0.6f * t;

        if (amount > 0)
        {
            float compression = 1.0f - effect * (1.0f - value);
            return value * compression;
        }
        else
        {
            float recovery = -effect * value;
            float maxRecovery = 1.0f - value;
            return value + Math.Min(recovery * 0.3f, maxRecovery * 0.5f);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float ApplyShadows(float value, float luminance, float amount)
    {
        if (Math.Abs(amount) < 0.001f)
            return value;

        float x = value;
        
        if (amount > 0)
        {
            float lift = amount * 0.3f;
            float result = x + lift * x * (1.0f - x) * 4.0f;
            return Math.Min(result, 1.0f);
        }
        else
        {
            float crush = -amount * 0.3f;
            float result = x * (1.0f - crush * (1.0f - x));
            return Math.Max(result, 0.0f);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float ApplyContrastSCurve(float value, float amount)
    {
        if (Math.Abs(amount) < 0.001f)
            return value;

        float x = value;
        float contrast = amount * 0.11f;
        float result = x + contrast * (x - 0.5f) * (1.0f - x) * 4.0f;
        
        return Math.Clamp(result, 0.0f, 1.0f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float Smoothstep(float edge0, float edge1, float x)
    {
        float t = Math.Clamp((x - edge0) / (edge1 - edge0), 0.0f, 1.0f);
        return t * t * (3.0f - 2.0f * t);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float SoftRolloff(float x)
    {
        if (x <= 0.0f) return 0.0f;
        if (x >= 1.0f)
        {
            float excess = x - 1.0f;
            return 1.0f + excess / (1.0f + excess * 2.0f) * 0.5f;
        }
        return x;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte ClampByte(float value)
    {
        if (value >= 255.0f) return 255;
        if (value <= 0.0f) return 0;
        return (byte)value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ApplyLutInline(ref float r, ref float g, ref float b, in PrecomputedAdjustments p)
    {
        var lut = p.Lut!;
        int size = lut.Size;
        float[] data = lut.Data;
        
        float nR = Clamp01(r / 255.0f);
        float nG = Clamp01(g / 255.0f);
        float nB = Clamp01(b / 255.0f);
        
        var (lutR, lutG, lutB) = SampleLutNearest(data, size, nR, nG, nB);
        
        float intensityNorm = p.LutIntensity / 100.0f;
        float oneMinus = 1.0f - intensityNorm;
        
        r = r * oneMinus + lutR * 255.0f * intensityNorm;
        g = g * oneMinus + lutG * 255.0f * intensityNorm;
        b = b * oneMinus + lutB * 255.0f * intensityNorm;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static (float r, float g, float b) SampleLutNearest(
        float[] data, int size, float r, float g, float b)
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
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float Lerp(float a, float b, float t) => a + (b - a) * t;
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ApplyVignette(ref float r, ref float g, ref float b, int x, int y, int width, int height, in PrecomputedAdjustments p)
    {
        float centerX = width * 0.5f;
        float centerY = height * 0.5f;
        float dx = (x - centerX) / centerX;
        float dy = (y - centerY) / centerY;
        float dist = MathF.Sqrt(dx * dx + dy * dy);

        float maxDist = MathF.Sqrt(2.0f);
        dist = dist / maxDist;

        float spread = Math.Clamp(p.VignetteSpread, 0.1f, 1.0f);
        float falloff = Smoothstep(spread, 1.0f, dist);

        if (p.VignetteIntensity > 0)
        {
            float factor = 1.0f - p.VignetteIntensity * 0.8f * falloff;
            r *= factor;
            g *= factor;
            b *= factor;
        }
        else if (p.VignetteIntensity < 0)
        {
            float intensity = -p.VignetteIntensity;
            float factor = 1.0f + intensity * 0.5f * falloff;
            r *= factor;
            g *= factor;
            b *= factor;
        }
    }

    public void Dispose()
    {
        _previewBitmap?.Dispose();
    }
}
