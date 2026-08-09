using System.Reflection;
using ClientOPreview.Localization;
using Xunit;

namespace ClientOPreview.Tests;

/// <summary>
/// Guards the acceptance criterion "no text left in the wrong language": Loc.Get falls back to
/// the key name when a translation is missing, so a key added to one table only would ship
/// silently and only show up in-game.
/// </summary>
public class LocTests
{
    private static Dictionary<string, string> Table(string field)
        => (Dictionary<string, string>)typeof(Loc)
            .GetField(field, BindingFlags.NonPublic | BindingFlags.Static)!
            .GetValue(null)!;

    private static Dictionary<string, string> English => Table("En");
    private static Dictionary<string, string> Portuguese => Table("Pt");

    [Fact]
    public void Both_tables_define_exactly_the_same_keys()
    {
        Assert.Empty(English.Keys.Except(Portuguese.Keys));
        Assert.Empty(Portuguese.Keys.Except(English.Keys));
    }

    [Fact]
    public void No_translation_is_left_blank()
    {
        Assert.All(English, kv => Assert.False(string.IsNullOrWhiteSpace(kv.Value), $"en '{kv.Key}' is blank"));
        Assert.All(Portuguese, kv => Assert.False(string.IsNullOrWhiteSpace(kv.Value), $"pt-BR '{kv.Key}' is blank"));
    }

    [Fact]
    public void Keys_have_no_dots_because_the_binding_indexer_chokes_on_them()
    {
        Assert.All(English.Keys, k => Assert.False(k.Contains('.'), $"'{k}' has a dot"));
    }

    [Fact]
    public void Placeholders_match_between_the_two_languages()
    {
        foreach (var (key, en) in English)
        {
            var expected = System.Text.RegularExpressions.Regex.Matches(en, @"\{\d+\}").Count;
            var actual = System.Text.RegularExpressions.Regex.Matches(Portuguese[key], @"\{\d+\}").Count;
            Assert.True(expected == actual, $"'{key}' has {expected} placeholder(s) in en and {actual} in pt-BR");
        }
    }

    [Theory]
    [InlineData(null, Loc.English)]
    [InlineData("", Loc.English)]
    [InlineData("en", Loc.English)]
    [InlineData("en-US", Loc.English)]
    [InlineData("fr", Loc.English)]
    [InlineData("pt", Loc.PortugueseBr)]
    [InlineData("pt-BR", Loc.PortugueseBr)]
    [InlineData("PT-br", Loc.PortugueseBr)]
    public void Normalize_collapses_a_culture_code_onto_a_supported_language(string? code, string expected)
    {
        Assert.Equal(expected, Loc.Normalize(code));
    }

    [Fact]
    public void An_unknown_key_returns_the_key_itself_instead_of_throwing()
    {
        Assert.Equal("NotAKey", Loc.Get("NotAKey"));
    }

    [Fact]
    public void Switching_language_switches_the_text()
    {
        try
        {
            Loc.SetLanguage(Loc.English);
            var english = Loc.Get("NavRegion");

            Loc.SetLanguage(Loc.PortugueseBr);
            Assert.NotEqual(english, Loc.Get("NavRegion"));
        }
        finally
        {
            Loc.SetLanguage(Loc.English);
        }
    }
}
