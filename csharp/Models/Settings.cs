using System.Collections.Generic;

namespace ClientOPreview.Models;

public class SettingsData
{
    public General General { get; set; } = new();
    public Thumbnail Thumbnail { get; set; } = new();
    public Hotkeys Hotkeys { get; set; } = new();
    public Zoom Zoom { get; set; } = new();
    public Dictionary<string, string> Layouts { get; set; } = new();
    public List<string> LastOpenWindows { get; set; } = new();
}

public class Zoom
{
    public bool ResizeOnHover { get; set; } = false;
    public bool InternalZoom { get; set; } = true;
    public double Magnification { get; set; } = 1.5;
    public double OffsetX { get; set; } = 0.5; // 0.0 to 1.0 (center X)
    public double OffsetY { get; set; } = 0.5; // 0.0 to 1.0 (center Y)
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
    public int Width { get; set; } = 160;
    public int Height { get; set; } = 90;
    public int OpacityPct { get; set; } = 90;
    public int TitleFontSize { get; set; } = 12;
    public string ActiveHighlightColor { get; set; } = "#2864C8"; // Default blue
}

public class Hotkeys
{
    public bool Enabled { get; set; } = true;
    public string CycleKey { get; set; } = "Tab";
    public string CycleModifiers { get; set; } = "Alt";
    public string DirectModifiers { get; set; } = "Alt";
    public List<string> DirectKeys { get; set; } = new()
    {
        "NumPad1", "NumPad2", "NumPad3", "NumPad4", "NumPad5",
        "NumPad6", "NumPad7", "NumPad8", "NumPad9", "NumPad0"
    };
    // Maps hotkey index (0-9) to window title
    public Dictionary<int, string> DirectKeyMappings { get; set; } = new();
}
