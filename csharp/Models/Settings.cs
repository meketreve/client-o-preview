using System.Collections.Generic;

namespace ClientOPreview.Models;

public class SettingsData
{
    public General General { get; set; } = new();
    public Thumbnail Thumbnail { get; set; } = new();
    public Dictionary<string, string> Layouts { get; set; } = new();
}

public class General
{
    public bool MinimizeToTray { get; set; } = false;
    public bool TrackLocations { get; set; } = true;
    public bool HideActivePreview { get; set; } = true;
    public bool MinimizeInactive { get; set; } = false;
    public bool ReorderInactive { get; set; } = true;
    public bool PreviewsTopmost { get; set; } = true;
    public bool HideWhenNotActive { get; set; } = false;
    public bool UniqueLayout { get; set; } = true;
}

public class Thumbnail
{
    public int Width { get; set; } = 384;
    public int Height { get; set; } = 216;
    public int OpacityPct { get; set; } = 90;
}
