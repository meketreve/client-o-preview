using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using ClientOPreview.Models;

namespace ClientOPreview.Views;

public partial class ClientsPage : System.Windows.Controls.UserControl
{
    public event EventHandler? RefreshRequested;
    public event EventHandler? OpenStreamsRequested;

    public ClientsPage()
    {
        InitializeComponent();
    }

    public void SetWindows(IList<WindowItem> items)
    {
        ListWindows.ItemsSource = items;
    }

    public IEnumerable<WindowItem> SelectedWindows => ListWindows.SelectedItems.Cast<WindowItem>();

    private void OnRefresh(object sender, RoutedEventArgs e) => RefreshRequested?.Invoke(this, EventArgs.Empty);
    private void OnOpen(object sender, RoutedEventArgs e) => OpenStreamsRequested?.Invoke(this, EventArgs.Empty);
}
