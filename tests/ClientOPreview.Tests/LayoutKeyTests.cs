using ClientOPreview.Services;
using Xunit;

namespace ClientOPreview.Tests;

public class LayoutKeyTests
{
    [Fact]
    public void Sanitize_strips_characters_that_break_json_keys()
    {
        Assert.Equal("EVE  Pilot", LayoutKey.Sanitize("EVE \"/\\ Pilot\n"));
    }

    [Fact]
    public void For_includes_the_occurrence_so_two_clients_never_share_a_key()
    {
        Assert.NotEqual(LayoutKey.For("EVE", 0), LayoutKey.For("EVE", 1));
        Assert.Equal("title:1:EVE", LayoutKey.For("EVE", 1));
    }

    [Fact]
    public void For_falls_back_to_the_shared_key_when_the_window_has_no_title()
    {
        Assert.Equal(LayoutKey.Shared, LayoutKey.For("", 0));
        Assert.Equal(LayoutKey.Shared, LayoutKey.For("   ", 3));
    }

    [Fact]
    public void Legacy_key_is_the_pre_occurrence_format()
    {
        Assert.Equal("title:EVE", LayoutKey.LegacyFor("EVE"));
    }

    [Fact]
    public void Geometry_survives_a_round_trip()
    {
        var text = LayoutKey.FormatGeometry(left: -30, top: 250, width: 640.4, height: 360.6);
        Assert.Equal("640x361+-30+250", text);

        Assert.True(LayoutKey.TryParseGeometry(text, out var left, out var top, out var width, out var height));
        Assert.Equal(-30, left);
        Assert.Equal(250, top);
        Assert.Equal(640, width);
        Assert.Equal(361, height);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("garbage")]
    [InlineData("640x360")]
    [InlineData("640x360+10")]
    [InlineData("0x360+10+10")]     // a zero-wide preview would be unusable
    [InlineData("axb+10+10")]
    public void Unreadable_geometry_is_rejected_instead_of_throwing(string? stored)
    {
        Assert.False(LayoutKey.TryParseGeometry(stored, out _, out _, out _, out _));
    }
}
