using System;
using System.Collections.Generic;
using ClientOPreview.Models;
using ClientOPreview.Views;

namespace ClientOPreview.Services;

/// <summary>
/// Which crop each preview shows. The persisted assignment is keyed by window title, which
/// drifts at runtime (login screen -> pilot name), so while a preview is alive the answer
/// comes from an in-session map keyed by HWND (bug-001).
/// </summary>
public sealed class RegionCoordinator
{
    private readonly SettingsService _service;
    private readonly SettingsData _settings;
    private readonly StreamManager _streams;
    private readonly Dictionary<IntPtr, string?> _live = new();

    private RegionPickerWindow? _picker;

    public RegionCoordinator(SettingsService service, StreamManager streams)
    {
        _service = service;
        _settings = service.GetSettings();
        _streams = streams;
    }

    /// <summary>Presets or assignments changed; the page should redraw.</summary>
    public event EventHandler? Changed;

    public IList<RegionPreset> Presets => _settings.Regions.Presets;

    public string? Resolve(IntPtr hwnd)
    {
        if (_live.TryGetValue(hwnd, out var live)) return live;
        return _settings.Regions.Assignments.TryGetValue(KeyFor(hwnd), out var name) ? name : null;
    }

    public void Apply(StreamWindow win, bool fit = false)
    {
        var preset = _settings.Regions.FindPreset(Resolve(win.SourceHwnd));
        win.ApplyRegion(preset);
        if (fit && preset != null) win.FitToRegion();
    }

    public void Assign(StreamWindow win, string? presetName, bool fit)
    {
        var hwnd = win.SourceHwnd;
        _live[hwnd] = string.IsNullOrEmpty(presetName) ? null : presetName;

        var key = KeyFor(hwnd);
        if (string.IsNullOrEmpty(presetName)) _settings.Regions.Assignments.Remove(key);
        else _settings.Regions.Assignments[key] = presetName;
        _service.SaveSettings();

        Apply(win, fit);
    }

    public void Forget(IntPtr hwnd) => _live.Remove(hwnd);

    public void DeletePreset(string name)
    {
        _settings.Regions.RemovePreset(name);
        foreach (var hwnd in new List<IntPtr>(_live.Keys))
        {
            if (string.Equals(_live[hwnd], name, StringComparison.OrdinalIgnoreCase)) _live[hwnd] = null;
        }
        _service.SaveSettings();

        foreach (var win in _streams.Windows) Apply(win);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <param name="newPreset">Start from a blank name so an existing preset can never be overwritten.</param>
    public void OpenPicker(StreamWindow win, bool newPreset = false)
    {
        if (_picker != null)
        {
            _picker.Activate();
            return;
        }

        var hwnd = win.SourceHwnd;
        var existing = newPreset ? null : _settings.Regions.FindPreset(Resolve(hwnd));
        var title = WindowEnumerator.GetTitle(hwnd);
        if (string.IsNullOrEmpty(title)) title = win.WindowTitle;

        var picker = new RegionPickerWindow(hwnd, title, existing, suggestNameFromTitle: !newPreset)
        {
            IsNameTaken = name =>
                !string.Equals(name, existing?.Name, StringComparison.OrdinalIgnoreCase)
                && _settings.Regions.FindPreset(name) != null
        };
        picker.RegionSaved += (_, result) =>
        {
            _settings.Regions.UpsertPreset(result.Preset);
            Assign(win, result.Preset.Name, fit: result.FitPreview);
            Changed?.Invoke(this, EventArgs.Empty);
        };
        picker.Closed += (_, __) => _picker = null;

        _picker = picker;
        picker.Show();
    }

    public void ClosePicker() => _picker?.Close();

    /// <summary>One row per open preview, for the Region Focus page.</summary>
    public List<StreamEntry> BuildEntries()
    {
        var entries = new List<StreamEntry>();
        foreach (var win in _streams.Windows)
        {
            var hwnd = win.SourceHwnd;
            var title = WindowEnumerator.GetTitle(hwnd);
            if (string.IsNullOrEmpty(title)) title = win.WindowTitle;
            if (win.OccurrenceIndex > 0) title = $"{title}  #{win.OccurrenceIndex + 1}";

            entries.Add(new StreamEntry
            {
                HWnd = hwnd,
                Key = KeyFor(hwnd),
                Title = title,
                AssignedPreset = Resolve(hwnd)
            });
        }
        return entries;
    }

    /// <summary>Persisted key. Unlike layouts, it ignores the TrackLocations toggle.</summary>
    private string KeyFor(IntPtr hwnd)
    {
        var title = WindowEnumerator.GetTitle(hwnd);
        if (string.IsNullOrEmpty(title)) title = "unknown";
        var occurrence = _streams.TryGet(hwnd, out var win) ? win.OccurrenceIndex : 0;
        return LayoutKey.For(title, occurrence);
    }
}
