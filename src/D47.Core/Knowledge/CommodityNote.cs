using D47.Core.Capabilities;

namespace D47.Core.Knowledge;

/// <summary>The words for an <see cref="AcquisitionMethod"/>, spoken by <c>how_to_get</c> and drawn on the Market page.</summary>
public static class AcquisitionWords
{
    /// <summary>The method as a lower-case phrase: "mined from asteroid rings".</summary>
    public static string Of(AcquisitionMethod method) => method switch
    {
        AcquisitionMethod.Shipyard => "bought at a shipyard",
        AcquisitionMethod.Outfitting => "bought at outfitting",
        AcquisitionMethod.Market => "bought at a market",
        AcquisitionMethod.RareMarket => "sold at one station only, allocation-limited",
        AcquisitionMethod.PioneerSupplies => "bought at Pioneer Supplies",
        AcquisitionMethod.ComesWithSuit => "issued with a suit",
        AcquisitionMethod.MaterialTrader => "exchanged at a material trader",
        AcquisitionMethod.Bartender => "exchanged with the Bartender for other Components",
        AcquisitionMethod.RingMining => "mined from asteroid rings",
        AcquisitionMethod.SurfaceMining =>
            "surface mining from a Rhino SRV, at a Planetary Mining Location the Detailed Surface Scanner finds",
        AcquisitionMethod.SurfaceProspecting => "found by surface prospecting",
        AcquisitionMethod.Settlement => "found in settlements",
        AcquisitionMethod.Salvage => "salvaged",
        AcquisitionMethod.Scanning => "found by scanning",
        AcquisitionMethod.MissionReward => "given as a mission reward",
        AcquisitionMethod.GuardianSite => "found at Guardian sites",
        AcquisitionMethod.TechBroker => "unlocked at a tech broker",
        AcquisitionMethod.Engineer => "applied by an engineer",
        AcquisitionMethod.Unobtainable => "no longer obtainable",
        _ => method.ToString(),
    };
}

/// <summary>One line of the Market page's "how to get it" note.</summary>
/// <param name="Sentence">The line, with the commodity's name first when the answer asked for several.</param>
/// <param name="Hotspot">The commodity a ring-hotspot search can be run for, or null.</param>
public sealed record CommodityNoteLine(string Sentence, string? Hotspot);

/// <summary>How to obtain what a commodity answer was for, in the words <c>how_to_get</c> uses.</summary>
public static class CommodityNote
{
    /// <summary>The lines for <paramref name="posting"/>; empty when no commodity has a method to show.</summary>
    public static IReadOnlyList<CommodityNoteLine> For(CommodityPosting posting)
    {
        if (posting.Query.Side != TradeSide.Buying)
        {
            return [];
        }

        var noStation = posting.CarriersOnly || posting.Answer.Offers.Count == 0;
        var several = posting.Query.More is { Count: > 0 };
        var lines = new List<CommodityNoteLine>();

        foreach (var want in posting.Query.Wants)
        {
            if (AcquisitionGuide.For(want.Commodity) is not { Kind: AcquisitionKind.Commodity } acquisition)
            {
                continue;
            }

            var shown = acquisition.Methods
                .Where(method => method is AcquisitionMethod.RingMining or AcquisitionMethod.SurfaceMining
                    || (noStation && method is not (AcquisitionMethod.Market or AcquisitionMethod.RareMarket)))
                .ToList();

            if (shown.Count == 0)
            {
                continue;
            }

            var words = string.Join(", or ", shown.Select(AcquisitionWords.Of));

            var hotspot = shown.Contains(AcquisitionMethod.RingMining)
                          && BodyCatalogue.MatchRingSignal(acquisition.Name) is { } signal
                          && string.Equals(signal, acquisition.Name, StringComparison.OrdinalIgnoreCase)
                ? signal
                : null;

            lines.Add(new CommodityNoteLine(
                several ? $"{acquisition.Name}: {words}." : $"{SpokenText.Capitalised(words)}.",
                hotspot));
        }

        return lines;
    }
}
