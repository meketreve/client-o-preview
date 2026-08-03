using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using ClientOPreview.Localization;
using ClientOPreview.Models;

namespace ClientOPreview.Views;

public class StreamEntry
{
    public IntPtr HWnd { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? AssignedPreset { get; set; }

    public string Display => string.IsNullOrEmpty(AssignedPreset)
        ? Title
        : $"{Title}   ▣ {AssignedPreset}";
}

public class PresetEntry
{
    public RegionPreset Preset { get; set; } = new();
    public string Display => $"{Preset.Name}   ({Preset.W:P0} × {Preset.H:P0})";
}

public partial class RegionPage : System.Windows.Controls.UserControl
{
    private bool _loading;

    // Kept so the lists can be rebuilt when the language changes.
    private IList<StreamEntry> _streams = new List<StreamEntry>();
    private IList<RegionPreset> _presets = new List<RegionPreset>();

    public event EventHandler? RefreshRequested;
    public event EventHandler<StreamEntry>? DefineRequested;
    public event EventHandler<StreamEntry?>? NewPresetRequested;
    public event EventHandler<(StreamEntry Stream, string? Preset)>? AssignRequested;
    public event EventHandler<string>? DeletePresetRequested;

    public RegionPage()
    {
        InitializeComponent();
        Loc.LanguageChanged += (_, __) => { SetPresets(_presets); SetStreams(_streams); };
    }

    private static string NoneLabel => Loc.Get("RegionNone");

    public void SetStreams(IList<StreamEntry> streams)
    {
        _loading = true;
        _streams = streams;
        var previousKey = (ListStreams.SelectedItem as StreamEntry)?.Key;
        ListStreams.ItemsSource = null;
        ListStreams.ItemsSource = streams;
        if (previousKey != null)
            ListStreams.SelectedItem = streams.FirstOrDefault(s => s.Key == previousKey);
        if (ListStreams.SelectedItem == null && streams.Count > 0)
            ListStreams.SelectedIndex = 0;
        _loading = false;
        SyncCombo();
    }

    public void SetPresets(IList<RegionPreset> presets)
    {
        _loading = true;
        _presets = presets;
        var previousPreset = ListPresets.SelectedItem as PresetEntry;
        var entries = presets.Select(p => new PresetEntry { Preset = p }).ToList();
        ListPresets.ItemsSource = entries;
        if (previousPreset != null)
            ListPresets.SelectedItem = entries.FirstOrDefault(p => p.Preset.Name == previousPreset.Preset.Name);

        var items = new List<string> { NoneLabel };
        items.AddRange(presets.Select(p => p.Name));
        CmbPresets.ItemsSource = items;
        _loading = false;
        SyncCombo();
    }

    private void SyncCombo()
    {
        _loading = true;
        var stream = ListStreams.SelectedItem as StreamEntry;
        var assigned = stream?.AssignedPreset;
        CmbPresets.SelectedItem = string.IsNullOrEmpty(assigned) ? NoneLabel : assigned;
        if (CmbPresets.SelectedItem == null) CmbPresets.SelectedItem = NoneLabel;
        _loading = false;
    }

    private void OnStreamSelected(object sender, SelectionChangedEventArgs e) => SyncCombo();

    private void OnPresetSelected(object sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;
        if (ListStreams.SelectedItem is not StreamEntry stream) return;

        var selected = CmbPresets.SelectedItem as string;
        var preset = selected == NoneLabel ? null : selected;
        stream.AssignedPreset = preset;
        AssignRequested?.Invoke(this, (stream, preset));
    }

    private void OnRefresh(object sender, RoutedEventArgs e) => RefreshRequested?.Invoke(this, EventArgs.Empty);

    private void OnNewPreset(object sender, RoutedEventArgs e)
    {
        if (ListStreams.SelectedItem is StreamEntry stream)
            NewPresetRequested?.Invoke(this, stream);
        else
            System.Windows.MessageBox.Show(Loc.Get("RegionNoExample"), Loc.Get("RegionTitle"),
                MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void OnDefine(object sender, RoutedEventArgs e)
    {
        if (ListStreams.SelectedItem is StreamEntry stream)
            DefineRequested?.Invoke(this, stream);
        else
            System.Windows.MessageBox.Show(Loc.Get("RegionSelectFirst"), Loc.Get("RegionTitle"),
                MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void OnClear(object sender, RoutedEventArgs e)
    {
        if (ListStreams.SelectedItem is not StreamEntry stream) return;
        stream.AssignedPreset = null;
        SyncCombo();
        AssignRequested?.Invoke(this, (stream, null));
    }

    private void OnDeletePreset(object sender, RoutedEventArgs e)
    {
        if (ListPresets.SelectedItem is not PresetEntry entry) return;
        DeletePresetRequested?.Invoke(this, entry.Preset.Name);
    }
}
