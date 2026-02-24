using System;
using Sdcb.LibRaw.Natives;

namespace SimpleRawEditor.Models;

public class ImageMetadata
{

    public string? Model { get; set; }
    

    public float IsoSpeed { get; set; }
    public float? Shutter { get; set; }

    public float? Aperture { get; set; }
    public DateTime? Date { get; set; }

    public string Lens { get; set; }


    public int? Width { get; set; }
    public int? Height { get; set; }
}
