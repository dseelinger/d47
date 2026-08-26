using System.Text;

namespace D47.Core.Knowledge;

/// <summary>
/// What to say when a name lands on the wrong one of the three material tools
/// (<a href="https://github.com/dseelinger/d47/issues/58">#58</a>).
/// <para>
/// <b>Three tools read three tables, and a Commander does not speak in ledgers.</b>
/// <c>find_nearest_station</c> answers about market commodities, <c>find_material</c> about ship
/// materials and <c>find_micro_resource</c> about Odyssey goods — and "where do I get X" is one
/// sentence whichever of the three X happens to live in. Tools are organised by where the data
/// lives; questions are asked by what the Commander wants to know, and the gap between those two
/// is this file.
/// </para>
/// <para>
/// <b>This is the strongest of the levers against a wrong tool choice, because it does not depend
/// on the model choosing correctly at all.</b> A wrong choice costs a sentence instead of three
/// turns of the Commander steering — which is what
/// <a href="https://github.com/dseelinger/d47/issues/54">#54</a> actually cost.
/// </para>
/// <para>
/// <b>Four rules, established by the fix that shipped in v0.73.0 and kept here.</b>
/// <list type="number">
/// <item><b>Answer, do not redirect</b>, wherever the answer is already in hand. A redirect is a
/// fourth turn when the catalogue was holding the answer all along.</item>
/// <item><b>Above the availability gate</b>, because none of this reaches a network. A Commander
/// running local-only gets the right answer rather than "the search is off" plus a wrong idea
/// about what the thing is.</item>
/// <item><b>Read a declared fact, never a name list.</b> <see cref="MaterialEntry.Ledger"/>
/// decides it. A second list of names kept beside the check is a second thing to keep true.</item>
/// <item><b>Both directions, at every seam</b>, so a pair cannot drift.</item>
/// </list>
/// </para>
/// <para>
/// <b>One seam is closed by construction rather than by a check here</b>, and is recorded so
/// nobody goes looking for it: <c>find_material_trader</c> takes a <c>type</c> from
/// <c>StationQuery.TraderTypes</c>, a closed set with <c>AllowedValues</c> on the parameter, so a
/// material name cannot arrive on it at all.
/// </para>
/// </summary>
public static class MaterialSeam
{
    /// <summary>The tool that answers about ship materials.</summary>
    public const string MaterialTool = "find_material";

    /// <summary>The tool that answers about Odyssey ship-locker goods.</summary>
    public const string MicroResourceTool = "find_micro_resource";

    /// <summary>The tool that answers about things a station trades.</summary>
    public const string MarketTool = "find_nearest_station";

    /// <summary>
    /// Which tool this thing actually belongs to, from the ledger it declares.
    /// </summary>
    public static string ToolFor(MaterialLedger ledger) => ledger switch
    {
        MaterialLedger.Material => MaterialTool,
        MaterialLedger.ShipLocker => MicroResourceTool,
        _ => MarketTool,
    };

    /// <summary>
    /// What this thing is, in a Commander's words rather than a ledger's.
    /// </summary>
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
    /// The whole answer for a name that reached the wrong tool: what it actually is, where it
    /// comes from where the table knows, and which tool answers properly.
    /// </summary>
    /// <param name="material">The thing that was named.</param>
    /// <param name="askedOf">
    /// The tool that received it, so the sentence can say what that tool covers rather than
    /// leaving the Commander to work out why they were told no.
    /// </param>
    public static string NotThisOne(MaterialEntry material, string askedOf)
    {
        var said = new StringBuilder();

        said.Append(material.Name).Append(' ').Append(Denial(askedOf)).Append(' ');
        said.Append("It is ").Append(Describe(material)).Append('.');

        // Answered rather than redirected, wherever the table can. Origins are carried on the
        // entry for ship materials and the buildings and containers for Odyssey goods, and
        // reading either costs nothing.
        if (material.Origins.Count > 0)
        {
            said.Append(" Found at: ").Append(string.Join("; ", material.Origins)).Append('.');
        }

        // Both, not one or the other. Measured against the generated table: 163 of the 196
        // ship-locker entries carry buildings and every one of those carries origins too, so a
        // fallback would have been unreachable code. Additive makes it the answer
        // find_micro_resource would have given — where it comes from, and which buildings hold it.
        if (material.Buildings.Count > 0)
        {
            said.Append(" Held in: ").Append(string.Join("; ", material.Buildings)).Append('.');
        }

        var answers = ToolFor(material.Ledger);

        said.Append(" Ask ").Append(answers).Append(' ').Append(Wants(answers)).Append('.');

        return said.ToString();
    }

    /// <summary>
    /// Why the tool that was asked cannot answer, in terms of what that tool is for. Said in the
    /// tool's own terms rather than as a bare "wrong tool", because the Commander did not choose
    /// the tool and should not have to care that one was chosen.
    /// </summary>
    private static string Denial(string askedOf) => askedOf switch
    {
        MarketTool => "is not a commodity — no station trades it.",
        MaterialTool => "is not a ship material, so no engineering search applies to it.",
        MicroResourceTool => "is not an on-foot material, so it is not in any ship locker.",
        _ => "is not what that search covers.",
    };

    /// <summary>What the tool that <em>does</em> answer will tell them, so the offer is concrete.</summary>
    private static string Wants(string tool) => tool switch
    {
        MaterialTool => "for where to get it and what a trader could turn into it",
        MicroResourceTool => "for which settlements and containers hold it",
        MarketTool => "for where to buy it",
        _ => "instead",
    };
}
