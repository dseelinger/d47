using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Knowledge;

/// <summary>OutfittingItem: parsing, the type-ahead and the list it offers.</summary>
public class AnOutfittingItemIsReadAndOfferedByNameTests
{
    private static readonly string[] ExpectedUnmapped =
    [
        "Advanced Discovery Scanner",
        "Basic Discovery Scanner",
        "Cargo Hatch",
        "Intermediate Discovery Scanner",
        "MkII Business Class Passenger Cabin (size5)",
        "MkII Business Class Passenger Cabin (size6)",
        "MkII Economy Class Passenger Cabin (size5)",
        "MkII Economy Class Passenger Cabin (size6)",
    ];

    [Fact]
    public void ASizedAndRatedModuleIsRead()
    {
        Assert.True(OutfittingItem.TryParse("5A Frame Shift Drive (SCO)", out var item, out _));
        Assert.Equal("Frame Shift Drive (SCO)", item.Name);
        Assert.Equal(5, item.Class);
        Assert.Equal("A", item.Rating);
        Assert.False(item.IsShip);
        Assert.Equal("5A Frame Shift Drive (SCO)", item.Display);
    }

    [Fact]
    public void ABareModuleNameIsReadWithNoSizeOrRating()
    {
        Assert.True(OutfittingItem.TryParse("fuel scoop", out var item, out _));
        Assert.Equal("Fuel Scoop", item.Name);
        Assert.Null(item.Class);
        Assert.Null(item.Rating);
        Assert.Equal("Fuel Scoop", item.Display);
    }

    [Fact]
    public void AShipIsReadAsAShip()
    {
        Assert.True(OutfittingItem.TryParse("Python MkII", out var item, out _));
        Assert.True(item.IsShip);
        Assert.Equal("Python MkII", item.Name);
    }

    [Theory]
    [InlineData("9Z Fuel Scoop")]
    [InlineData("5Z Fuel Scoop")]
    [InlineData("9A Fuel Scoop")]
    public void AnImpossibleSizeOrRatingIsRefused(string text)
    {
        Assert.False(OutfittingItem.TryParse(text, out _, out var failure));
        Assert.Contains(text, failure, StringComparison.Ordinal);
    }

    [Fact]
    public void ANameNobodyHasHeardOfIsRefusedWithSuggestions()
    {
        Assert.False(OutfittingItem.TryParse("Frame Shift Drve", out _, out var failure));
        Assert.Contains("Frame Shift Drive", failure, StringComparison.Ordinal);
    }

    [Fact]
    public void TheTypeAheadOffersTheShortFormsAndIgnoresParentheses()
    {
        Assert.Equal("5A Frame Shift Drive (SCO)", OutfittingItem.Suggest("5a fsd sco")[0].Display);
    }

    [Fact]
    public void TheTypeAheadOffersAShipAndItsMarkTwo()
    {
        Assert.Equal(["Python", "Python MkII"], OutfittingItem.Suggest("python").Select(item => item.Display));
    }

    [Fact]
    public void NothingTypedOffersNothing() => Assert.Empty(OutfittingItem.Suggest("  "));

    [Fact]
    public void AllHoldsTheSizedModuleAndNoFreeRow()
    {
        var displays = OutfittingItem.All.Select(item => item.Display).ToList();

        Assert.Contains("5A Frame Shift Drive (SCO)", displays);
        Assert.Contains("Python MkII", displays);
        Assert.DoesNotContain(displays, display => display.Contains("(free)", StringComparison.Ordinal));
        Assert.Equal(displays.Count, displays.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void EveryCatalogueModuleAppearsAtLeastBare()
    {
        var bare = OutfittingItem.All.Where(item => item is { IsShip: false, Class: null, Rating: null })
            .Select(item => item.Name);

        Assert.Empty(OutfittingCatalogue.Modules.Except(bare));
    }

    [Fact]
    public void EveryItemAllOffersIsReadBackAsItself()
    {
        foreach (var item in OutfittingItem.All)
        {
            Assert.True(OutfittingItem.TryParse(item.Display, out var read, out var failure), failure);
            Assert.Equal(item.Display, read.Display);
        }
    }

    [Fact]
    public void ASpecificationNameWithNoCatalogueNameIsListedHereWhenItAppears()
    {
        Assert.Equal(ExpectedUnmapped, OutfittingItem.Unmapped);
    }
}
