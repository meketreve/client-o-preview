using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ClientOPreview.Localization;
using ClientOPreview.Models;

namespace ClientOPreview.Views;

public class ThumbnailOption
{
    public string Title { get; set; } = "";
    // The "(None)" entry is translated, so it cannot be recognized by its text.
    public bool IsNone { get; set; }
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

public class OpenThumbnailItem : INotifyPropertyChanged
{
    /// <summary>Window handle of the client, the only thing that tells two same-title clients apart.</summary>
    public IntPtr Handle { get; set; }

    public string Index { get; set; } = "";
    public string Title { get; set; } = "";

    private bool _isDropTarget;
    /// <summary>True while a dragged item would land above this one.</summary>
    public bool IsDropTarget
    {
        get => _isDropTarget;
        set { _isDropTarget = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsDropTarget))); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}

public partial class HotkeysPage : System.Windows.Controls.UserControl
{
    public event EventHandler<Hotkeys>? HotkeysChanged;

    /// <summary>The open previews, in the order the user dragged them into.</summary>
    public event EventHandler<IReadOnlyList<IntPtr>>? CycleOrderChanged;

    private readonly List<DirectKeyItem> _directKeyItems = new();
    private readonly List<OpenThumbnailItem> _openThumbnailItems = new();
    private readonly List<string> _availableTitles = new();
    private Hotkeys _hotkeys = new();
    private bool _loading = false;
    private System.Windows.Point _dragStart;
    private OpenThumbnailItem? _dragItem;

    public HotkeysPage()
    {
        InitializeComponent();
        Loc.LanguageChanged += (_, __) =>
        {
            foreach (var item in _directKeyItems)
                item.Label = Loc.Format("HotkeysItemLabel", item.Index + 1);
            RefreshDirectKeysList();
        };
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
                Label = Loc.Format("HotkeysItemLabel", i + 1),
                Key = i < hotkeys.DirectKeys.Count ? hotkeys.DirectKeys[i] : ""
            });
        }
        
        RefreshDirectKeysList();
        _loading = false;
    }

    /// <summary>Open previews, in cycle order. The handle comes along because titles repeat.</summary>
    public void UpdateOpenThumbnails(IEnumerable<(IntPtr Handle, string Title)> previews)
    {
        _openThumbnailItems.Clear();
        foreach (var (handle, title) in previews)
        {
            _openThumbnailItems.Add(new OpenThumbnailItem { Handle = handle, Title = title });
        }

        _availableTitles.Clear();
        _availableTitles.AddRange(_openThumbnailItems.Select(i => i.Title));

        RefreshOpenThumbnails();

        // Update dropdowns in direct keys list
        RefreshDirectKeysList();
    }

    private void RefreshOpenThumbnails()
    {
        for (int i = 0; i < _openThumbnailItems.Count; i++)
        {
            _openThumbnailItems[i].Index = $"{i + 1}.";
            _openThumbnailItems[i].IsDropTarget = false;
        }

        OpenThumbnailsList.ItemsSource = null;
        OpenThumbnailsList.ItemsSource = _openThumbnailItems;

        TxtNoThumbnails.Visibility = _openThumbnailItems.Count == 0
            ? Visibility.Visible
            : Visibility.Collapsed;
        TxtReorderHint.Visibility = _openThumbnailItems.Count > 1
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void RefreshDirectKeysList()
    {
        _loading = true;
        
        // Create thumbnail options with the "(None)" option
        var options = new List<ThumbnailOption> { new ThumbnailOption { Title = Loc.Get("HotkeysNone"), IsNone = true } };
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
            if (selected != null && !selected.IsNone)
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
        
        var name = NameOf(key);
        TxtCycleKey.Text = name;
        _hotkeys.CycleKey = name;
        NotifyChanged();
    }

    /// <summary>
    /// Key name to store, or empty for Esc / Delete / Backspace — a binding you can set has to be
    /// one you can unset, and an empty name simply registers nothing.
    /// </summary>
    private static string NameOf(Key key) =>
        key is Key.Escape or Key.Delete or Key.Back ? "" : key.ToString();

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
            var name = NameOf(key);
            tb.Text = name;
            if (index < _directKeyItems.Count)
            {
                _directKeyItems[index].Key = name;
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

    // ===== Reordering the cycle by dragging the open previews list =====

    private void OnOrderMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        _dragStart = e.GetPosition(null);
        _dragItem = ItemUnder(e.OriginalSource as DependencyObject);
    }

    private void OnOrderMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (e.LeftButton != System.Windows.Input.MouseButtonState.Pressed || _dragItem is null || _openThumbnailItems.Count < 2) return;

        // Below the system drag threshold this is a click, not a drag.
        var moved = _dragStart - e.GetPosition(null);
        if (Math.Abs(moved.X) < System.Windows.SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(moved.Y) < System.Windows.SystemParameters.MinimumVerticalDragDistance) return;

        System.Windows.DragDrop.DoDragDrop(OpenThumbnailsList, _dragItem, System.Windows.DragDropEffects.Move);
        ClearDropMarks();
    }

    private void OnOrderDragOver(object sender, System.Windows.DragEventArgs e)
    {
        var over = ItemUnder(e.OriginalSource as DependencyObject);
        e.Effects = _dragItem is null ? System.Windows.DragDropEffects.None : System.Windows.DragDropEffects.Move;
        e.Handled = true;

        foreach (var item in _openThumbnailItems) item.IsDropTarget = item == over && item != _dragItem;
    }

    private void OnOrderDrop(object sender, System.Windows.DragEventArgs e)
    {
        var dragged = _dragItem;
        ClearDropMarks();
        if (dragged is null) return;

        var target = ItemUnder(e.OriginalSource as DependencyObject);
        if (ReferenceEquals(target, dragged)) return;

        _openThumbnailItems.Remove(dragged);
        // No item under the cursor means the empty space below the list: drop at the end.
        var at = target is null ? _openThumbnailItems.Count : _openThumbnailItems.IndexOf(target);
        _openThumbnailItems.Insert(at, dragged);

        RefreshOpenThumbnails();
        CycleOrderChanged?.Invoke(this, _openThumbnailItems.Select(i => i.Handle).ToList());
    }

    private void ClearDropMarks()
    {
        _dragItem = null;
        foreach (var item in _openThumbnailItems) item.IsDropTarget = false;
    }

    /// <summary>The list item the mouse is over, walking up from whatever bit of the template was hit.</summary>
    private static OpenThumbnailItem? ItemUnder(DependencyObject? source)
    {
        while (source is not null and not System.Windows.Controls.ListBoxItem)
        {
            source = source is System.Windows.Media.Visual or System.Windows.Media.Media3D.Visual3D
                ? System.Windows.Media.VisualTreeHelper.GetParent(source)
                : LogicalTreeHelper.GetParent(source);
        }
        return (source as System.Windows.Controls.ListBoxItem)?.DataContext as OpenThumbnailItem;
    }

    /// <summary>Show which combos Windows refused, so a dead hotkey does not look like a broken app.</summary>
    public void ShowRegistrationResult(IReadOnlyList<string> failedCombos)
    {
        if (failedCombos.Count == 0)
        {
            TxtRegisterWarning.Visibility = Visibility.Collapsed;
            return;
        }

        TxtRegisterWarning.Text = Loc.Format("HotkeysRegisterFailed", string.Join(", ", failedCombos));
        TxtRegisterWarning.Visibility = Visibility.Visible;
    }

    private void NotifyChanged()
    {
        HotkeysChanged?.Invoke(this, _hotkeys);
    }
}
