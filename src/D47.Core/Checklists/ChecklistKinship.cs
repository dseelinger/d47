namespace D47.Core.Checklists;

/// <summary>
/// Whether two checklist lines are about the same module (GitHub issue 31).
/// <para>
/// <b>One answer to "is this that module's effect?", in one place.</b> An experimental effect and
/// the blueprint it belongs to are two lines about one slot, and three different mechanisms need to
/// know they are kin: the engineer band decides whether the pair is <em>shown</em>
/// (<see cref="EngineersHere"/>), the ordering decides where the pair <em>sits</em>
/// (<see cref="ChecklistOrdering"/>), and both were about to derive it separately. Two mechanisms
/// answering this differently is the thing to avoid.
/// </para>
/// <para>
/// The pair is ship and slot — <see cref="ChecklistScope"/> and <see cref="ChecklistIntent.Subject"/>
/// — which is what "that module" means and the same pair the line's own wording resolves through.
/// </para>
/// </summary>
public static class ChecklistKinship
{
    /// <summary>
    /// Whether both lines are about one module. False where either names no slot: a line with no
    /// subject is not kin to everything else that also has none.
    /// </summary>
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

    /// <summary>
    /// The blueprint line this experimental effect belongs to, or null.
    /// <para>
    /// <b>An experimental effect does not exist without an upgrade</b>, which is why the pairing is
    /// worth keeping: an effect on its own is a real and ordinary line — the upgrade is already
    /// rolled — but an effect that has drifted away from an upgrade still on the list reads as a
    /// job that could be done next when it cannot.
    /// </para>
    /// </summary>
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
