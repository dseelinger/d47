using D47.Core.Interface;
using Xunit;

namespace D47.Core.Tests.Interface;

/// <summary>The renamed theme keeps its id, so a saved setting still loads (#408).</summary>
public sealed class TheHudPaletteThemeIsCalledMyHudColoursTests
{
    [Fact]
    public void TheElitePaletteThemesNameIsMyHudColours()
    {
        Assert.Equal("My HUD colours", ThemeCatalog.Selected(ThemeCatalog.ElitePaletteId).Name);
    }

    [Fact]
    public void TheThemeIdIsUnchanged()
    {
        Assert.Equal("elite-palette", ThemeCatalog.ElitePaletteId);
    }
}
