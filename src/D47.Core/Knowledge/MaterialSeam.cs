using System.Text;

namespace D47.Core.Knowledge;

/// <summary>What to say when a name lands on the wrong one of the three material tools (#58).</summary>
public static class MaterialSeam
{
    /// <summary>The tool that answers about ship materials.</summary>
    public const string MaterialTool = "find_material";

    /// <summary>The tool that answers about Odyssey ship-locker goods.</summary>
    public const string MicroResourceTool = "find_micro_resource";

    /// <summary>The tool that answers about things a station trades.</summary>
    public const string MarketTool = "find_nearest_station";

    /// <summary>Which tool this thing actually belongs to, from the ledger it declares.</summary>
    public static string ToolFor(MaterialLedger ledger) => ledger switch
    {
        MaterialLedger.Material => MaterialTool,
        MaterialLedger.ShipLocker => MicroResourceTool,
        _ => MarketTool,
    };

    /// <summary>What this thing is, in a Commander's words rather than a ledger's.</summary>
    public static string Describe(MaterialEntry material) => material.Ledger switch
    {
        MaterialLedger.Material =>
            $"a{(material.Category is { Length: > 0 } kind ? " " + kind.ToLowerInvariant() : "n")} "
            + $"engineering material{(material.Grade is { } grade ? $", grade {grade}" : string.Empty)}",

        MaterialLedger.ShipLocker => "an Odyssey ship-locker item, carried on foot",
        MaterialLedger.Cargo => "a market commodity, measured in tonnes",
        MaterialLedger.RareCargo => "a rare commodity, bought at one station and allocation-limited",
        _ => "something with no ledger",
    };

    /// <summary>
    /// The whole answer for a name that reached the wrong tool: what it actually is, where it comes
    /// from where the table knows, and which tool answers properly.
    /// </summary>
    /// <param name="material">The thing that was named.</param>
    /// <param name="askedOf">
    /// The tool that received it, so the sentence can say what that tool covers rather than leaving the
    /// Commander to work out why they were told no.
    /// </param>
    public static string NotThisOne(MaterialEntry material, string askedOf)
    {
        var said = new StringBuilder();

        said.Append(material.Name).Append(' ').Append(Denial(askedOf)).Append(' ');
        said.Append("It is ").Append(Describe(material)).Append('.');

        // Answered rather than redirected, wherever the table can.
        if (material.Origins.Count > 0)
        {
            said.Append(" Found at: ").Append(string.Join("; ", material.Origins)).Append('.');
        }

        // Both, not one or the other.
        if (material.Buildings.Count > 0)
        {
            said.Append(" Held in: ").Append(string.Join("; ", material.Buildings)).Append('.');
        }

        var answers = ToolFor(material.Ledger);

        said.Append(" Ask ").Append(answers).Append(' ').Append(Wants(answers)).Append('.');

        return said.ToString();
    }

    /// <summary>Why the tool that was asked cannot answer, in terms of what that tool is for.</summary>
    private static string Denial(string askedOf) => askedOf switch
    {
        MarketTool => "is not a commodity — no station trades it.",
        MaterialTool => "is not a ship material, so no engineering search applies to it.",
        MicroResourceTool => "is not an on-foot material, so it is not in any ship locker.",
        _ => "is not what that search covers.",
    };

    /// <summary>What the tool that does answer will tell them, so the offer is concrete.</summary>
    private static string Wants(string tool) => tool switch
    {
        MaterialTool => "for where to get it and what a trader could turn into it",
        MicroResourceTool => "for which settlements and containers hold it",
        MarketTool => "for where to buy it",
        _ => "instead",
    };
}
