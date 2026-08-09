using ClientOPreview.Services;
using Xunit;

namespace ClientOPreview.Tests;

/// <summary>
/// A round trip is the cheapest guard against bug-002: a setting that the UI can change but
/// that never reaches the file looks fine until the app is restarted.
/// </summary>
public class SettingsRoundTripTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "cop-tests-" + Guid.NewGuid().ToString("N"));

    private string Path_ => Path.Combine(_dir, "settings.json");

    public SettingsRoundTripTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* temp dir */ }
    }

    [Fact]
    public void Every_setting_the_ui_can_change_survives_a_restart()
    {
        var first = new SettingsService(Path_);
        var s = first.GetSettings();

        s.Thumbnail.Width = 321;
        s.Thumbnail.Height = 123;
        s.Thumbnail.OpacityPct = 55;
        s.Thumbnail.TitleFontSize = 17;
        s.Thumbnail.ActiveHighlightColor = "#ABCDEF";
        s.General.MinimizeToTray = true;
        s.General.TrackLocations = false;
        s.General.PreviewsTopmost = false;
        s.General.TopmostOnlyWhenClientFocused = true;
        s.General.UniqueLayout = false;
        s.General.SnapToGrid = true;
        s.General.GridSize = 42;
        s.Zoom.ResizeOnHover = true;
        s.Zoom.InternalZoom = false;
        s.Zoom.Magnification = 2.25;
        s.Zoom.OffsetX = 0.1;
        s.Zoom.OffsetY = 0.9;
        s.Hotkeys.Enabled = false;
        s.Hotkeys.CycleKey = "F9";
        s.Hotkeys.CycleModifiers = "Ctrl+Shift";
        s.Hotkeys.DirectKeyMappings[3] = "EVE - Pilot";
        s.Language = "pt-BR";
        first.SaveSettings();

        var reloaded = new SettingsService(Path_).GetSettings();

        Assert.Equal(321, reloaded.Thumbnail.Width);
        Assert.Equal(123, reloaded.Thumbnail.Height);
        Assert.Equal(55, reloaded.Thumbnail.OpacityPct);
        Assert.Equal(17, reloaded.Thumbnail.TitleFontSize);
        Assert.Equal("#ABCDEF", reloaded.Thumbnail.ActiveHighlightColor);
        Assert.True(reloaded.General.MinimizeToTray);
        Assert.False(reloaded.General.TrackLocations);
        Assert.False(reloaded.General.PreviewsTopmost);
        Assert.True(reloaded.General.TopmostOnlyWhenClientFocused);
        Assert.False(reloaded.General.UniqueLayout);
        Assert.True(reloaded.General.SnapToGrid);
        Assert.Equal(42, reloaded.General.GridSize);
        Assert.True(reloaded.Zoom.ResizeOnHover);
        Assert.False(reloaded.Zoom.InternalZoom);
        Assert.Equal(2.25, reloaded.Zoom.Magnification);
        Assert.Equal(0.1, reloaded.Zoom.OffsetX);
        Assert.Equal(0.9, reloaded.Zoom.OffsetY);
        Assert.False(reloaded.Hotkeys.Enabled);
        Assert.Equal("F9", reloaded.Hotkeys.CycleKey);
        Assert.Equal("Ctrl+Shift", reloaded.Hotkeys.CycleModifiers);
        Assert.Equal("EVE - Pilot", reloaded.Hotkeys.DirectKeyMappings[3]);
        Assert.Equal("pt-BR", reloaded.Language);
    }

    [Fact]
    public void Region_presets_and_assignments_survive_a_restart()
    {
        var service = new SettingsService(Path_);
        var regions = service.GetSettings().Regions;
        regions.UpsertPreset(new Models.RegionPreset
        {
            Name = "Cãpacitor",
            X = 0.125,
            Y = 0.25,
            W = 0.5,
            H = 0.75,
            LockAspect = false
        });
        regions.Assignments["title:1:EVE"] = "Cãpacitor";
        service.SaveSettings();

        var reloaded = new SettingsService(Path_).GetSettings().Regions;
        var preset = reloaded.FindPreset("cãpacitor");   // lookup is case-insensitive

        Assert.NotNull(preset);
        Assert.Equal(0.125, preset!.X);
        Assert.Equal(0.75, preset.H);
        Assert.False(preset.LockAspect);
        Assert.Equal("Cãpacitor", reloaded.Assignments["title:1:EVE"]);
    }

    [Fact]
    public void The_json_keeps_the_snake_case_names_older_versions_wrote()
    {
        var service = new SettingsService(Path_);
        service.GetSettings().General.TopmostOnlyWhenClientFocused = true;
        service.GetSettings().Regions.UpsertPreset(new Models.RegionPreset { Name = "Drones", W = 0.4, H = 0.3 });
        service.SaveSettings();

        var json = File.ReadAllText(Path_);
        foreach (var key in new[]
                 {
                     "\"general\"", "\"thumbnail\"", "\"hotkeys\"", "\"zoom\"", "\"regions\"",
                     "\"layouts\"", "\"last_open_windows\"", "\"language\"",
                     "\"minimize_to_tray\"", "\"track_locations\"", "\"previews_topmost\"",
                     "\"topmost_only_when_client_focused\"", "\"unique_layout\"", "\"snap_to_grid\"",
                     "\"grid_size\"", "\"opacity_pct\"", "\"title_font_size\"", "\"active_highlight_color\"",
                     "\"cycle_key\"", "\"cycle_modifiers\"", "\"direct_modifiers\"", "\"direct_keys\"",
                     "\"direct_key_mappings\"", "\"resize_on_hover\"", "\"internal_zoom\"",
                     "\"offset_x\"", "\"offset_y\"", "\"lock_aspect\""
                 })
        {
            Assert.Contains(key, json);
        }
    }

    [Fact]
    public void A_settings_file_written_by_v070_still_loads()
    {
        File.WriteAllText(Path_, """
        {
          "general": { "minimize_to_tray": true, "grid_size": 25 },
          "thumbnail": { "width": 200, "active_highlight_color": "#112233" },
          "hotkeys": { "cycle_key": "Tab", "direct_keys": ["NumPad1", "NumPad2"],
                       "direct_key_mappings": { "0": "EVE - Alpha" } },
          "zoom": { "zoom_on_hover": true, "magnification": 1.75 },
          "regions": { "presets": [ { "name": "Drones", "x": 0.1, "y": 0.2, "w": 0.3, "h": 0.4 } ],
                       "assignments": { "title:0:EVE": "Drones" } },
          "language": "pt-BR",
          "layouts": { "title:0:EVE": "640x360+10+20" },
          "last_open_windows": [ "EVE - Alpha" ]
        }
        """);

        var s = new SettingsService(Path_).GetSettings();

        Assert.True(s.General.MinimizeToTray);
        Assert.Equal(25, s.General.GridSize);
        Assert.True(s.General.TrackLocations);          // absent -> model default, as before
        Assert.Equal(200, s.Thumbnail.Width);
        Assert.Equal(90, s.Thumbnail.Height);           // absent -> default
        Assert.Equal("#112233", s.Thumbnail.ActiveHighlightColor);
        Assert.Equal(2, s.Hotkeys.DirectKeys.Count);    // replaced, not appended to the defaults
        Assert.Equal("EVE - Alpha", s.Hotkeys.DirectKeyMappings[0]);
        Assert.True(s.Zoom.ResizeOnHover);              // legacy "zoom_on_hover"
        Assert.Equal(1.75, s.Zoom.Magnification);
        Assert.True(s.Regions.Presets[0].LockAspect);   // absent -> default true
        Assert.Equal("Drones", s.Regions.Assignments["title:0:EVE"]);
        Assert.Equal("pt-BR", s.Language);
        Assert.Equal("640x360+10+20", s.Layouts["title:0:EVE"]);
        Assert.Single(s.LastOpenWindows);
    }

    [Fact]
    public void A_corrupt_file_falls_back_to_defaults_and_is_kept_as_bak()
    {
        File.WriteAllText(Path_, "{ this is not json");

        var s = new SettingsService(Path_).GetSettings();

        Assert.Equal(160, s.Thumbnail.Width);           // defaults
        Assert.True(File.Exists(Path_ + ".bak"));       // the broken file is preserved
    }

    [Fact]
    public void Layout_helpers_write_through_to_the_file()
    {
        var service = new SettingsService(Path_);
        service.SetLayout("title:0:EVE", "640x360+10+20");
        service.SetLastOpenWindows(new List<string> { "EVE - Alpha" });

        var reloaded = new SettingsService(Path_);
        Assert.Equal("640x360+10+20", reloaded.GetLayout("title:0:EVE"));
        Assert.Equal(new[] { "EVE - Alpha" }, reloaded.GetLastOpenWindows());

        reloaded.RemoveLayout("title:0:EVE");
        Assert.Null(new SettingsService(Path_).GetLayout("title:0:EVE"));
    }
}
