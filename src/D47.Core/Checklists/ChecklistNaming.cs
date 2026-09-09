using D47.Core.Journal;
using D47.Core.Knowledge;

namespace D47.Core.Checklists;

/// <summary>Whether a plan's wording and the journal's wording are talking about the same thing.</summary>
public static class ChecklistNaming
{
    /// <summary>Whether <paramref name="journalName"/> is the thing <paramref name="wanted"/> asked for.</summary>
    public static bool? Confirms(string? wanted, string? journalName)
    {
        // Nothing was claimed, so nothing is unconfirmed.
        if (string.IsNullOrWhiteSpace(wanted))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(journalName))
        {
            return null;
        }

        var asked = ChecklistKeys.Compact(wanted);

        // The join, first.
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
    /// What the Commander calls the blueprint Elite named, falling back to Frontier's spelling with the
    /// decoration taken out — <c>Engine_Dirty</c> becoming "Engine Dirty".
    /// </summary>
    public static string Readable(string? symbol) =>
        BlueprintCatalogue.NameOf(symbol) ?? ModuleNames.ReadableOrNull(symbol) ?? string.Empty;

    /// <summary>The sentence for an item whose name could not be confirmed.</summary>
    public static string CannotConfirm(string wanted, string journalName) =>
        $"I cannot confirm that is \"{wanted}\": Elite writes the blueprint as {Readable(journalName)} "
        + "and I have no recipe under that name to check it against.";
}
