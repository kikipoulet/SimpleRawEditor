using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using SimpleRawEditor.Models;

namespace SimpleRawEditor.Services;

public class DebouncedImageProcessor : IDisposable
{
    private readonly ImageProcessingService _processingService;
    private CancellationTokenSource? _cancellationTokenSource;
    private readonly Timer _debounceTimer;
    private readonly object _lockObject = new();
    
    private Bitmap? _originalBitmap;
    private ImageAdjustments? _pendingAdjustments;
    private bool _isDragging;
    
    public event EventHandler<Bitmap>? ImageProcessed;
    public event EventHandler<string>? ProcessingError;
    
    private const int DebounceDelayMs = 50; // 50ms de debounce
    private const int PreviewQualityDivisor = 4; // 1/4 de la résolution pendant le drag

    public DebouncedImageProcessor()
    {
        _processingService = new ImageProcessingService();
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
            
            // Initialiser le cache de débruitage si c'est un WriteableBitmap
            if (bitmap is WriteableBitmap writeableBitmap)
            {
                _processingService.InitializeCache(writeableBitmap);
            }
        }
    }
    
    /// <summary>
    /// Invalide le cache de débruitage (appeler quand DenoiseAmount ou DenoiseMode change).
    /// </summary>
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
            
            Console.WriteLine($"[DEBUG] RequestProcessing called - DenoiseAmount: {adjustments.DenoiseAmount}, IsDragging: {isDragging}");
            
            // Reset et redémarrage du timer
            _debounceTimer.Change(Timeout.Infinite, Timeout.Infinite);
            _debounceTimer.Change(DebounceDelayMs, Timeout.Infinite);
        }
    }

    private void OnDebounceElapsed(object? state)
    {
        lock (_lockObject)
        {
            Console.WriteLine($"[DEBUG] OnDebounceElapsed - HasBitmap: {_originalBitmap != null}, HasAdjustments: {_pendingAdjustments != null}");
            
            if (_originalBitmap == null || _pendingAdjustments == null)
                return;

            // Annuler le traitement précédent
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource = new CancellationTokenSource();
            var token = _cancellationTokenSource.Token;

            // Lancer le traitement
            var adjustments = _pendingAdjustments;
            var isDragging = _isDragging;
            var originalBitmap = _originalBitmap;

            Console.WriteLine($"[DEBUG] Starting processing - Denoise: {adjustments.DenoiseAmount}, IsPreview: {isDragging}");
            Task.Run(() => ProcessImageAsync(originalBitmap, adjustments, isDragging, token), token);
        }
    }

    private async Task ProcessImageAsync(Bitmap original, ImageAdjustments adjustments, bool isPreview, CancellationToken token)
    {
        try
        {
            Console.WriteLine("[DEBUG] ProcessImageAsync START");
            token.ThrowIfCancellationRequested();

            Bitmap? processedImage = null;
            
            if (isPreview && original is WriteableBitmap writeableOriginal)
            {
                // Mode preview rapide (1/4 de résolution)
                Console.WriteLine("[DEBUG] Calling ApplyAdjustmentsFast");
                processedImage = await Task.Run(() => 
                    _processingService.ApplyAdjustmentsFast(writeableOriginal, adjustments, token), token);
                Console.WriteLine($"[DEBUG] ApplyAdjustmentsFast returned: {processedImage != null}");
            }
            else
            {
                // Mode pleine qualité
                Console.WriteLine("[DEBUG] Calling ApplyAdjustments");
                processedImage = await Task.Run(() => 
                    _processingService.ApplyAdjustments(original, adjustments), token);
                Console.WriteLine($"[DEBUG] ApplyAdjustments returned: {processedImage != null}");
            }

            token.ThrowIfCancellationRequested();

            // Notification sur le thread UI
            if (processedImage != null)
            {
                Console.WriteLine("[DEBUG] Notifying UI with processed image");
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    ImageProcessed?.Invoke(this, processedImage);
                });
            }
            else
            {
                Console.WriteLine("[DEBUG] processedImage is NULL!");
            }
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("[DEBUG] Processing cancelled");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DEBUG] Exception: {ex}");
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                ProcessingError?.Invoke(this, $"Erreur traitement: {ex.Message}");
            });
        }
        Console.WriteLine("[DEBUG] ProcessImageAsync END");
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
