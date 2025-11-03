using System;
using System.Windows;
using System.Windows.Controls;

namespace ClientOPreview.Views;

public partial class GeneralPage : UserControl
{
    public event EventHandler<bool>? PreviewsTopmostChanged;
    public event EventHandler<bool>? MinimizeInactiveChanged;
    public event EventHandler<bool>? ReorderInactiveChanged;

    public GeneralPage()
    {
        InitializeComponent();
    }

    private void OnChanged(object sender, RoutedEventArgs e)
    {
        if (sender == ChkTopmost)
            PreviewsTopmostChanged?.Invoke(this, ChkTopmost.IsChecked == true);
        else if (sender == ChkMinimizeInactive)
            MinimizeInactiveChanged?.Invoke(this, ChkMinimizeInactive.IsChecked == true);
        else if (sender == ChkReorderInactive)
            ReorderInactiveChanged?.Invoke(this, ChkReorderInactive.IsChecked == true);
    }
}
