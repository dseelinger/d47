using D47.Core.Journal;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Knowledge;

/// <summary>
/// What the Commander says is on their fleet carrier (list.md Phase 50, amended by the Commander on
/// 2026-08-25).
/// <para>
/// <b>The plan of record kept the carrier out of the arithmetic entirely</b>, and the measurement
/// behind that stands: reconciling <c>CargoTransfer</c> against <c>CarrierStats</c> came out wrong
/// 679 times against right 347 and drove eleven commodities negative. What changed is not that
/// ruling — it is that a figure the Commander <em>types</em> is a statement of fact rather than an
/// inference, and they can see the inventory screen d47 cannot.
/// </para>
/// </summary>
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

    /// <summary>
    /// Per Commander, keyed in the document. Two people on one machine do not share a carrier, and
    /// they certainly do not share each other's memory of one.
    /// </summary>
    [Fact]
    public void OneCommandersFigureIsNotAnothers()
    {
        var manifest = Manifest(out _);

        manifest.Set("F1", "Tritium", 300, When);

        Assert.Empty(manifest.For("F2"));
        Assert.Single(manifest.For("F1"));
    }

    /// <summary>
    /// <b>Zero removes it.</b> "I have none" and "I have not said" are the same instruction to a
    /// plan, and a stored nought would leave the page listing commodities the Commander has
    /// finished with.
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

    /// <summary>The point of the whole thing: what is aboard comes off the shopping list.</summary>
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
    /// A row the carrier clears entirely drops out of the shopping list, because there is nothing
    /// left to go and buy — <b>but it is still named</b>, so an answer can say why a commodity the
    /// Commander expected to see is not on the list. A sourcing plan that silently lost a row is
    /// the failure this phase's acceptance exists to prevent, and a good reason does not make it
    /// acceptable.
    /// </summary>
    [Fact]
    public void ARowTheCarrierClearsDropsOutAndIsStillNamed()
    {
        var (left, counted) = CarrierManifest.Deduct(
            [Needs("Tritium", 200)],
            [new CarrierStock("Tritium", 500, When)]);

        Assert.Empty(left);

        // Counted at what the site actually wanted. Five hundred aboard against two hundred
        // outstanding is two hundred tonnes of progress and three hundred tonnes of something else
        // to do with, and saying "500" here would be subtracting a number that is not about this
        // site.
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
