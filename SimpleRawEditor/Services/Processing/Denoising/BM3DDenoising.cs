using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace SimpleRawEditor.Services.Processing.Denoising;

public unsafe class BM3DDenoising : IDenoisingAlgorithm
{
    public string Name => "NLM-Optimized";

    public byte[]? Process(byte[] sourcePixels, int width, int height, int stride, float strength)
    {
        if (strength < 0.5f) return null;
        if (width < 8 || height < 8) return null;

        float h = Math.Max(5f, strength * 0.15f);
        float h2 = h * h;
        
        int searchRadius = strength switch
        {
            < 30 => 5,
            < 60 => 6,
            _ => 7
        };
        int patchRadius = 2;
        int patchDiameter = patchRadius * 2 + 1;

        byte[] result = new byte[sourcePixels.Length];

        int integralSize = (width + 1) * (height + 1);
        float[] integralR = new float[integralSize];
        float[] integralG = new float[integralSize];
        float[] integralB = new float[integralSize];
        float[] integralR2 = new float[integralSize];
        float[] integralG2 = new float[integralSize];
        float[] integralB2 = new float[integralSize];

        ComputeIntegralImages(sourcePixels, width, height, stride, 
            integralR, integralG, integralB, 
            integralR2, integralG2, integralB2);

        int processors = Environment.ProcessorCount;
        int rowsPerProcessor = (height + processors - 1) / processors;

        Parallel.For(0, processors, p =>
        {
            int yStart = p * rowsPerProcessor;
            int yEnd = Math.Min(height - 1, (p + 1) * rowsPerProcessor - 1);

            fixed (byte* srcPtr = sourcePixels, dstPtr = result)
            fixed (float* iR = integralR, iG = integralG, iB = integralB,
                         iR2 = integralR2, iG2 = integralG2, iB2 = integralB2)
            {
                for (int y = yStart; y <= yEnd; y++)
                {
                    byte* rowDst = dstPtr + y * stride;

                    for (int x = 0; x < width; x++)
                    {
                        int centerIdx = y * stride + x * 4;

                        float sumR = 0, sumG = 0, sumB = 0;
                        float weightSum = 0;

                        int syStart = Math.Max(0, y - searchRadius);
                        int syEnd = Math.Min(height - 1, y + searchRadius);

                        for (int sy = syStart; sy <= syEnd; sy++)
                        {
                            int sxStart = Math.Max(0, x - searchRadius);
                            int sxEnd = Math.Min(width - 1, x + searchRadius);

                            for (int sx = sxStart; sx <= sxEnd; sx++)
                            {
                                float dist = PatchDistanceIntegral(
                                    iR, iG, iB, iR2, iG2, iB2,
                                    width, height, x, y, sx, sy, patchRadius);

                                float weight = MathF.Exp(-dist / h2);

                                int neighborIdx = sy * stride + sx * 4;
                                sumR += srcPtr[neighborIdx + 2] * weight;
                                sumG += srcPtr[neighborIdx + 1] * weight;
                                sumB += srcPtr[neighborIdx] * weight;
                                weightSum += weight;
                            }
                        }

                        if (weightSum > 0)
                        {
                            rowDst[x * 4 + 2] = ClampByte(sumR / weightSum);
                            rowDst[x * 4 + 1] = ClampByte(sumG / weightSum);
                            rowDst[x * 4] = ClampByte(sumB / weightSum);
                        }
                        else
                        {
                            rowDst[x * 4 + 2] = srcPtr[centerIdx + 2];
                            rowDst[x * 4 + 1] = srcPtr[centerIdx + 1];
                            rowDst[x * 4] = srcPtr[centerIdx];
                        }
                        rowDst[x * 4 + 3] = srcPtr[centerIdx + 3];
                    }
                }
            }
        });

        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ComputeIntegralImages(
        byte[] pixels, int width, int height, int stride,
        float[] integralR, float[] integralG, float[] integralB,
        float[] integralR2, float[] integralG2, float[] integralB2)
    {
        fixed (byte* src = pixels)
        fixed (float* iR = integralR, iG = integralG, iB = integralB,
                     iR2 = integralR2, iG2 = integralG2, iB2 = integralB2)
        {
            int w1 = width + 1;
            
            iR[0] = iG[0] = iB[0] = 0;
            iR2[0] = iG2[0] = iB2[0] = 0;

            for (int x = 1; x <= width; x++)
            {
                int idx = x;
                iR[idx] = iG[idx] = iB[idx] = 0;
                iR2[idx] = iG2[idx] = iB2[idx] = 0;
            }

            for (int y = 1; y <= height; y++)
            {
                int rowIdx = y * w1;
                iR[rowIdx] = iG[rowIdx] = iB[rowIdx] = 0;
                iR2[rowIdx] = iG2[rowIdx] = iB2[rowIdx] = 0;

                float sumR = 0, sumG = 0, sumB = 0;
                float sumR2 = 0, sumG2 = 0, sumB2 = 0;

                byte* srcRow = src + (y - 1) * stride;

                for (int x = 1; x <= width; x++)
                {
                    int srcIdx = (x - 1) * 4;
                    float r = srcRow[srcIdx + 2];
                    float g = srcRow[srcIdx + 1];
                    float b = srcRow[srcIdx];

                    sumR += r; sumG += g; sumB += b;
                    sumR2 += r * r; sumG2 += g * g; sumB2 += b * b;

                    int idx = rowIdx + x;
                    int prevRowIdx = idx - w1;

                    iR[idx] = iR[prevRowIdx] + sumR;
                    iG[idx] = iG[prevRowIdx] + sumG;
                    iB[idx] = iB[prevRowIdx] + sumB;
                    iR2[idx] = iR2[prevRowIdx] + sumR2;
                    iG2[idx] = iG2[prevRowIdx] + sumG2;
                    iB2[idx] = iB2[prevRowIdx] + sumB2;
                }
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float PatchDistanceIntegral(
        float* iR, float* iG, float* iB,
        float* iR2, float* iG2, float* iB2,
        int width, int height,
        int x1, int y1, int x2, int y2, int patchRadius)
    {
        int w1 = width + 1;

        int x1a = Math.Max(0, x1 - patchRadius);
        int y1a = Math.Max(0, y1 - patchRadius);
        int x1b = Math.Min(width - 1, x1 + patchRadius);
        int y1b = Math.Min(height - 1, y1 + patchRadius);

        int x2a = x2 - (x1 - x1a);
        int y2a = y2 - (y1 - y1a);
        int x2b = x2 + (x1b - x1);
        int y2b = y2 + (y1b - y1);

        if (x2a < 0 || y2a < 0 || x2b >= width || y2b >= height)
        {
            return PatchDistanceFallback(iR, iG, iB, iR2, iG2, iB2, w1, x1, y1, x2, y2, patchRadius);
        }

        int count = (x1b - x1a + 1) * (y1b - y1a + 1);
        float invCount = 1.0f / count;

        float sumR1 = GetRegionSum(iR, w1, x1a, y1a, x1b, y1b);
        float sumG1 = GetRegionSum(iG, w1, x1a, y1a, x1b, y1b);
        float sumB1 = GetRegionSum(iB, w1, x1a, y1a, x1b, y1b);
        float sumR1sq = GetRegionSum(iR2, w1, x1a, y1a, x1b, y1b);
        float sumG1sq = GetRegionSum(iG2, w1, x1a, y1a, x1b, y1b);
        float sumB1sq = GetRegionSum(iB2, w1, x1a, y1a, x1b, y1b);

        float sumR2 = GetRegionSum(iR, w1, x2a, y2a, x2b, y2b);
        float sumG2 = GetRegionSum(iG, w1, x2a, y2a, x2b, y2b);
        float sumB2 = GetRegionSum(iB, w1, x2a, y2a, x2b, y2b);
        float sumR2sq = GetRegionSum(iR2, w1, x2a, y2a, x2b, y2b);
        float sumG2sq = GetRegionSum(iG2, w1, x2a, y2a, x2b, y2b);
        float sumB2sq = GetRegionSum(iB2, w1, x2a, y2a, x2b, y2b);

        float crossR = GetCrossSum(iR, w1, x1a, y1a, x1b, y1b, x2a, y2a);
        float crossG = GetCrossSum(iG, w1, x1a, y1a, x1b, y1b, x2a, y2a);
        float crossB = GetCrossSum(iB, w1, x1a, y1a, x1b, y1b, x2a, y2a);

        float varR = sumR1sq + sumR2sq - 2 * crossR;
        float varG = sumG1sq + sumG2sq - 2 * crossG;
        float varB = sumB1sq + sumB2sq - 2 * crossB;

        return (varR + varG + varB) * invCount;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float PatchDistanceFallback(
        float* iR, float* iG, float* iB,
        float* iR2, float* iG2, float* iB2,
        int w1, int x1, int y1, int x2, int y2, int patchRadius)
    {
        float dist = 0;
        int count = 0;

        for (int dy = -patchRadius; dy <= patchRadius; dy++)
        {
            for (int dx = -patchRadius; dx <= patchRadius; dx++)
            {
                int px1 = x1 + dx, py1 = y1 + dy;
                int px2 = x2 + dx, py2 = y2 + dy;

                if (px1 >= 0 && px1 < w1 - 1 && py1 >= 0 && py1 < w1 - 1 &&
                    px2 >= 0 && px2 < w1 - 1 && py2 >= 0 && py2 < w1 - 1)
                {
                    float r1 = GetPixelValue(iR, w1, px1, py1);
                    float g1 = GetPixelValue(iG, w1, px1, py1);
                    float b1 = GetPixelValue(iB, w1, px1, py1);
                    float r2 = GetPixelValue(iR, w1, px2, py2);
                    float g2 = GetPixelValue(iG, w1, px2, py2);
                    float b2 = GetPixelValue(iB, w1, px2, py2);

                    float dr = r1 - r2, dg = g1 - g2, db = b1 - b2;
                    dist += dr * dr + dg * dg + db * db;
                    count++;
                }
            }
        }

        return count > 0 ? dist / count : float.MaxValue;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float GetPixelValue(float* integral, int w1, int x, int y)
    {
        int idx1 = (y + 1) * w1 + (x + 1);
        int idx2 = y * w1 + (x + 1);
        int idx3 = (y + 1) * w1 + x;
        int idx4 = y * w1 + x;
        return integral[idx1] - integral[idx2] - integral[idx3] + integral[idx4];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float GetRegionSum(float* integral, int w1, int xa, int ya, int xb, int yb)
    {
        int x1 = xa, y1 = ya, x2 = xb + 1, y2 = yb + 1;
        int idx1 = y2 * w1 + x2;
        int idx2 = y1 * w1 + x2;
        int idx3 = y2 * w1 + x1;
        int idx4 = y1 * w1 + x1;
        return integral[idx1] - integral[idx2] - integral[idx3] + integral[idx4];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float GetCrossSum(float* integral, int w1, int x1a, int y1a, int x1b, int y1b, int x2a, int y2a)
    {
        float sum = 0;
        for (int dy = 0; dy <= y1b - y1a; dy++)
        {
            for (int dx = 0; dx <= x1b - x1a; dx++)
            {
                int px1 = x1a + dx, py1 = y1a + dy;
                int px2 = x2a + dx, py2 = y2a + dy;
                sum += GetPixelValue(integral, w1, px1, py1) * GetPixelValue(integral, w1, px2, py2);
            }
        }
        return sum;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte ClampByte(float value)
    {
        if (value >= 255f) return 255;
        if (value <= 0f) return 0;
        return (byte)(value + 0.5f);
    }
}
