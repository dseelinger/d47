using D47.Core.Journal;
using D47.Core.Knowledge;

namespace D47.Core.Ships;

/// <summary>Whether the engineering a slot plan asks for is already on the fitted module.</summary>
public static class AppliedEngineering
{
    /// <summary>Whether the planned blueprint is applied at or above the planned grade; true where the plan names none.</summary>
    public static bool Blueprint(SlotPlan plan, ShipModule? fitted)
    {
        if (plan.Blueprint is not { Length: > 0 } wanted)
        {
            return true;
        }

        if (fitted?.Blueprint is not { Length: > 0 } symbol
            || !(Same(wanted, symbol) || Same(wanted, BlueprintCatalogue.NameOf(symbol))))
        {
            return false;
        }

        return plan.Grade <= 0 || (fitted.BlueprintLevel ?? 0) >= plan.Grade;
    }

    /// <summary>
    /// Whether the planned experimental is applied; true where the plan names none. Matched on the
    /// journal's symbol, and on the localised name only where the symbol is missing or unknown.
    /// </summary>
    public static bool Experimental(SlotPlan plan, ShipModule? fitted)
    {
        if (plan.Experimental is not { Length: > 0 } wanted)
        {
            return true;
        }

        if (fitted is null)
        {
            return false;
        }

        if (BlueprintCatalogue.NameOf(fitted.ExperimentalSymbol) is { Length: > 0 } named)
        {
            return Same(wanted, named);
        }

        return Same(wanted, fitted.Experimental) || Same(wanted, BlueprintCatalogue.NameOf(fitted.Experimental));
    }

    private static bool Same(string wanted, string? applied) =>
        string.Equals(wanted, applied, StringComparison.OrdinalIgnoreCase);
}
