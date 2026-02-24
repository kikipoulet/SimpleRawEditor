using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Sdcb.LibRaw;
using Sdcb.LibRaw.Natives;
using SimpleRawEditor.Models;

namespace SimpleRawEditor.Services.Core;

public class RawImageService : IRawImageService
{
    public async Task<RawImageData?> LoadRawImageAsync(string filePath)
    {
        return await Task.Run(() =>
        {
            try
            {
                using var context = RawContext.OpenFile(filePath);
                LibRawImageParams imageParams = context.ImageParams;
                LibRawImageOtherParams otherParams = context.ImageOtherParams;
                
                context.Unpack();

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
                    Height = processedImage.Height,
                    Metadata = new ImageMetadata()
                    {
                        Model = imageParams.Make +" " +imageParams.Model,
                        IsoSpeed = otherParams.IsoSpeed,
                        Shutter = 1/  otherParams.Shutter,
                        Aperture = otherParams.Aperture,
                        Lens = context.LensInfo.Lens,
                        Date = DateTimeOffset
                            .FromUnixTimeSeconds(otherParams.Timestamp)
                            .UtcDateTime,
                        Width = processedImage.Width,
                        Height = processedImage.Height,
                    }
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
                    c.HalfSize = true;
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

        int srcStride = width * 3;
        int totalBytes = srcStride * height;
        byte[] srcData = new byte[totalBytes];
        Marshal.Copy(rgbImage.DataPointer, srcData, 0, totalBytes);

        for (int i = 0; i < srcData.Length; i += 3)
        {
            (srcData[i], srcData[i + 2]) = (srcData[i + 2], srcData[i]);
        }

        var writeableBitmap = new WriteableBitmap(
            new PixelSize(width, height),
            new Vector(96, 96),
            PixelFormat.Bgra8888,
            AlphaFormat.Opaque);

        using var buffer = writeableBitmap.Lock();

        int destStride = buffer.RowBytes;

        byte* destPtr = (byte*)buffer.Address;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int srcIndex = y * srcStride + x * 3;
                int destIndex = y * destStride + x * 4;

                byte b = srcData[srcIndex];
                byte g = srcData[srcIndex + 1];
                byte r = srcData[srcIndex + 2];

                destPtr[destIndex] = b;
                destPtr[destIndex + 1] = g;
                destPtr[destIndex + 2] = r;
                destPtr[destIndex + 3] = 255;
            }
        }

        return writeableBitmap;
    }
}
