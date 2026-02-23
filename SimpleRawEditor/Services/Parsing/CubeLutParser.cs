using System;
using System.Globalization;
using System.IO;
using SimpleRawEditor.Models;

namespace SimpleRawEditor.Services.Parsing;

public static class CubeLutParser
{
    public static CubeLut Parse(string filePath)
    {
        var lines = File.ReadAllLines(filePath);
        return ParseLines(lines);
    }
    
    public static CubeLut ParseLines(string[] lines)
    {
        var lut = new CubeLut();
        int dataIndex = 0;
        int lineIndex = 0;
        
        for (lineIndex = 0; lineIndex < lines.Length; lineIndex++)
        {
            string line = lines[lineIndex].Trim();
            
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                continue;
            
            if (line.StartsWith("TITLE", StringComparison.OrdinalIgnoreCase))
            {
                int start = line.IndexOf('"');
                int end = line.LastIndexOf('"');
                if (start >= 0 && end > start)
                    lut.Title = line.Substring(start + 1, end - start - 1);
            }
            else if (line.StartsWith("LUT_3D_SIZE", StringComparison.OrdinalIgnoreCase))
            {
                var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2 && int.TryParse(parts[1], out int size))
                    lut.Size = size;
            }
            else if (line.StartsWith("DOMAIN_MIN", StringComparison.OrdinalIgnoreCase))
            {
                var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 4)
                {
                    lut.DomainMin[0] = float.Parse(parts[1], CultureInfo.InvariantCulture);
                    lut.DomainMin[1] = float.Parse(parts[2], CultureInfo.InvariantCulture);
                    lut.DomainMin[2] = float.Parse(parts[3], CultureInfo.InvariantCulture);
                }
            }
            else if (line.StartsWith("DOMAIN_MAX", StringComparison.OrdinalIgnoreCase))
            {
                var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 4)
                {
                    lut.DomainMax[0] = float.Parse(parts[1], CultureInfo.InvariantCulture);
                    lut.DomainMax[1] = float.Parse(parts[2], CultureInfo.InvariantCulture);
                    lut.DomainMax[2] = float.Parse(parts[3], CultureInfo.InvariantCulture);
                }
            }
            else if (IsDataLine(line))
            {
                break;
            }
        }
        
        if (lut.Size <= 0)
            throw new InvalidDataException("LUT_3D_SIZE non trouvé ou invalide");
        
        lut.Data = new float[lut.DataLength];
        
        for (; lineIndex < lines.Length; lineIndex++)
        {
            string line = lines[lineIndex].Trim();
            
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                continue;
            
            var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 3)
            {
                if (dataIndex + 2 < lut.Data.Length)
                {
                    lut.Data[dataIndex++] = float.Parse(parts[0], CultureInfo.InvariantCulture);
                    lut.Data[dataIndex++] = float.Parse(parts[1], CultureInfo.InvariantCulture);
                    lut.Data[dataIndex++] = float.Parse(parts[2], CultureInfo.InvariantCulture);
                }
            }
        }
        
        Console.WriteLine($"LUT chargée: Size={lut.Size}, DataLength={lut.Data.Length}, ValuesRead={dataIndex}");
        
        return lut;
    }
    
    private static bool IsDataLine(string line)
    {
        var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 3) return false;
        
        return float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out _);
    }
}
