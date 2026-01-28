using System;
using System.Windows;
using System.Windows.Controls;

namespace ClientOPreview.Views;

public partial class GeneralPage : System.Windows.Controls.UserControl
{
    public event EventHandler<bool>? PreviewsTopmostChanged;
    public event EventHandler<bool>? MinimizeToTrayChanged;
    public event EventHandler<bool>? TrackLocationsChanged;
    public event EventHandler<bool>? UniqueLayoutChanged;
    public event EventHandler<bool>? SnapToGridChanged;
    public event EventHandler<int>? GridSizeChanged;

    public GeneralPage()
    {
        InitializeComponent();
    }

    public void LoadFrom(ClientOPreview.Models.General gen)
    {
        ChkMinimizeToTray.IsChecked = gen.MinimizeToTray;
        ChkTrackLocations.IsChecked = gen.TrackLocations;
        ChkTopmost.IsChecked = gen.PreviewsTopmost;
        ChkUniqueLayout.IsChecked = gen.UniqueLayout;
        ChkSnapToGrid.IsChecked = gen.SnapToGrid;
        SliderGridSize.Value = gen.GridSize;
    }

    private void OnChanged(object sender, RoutedEventArgs e)
    {
        if (sender == ChkTopmost)
            PreviewsTopmostChanged?.Invoke(this, ChkTopmost.IsChecked == true);
        else if (sender == ChkMinimizeToTray)
            MinimizeToTrayChanged?.Invoke(this, ChkMinimizeToTray.IsChecked == true);
        else if (sender == ChkTrackLocations)
            TrackLocationsChanged?.Invoke(this, ChkTrackLocations.IsChecked == true);
        else if (sender == ChkUniqueLayout)
            UniqueLayoutChanged?.Invoke(this, ChkUniqueLayout.IsChecked == true);
        else if (sender == ChkSnapToGrid)
            SnapToGridChanged?.Invoke(this, ChkSnapToGrid.IsChecked == true);
        else if (sender == SliderGridSize)
            GridSizeChanged?.Invoke(this, (int)SliderGridSize.Value);
    }
}
