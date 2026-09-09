namespace D47.Core.Checklists;

/// <summary>Whether two checklist lines are about the same module (GitHub issue 31).</summary>
public static class ChecklistKinship
{
    /// <summary>Whether both lines are about one module.</summary>
    public static bool SameModule(ChecklistItem? one, ChecklistItem? other)
    {
        if (one is null || other is null)
        {
            return false;
        }

        if (one.Intent?.Subject is not { Length: > 0 } here
            || other.Intent?.Subject is not { Length: > 0 } there)
        {
            return false;
        }

        return one.Scope.Same(other.Scope) && string.Equals(here, there, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The blueprint line this experimental effect belongs to, or null.</summary>
    public static ChecklistItem? UpgradeFor(ChecklistItem effect, IEnumerable<ChecklistItem> among)
    {
        ArgumentNullException.ThrowIfNull(effect);
        ArgumentNullException.ThrowIfNull(among);

        return effect.Intent?.Kind != ChecklistIntentKind.Experimental
            ? null
            : among.FirstOrDefault(candidate =>
                candidate.Intent?.Kind == ChecklistIntentKind.Blueprint && SameModule(candidate, effect));
    }
}
