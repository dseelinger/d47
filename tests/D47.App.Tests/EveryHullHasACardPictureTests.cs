using D47.Core.Knowledge;
using Xunit;

namespace D47.App.Tests;

/// <summary>Every hull in the specifications table has a card still in <c>assets\ships</c>, or is listed as drawn without one.</summary>
[Trait("Category", "Gate")]
public class EveryHullHasACardPictureTests
{
    private static readonly string[] DrawnWithoutAPicture =
    [
        "cobramkiv", // unobtainable (#1098)
    ];

    private static string Assets
    {
        get
        {
            var at = new DirectoryInfo(AppContext.BaseDirectory);

            while (at is not null && !Directory.Exists(Path.Combine(at.FullName, "assets", "ships")))
            {
                at = at.Parent;
            }

            return at is null
                ? throw new DirectoryNotFoundException("assets/ships not found above the test binary")
                : Path.Combine(at.FullName, "assets", "ships");
        }
    }

    [Fact]
    public void EveryHullHasAPictureOrIsListedWithout()
    {
        var missing = Missing(DrawnWithoutAPicture);

        Assert.True(
            missing.Length == 0,
            "Hulls with no card picture: " + string.Join(", ", missing)
            + ". Run tools\\ship-art.ps1, or add the hull to DrawnWithoutAPicture.");
    }

    [Fact]
    public void ARemovedEntryFromTheListFailsTheCheck()
    {
        Assert.Contains("cobramkiv", Missing([]));
    }

    private static string[] Missing(IEnumerable<string> listed) =>
        [.. EliteSpecifications.Ships
            .Select(ship => ship.Symbol)
            .Where(symbol => !listed.Contains(symbol, StringComparer.OrdinalIgnoreCase)
                && !File.Exists(Path.Combine(Assets, symbol + ".png")))
            .Order(StringComparer.Ordinal)];
}
