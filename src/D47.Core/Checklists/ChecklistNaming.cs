using D47.Core.Journal;
using D47.Core.Knowledge;

namespace D47.Core.Checklists;

/// <summary>
/// Whether a plan's wording and the journal's wording are talking about the same thing.
/// <para>
/// <b>This used to be the one field in a ship plan d47 could not check, and the join it wanted now
/// exists.</b> Elite writes a blueprint as a symbol — <c>Engine_Dirty</c>, <c>FSD_LongRange</c> —
/// and never localises it, measured across 20,526 engineered modules and 6,272
/// <c>EngineerCraft</c> events. The shipped table calls that recipe "Dirty Drive Tuning". Nothing
/// d47 shipped carried both spellings, so this answered <c>null</c> for cannot-say and
/// <see cref="CannotConfirm"/> told the Commander outright that nothing joined them.
/// </para>
/// <para>
/// <b>That is no longer true</b> (remediation.md 15, item 10). The symbol Elite writes is
/// coriolis-data's own key for the same blueprint, and <c>Blueprints.tsv</c> now carries it against
/// every one of its 940 recipes — so <see cref="BlueprintCatalogue.NameOf"/> turns
/// <c>PowerDistributor_PrioritySystems</c> into <b>System Focused</b>, and a roll the Commander
/// already has is read back as the name they know it by.
/// </para>
/// <para>
/// <b>Null is kept and still means cannot-say</b>, for the four blueprints the table has no recipe
/// for and for anything Frontier adds before the tables are regenerated. Guessing <c>false</c>
/// would tell a Commander their finished module is unfinished, and inventing the mapping would be
/// inventing game data with a straight face. What changed is how rarely it is reached.
/// </para>
/// <para>
/// <b>An experimental effect was always the opposite case and still is.</b> Elite localises that
/// one — "Thermal Spread" arrives as words — so a plan naming an experimental is checked exactly,
/// by the same comparison, with no null in sight.
/// </para>
/// </summary>
public static class ChecklistNaming
{
    /// <summary>
    /// Whether <paramref name="journalName"/> is the thing <paramref name="wanted"/> asked for.
    /// True when they match, null when the two spellings cannot be compared at all.
    /// </summary>
    public static bool? Confirms(string? wanted, string? journalName)
    {
        // Nothing was claimed, so nothing is unconfirmed. This is the wildcard case, and it is a
        // real answer rather than a shrug: "grade 5 and I don't care which thrusters" is met by
        // whatever is in the slot.
        if (string.IsNullOrWhiteSpace(wanted))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(journalName))
        {
            return null;
        }

        var asked = ChecklistKeys.Compact(wanted);

        // The join, first. Everything below it is what this did before there was one, and is kept
        // because a symbol the table does not carry still has to be compared somehow.
        if (BlueprintCatalogue.NameOf(journalName) is { Length: > 0 } named
            && asked == ChecklistKeys.Compact(named))
        {
            return true;
        }

        return asked == ChecklistKeys.Compact(journalName) || asked == ChecklistKeys.Compact(Readable(journalName))
            ? true
            : null;
    }

    /// <summary>
    /// What the Commander calls the blueprint Elite named, falling back to Frontier's spelling with
    /// the decoration taken out — <c>Engine_Dirty</c> becoming "Engine Dirty".
    /// <para>
    /// <b>The fallback used to be the whole method</b>, and its own comment called it ugly and
    /// true. It is now the last resort rather than the answer: the table is asked first, and it
    /// knows 940 of the recipes Elite writes.
    /// </para>
    /// </summary>
    public static string Readable(string? symbol) =>
        BlueprintCatalogue.NameOf(symbol) ?? ModuleNames.ReadableOrNull(symbol) ?? string.Empty;

    /// <summary>
    /// The sentence for an item whose name could not be confirmed. Said <b>once</b> per item —
    /// an unknown repeated every time is how a Commander learns to stop listening.
    /// <para>
    /// It no longer claims nothing joins the two spellings, because that stopped being true. What
    /// is left is the honest remainder: this particular blueprint is not one the table carries.
    /// </para>
    /// </summary>
    public static string CannotConfirm(string wanted, string journalName) =>
        $"I cannot confirm that is \"{wanted}\": Elite writes the blueprint as {Readable(journalName)} "
        + "and I have no recipe under that name to check it against.";
}
