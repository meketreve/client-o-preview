using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
// WinForms is enabled in this project, so pin the WPF types.
using Rectangle = System.Windows.Shapes.Rectangle;
using Point = System.Windows.Point;
using Color = System.Windows.Media.Color;
using Cursor = System.Windows.Input.Cursor;
using Cursors = System.Windows.Input.Cursors;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using MouseButtonEventArgs = System.Windows.Input.MouseButtonEventArgs;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace ClientOPreview;

// Transparent click-through-free layer placed exactly over the DWM thumbnail of the
// picker. DWM composites thumbnails above the content of their host window, so the
// selection rectangle has to live in a separate top-level window.
public partial class RegionOverlayWindow : Window
{
    private enum DragMode { None, New, Move, Resize }

    private const double HandleSize = 10;
    private const double MinSize = 14;

    private readonly Dictionary<string, Rectangle> _handles = new();
    // Normalized rect is the source of truth: the overlay is resized/repositioned
    // whenever the picker moves, and pixel coordinates would drift.
    private Rect _norm = new(0.25, 0.25, 0.5, 0.5);
    private Rect _sel = new(0, 0, 0, 0);
    private DragMode _mode = DragMode.None;
    private string _activeHandle = string.Empty;
    private Point _dragStart;
    private Rect _dragOrigin;
    private bool _suppressNotify;

    /// <summary>Selection normalized to 0..1 of the preview area.</summary>
    public event EventHandler<Rect>? SelectionChanged;
    public event EventHandler<Key>? KeyPressed;

    public RegionOverlayWindow()
    {
        InitializeComponent();
        CreateHandles();
    }

    private void CreateHandles()
    {
        var kinds = new (string Key, Cursor Cursor)[]
        {
            ("nw", Cursors.SizeNWSE), ("n", Cursors.SizeNS), ("ne", Cursors.SizeNESW),
            ("w",  Cursors.SizeWE),                          ("e", Cursors.SizeWE),
            ("sw", Cursors.SizeNESW), ("s", Cursors.SizeNS), ("se", Cursors.SizeNWSE)
        };

        foreach (var (key, cursor) in kinds)
        {
            var handle = new Rectangle
            {
                Width = HandleSize,
                Height = HandleSize,
                Fill = new SolidColorBrush(Color.FromRgb(0xF5, 0xF5, 0xF5)),
                Stroke = new SolidColorBrush(Color.FromRgb(0x4F, 0xC3, 0xF7)),
                StrokeThickness = 1,
                Cursor = cursor,
                Tag = key
            };
            _handles[key] = handle;
            Root.Children.Add(handle);
        }
    }

    public void SetNormalizedSelection(Rect norm)
    {
        _norm = norm;
        ProjectNormalized();
    }

    private void ProjectNormalized()
    {
        double w = Root.ActualWidth, h = Root.ActualHeight;
        if (w <= 0 || h <= 0) return;   // laid out later; Window_SizeChanged retries

        _suppressNotify = true;
        _sel = new Rect(_norm.X * w, _norm.Y * h, _norm.Width * w, _norm.Height * h);
        ClampSelection();
        UpdateVisual();
        _suppressNotify = false;
    }

    private Rect NormalizedSelection()
    {
        double w = Root.ActualWidth, h = Root.ActualHeight;
        if (w <= 0 || h <= 0) return new Rect(0, 0, 1, 1);
        return new Rect(_sel.X / w, _sel.Y / h, _sel.Width / w, _sel.Height / h);
    }

    private void ClampSelection()
    {
        double w = Root.ActualWidth, h = Root.ActualHeight;
        if (w <= 0 || h <= 0) return;

        double sw = Math.Min(Math.Max(MinSize, _sel.Width), w);
        double sh = Math.Min(Math.Max(MinSize, _sel.Height), h);
        double sx = Math.Min(Math.Max(0, _sel.X), w - sw);
        double sy = Math.Min(Math.Max(0, _sel.Y), h - sh);
        _sel = new Rect(sx, sy, sw, sh);
    }

