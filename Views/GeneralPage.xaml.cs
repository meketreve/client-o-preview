using System;
using System.Windows;
using System.Windows.Controls;

namespace ClientOPreview.Views;

public partial class GeneralPage : System.Windows.Controls.UserControl
{
    public event EventHandler<bool>? PreviewsTopmostChanged;
    public event EventHandler<bool>? MinimizeInactiveChanged;
    public event EventHandler<bool>? ReorderInactiveChanged;
    public event EventHandler<bool>? MinimizeToTrayChanged;
    public event EventHandler<bool>? TrackLocationsChanged;
    public event EventHandler<bool>? HideActivePreviewChanged;
    public event EventHandler<bool>? HideWhenNotActiveChanged;
    public event EventHandler<bool>? UniqueLayoutChanged;

    public GeneralPage()
    {
        InitializeComponent();
    }

    public void LoadFrom(ClientOPreview.Models.General gen)
    {
        ChkMinimizeToTray.IsChecked = gen.MinimizeToTray;
        ChkTrackLocations.IsChecked = gen.TrackLocations;
        ChkHideActivePreview.IsChecked = gen.HideActivePreview;
        ChkMinimizeInactive.IsChecked = gen.MinimizeInactive;
        ChkReorderInactive.IsChecked = gen.ReorderInactive;
        ChkTopmost.IsChecked = gen.PreviewsTopmost;
        ChkHideWhenNotActive.IsChecked = gen.HideWhenNotActive;
        ChkUniqueLayout.IsChecked = gen.UniqueLayout;
    }

    private void OnChanged(object sender, RoutedEventArgs e)
    {
        if (sender == ChkTopmost)
            PreviewsTopmostChanged?.Invoke(this, ChkTopmost.IsChecked == true);
        else if (sender == ChkMinimizeInactive)
            MinimizeInactiveChanged?.Invoke(this, ChkMinimizeInactive.IsChecked == true);
        else if (sender == ChkReorderInactive)
            ReorderInactiveChanged?.Invoke(this, ChkReorderInactive.IsChecked == true);
        else if (sender == ChkMinimizeToTray)
            MinimizeToTrayChanged?.Invoke(this, ChkMinimizeToTray.IsChecked == true);
        else if (sender == ChkTrackLocations)
            TrackLocationsChanged?.Invoke(this, ChkTrackLocations.IsChecked == true);
        else if (sender == ChkHideActivePreview)
            HideActivePreviewChanged?.Invoke(this, ChkHideActivePreview.IsChecked == true);
        else if (sender == ChkHideWhenNotActive)
            HideWhenNotActiveChanged?.Invoke(this, ChkHideWhenNotActive.IsChecked == true);
        else if (sender == ChkUniqueLayout)
            UniqueLayoutChanged?.Invoke(this, ChkUniqueLayout.IsChecked == true);
    }
}
