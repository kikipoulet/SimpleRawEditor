using System;

namespace SimpleRawEditor.Models;

public class CubeLut
{
    public string? Title { get; set; }
    public int Size { get; set; }
    public float[] DomainMin { get; set; } = { 0f, 0f, 0f };
    public float[] DomainMax { get; set; } = { 1f, 1f, 1f };
    
    public float[] Data { get; set; } = Array.Empty<float>();
    
    public int DataLength => Size * Size * Size * 3;
    
    public CubeLut(int size)
    {
        Size = size;
        Data = new float[DataLength];
    }
    
    public CubeLut()
    {
    }
    
    public (float r, float g, float b) Sample(int r, int g, int b)
    {
        r = Math.Clamp(r, 0, Size - 1);
        g = Math.Clamp(g, 0, Size - 1);
        b = Math.Clamp(b, 0, Size - 1);
        
        int index = (r * Size * Size + g * Size + b) * 3;
        return (Data[index], Data[index + 1], Data[index + 2]);
    }
    
    public void Set(int r, int g, int b, float rVal, float gVal, float bVal)
    {
        int index = (r * Size * Size + g * Size + b) * 3;
        Data[index] = rVal;
        Data[index + 1] = gVal;
        Data[index + 2] = bVal;
    }
}
