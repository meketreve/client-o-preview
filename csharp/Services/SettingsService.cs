using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using ClientOPreview.Models;

namespace ClientOPreview.Services;

public class SettingsService
{
    private readonly string _path;

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
    }

    public SettingsData Load()
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

    public void Save(SettingsData data)
    {
        try
        {
            using var stream = new MemoryStream();
            using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });
            writer.WriteStartObject();
            writer.WritePropertyName("general");
            writer.WriteStartObject();
            writer.WriteBoolean("minimize_to_tray", data.General.MinimizeToTray);
            writer.WriteBoolean("track_locations", data.General.TrackLocations);
            writer.WriteBoolean("hide_active_preview", data.General.HideActivePreview);
            writer.WriteBoolean("minimize_inactive", data.General.MinimizeInactive);
            writer.WriteBoolean("reorder_inactive", data.General.ReorderInactive);
            writer.WriteBoolean("previews_topmost", data.General.PreviewsTopmost);
            writer.WriteBoolean("hide_when_not_active", data.General.HideWhenNotActive);
            writer.WriteBoolean("unique_layout", data.General.UniqueLayout);
            writer.WriteEndObject();

            writer.WritePropertyName("thumbnail");
            writer.WriteStartObject();
            writer.WriteNumber("width", data.Thumbnail.Width);
            writer.WriteNumber("height", data.Thumbnail.Height);
            writer.WriteNumber("opacity_pct", data.Thumbnail.OpacityPct);
            writer.WriteEndObject();

            writer.WritePropertyName("layouts");
            writer.WriteStartObject();
            foreach (var kv in data.Layouts)
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

    public string? GetLayout(string key)
    {
        try { return Load().Layouts.TryGetValue(key, out var g) ? g : null; } catch { return null; }
    }

    public void SetLayout(string key, string geometry)
    {
        try
        {
            var d = Load();
            d.Layouts[key] = geometry;
            Save(d);
        }
        catch { }
    }
}
