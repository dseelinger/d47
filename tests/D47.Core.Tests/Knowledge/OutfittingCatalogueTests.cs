using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Knowledge;

/// <summary>
/// Matching what a Commander said against the real catalogue. The names arrive through a
/// transcriber, so both a fragment and a misspelling have to land somewhere useful.
/// </summary>
public class OutfittingCatalogueTests
{
    [Theory]
    [InlineData("Frame Shift Drive", "Frame Shift Drive")]
    [InlineData("frame shift drive", "Frame Shift Drive")]
    [InlineData("FRAME SHIFT DRIVE", "Frame Shift Drive")]
    [InlineData("multi cannon", "Multi-Cannon")]
    [InlineData("frame shift drive sco", "Frame Shift Drive (SCO)")]
    public void ANameIsMatchedThroughThePunctuationNobodySaysOutLoud(string spoken, string expected) =>
        Assert.Equal(expected, OutfittingCatalogue.MatchModule(spoken));

    [Fact]
    public void AnExactMatchIsNotCapturedByALongerNameContainingIt()
    {
        // "Cargo Rack" must not resolve to "Mk II Cargo Rack".
        Assert.Equal("Cargo Rack", OutfittingCatalogue.MatchModule("Cargo Rack"));
        Assert.Equal("Mk II Cargo Rack", OutfittingCatalogue.MatchModule("Mk II Cargo Rack"));
    }

    [Fact]
    public void SomethingThatIsNotAModuleMatchesNothing() =>
        Assert.Null(OutfittingCatalogue.MatchModule("Infinite Improbability Drive"));

    [Fact]
    public void AFragmentOfARealNameIsSuggested()
    {
        // What a Commander actually says.
        var near = OutfittingCatalogue.Near(OutfittingCatalogue.Modules, "shield generator");

        Assert.Contains("Bi-Weave Shield Generator", near);
    }

    [Fact]
    public void AMisspellingIsSuggestedToo()
    {
        // What the transcriber does to them.
        var near = OutfittingCatalogue.Near(OutfittingCatalogue.Modules, "Frame Shift Drve");

        Assert.Contains("Frame Shift Drive", near);
    }

    [Fact]
    public void FragmentsRankAboveMisspellings()
    {
        // A Commander who said a real thing shortly should not be shown a list headed by a typo
        // of something else.
        var near = OutfittingCatalogue.Near(OutfittingCatalogue.Modules, "Cargo Rack");

        Assert.Contains("Cargo Rack", near[0], StringComparison.Ordinal);
    }

    [Fact]
    public void NothingCloseSuggestsNothingRatherThanReachingForAnything()
    {
        Assert.Empty(OutfittingCatalogue.Near(OutfittingCatalogue.Modules, "banana"));
    }

    [Fact]
    public void ShipsMatchTheSameWay()
    {
        Assert.Equal("Krait MkII", OutfittingCatalogue.MatchShip("krait mk2".Replace("2", "II", StringComparison.Ordinal)));
        Assert.Equal("Type-9 Heavy", OutfittingCatalogue.MatchShip("type 9 heavy"));
        Assert.Null(OutfittingCatalogue.MatchShip("Millennium Falcon"));
    }

    [Fact]
    public void TheCataloguesAreTheOnesTheServiceActuallyHas()
    {
        // Read from the service's own field_values endpoints on 2026-08-14. The counts are here
        // so that a careless edit to either list is visible rather than silent.
        Assert.Equal(48, OutfittingCatalogue.Ships.Count);
        Assert.Equal(132, OutfittingCatalogue.Modules.Count);
    }
}
