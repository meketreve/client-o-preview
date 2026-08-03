using System;
using System.Windows;
using System.Windows.Controls;
using ClientOPreview.Models;

namespace ClientOPreview.Views;

public record ThumbnailArgs(int Width, int Height, int OpacityPct, int TitleFontSize, string ActiveColor);

// Single settings page: preview look/behaviour plus everything that used to live in the
// old "General" tab.
public partial class ThumbnailPage : System.Windows.Controls.UserControl
{
    public event EventHandler<ThumbnailArgs>? ThumbnailChanged;
    public event EventHandler<bool>? TopmostChanged;
    public event EventHandler<bool>? TopmostOnlyWhenClientFocusedChanged;
    public event EventHandler<bool>? MinimizeToTrayChanged;
    public event EventHandler<bool>? TrackLocationsChanged;
    public event EventHandler<bool>? UniqueLayoutChanged;
    public event EventHandler<bool>? SnapToGridChanged;
    public event EventHandler<int>? GridSizeChanged;

    private bool _loading = false;

    public ThumbnailPage()
    {
        InitializeComponent();
    }

    public void LoadFrom(Thumbnail thumb, General gen)
    {
        _loading = true;
        TxtWidth.Text = thumb.Width.ToString();
        TxtHeight.Text = thumb.Height.ToString();
        SldOpacity.Value = thumb.OpacityPct;
        SldFontSize.Value = thumb.TitleFontSize;
        TxtActiveColor.Text = thumb.ActiveHighlightColor;

        ChkTopmost.IsChecked = gen.PreviewsTopmost;
        ChkTopmostOnlyFocused.IsChecked = gen.TopmostOnlyWhenClientFocused;
        ChkMinimizeToTray.IsChecked = gen.MinimizeToTray;
        ChkTrackLocations.IsChecked = gen.TrackLocations;
        ChkUniqueLayout.IsChecked = gen.UniqueLayout;
        ChkSnapToGrid.IsChecked = gen.SnapToGrid;
        SliderGridSize.Value = gen.GridSize;
        _loading = false;
        UpdateLabels();
    }

    private void UpdateLabels()
    {
        if (LblOpacity != null) LblOpacity.Text = $"{(int)SldOpacity.Value}%";
        if (LblFontSize != null) LblFontSize.Text = $"{(int)SldFontSize.Value}px";
    }

    private void OnSliderChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        UpdateLabels();
    }

    private void OnColorPresetClick(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button btn && btn.Background is System.Windows.Media.SolidColorBrush brush)
        {
            var color = brush.Color;
            TxtActiveColor.Text = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
        }
    }

    private void OnApply(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        if (!int.TryParse(TxtWidth.Text, out var w)) w = 160;
        if (!int.TryParse(TxtHeight.Text, out var h)) h = 90;
        var pct = (int)SldOpacity.Value;
        var fs = (int)SldFontSize.Value;
        var color = TxtActiveColor.Text;
        if (string.IsNullOrWhiteSpace(color) || !color.StartsWith("#")) color = "#2864C8";
        ThumbnailChanged?.Invoke(this, new ThumbnailArgs(w, h, pct, fs, color));
    }

    private void OnTopmostChanged(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        TopmostChanged?.Invoke(this, ChkTopmost.IsChecked == true);
    }

    private void OnGeneralChanged(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        if (sender == ChkTopmostOnlyFocused)
            TopmostOnlyWhenClientFocusedChanged?.Invoke(this, ChkTopmostOnlyFocused.IsChecked == true);
        else if (sender == ChkMinimizeToTray)
            MinimizeToTrayChanged?.Invoke(this, ChkMinimizeToTray.IsChecked == true);
        else if (sender == ChkTrackLocations)
            TrackLocationsChanged?.Invoke(this, ChkTrackLocations.IsChecked == true);
        else if (sender == ChkUniqueLayout)
            UniqueLayoutChanged?.Invoke(this, ChkUniqueLayout.IsChecked == true);
        else if (sender == ChkSnapToGrid)
            SnapToGridChanged?.Invoke(this, ChkSnapToGrid.IsChecked == true);
    }

    private void OnGridSizeChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_loading) return;
        GridSizeChanged?.Invoke(this, (int)SliderGridSize.Value);
    }
}
