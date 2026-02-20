using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Sdcb.LibRaw;
using SimpleRawEditor.Models;

namespace SimpleRawEditor.Services;

public class RawImageService
{
    public async Task<RawImageData?> LoadRawImageAsync(string filePath)
    {
        return await Task.Run(() =>
        {
            try
            {
                using var context = RawContext.OpenFile(filePath);
                context.Unpack();
                
                // Traitement avec les paramètres par défaut
                context.DcrawProcess(c =>
                {
                    c.UseCameraWb = true;
                    c.HalfSize = false;
                });
                
                using var processedImage = context.MakeDcrawMemoryImage();
                var bitmap = ProcessedImageToBitmap(processedImage);
                
                return new RawImageData
                {
                    FilePath = filePath,
                    FileName = Path.GetFileName(filePath),
                    OriginalBitmap = bitmap,
                    Width = processedImage.Width,
                    Height = processedImage.Height
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur chargement RAW: {ex.Message}");
                return null;
            }
        });
    }

    public async Task<Bitmap?> LoadThumbnailAsync(string filePath)
    {
        return await Task.Run(() =>
        {
            try
            {
                using var context = RawContext.OpenFile(filePath);
                context.Unpack();
                context.DcrawProcess(c =>
                {
                    c.UseCameraWb = true;
                    c.HalfSize = true; // Pour accélérer le traitement (1/4 de la taille)
                });
                
                using var processedImage = context.MakeDcrawMemoryImage();
                return ProcessedImageToBitmap(processedImage);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur chargement thumbnail: {ex.Message}");
                return null;
            }
        });
    }

    private static unsafe Bitmap ProcessedImageToBitmap(ProcessedImage rgbImage)
    {
        int width = rgbImage.Width;
        int height = rgbImage.Height;
        
        var writeableBitmap = new WriteableBitmap(
            new PixelSize(width, height),
            new Vector(96, 96),
            PixelFormat.Bgra8888,
            AlphaFormat.Opaque);
        
        using var buffer = writeableBitmap.Lock();
        
        var srcData = rgbImage.GetData<byte>();
        int srcStride = width * 3;
        int destStride = buffer.RowBytes;
        
        byte* destPtr = (byte*)buffer.Address;
        
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int srcIndex = y * srcStride + x * 3;
                int destIndex = y * destStride + x * 4;
                
                byte r = srcData[srcIndex];
                byte g = srcData[srcIndex + 1];
                byte b = srcData[srcIndex + 2];
                
                destPtr[destIndex] = b;
                destPtr[destIndex + 1] = g;
                destPtr[destIndex + 2] = r;
                destPtr[destIndex + 3] = 255;
            }
        }
        
        return writeableBitmap;
    }
}

public class RawImageData : IDisposable
{
    public string FilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public Bitmap? OriginalBitmap { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    
    public void Dispose()
    {
        OriginalBitmap?.Dispose();
    }
}
