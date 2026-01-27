using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ClientOPreview.Models;

namespace ClientOPreview.Views;

public class ThumbnailOption
{
    public string Title { get; set; } = "";
}

public class DirectKeyItem : INotifyPropertyChanged
{
    public int Index { get; set; }
    public string Label { get; set; } = "";
    public string Key { get; set; } = "";
    
    private ObservableCollection<ThumbnailOption> _availableThumbnails = new();
    public ObservableCollection<ThumbnailOption> AvailableThumbnails
    {
        get => _availableThumbnails;
        set { _availableThumbnails = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AvailableThumbnails))); }
    }
    
    private ThumbnailOption? _selectedThumbnail;
    public ThumbnailOption? SelectedThumbnail
    {
        get => _selectedThumbnail;
        set { _selectedThumbnail = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedThumbnail))); }
    }
    
    public event PropertyChangedEventHandler? PropertyChanged;
}

public class OpenThumbnailItem
{
    public string Index { get; set; } = "";
    public string Title { get; set; } = "";
}

public partial class HotkeysPage : System.Windows.Controls.UserControl
{
    public event EventHandler<Hotkeys>? HotkeysChanged;
    
    private readonly List<DirectKeyItem> _directKeyItems = new();
    private readonly List<OpenThumbnailItem> _openThumbnailItems = new();
    private readonly List<string> _availableTitles = new();
    private Hotkeys _hotkeys = new();
    private bool _loading = false;

    public HotkeysPage()
    {
        InitializeComponent();
    }

    public void LoadFrom(Hotkeys hotkeys)
    {
        _loading = true;
        _hotkeys = hotkeys;
        
        ChkEnabled.IsChecked = hotkeys.Enabled;
        
        // Cycle modifiers
        ChkCycleAlt.IsChecked = hotkeys.CycleModifiers.Contains("Alt");
        ChkCycleCtrl.IsChecked = hotkeys.CycleModifiers.Contains("Ctrl");
        ChkCycleShift.IsChecked = hotkeys.CycleModifiers.Contains("Shift");
        TxtCycleKey.Text = hotkeys.CycleKey;
        
        // Direct modifiers
        ChkDirectAlt.IsChecked = hotkeys.DirectModifiers.Contains("Alt");
        ChkDirectCtrl.IsChecked = hotkeys.DirectModifiers.Contains("Ctrl");
        ChkDirectShift.IsChecked = hotkeys.DirectModifiers.Contains("Shift");
        
        // Direct keys - will be populated with thumbnails in UpdateOpenThumbnails
        _directKeyItems.Clear();
        for (int i = 0; i < 10; i++)
        {
            _directKeyItems.Add(new DirectKeyItem
            {
                Index = i,
                Label = $"Hotkey {i + 1}:",
                Key = i < hotkeys.DirectKeys.Count ? hotkeys.DirectKeys[i] : ""
            });
        }
        
        RefreshDirectKeysList();
        _loading = false;
    }

    public void UpdateOpenThumbnails(IEnumerable<string> titles)
    {
        _availableTitles.Clear();
        _availableTitles.AddRange(titles);
        
        // Update open thumbnails display list
        _openThumbnailItems.Clear();
        int index = 1;
        foreach (var title in titles)
        {
            _openThumbnailItems.Add(new OpenThumbnailItem
            {
                Index = $"{index}.",
                Title = title
            });
            index++;
        }
        
        OpenThumbnailsList.ItemsSource = null;
        OpenThumbnailsList.ItemsSource = _openThumbnailItems;
        
        TxtNoThumbnails.Visibility = _openThumbnailItems.Count == 0 
            ? Visibility.Visible 
            : Visibility.Collapsed;
        
        // Update dropdowns in direct keys list
        RefreshDirectKeysList();
    }

