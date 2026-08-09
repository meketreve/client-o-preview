using System;
using System.Collections.Generic;
using ClientOPreview.Models;

namespace ClientOPreview.Services;

/// <summary>
/// Where each preview reopens: geometry per window plus the list of previews that were
/// open when the app last closed. Key building and parsing live in <see cref="LayoutKey"/>.
/// </summary>
public sealed class LayoutStore
{
    private readonly SettingsService _service;
    private readonly SettingsData _settings;
    private int _stagger;

    public LayoutStore(SettingsService service)
    {
        _service = service;
        _settings = service.GetSettings();
    }

    private bool Tracking => _settings.General.TrackLocations;

    /// <summary>Empty when tracking is off — callers treat that as "do not persist".</summary>
    public string KeyFor(IntPtr hwnd, int occurrence)
    {
        if (!Tracking) return string.Empty;
        if (!_settings.General.UniqueLayout) return LayoutKey.Shared;
        return LayoutKey.For(WindowEnumerator.GetTitle(hwnd), occurrence);
    }

    /// <summary>Restores the saved geometry, or staggers the window so previews do not stack.</summary>
    public void Apply(StreamWindow win)
    {
        var key = KeyFor(win.SourceHwnd, win.OccurrenceIndex);
        if (string.IsNullOrEmpty(key)) return;

        var geometry = _service.GetLayout(key) ?? MigrateLegacy(win, key);

        if (!LayoutKey.TryParseGeometry(geometry, out var left, out var top, out var width, out var height))
        {
            if (!string.IsNullOrWhiteSpace(geometry))
                AppLog.Warn($"unreadable layout for '{key}'", geometry!);

            win.Left = 50 + (_stagger % 10) * 30;
            win.Top = 50 + (_stagger % 10) * 30;
            _stagger++;
            return;
        }

        win.Left = left;
        win.Top = top;
        win.Width = Math.Max(120, width);
        win.Height = Math.Max(90, height);
    }

    /// <summary>Layouts saved before the occurrence index existed; moved to the new key once.</summary>
    private string? MigrateLegacy(StreamWindow win, string key)
    {
        if (win.OccurrenceIndex != 0 || !_settings.General.UniqueLayout) return null;

        var legacyKey = LayoutKey.LegacyFor(WindowEnumerator.GetTitle(win.SourceHwnd));
        var geometry = _service.GetLayout(legacyKey);
        if (string.IsNullOrWhiteSpace(geometry)) return null;

        _service.SetLayout(key, geometry);
        _service.RemoveLayout(legacyKey);
        return geometry;
    }

    public void Save(StreamWindow win)
    {
        var key = KeyFor(win.SourceHwnd, win.OccurrenceIndex);
        if (string.IsNullOrEmpty(key)) return;
        _service.SetLayout(key, LayoutKey.FormatGeometry(win.Left, win.Top, win.Width, win.Height));
    }

    public void RememberOpenWindows(IEnumerable<IntPtr> handles)
    {
        var titles = new List<string>();
        foreach (var hwnd in handles)
        {
            var title = WindowEnumerator.GetTitle(hwnd);
            if (!string.IsNullOrEmpty(title) && !titles.Contains(title)) titles.Add(title);
        }
        _service.SetLastOpenWindows(titles);
    }

    public List<string> LastOpenWindows() => _service.GetLastOpenWindows();
}
