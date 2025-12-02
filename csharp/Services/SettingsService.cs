using System;
using System.IO;
using System.Text.Json;
using ClientOPreview.Models;

namespace ClientOPreview.Services;

public class SettingsService
{
    private readonly string _path;
    private SettingsData _settings;
    private readonly object _lock = new();

    public SettingsService()
    {
        try
        {
            var appdata = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var dir = Path.Combine(appdata, "client-o-preview");
            Directory.CreateDirectory(dir);
            _path = Path.Combine(dir, "settings.json");
        }
        catch
        {
            _path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");
        }
        _settings = Load();
    }

    private SettingsData Load()
    {
        try
        {
            if (File.Exists(_path))
            {
                var json = File.ReadAllText(_path);
                var doc = JsonDocument.Parse(json);
                // compatibilidade com o formato Python
                var data = new SettingsData();
                if (doc.RootElement.TryGetProperty("general", out var gen))
                {
                    data.General.MinimizeToTray = gen.TryGetProperty("minimize_to_tray", out var v1) && v1.GetBoolean();
                    data.General.TrackLocations = !gen.TryGetProperty("track_locations", out var v2) || v2.GetBoolean();
                    data.General.HideActivePreview = gen.TryGetProperty("hide_active_preview", out var v3) && v3.GetBoolean();
                    data.General.MinimizeInactive = gen.TryGetProperty("minimize_inactive", out var v4) && v4.GetBoolean();
                    data.General.ReorderInactive = !gen.TryGetProperty("reorder_inactive", out var v5) || v5.GetBoolean();
                    data.General.PreviewsTopmost = !gen.TryGetProperty("previews_topmost", out var v6) || v6.GetBoolean();
                    data.General.HideWhenNotActive = gen.TryGetProperty("hide_when_not_active", out var v7) && v7.GetBoolean();
                    data.General.UniqueLayout = !gen.TryGetProperty("unique_layout", out var v8) || v8.GetBoolean();
                }
                if (doc.RootElement.TryGetProperty("thumbnail", out var th))
                {
                    data.Thumbnail.Width = th.TryGetProperty("width", out var w) ? w.GetInt32() : data.Thumbnail.Width;
                    data.Thumbnail.Height = th.TryGetProperty("height", out var h) ? h.GetInt32() : data.Thumbnail.Height;
                    data.Thumbnail.OpacityPct = th.TryGetProperty("opacity_pct", out var op) ? op.GetInt32() : data.Thumbnail.OpacityPct;
                }
                if (doc.RootElement.TryGetProperty("layouts", out var layouts) && layouts.ValueKind == JsonValueKind.Object)
                {
                    foreach (var prop in layouts.EnumerateObject())
                        data.Layouts[prop.Name] = prop.Value.GetString() ?? string.Empty;
                }
                return data;
            }
        }
        catch { }
        return new SettingsData();
    }

    private void Save()
    {
        try
        {
            using var stream = new MemoryStream();
            using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });
            writer.WriteStartObject();
            writer.WritePropertyName("general");
            writer.WriteStartObject();
            writer.WriteBoolean("minimize_to_tray", _settings.General.MinimizeToTray);
            writer.WriteBoolean("track_locations", _settings.General.TrackLocations);
            writer.WriteBoolean("hide_active_preview", _settings.General.HideActivePreview);
            writer.WriteBoolean("minimize_inactive", _settings.General.MinimizeInactive);
            writer.WriteBoolean("reorder_inactive", _settings.General.ReorderInactive);
            writer.WriteBoolean("previews_topmost", _settings.General.PreviewsTopmost);
            writer.WriteBoolean("hide_when_not_active", _settings.General.HideWhenNotActive);
            writer.WriteBoolean("unique_layout", _settings.General.UniqueLayout);
            writer.WriteEndObject();

            writer.WritePropertyName("thumbnail");
            writer.WriteStartObject();
            writer.WriteNumber("width", _settings.Thumbnail.Width);
            writer.WriteNumber("height", _settings.Thumbnail.Height);
            writer.WriteNumber("opacity_pct", _settings.Thumbnail.OpacityPct);
            writer.WriteEndObject();

            writer.WritePropertyName("layouts");
            writer.WriteStartObject();
            foreach (var kv in _settings.Layouts)
            {
                writer.WriteString(kv.Key, kv.Value);
            }
            writer.WriteEndObject();

            writer.WriteEndObject();
            writer.Flush();
            File.WriteAllBytes(_path, stream.ToArray());
        }
        catch { }
    }

    public SettingsData GetSettings()
    {
        return _settings;
    }

    public void SaveSettings()
    {
        lock(_lock)
        {
            Save();
        }
    }

    public string? GetLayout(string key)
    {
        lock (_lock)
        {
            return _settings.Layouts.TryGetValue(key, out var g) ? g : null;
        }
    }

    public void SetLayout(string key, string geometry)
    {
        lock (_lock)
        {
            _settings.Layouts[key] = geometry;
            Save();
        }
    }
}
