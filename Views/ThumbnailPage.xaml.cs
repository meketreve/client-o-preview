using System;
using System.Windows;
using System.Windows.Controls;

namespace ClientOPreview.Views;

public record ThumbnailArgs(int Width, int Height, int OpacityPct);

public partial class ThumbnailPage : UserControl
{
    public event EventHandler<ThumbnailArgs>? ThumbnailChanged;

    public ThumbnailPage(int width, int height, int opacityPct)
    {
        InitializeComponent();
        TxtWidth.Text = width.ToString();
        TxtHeight.Text = height.ToString();
        SldOpacity.Value = opacityPct;
        // Label será definido no Loaded, após materialização completa
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        UpdateLabel();
        SldOpacity.ValueChanged += OnChanged;
    }

    private void UpdateLabel()
    {
        try
        {
            var slider = SldOpacity;
            var label = LblOpacity;
            if (label != null && slider != null)
                label.Text = $"{(int)slider.Value}%";
        }
        catch { /* ignore during early init */ }
    }

    private void OnChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (LblOpacity != null)
            UpdateLabel();
    }

    private void OnApply(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(TxtWidth.Text, out var w)) w = 384;
        if (!int.TryParse(TxtHeight.Text, out var h)) h = 216;
        var pct = (int)SldOpacity.Value;
        ThumbnailChanged?.Invoke(this, new ThumbnailArgs(w, h, pct));
    }
}
