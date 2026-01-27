using System;
using System.Windows;
using System.Windows.Controls;

namespace ClientOPreview.Views;

public record ThumbnailArgs(int Width, int Height, int OpacityPct, int TitleFontSize, string ActiveColor);

public partial class ThumbnailPage : System.Windows.Controls.UserControl
{
    public event EventHandler<ThumbnailArgs>? ThumbnailChanged;
    public event EventHandler<bool>? TopmostChanged;

    public ThumbnailPage(int width, int height, int opacityPct, int titleFontSize, string activeColor, bool topmost = true)
    {
        InitializeComponent();
        TxtWidth.Text = width.ToString();
        TxtHeight.Text = height.ToString();
        SldOpacity.Value = opacityPct;
        SldFontSize.Value = titleFontSize;
        TxtActiveColor.Text = activeColor;
        ChkTopmost.IsChecked = topmost;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        UpdateLabels();
        SldOpacity.ValueChanged += OnChanged;
        SldFontSize.ValueChanged += OnChanged;
    }

    private void UpdateLabels()
    {
        if (LblOpacity != null) LblOpacity.Text = $"{(int)SldOpacity.Value}%";
        if (LblFontSize != null) LblFontSize.Text = $"{(int)SldFontSize.Value}px";
    }

    private void OnChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
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
        TopmostChanged?.Invoke(this, ChkTopmost.IsChecked == true);
    }
}
