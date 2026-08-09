using System.Collections.Generic;

namespace ClientOPreview.Models;

public class SettingsData
{
    public General General { get; set; } = new();
    public Thumbnail Thumbnail { get; set; } = new();
    public Hotkeys Hotkeys { get; set; } = new();
    public Zoom Zoom { get; set; } = new();
    public RegionSettings Regions { get; set; } = new();
    // UI language: "en" or "pt-BR". Empty means "not chosen yet" -> follow the system.
    public string Language { get; set; } = string.Empty;
    public Dictionary<string, string> Layouts { get; set; } = new();
    public List<string> LastOpenWindows { get; set; } = new();
}

// A named crop of the source window, stored normalized (0.0 - 1.0) so it survives
// resolution changes of the monitored client.
public class RegionPreset
{
    public string Name { get; set; } = string.Empty;
    public double X { get; set; } = 0.0;
    public double Y { get; set; } = 0.0;
    public double W { get; set; } = 1.0;
    public double H { get; set; } = 1.0;
    // Letterbox the preview so the crop keeps its original proportions.
    public bool LockAspect { get; set; } = true;

    public RegionPreset Clone() => new()
    {
        Name = Name, X = X, Y = Y, W = W, H = H, LockAspect = LockAspect
    };
}

public class RegionSettings
{
    public List<RegionPreset> Presets { get; set; } = new();
    // Layout key of a stream (title:occurrence:title) -> preset name
    public Dictionary<string, string> Assignments { get; set; } = new();

    public RegionPreset? FindPreset(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        foreach (var p in Presets)
        {
            if (string.Equals(p.Name, name, System.StringComparison.OrdinalIgnoreCase)) return p;
        }
        return null;
    }

    public void UpsertPreset(RegionPreset preset)
    {
        for (int i = 0; i < Presets.Count; i++)
        {
            if (string.Equals(Presets[i].Name, preset.Name, System.StringComparison.OrdinalIgnoreCase))
            {
                Presets[i] = preset;
                return;
            }
        }
        Presets.Add(preset);
    }

    public void RemovePreset(string name)
    {
        Presets.RemoveAll(p => string.Equals(p.Name, name, System.StringComparison.OrdinalIgnoreCase));
        var orphans = new List<string>();
        foreach (var kv in Assignments)
        {
            if (string.Equals(kv.Value, name, System.StringComparison.OrdinalIgnoreCase)) orphans.Add(kv.Key);
        }
        foreach (var key in orphans) Assignments.Remove(key);
    }
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
    public bool PreviewsTopmost { get; set; } = true;
    // Only keep the previews on top while a monitored client (or this app) is the foreground
    // window; otherwise they sink behind whatever the user is doing.
    public bool TopmostOnlyWhenClientFocused { get; set; } = false;
    public bool UniqueLayout { get; set; } = true;
    public bool SnapToGrid { get; set; } = false;
    public int GridSize { get; set; } = 20;
}

public class Thumbnail
{
    public int Width { get; set; } = 160;
    public int Height { get; set; } = 90;
    public int OpacityPct { get; set; } = 90;
    public int TitleFontSize { get; set; } = 12;
    public string ActiveHighlightColor { get; set; } = "#2864C8";
}

public class Hotkeys
{
    public bool Enabled { get; set; } = true;
    public string CycleKey { get; set; } = "Tab";
    // Not Alt+Tab: Windows reserves it, so RegisterHotKey refuses and the default never fires.
    public string CycleModifiers { get; set; } = "Ctrl";
    public string DirectModifiers { get; set; } = "Alt";
    public List<string> DirectKeys { get; set; } = new()
    {
        "NumPad1", "NumPad2", "NumPad3", "NumPad4", "NumPad5",
        "NumPad6", "NumPad7", "NumPad8", "NumPad9", "NumPad0"
    };
    // Maps hotkey index (0-9) to window title
    public Dictionary<int, string> DirectKeyMappings { get; set; } = new();

    // Preview keys (same form as the layout keys) in the order the cycle hotkey walks them.
    // Previews absent from this list open at the end.
    public List<string> CycleOrder { get; set; } = new();
}
