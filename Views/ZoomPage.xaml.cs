using System;
using System.Windows;
using System.Windows.Controls;
using ClientOPreview.Models;

namespace ClientOPreview.Views;

public partial class ZoomPage : System.Windows.Controls.UserControl
{
    private bool _loading = false;
    private Zoom _zoom = new();

    public event EventHandler<Zoom>? ZoomChanged;

    public ZoomPage()
    {
        _loading = true;
        InitializeComponent();
        _loading = false;
    }

    public void LoadFrom(Zoom zoom)
    {
        _loading = true;
        _zoom = zoom;
        ChkResizeOnHover.IsChecked = zoom.ResizeOnHover;
        ChkInternalZoom.IsChecked = zoom.InternalZoom;
        SliderMagnification.Value = zoom.Magnification;
        SliderOffsetX.Value = zoom.OffsetX;
        SliderOffsetY.Value = zoom.OffsetY;
        _loading = false;
    }

    private void OnChanged(object sender, RoutedEventArgs e)
    {
        if (_loading) return;

        _zoom.ResizeOnHover = ChkResizeOnHover.IsChecked == true;
        _zoom.InternalZoom = ChkInternalZoom.IsChecked == true;
        _zoom.Magnification = SliderMagnification.Value;
        _zoom.OffsetX = SliderOffsetX.Value;
        _zoom.OffsetY = SliderOffsetY.Value;

        ZoomChanged?.Invoke(this, _zoom);
    }
}
