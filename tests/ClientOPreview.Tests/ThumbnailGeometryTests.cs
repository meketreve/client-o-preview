using ClientOPreview.Models;
using ClientOPreview.Services;
using Xunit;
using static ClientOPreview.Native.NativeMethods;

namespace ClientOPreview.Tests;

public class ThumbnailGeometryTests
{
    private static SIZE Source(int w, int h) => new() { cx = w, cy = h };

    private static readonly Zoom NoZoom = new();

    [Fact]
    public void A_preset_covering_the_whole_window_is_not_a_crop()
    {
        Assert.True(ThumbnailGeometry.IsFullWindow(new RegionPreset { X = 0, Y = 0, W = 1, H = 1 }));
        Assert.False(ThumbnailGeometry.NeedsCrop(new RegionPreset { X = 0, Y = 0, W = 1, H = 1 }));
        Assert.False(ThumbnailGeometry.NeedsCrop(null));
        Assert.True(ThumbnailGeometry.NeedsCrop(new RegionPreset { X = 0.5, Y = 0, W = 0.5, H = 1 }));
    }

    [Fact]
    public void Crop_maps_the_normalized_preset_onto_the_source_pixels()
    {
        var preset = new RegionPreset { X = 0.5, Y = 0.25, W = 0.25, H = 0.5 };
        var rect = ThumbnailGeometry.CropSource(Source(1920, 1080), preset, applyZoom: false, NoZoom);

        Assert.Equal(960, rect.Left);
        Assert.Equal(270, rect.Top);
        Assert.Equal(1440, rect.Right);
        Assert.Equal(810, rect.Bottom);
    }

    [Fact]
    public void Crop_without_a_preset_is_the_whole_source()
    {
        var rect = ThumbnailGeometry.CropSource(Source(800, 600), null, applyZoom: false, NoZoom);

        Assert.Equal(0, rect.Left);
        Assert.Equal(0, rect.Top);
        Assert.Equal(800, rect.Right);
        Assert.Equal(600, rect.Bottom);
    }

    [Fact]
    public void Zoom_magnifies_inside_the_region_and_stays_within_it()
    {
        var preset = new RegionPreset { X = 0.5, Y = 0.0, W = 0.5, H = 1.0 };
        var zoom = new Zoom { Magnification = 2.0, OffsetX = 0.5, OffsetY = 0.5 };

        var region = ThumbnailGeometry.CropSource(Source(1000, 1000), preset, applyZoom: false, zoom);
        var zoomed = ThumbnailGeometry.CropSource(Source(1000, 1000), preset, applyZoom: true, zoom);

        Assert.Equal(250, zoomed.Right - zoomed.Left);   // half of the 500px-wide region
        Assert.Equal(500, zoomed.Bottom - zoomed.Top);
        Assert.True(zoomed.Left >= region.Left && zoomed.Right <= region.Right);
        Assert.True(zoomed.Top >= region.Top && zoomed.Bottom <= region.Bottom);
    }

    [Fact]
    public void Crop_never_leaves_the_source_even_with_a_preset_that_overflows()
    {
        var preset = new RegionPreset { X = 0.9, Y = 0.9, W = 1.0, H = 1.0 };
        var rect = ThumbnailGeometry.CropSource(Source(100, 100), preset, applyZoom: false, NoZoom);

        Assert.True(rect.Left >= 0 && rect.Top >= 0);
        Assert.True(rect.Right <= 100 && rect.Bottom <= 100);
        Assert.True(rect.Right > rect.Left && rect.Bottom > rect.Top);
    }

    [Fact]
    public void Letterbox_keeps_the_crop_proportions_and_centres_it()
    {
        var dest = new RECT { Left = 0, Top = 20, Right = 400, Bottom = 320 };   // 400x300
        var boxed = ThumbnailGeometry.Letterbox(dest, srcW: 100, srcH: 100);     // square crop

        Assert.Equal(300, boxed.Right - boxed.Left);
        Assert.Equal(300, boxed.Bottom - boxed.Top);
        Assert.Equal(50, boxed.Left);      // (400-300)/2
        Assert.Equal(20, boxed.Top);       // fills the height, no vertical bars
    }

    [Fact]
    public void Letterbox_leaves_a_degenerate_destination_alone()
    {
        var dest = new RECT { Left = 0, Top = 0, Right = 0, Bottom = 0 };
        Assert.Equal(dest, ThumbnailGeometry.Letterbox(dest, 100, 100));
    }
}
