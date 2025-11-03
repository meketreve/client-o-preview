using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using ClientOPreview.Models;
using ClientOPreview.Services;
using static ClientOPreview.Native.NativeMethods;

namespace ClientOPreview;

public partial class MainWindow : Window
{
    private readonly Dictionary<IntPtr, StreamWindow> _streams = new();

    // UI pages
    private Views.GeneralPage _generalPage = null!;
    private Views.ThumbnailPage _thumbnailPage = null!;
    private Views.ZoomPage _zoomPage = null!;
    private Views.OverlayPage _overlayPage = null!;
    private Views.ClientsPage _clientsPage = null!;
    private Views.AboutPage _aboutPage = null!;

    // State (similar to Python defaults)
    private bool _minimizeInactive = false;
    private bool _reorderInactive = true;
    private bool _previewsTopmost = true;
    private int _thumbWidth = 384;
    private int _thumbHeight = 216;
    private int _opacityPct = 90; // 20..100

    public MainWindow()
    {
        InitializeComponent();

        // Instantiate pages
        _generalPage = new Views.GeneralPage();
        _thumbnailPage = new Views.ThumbnailPage(_thumbWidth, _thumbHeight, _opacityPct);
        _zoomPage = new Views.ZoomPage();
        _overlayPage = new Views.OverlayPage();
        _clientsPage = new Views.ClientsPage();
        _aboutPage = new Views.AboutPage();

        // Wire events
        _clientsPage.RefreshRequested += (_, __) => RefreshList();
        _clientsPage.OpenStreamsRequested += (_, __) => OpenSelectedStreams();

        _generalPage.PreviewsTopmostChanged += (_, v) => { _previewsTopmost = v; ApplyGlobalTopmost(); };
        _generalPage.MinimizeInactiveChanged += (_, v) => { _minimizeInactive = v; };
        _generalPage.ReorderInactiveChanged += (_, v) => { _reorderInactive = v; };

        _thumbnailPage.ThumbnailChanged += (_, args) =>
        {
            _thumbWidth = args.Width;
            _thumbHeight = args.Height;
            _opacityPct = args.OpacityPct;
            ApplyThumbnailToStreams();
        };

        // Default page
        ContentHost.Content = _clientsPage;
        Loaded += (_, __) => RefreshList();
    }

    // Navigation handlers
    private void Nav_General(object sender, RoutedEventArgs e) => ContentHost.Content = _generalPage;
    private void Nav_Thumbnail(object sender, RoutedEventArgs e) => ContentHost.Content = _thumbnailPage;
    private void Nav_Zoom(object sender, RoutedEventArgs e) => ContentHost.Content = _zoomPage;
    private void Nav_Overlay(object sender, RoutedEventArgs e) => ContentHost.Content = _overlayPage;
    private void Nav_Clients(object sender, RoutedEventArgs e) => ContentHost.Content = _clientsPage;
    private void Nav_About(object sender, RoutedEventArgs e) => ContentHost.Content = _aboutPage;

    private void RefreshList()
    {
        var selfHwnd = new WindowInteropHelper(this).Handle;
        var items = WindowEnumerator.GetTopLevelWindows(selfHwnd);
        _clientsPage.SetWindows(items);
    }

    private void OpenSelectedStreams()
    {
        var selected = _clientsPage.SelectedWindows.ToList();
        foreach (var item in selected)
        {
            if (_streams.ContainsKey(item.HWnd))
            {
                _streams[item.HWnd].Activate();
                continue;
            }
            var win = new StreamWindow(this, item)
            {
                Topmost = _previewsTopmost
            };
            win.SetOpacity(_opacityPct / 100.0);
            win.SetSize(_thumbWidth, _thumbHeight);
            win.Closed += (_, __) => _streams.Remove(item.HWnd);
            _streams[item.HWnd] = win;
            win.Show();
        }
    }

    private void ApplyGlobalTopmost()
    {
        foreach (var w in _streams.Values)
        {
            w.Topmost = _previewsTopmost;
        }
    }

    private void ApplyThumbnailToStreams()
    {
        var alpha = Math.Max(0.2, Math.Min(1.0, _opacityPct / 100.0));
        foreach (var w in _streams.Values)
        {
            w.SetOpacity(alpha);
            w.SetSize(_thumbWidth, _thumbHeight);
        }
    }

    internal void OnPreviewClicked(IntPtr hwnd)
    {
        if (IsIconic(hwnd)) ShowWindow(hwnd, SW_RESTORE);
        SetForegroundWindow(hwnd);

        if (_minimizeInactive)
        {
            foreach (var kv in _streams)
            {
                if (kv.Key == hwnd) continue;
                ShowWindow(kv.Key, SW_MINIMIZE);
            }
        }
        else if (_reorderInactive)
        {
            // Bring active to top; send others to back
            SetWindowPos(hwnd, HWND_TOP, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE);
            foreach (var kv in _streams)
            {
                if (kv.Key == hwnd) continue;
                SetWindowPos(kv.Key, HWND_BOTTOM, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
            }
        }
    }
}
