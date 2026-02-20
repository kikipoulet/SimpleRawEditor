using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace SimpleRawEditor.Services.Denoising;

public class MedianFilter : IDenoisingAlgorithm
{
    public string Name => "Median";

    public byte[]? Process(byte[] sourcePixels, int width, int height, int stride, float strength)
    {
        if (strength < 0.5f) return null;
        if (width < 3 || height < 3) return null;

        int radius = strength switch
        {
            < 10 => 1,
            < 30 => 2,
            < 70 => 3,
            _ => 4
        };

        int kernelSize = radius * 2 + 1;
        int windowSize = kernelSize * kernelSize;
        int medianIndex = windowSize / 2;

        byte[] result = new byte[sourcePixels.Length];

        byte[] reds = new byte[windowSize];
        byte[] greens = new byte[windowSize];
        byte[] blues = new byte[windowSize];

        int processors = Environment.ProcessorCount;
        int rowsPerProcessor = height / processors + 1;

        Parallel.For(0, processors, p =>
        {
            int yStart = Math.Max(p * rowsPerProcessor, radius);
            int yEnd = Math.Min(height - 1 - radius, (p + 1) * rowsPerProcessor - 1);

            for (int y = yStart; y <= yEnd; y++)
            {
                for (int x = radius; x < width - radius; x++)
                {
                    int kernelIdx = 0;

                    for (int ky = -radius; ky <= radius; ky++)
                    {
                        int py = y + ky;
                        int rowOffset = py * stride;

                        for (int kx = -radius; kx <= radius; kx++)
                        {
                            int px = x + kx;
                            int pixelIdx = rowOffset + px * 4;

                            reds[kernelIdx] = sourcePixels[pixelIdx + 2];
                            greens[kernelIdx] = sourcePixels[pixelIdx + 1];
                            blues[kernelIdx] = sourcePixels[pixelIdx];
                            kernelIdx++;
                        }
                    }

                    int resultIdx = y * stride + x * 4;
                    result[resultIdx + 2] = QuickSelect(reds, medianIndex);
                    result[resultIdx + 1] = QuickSelect(greens, medianIndex);
                    result[resultIdx] = QuickSelect(blues, medianIndex);
                    result[resultIdx + 3] = sourcePixels[resultIdx + 3];
                }
            }
        });

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int idx = y * stride + x * 4;
                if (result[idx + 3] == 0)
                {
                    int srcIdx = y * stride + x * 4;
                    result[idx] = sourcePixels[srcIdx];
                    result[idx + 1] = sourcePixels[srcIdx + 1];
                    result[idx + 2] = sourcePixels[srcIdx + 2];
                    result[idx + 3] = sourcePixels[srcIdx + 3];
                }
            }
        }

        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte QuickSelect(byte[] arr, int k)
    {
        int n = arr.Length;
        int low = 0;
        int high = n - 1;

        while (true)
        {
            if (low == high)
                return arr[low];

            int pivotIndex = Partition(arr, low, high, k);

            if (k == pivotIndex)
                return arr[k];
            else if (k < pivotIndex)
                high = pivotIndex - 1;
            else
                low = pivotIndex + 1;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int Partition(byte[] arr, int low, int high, int pivotIndex)
    {
        byte pivotValue = arr[pivotIndex];
        Swap(arr, pivotIndex, high);

        int storeIndex = low;

        for (int i = low; i < high; i++)
        {
            if (arr[i] < pivotValue)
            {
                Swap(arr, i, storeIndex);
                storeIndex++;
            }
        }

        Swap(arr, storeIndex, high);
        return storeIndex;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Swap(byte[] arr, int i, int j)
    {
        byte temp = arr[i];
        arr[i] = arr[j];
        arr[j] = temp;
    }
}
