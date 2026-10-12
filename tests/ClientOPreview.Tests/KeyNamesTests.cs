using ClientOPreview.Linux.Platform;
using Xunit;

namespace ClientOPreview.Tests;

/// <summary>Key names in settings.json are shared with the Windows build; Linux must read them all.</summary>
public class KeyNamesTests
{
    [Theory]
    [InlineData("Tab", 0xff09u)]
    [InlineData("NumPad1", 0xffb1u)]
    [InlineData("NumPad0", 0xffb0u)]
    [InlineData("F1", 0xffbeu)]
    [InlineData("F12", 0xffc9u)]
    [InlineData("A", (uint)'a')]
    [InlineData("z", (uint)'z')]
    [InlineData("D3", (uint)'3')]
    [InlineData("7", (uint)'7')]
    [InlineData("OemTilde", 0x60u)]
    [InlineData("PageDown", 0xff56u)]
    [InlineData("Next", 0xff56u)]
    public void Known_names_map_to_their_keysym(string name, uint keysym)
    {
        Assert.Equal(keysym, KeyNames.KeysymOf(name));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("F25")]
    [InlineData("NumPadX")]
    [InlineData("Nonsense")]
    public void Unknown_or_empty_names_map_to_zero(string? name)
    {
        Assert.Equal(0u, KeyNames.KeysymOf(name));
    }

    [Fact]
    public void Default_hotkeys_of_the_model_are_all_understood()
    {
        var hk = new ClientOPreview.Models.Hotkeys();
        Assert.NotEqual(0u, KeyNames.KeysymOf(hk.CycleKey));
        Assert.All(hk.DirectKeys, k => Assert.NotEqual(0u, KeyNames.KeysymOf(k)));
    }

    [Theory]
    [InlineData("None", 0u)]
    [InlineData("", 0u)]
    [InlineData("Ctrl", KeyNames.Control)]
    [InlineData("Alt, Ctrl", KeyNames.Alt | KeyNames.Control)]
    [InlineData("Ctrl+Shift+Win", KeyNames.Control | KeyNames.Shift | KeyNames.Super)]
    public void Modifiers_parse_like_the_windows_build(string mods, uint mask)
    {
        Assert.Equal(mask, KeyNames.ModifiersOf(mods));
    }

    [Fact]
    public void Formatted_modifiers_parse_back_to_the_same_mask()
    {
        var text = KeyNames.FormatModifiers(ctrl: true, alt: true, shift: false, win: true);
        Assert.Equal(KeyNames.Control | KeyNames.Alt | KeyNames.Super, KeyNames.ModifiersOf(text));
        Assert.Equal("None", KeyNames.FormatModifiers(false, false, false, false));
    }
}
