using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace SimpleRawEditor.Services.Denoising;

public class BilateralFilter : IDenoisingAlgorithm
{
    public string Name => "Bilateral Filter";

    public byte[]? Process(byte[] sourcePixels, int width, int height, int stride, float strength)
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
    private static byte ClampByte(float value)
    {
        if (value >= 255f) return 255;
        if (value <= 0f) return 0;
        return (byte)(value + 0.5f);
    }
}
