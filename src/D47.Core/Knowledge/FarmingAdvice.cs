using System.Text;

namespace D47.Core.Knowledge;

/// <summary>
/// The two farming tiers <c>find_material</c> answers with, ahead of the origins text: the general
/// methods that yield a material, and the one hand-picked site that reaches its trade group fastest
/// (#243).
/// </summary>
public static class FarmingAdvice
{
    /// <summary>The tiers for one material, and the system a "copy that" should offer instead, if any.</summary>
    public static (string Report, string? System) For(MaterialEntry material, double? jumpRange)
    {
        if (material.Line is null && FarmingSites.DirectSite(material.Symbol) is { } direct)
        {
            var report = new StringBuilder();
            report.AppendLine("Fastest: " + Site(direct, jumpRange));
            return (report.ToString(), direct.System);
        }

        return material.Category switch
        {
            "Raw" => Raw(material, jumpRange),
            "Manufactured" => Manufactured(material, jumpRange),
            "Encoded" => Encoded(material, jumpRange),
            _ => (string.Empty, null),
        };
    }

    private static (string Report, string? System) Raw(MaterialEntry material, double? jumpRange)
    {
        var report = new StringBuilder();
        report.AppendLine("General: crystalline shards or brain trees for grade 4 raws, then trade down.");

        if (material.Line is not { } line || FarmingSites.FastestFor(line) is not { } site)
        {
            return (report.ToString(), null);
        }

        report.AppendLine("Fastest: " + Site(site, jumpRange, DownTrade(site, material)));
        return (report.ToString(), site.System);
    }

    private static (string Report, string? System) Manufactured(MaterialEntry material, double? jumpRange)
    {
        var report = new StringBuilder();

        var top = TopOf(material.Line);

        if (top is not null && EmissionRules.Holding(top.Symbol) is not null)
        {
            report.AppendLine("General: a High Grade Emission of the right allegiance and state, then trade down.");
            return (report.ToString(), null);
        }

        var named = top?.Name ?? material.Name;

        report.AppendLine($"General: mission reward only — no High Grade Emission carries {named}.");

        if (material.Grade is { } grade && EngineeringRules.TradeRate(5, grade, sameLine: false) is { } exchange)
        {
            report.AppendLine(
                $"Fastest: no High Grade Emission carries {named}; trade {exchange.Paid} × any grade 5 "
                + $"manufactured material for {exchange.Received} × {material.Name}.");
        }

        return (report.ToString(), null);
    }

    private static (string Report, string? System) Encoded(MaterialEntry material, double? jumpRange)
    {
        var report = new StringBuilder();
        report.AppendLine("General: data points at crash sites, then trade down.");

        if (material.Line is not { } line)
        {
            return (report.ToString(), null);
        }

        if (FarmingSites.FastestFor(line) is { } direct)
        {
            report.AppendLine("Fastest: " + Site(direct, jumpRange, DownTrade(direct, material)));
            return (report.ToString(), direct.System);
        }

        // No site tops this group directly — reach it by trading across from the one encoded site that
        // does, Adaptive Encryptors Capture.
        if (FarmingSites.FastestFor("encoded-encryption-files") is not { } donorSite
            || MaterialCatalogue.Find(donorSite.MaterialSymbol) is not { } donor
            || material.Grade is not { } grade
            || EngineeringRules.TradeRate(donor.Grade ?? 5, grade, sameLine: false) is not { } exchange)
        {
            return (report.ToString(), null);
        }

        report.AppendLine(
            $"Fastest: no site tops it directly; farm {donor.Name} at {donorSite.System} {donorSite.Body} "
            + $"({donorSite.Method}, {donorSite.Coordinates}"
            + (donorSite.RespawnsOnRelog ? ", a relog respawns it" : string.Empty)
            + $") and trade {exchange.Paid} × {donor.Name} for {exchange.Received} × {material.Name}.");

        return (report.ToString(), donorSite.System);
    }

    /// <summary>The topmost-grade material in a trade group, or null for a group nothing is written for.</summary>
    private static MaterialEntry? TopOf(string? line) =>
        line is null
            ? null
            : MaterialCatalogue.All
                .Where(entry => string.Equals(entry.Line, line, StringComparison.Ordinal))
                .OrderByDescending(entry => entry.Grade)
                .FirstOrDefault();

    /// <summary>What trading the site's own material down to the one asked about costs, or empty for none.</summary>
    private static string DownTrade(FarmingSite site, MaterialEntry material)
    {
        if (MaterialCatalogue.Find(site.MaterialSymbol) is not { } top
            || string.Equals(top.Symbol, material.Symbol, StringComparison.Ordinal)
            || top.Grade is not { } topGrade
            || material.Grade is not { } grade
            || EngineeringRules.TradeRate(topGrade, grade, sameLine: true) is not { } exchange)
        {
            return string.Empty;
        }

        return $"Farm {top.Name} there and trade {exchange.Paid} for {exchange.Received} into {material.Name}.";
    }

    private static string Site(FarmingSite site, double? jumpRange, string trade = "")
    {
        var sentence = new StringBuilder($"{site.System} {site.Body}, {site.Method}, {site.Coordinates}");

        sentence.Append(site.RespawnsOnRelog
            ? " — a relog respawns it."
            : " — does not reliably respawn on a relog; move on to the next cluster or tree.");

        if (trade.Length > 0)
        {
            sentence.Append(' ').Append(trade);
        }

        if (site.JumpRangeWarning)
        {
            sentence.Append(' ').Append(FarmingSites.JumpRangeAdvice(jumpRange));
        }

        return sentence.ToString();
    }
}
