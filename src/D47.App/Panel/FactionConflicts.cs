using D47.App.Theming;
using D47.Core.Knowledge;

namespace D47.App.Panel;

/// <summary>The Conflicts section's headings and text.</summary>
public static class FactionConflicts
{
    public const string OpponentUnknownText = "Opponent not known yet";

    public const string NoteText =
        "Days won and what is at stake are known only for systems you have visited. "
        + "The opponent is the other faction in the same state in that system.";

    public const string NoneText = "No system of this faction is in a conflict.";

    /// <summary>The Conflicts columns.</summary>
    public static IReadOnlyList<SortColumn<FactionConflict>> Heads { get; } =
    [
        SortColumn<FactionConflict>.Text("System", conflict => conflict.System),
        SortColumn<FactionConflict>.Text("Conflict", conflict => conflict.Type, conflict => conflict.Status) with { Second = "Active or pending" },
        SortColumn<FactionConflict>.Text("Opponent", conflict => conflict.Opponent),
        SortColumn<FactionConflict>.Figure<int>("Days won", conflict => conflict.Visited ? conflict.OursWon - conflict.TheirsWon : null, SortDirection.Descending) with { Second = "Ours – theirs" },
        SortColumn<FactionConflict>.Text("At stake", conflict => conflict.OurStake),
    ];

}
