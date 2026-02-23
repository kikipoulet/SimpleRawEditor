using System.Collections.Generic;
using SimpleRawEditor.Models;

namespace SimpleRawEditor.Services.Core;

public interface ILutService
{
    IEnumerable<string> GetAvailableLuts();
    CubeLut? LoadLut(string name);
    CubeLut LoadFromPath(string filePath);
    string? GetLutPath(string name);
}
