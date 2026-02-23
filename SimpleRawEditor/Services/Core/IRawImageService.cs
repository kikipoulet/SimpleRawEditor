using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using SimpleRawEditor.Models;

namespace SimpleRawEditor.Services.Core;

public interface IRawImageService
{
    Task<RawImageData?> LoadRawImageAsync(string filePath);
    Task<Bitmap?> LoadThumbnailAsync(string filePath);
}
