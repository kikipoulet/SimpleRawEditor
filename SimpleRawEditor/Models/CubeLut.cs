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
}
