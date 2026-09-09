using D47.Core.Journal;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Knowledge;

/// <summary>What the Commander says is on their fleet carrier.</summary>
public class TheCarrierIsToldNotDerivedTests
{
    private static CarrierManifest Manifest(out string root)
    {
        root = Path.Combine(Path.GetTempPath(), "d47-carrier-manifest", Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(root);

        return new CarrierManifest(
            Path.Combine(root, "carrier.json"), NullLogger<CarrierManifest>.Instance);
    }

    private static ConstructionResource Needs(string name, int required, int provided = 0) =>
        new(name, required, provided) { Symbol = JournalJson.Symbol(name) };

    private static readonly DateTimeOffset When = new(2026, 8, 25, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void AFigureSurvivesBeingWrittenAndReadBack()
    {
        var manifest = Manifest(out var root);

        manifest.Set("F1", "Tritium", 300, When);

        var reopened = new CarrierManifest(
            Path.Combine(root, "carrier.json"), NullLogger<CarrierManifest>.Instance);

        var stock = Assert.Single(reopened.For("F1"));

        Assert.Equal("Tritium", stock.Commodity);
        Assert.Equal(300, stock.Tonnes);
        Assert.Equal(When, stock.SaidAt);
    }

    /// <summary>Per Commander, keyed in the document.</summary>
    [Fact]
    public void OneCommandersFigureIsNotAnothers()
    {
        var manifest = Manifest(out _);

        manifest.Set("F1", "Tritium", 300, When);

        Assert.Empty(manifest.For("F2"));
        Assert.Single(manifest.For("F1"));
    }

    /// <summary>
    /// Zero removes it. "I have none" and "I have not said" are the same instruction to a plan, and a
    /// stored nought would leave the page listing commodities the Commander has finished with.
    /// </summary>
    [Fact]
    public void ZeroForgetsRatherThanStoringANought()
    {
        var manifest = Manifest(out _);

        manifest.Set("F1", "Tritium", 300, When);
        manifest.Set("F1", "Tritium", 0, When);

        Assert.Empty(manifest.For("F1"));
    }

    [Fact]
    public void ClearingForgetsEverythingThisCommanderSaid()
    {
        var manifest = Manifest(out _);

        manifest.Set("F1", "Tritium", 300, When);
        manifest.Set("F1", "Steel", 40, When);
        manifest.Clear("F1");

        Assert.Empty(manifest.For("F1"));
    }

    // ---- The subtraction ---------------------------------------------------------------------

    [Fact]
    public void WhatIsAboardComesOffTheShoppingList()
    {
        var (left, counted) = CarrierManifest.Deduct(
            [Needs("Tritium", 500), Needs("Steel", 200)],
            [new CarrierStock("Tritium", 300, When)]);

        Assert.Equal(2, left.Count);
        Assert.Equal(200, left.Single(row => row.Name == "Tritium").Remaining);
        Assert.Equal(200, left.Single(row => row.Name == "Steel").Remaining);

        var used = Assert.Single(counted);

        Assert.Equal("Tritium", used.Commodity);
        Assert.Equal(300, used.Tonnes);
    }

    /// <summary>
    /// A row the carrier clears entirely drops out of the shopping list, because there is nothing left
    /// to go and buy — but it is still named, so an answer can say why a commodity the Commander
    /// expected to see is not on the list.
    /// </summary>
    [Fact]
    public void ARowTheCarrierClearsDropsOutAndIsStillNamed()
    {
        var (left, counted) = CarrierManifest.Deduct(
            [Needs("Tritium", 200)],
            [new CarrierStock("Tritium", 500, When)]);

        Assert.Empty(left);

        // Counted at what the site actually wanted.
        var used = Assert.Single(counted);

        Assert.Equal(200, used.Tonnes);
    }

    /// <summary>A commodity the site does not want is not counted against it.</summary>
    [Fact]
    public void SomethingTheSiteDoesNotWantIsNotCounted()
    {
        var (left, counted) = CarrierManifest.Deduct(
            [Needs("Steel", 200)],
            [new CarrierStock("Tritium", 300, When)]);

        Assert.Single(left);
        Assert.Empty(counted);
    }

    /// <summary>Nothing said, nothing changed — and the same list handed straight back.</summary>
    [Fact]
    public void WithNothingSaidTheListIsUntouched()
    {
        IReadOnlyList<ConstructionResource> outstanding = [Needs("Steel", 200)];

        var (left, counted) = CarrierManifest.Deduct(outstanding, []);

        Assert.Same(outstanding, left);
        Assert.Empty(counted);
    }
}