    private void UpdateVisual()
    {
        double w = Root.ActualWidth, h = Root.ActualHeight;
        if (w <= 0 || h <= 0) return;

        Place(SelBorder, _sel);
        Place(DimTop, new Rect(0, 0, w, Math.Max(0, _sel.Top)));
        Place(DimBottom, new Rect(0, _sel.Bottom, w, Math.Max(0, h - _sel.Bottom)));
        Place(DimLeft, new Rect(0, _sel.Top, Math.Max(0, _sel.Left), _sel.Height));
        Place(DimRight, new Rect(_sel.Right, _sel.Top, Math.Max(0, w - _sel.Right), _sel.Height));

        double half = HandleSize / 2;
        SetHandle("nw", _sel.Left - half, _sel.Top - half);
        SetHandle("n", _sel.Left + _sel.Width / 2 - half, _sel.Top - half);
        SetHandle("ne", _sel.Right - half, _sel.Top - half);
        SetHandle("w", _sel.Left - half, _sel.Top + _sel.Height / 2 - half);
        SetHandle("e", _sel.Right - half, _sel.Top + _sel.Height / 2 - half);
        SetHandle("sw", _sel.Left - half, _sel.Bottom - half);
        SetHandle("s", _sel.Left + _sel.Width / 2 - half, _sel.Bottom - half);
        SetHandle("se", _sel.Right - half, _sel.Bottom - half);

        if (_suppressNotify) return;
        _norm = NormalizedSelection();
        SelectionChanged?.Invoke(this, _norm);
    }

    private static void Place(Rectangle target, Rect r)
    {
        Canvas.SetLeft(target, r.X);
        Canvas.SetTop(target, r.Y);
        target.Width = Math.Max(0, r.Width);
        target.Height = Math.Max(0, r.Height);
    }

    private void SetHandle(string key, double x, double y)
    {
        if (!_handles.TryGetValue(key, out var handle)) return;
        Canvas.SetLeft(handle, x);
        Canvas.SetTop(handle, y);
    }

    private void Window_SizeChanged(object sender, SizeChangedEventArgs e) => ProjectNormalized();

    private void Root_MouseDown(object sender, MouseButtonEventArgs e)
    {
        var pos = e.GetPosition(Root);
        _dragStart = pos;
        _dragOrigin = _sel;

        if (e.OriginalSource is Rectangle rect && rect.Tag is string key && _handles.ContainsKey(key))
        {
            _mode = DragMode.Resize;
            _activeHandle = key;
        }
        else if (_sel.Contains(pos))
        {
            _mode = DragMode.Move;
        }
        else
        {
            _mode = DragMode.New;
            _sel = new Rect(pos.X, pos.Y, MinSize, MinSize);
        }

        Root.CaptureMouse();
        e.Handled = true;
    }

    private void Root_MouseMove(object sender, MouseEventArgs e)
    {
        if (_mode == DragMode.None || e.LeftButton != MouseButtonState.Pressed) return;
        var pos = e.GetPosition(Root);

        switch (_mode)
        {
            case DragMode.New:
                _sel = new Rect(
                    Math.Min(_dragStart.X, pos.X), Math.Min(_dragStart.Y, pos.Y),
                    Math.Abs(pos.X - _dragStart.X), Math.Abs(pos.Y - _dragStart.Y));
                break;

            case DragMode.Move:
                _sel = new Rect(
                    _dragOrigin.X + (pos.X - _dragStart.X),
                    _dragOrigin.Y + (pos.Y - _dragStart.Y),
                    _dragOrigin.Width, _dragOrigin.Height);
                break;

            case DragMode.Resize:
                _sel = ResizeFromHandle(_dragOrigin, _activeHandle, pos);
                break;
        }

        ClampSelection();
        UpdateVisual();
    }

    private static Rect ResizeFromHandle(Rect origin, string handle, Point pos)
    {
        double left = origin.Left, top = origin.Top, right = origin.Right, bottom = origin.Bottom;

        if (handle.Contains('w')) left = Math.Min(pos.X, right - MinSize);
        if (handle.Contains('e')) right = Math.Max(pos.X, left + MinSize);
        if (handle.Contains('n')) top = Math.Min(pos.Y, bottom - MinSize);
        if (handle.Contains('s')) bottom = Math.Max(pos.Y, top + MinSize);

        return new Rect(left, top, right - left, bottom - top);
    }

    private void Root_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_mode == DragMode.None) return;
        _mode = DragMode.None;
        _activeHandle = string.Empty;
        Root.ReleaseMouseCapture();
        ClampSelection();
        UpdateVisual();
    }

    private void Root_MouseLeave(object sender, MouseEventArgs e)
    {
        if (_mode != DragMode.None && e.LeftButton != MouseButtonState.Pressed)
        {
            _mode = DragMode.None;
            Root.ReleaseMouseCapture();
        }
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);
        KeyPressed?.Invoke(this, e.Key);
    }
}
