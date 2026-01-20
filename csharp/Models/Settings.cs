using System.Collections.Generic;

namespace ClientOPreview.Models;

public class SettingsData
{
    public General General { get; set; } = new();
    public Thumbnail Thumbnail { get; set; } = new();
    public Hotkeys Hotkeys { get; set; } = new();
    public Dictionary<string, string> Layouts { get; set; } = new();
    public List<string> LastOpenWindows { get; set; } = new();
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
}
