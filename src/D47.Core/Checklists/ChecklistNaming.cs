using D47.Core.Journal;

namespace D47.Core.Checklists;

/// <summary>
/// Whether a plan's wording and the journal's wording are talking about the same thing.
/// <para>
/// <b>The blueprint name is the one field in a ship plan d47 cannot always check, and the reason
/// is a missing join rather than a bug.</b> Elite writes a blueprint as a symbol —
/// <c>Engine_Dirty</c>, <c>FSD_LongRange</c> — and never localises it; that was measured across
/// 20,526 engineered modules and 6,272 <c>EngineerCraft</c> events and is why
/// <see cref="Capabilities.Builtin.EngineeringCapability"/> already prints the symbol with its
/// underscores taken out rather than a friendly name. The shipped table calls the same recipe
/// "Dirty Drive Tuning". <b>Nothing d47 ships carries both spellings</b> — not
/// <c>Blueprints.tsv</c>, not <c>Materials.tsv</c>, not <c>Engineers.tsv</c>, not
/// <c>EliteSpecifications.tsv</c>, not <c>MaterialGrades.g.cs</c>, and not <c>EDCD/FDevIDs</c>,
/// whose 24 files stop short of blueprints. See <c>docs/spikes/blueprint-name-join.md</c> for
/// where the looking stopped and what has not been checked.
/// </para>
/// <para>
/// So this answers <c>true</c>, or <c>null</c> for cannot-say, and the caller turns a null into
/// <see cref="ChecklistState.Unverified"/> and a sentence. Inventing the mapping would be
/// inventing game data with a straight face, and guessing <c>false</c> would tell a Commander
/// their finished module is unfinished.
/// </para>
/// <para>
/// <b>An experimental effect is the opposite case and it is worth noticing why.</b> Elite
/// localises that one — "Thermal Spread" arrives as words — so a plan naming an experimental is
/// checked exactly, by the same comparison, with no null in sight.
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

        return asked == ChecklistKeys.Compact(journalName) || asked == ChecklistKeys.Compact(Readable(journalName))
            ? true
            : null;
    }

    /// <summary>
    /// Frontier's own spelling with the decoration taken out — <c>Engine_Dirty</c> becoming
    /// "Engine Dirty". Ugly and true, which is the treatment stored modules and fitted blueprints
    /// already get.
    /// </summary>
    public static string Readable(string? symbol) => ModuleNames.ReadableOrNull(symbol) ?? string.Empty;

    /// <summary>
    /// The sentence for an item whose name could not be confirmed. Said <b>once</b> per item —
    /// an unknown repeated every time is how a Commander learns to stop listening.
    /// </summary>
    public static string CannotConfirm(string wanted, string journalName) =>
        $"I cannot confirm that is \"{wanted}\": Elite writes the blueprint as {Readable(journalName)} "
        + "and my table names it differently, and nothing I ship joins the two spellings.";
}
