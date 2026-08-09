using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using ClientOPreview.Models;

namespace ClientOPreview.Services;

/// <summary>
/// Reads and writes %APPDATA%/client-o-preview/settings.json.
///
/// The JSON shape is derived from <see cref="SettingsData"/> by the snake_case naming policy,
/// so adding a setting means editing the model only — there is no hand-written reader/writer
/// pair to keep in sync (forgetting the writer half is what caused bug-002).
/// </summary>
public class SettingsService
{
    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private readonly string _path;
    private readonly object _lock = new();
    private SettingsData _settings;

    public SettingsService() : this(System.IO.Path.Combine(AppLog.Directory, "settings.json")) { }

    /// <summary>Explicit path, used by the tests.</summary>
    public SettingsService(string path)
    {
        _path = path;
        _settings = Load();
    }

    public string FilePath => _path;

    private SettingsData Load()
    {
        if (!File.Exists(_path)) return new SettingsData();

        string json;
        try
        {
            json = File.ReadAllText(_path);
        }
        catch (Exception ex)
        {
            AppLog.Error($"settings read failed ({_path}), using defaults", ex);
            return new SettingsData();
        }

        try
        {
            var data = JsonSerializer.Deserialize<SettingsData>(json, JsonOptions) ?? new SettingsData();
            ApplyLegacyFallbacks(json, data);
            return data;
        }
        catch (Exception ex)
        {
            // Never silently start from scratch: keep the unreadable file so it can be inspected.
            AppLog.Error($"settings parse failed, falling back to defaults (saved a copy as {_path}.bak)", ex);
            try { File.Copy(_path, _path + ".bak", overwrite: true); }
            catch (Exception copyEx) { AppLog.Warn("could not preserve the corrupt settings file", copyEx); }
            return new SettingsData();
        }
    }

    /// <summary>Keys written by versions before the current model. Kept read-only.</summary>
    private static void ApplyLegacyFallbacks(string json, SettingsData data)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("zoom", out var zoom)
                && !zoom.TryGetProperty("resize_on_hover", out _)
                && zoom.TryGetProperty("zoom_on_hover", out var legacy)
                && legacy.ValueKind == JsonValueKind.True)
            {
                data.Zoom.ResizeOnHover = true;
            }
        }
        catch (Exception ex)
        {
            AppLog.Warn("legacy settings fallback skipped", ex);
        }
    }

    private void Save()
    {
        var temp = _path + ".tmp";
        try
        {
            // Write-then-swap: a crash mid-write can no longer truncate a good settings.json.
            File.WriteAllText(temp, JsonSerializer.Serialize(_settings, JsonOptions));
            File.Move(temp, _path, overwrite: true);
        }
        catch (Exception ex)
        {
            AppLog.Error($"settings save failed ({_path})", ex);
            try { if (File.Exists(temp)) File.Delete(temp); } catch { /* nothing left to do */ }
        }
    }

    public SettingsData GetSettings() => _settings;

    public void SaveSettings()
    {
        lock (_lock) { Save(); }
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

    public void RemoveLayout(string key)
    {
        lock (_lock)
        {
            if (_settings.Layouts.Remove(key)) Save();
        }
    }

    public void SetLastOpenWindows(List<string> titles)
    {
        lock (_lock)
        {
            _settings.LastOpenWindows = titles;
            Save();
        }
    }

    public List<string> GetLastOpenWindows()
    {
        lock (_lock)
        {
            return new List<string>(_settings.LastOpenWindows);
        }
    }
}