    private void RefreshDirectKeysList()
    {
        _loading = true;
        
        // Create thumbnail options with "(None)" option
        var options = new List<ThumbnailOption> { new ThumbnailOption { Title = "(None)" } };
        options.AddRange(_availableTitles.Select(t => new ThumbnailOption { Title = t }));
        
        foreach (var item in _directKeyItems)
        {
            item.AvailableThumbnails = new ObservableCollection<ThumbnailOption>(options);
            
            // Set selected based on saved mapping
            if (_hotkeys.DirectKeyMappings.TryGetValue(item.Index, out var mappedTitle) && !string.IsNullOrEmpty(mappedTitle))
            {
                item.SelectedThumbnail = item.AvailableThumbnails.FirstOrDefault(t => t.Title == mappedTitle);
            }
            else
            {
                item.SelectedThumbnail = item.AvailableThumbnails.FirstOrDefault(); // (None)
            }
        }
        
        DirectKeysList.ItemsSource = null;
        DirectKeysList.ItemsSource = _directKeyItems;
        
        _loading = false;
    }

    private void OnThumbnailSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;
        
        if (sender is System.Windows.Controls.ComboBox cb && cb.Tag is int index)
        {
            var selected = cb.SelectedItem as ThumbnailOption;
            if (selected != null && selected.Title != "(None)")
            {
                _hotkeys.DirectKeyMappings[index] = selected.Title;
            }
            else
            {
                _hotkeys.DirectKeyMappings.Remove(index);
            }
            NotifyChanged();
        }
    }

    private void OnEnabledChanged(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        _hotkeys.Enabled = ChkEnabled.IsChecked == true;
        NotifyChanged();
    }

    private void OnCycleModifiersChanged(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        _hotkeys.CycleModifiers = BuildModifiersString(ChkCycleAlt, ChkCycleCtrl, ChkCycleShift);
        NotifyChanged();
    }

    private void OnDirectModifiersChanged(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        _hotkeys.DirectModifiers = BuildModifiersString(ChkDirectAlt, ChkDirectCtrl, ChkDirectShift);
        NotifyChanged();
    }

    private string BuildModifiersString(System.Windows.Controls.CheckBox alt, System.Windows.Controls.CheckBox ctrl, System.Windows.Controls.CheckBox shift)
    {
        var parts = new List<string>();
        if (alt.IsChecked == true) parts.Add("Alt");
        if (ctrl.IsChecked == true) parts.Add("Ctrl");
        if (shift.IsChecked == true) parts.Add("Shift");
        return parts.Count > 0 ? string.Join("+", parts) : "None";
    }

    private void OnCycleKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        e.Handled = true;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key == Key.LeftAlt || key == Key.RightAlt || 
            key == Key.LeftCtrl || key == Key.RightCtrl ||
            key == Key.LeftShift || key == Key.RightShift ||
            key == Key.LWin || key == Key.RWin)
            return;
        
        TxtCycleKey.Text = key.ToString();
        _hotkeys.CycleKey = key.ToString();
        NotifyChanged();
    }

    private void OnDirectKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        e.Handled = true;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key == Key.LeftAlt || key == Key.RightAlt || 
            key == Key.LeftCtrl || key == Key.RightCtrl ||
            key == Key.LeftShift || key == Key.RightShift ||
            key == Key.LWin || key == Key.RWin)
            return;

        if (sender is System.Windows.Controls.TextBox tb && tb.Tag is int index)
        {
            tb.Text = key.ToString();
            if (index < _directKeyItems.Count)
            {
                _directKeyItems[index].Key = key.ToString();
            }
            UpdateDirectKeysInSettings();
            NotifyChanged();
        }
    }

    private void UpdateDirectKeysInSettings()
    {
        _hotkeys.DirectKeys.Clear();
        foreach (var item in _directKeyItems)
        {
            _hotkeys.DirectKeys.Add(item.Key);
        }
    }

    private void OnKeyBoxFocus(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.TextBox tb)
        {
            tb.Background = System.Windows.Media.Brushes.LightYellow;
        }
    }

    private void OnKeyBoxLostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.TextBox tb)
        {
            tb.Background = System.Windows.Media.Brushes.White;
        }
    }

    private void NotifyChanged()
    {
        HotkeysChanged?.Invoke(this, _hotkeys);
    }
}
