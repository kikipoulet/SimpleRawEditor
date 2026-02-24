using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using SimpleRawEditor.Models;

namespace SimpleRawEditor.Services;

public class LutService
{
    private readonly string _lutFolder;

    public LutService()
    {
        var exeDir = AppContext.BaseDirectory;
        _lutFolder = Path.Combine(exeDir, "lut");
    }

    public IEnumerable<string> GetAvailableLuts()
    {
        if (!Directory.Exists(_lutFolder))
            return Enumerable.Empty<string>();

        return Directory.GetFiles(_lutFolder, "*.cube", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileNameWithoutExtension)
            .Where(name => name != null)
            .Cast<string>()
            .OrderBy(name => name)
            .ToList();
    }

    public CubeLut? LoadLut(string name)
    {
        var filePath = Path.Combine(_lutFolder, $"{name}.cube");
        if (!File.Exists(filePath))
            return null;

        return ParseCubeFile(filePath);
    }

    public CubeLut LoadFromPath(string filePath)
    {
        return ParseCubeFile(filePath);
    }

    private static CubeLut ParseCubeFile(string filePath)
    {
        var lines = File.ReadAllLines(filePath);
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

        return lut;
    }

    private static bool IsDataLine(string line)
    {
        var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 3) return false;

        return float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out _);
    }
}
