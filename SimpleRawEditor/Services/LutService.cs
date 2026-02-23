using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SimpleRawEditor.Models;
using SimpleRawEditor.Services.Interfaces;

namespace SimpleRawEditor.Services;

public class LutService : ILutService
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

        return CubeLutParser.Parse(filePath);
    }

    public CubeLut LoadFromPath(string filePath)
    {
        return CubeLutParser.Parse(filePath);
    }

    public string? GetLutPath(string name)
    {
        var filePath = Path.Combine(_lutFolder, $"{name}.cube");
        return File.Exists(filePath) ? filePath : null;
    }
}
