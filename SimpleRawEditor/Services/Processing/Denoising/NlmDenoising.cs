using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace SimpleRawEditor.Services.Processing.Denoising;

public class NlmDenoising : IDenoisingAlgorithm
{
    public string Name => "NLM";

    public byte[]? Process(byte[] sourcePixels, int width, int height, int stride, float strength)
    {
        if (strength < 0.5f) return null;
        if (width < 8 || height < 8) return null;

        float h = Math.Max(5f, strength * 0.8f);
        int searchRadius = Math.Min(10, 4 + (int)(strength / 20f));
        int patchRadius = 2;

        byte[] result = new byte[sourcePixels.Length];

        int processors = Environment.ProcessorCount;
        int rowsPerProcessor = height / processors + 1;

        Parallel.For(0, processors, p =>
        {
            int yStart = p * rowsPerProcessor;
            int yEnd = Math.Min(height - 1, (p + 1) * rowsPerProcessor - 1);

            for (int y = yStart; y <= yEnd; y++)
            {
                int row = y * stride;

                for (int x = 0; x < width; x++)
                {
                    int centerIdx = row + x * 4;

                    float sumR = 0, sumG = 0, sumB = 0;
                    float weightSum = 0;
                    float maxWeight = 0;

                    int syStart = Math.Max(0, y - searchRadius);
                    int syEnd = Math.Min(height - 1, y + searchRadius);

                    for (int sy = syStart; sy <= syEnd; sy++)
                    {
                        int sxStart = Math.Max(0, x - searchRadius);
                        int sxEnd = Math.Min(width - 1, x + searchRadius);

                        for (int sx = sxStart; sx <= sxEnd; sx++)
                        {
                            float dist = PatchDistance(sourcePixels, width, height, stride, x, y, sx, sy, patchRadius);
                            float weight = MathF.Exp(-dist / (h * h));

                            if (sx == x && sy == y)
                                maxWeight = weight;

                            int neighborIdx = sy * stride + sx * 4;
                            sumR += sourcePixels[neighborIdx + 2] * weight;
                            sumG += sourcePixels[neighborIdx + 1] * weight;
                            sumB += sourcePixels[neighborIdx] * weight;
                            weightSum += weight;
                        }
                    }

                    if (weightSum > 0)
                    {
                        result[centerIdx + 2] = ClampByte(sumR / weightSum);
                        result[centerIdx + 1] = ClampByte(sumG / weightSum);
                        result[centerIdx] = ClampByte(sumB / weightSum);
                    }
                    else
                    {
                        result[centerIdx + 2] = sourcePixels[centerIdx + 2];
                        result[centerIdx + 1] = sourcePixels[centerIdx + 1];
                        result[centerIdx] = sourcePixels[centerIdx];
                    }
                    result[centerIdx + 3] = sourcePixels[centerIdx + 3];
                }
            }
        });

        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float PatchDistance(byte[] pixels, int width, int height, int stride, int x1, int y1, int x2, int y2, int patchRadius)
    {
        float dist = 0;
        int count = 0;

        int prStart = -patchRadius;
        int prEnd = patchRadius;

        for (int dy = prStart; dy <= prEnd; dy++)
        {
            for (int dx = prStart; dx <= prEnd; dx++)
            {
                int px1 = x1 + dx;
                int py1 = y1 + dy;
                int px2 = x2 + dx;
                int py2 = y2 + dy;

                if (px1 >= 0 && px1 < width && py1 >= 0 && py1 < height &&
                    px2 >= 0 && px2 < width && py2 >= 0 && py2 < height)
                {
                    int idx1 = py1 * stride + px1 * 4;
                    int idx2 = py2 * stride + px2 * 4;

                    float dr = pixels[idx1 + 2] - pixels[idx2 + 2];
                    float dg = pixels[idx1 + 1] - pixels[idx2 + 1];
                    float db = pixels[idx1] - pixels[idx2];

                    dist += dr * dr + dg * dg + db * db;
                    count++;
                }
            }
        }

        return count > 0 ? dist / count : float.MaxValue;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte ClampByte(float value)
    {
        if (value >= 255f) return 255;
        if (value <= 0f) return 0;
        return (byte)(value + 0.5f);
    }
}
