namespace SimpleRawEditor.Services.Denoising;

public interface IDenoisingAlgorithm
{
    string Name { get; }
    
    /// <summary>
    /// Process image data for denoising.
    /// </summary>
    /// <param name="sourcePixels">Source pixel data in BGRA format (4 bytes per pixel)</param>
    /// <param name="width">Image width in pixels</param>
    /// <param name="height">Image height in pixels</param>
    /// <param name="stride">Number of bytes per row (may include padding)</param>
    /// <param name="strength">Denoising strength (0-250 typically)</param>
    /// <returns>Denoised pixel data in BGRA format, or null if processing failed</returns>
    byte[]? Process(byte[] sourcePixels, int width, int height, int stride, float strength);
}
