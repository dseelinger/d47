using D47.Core.Callouts;
using D47.Core.Journal;

namespace D47.Core.Memory;

/// <summary>
/// What was going on, reduced to the three things a memory can usefully be filed under (Phase 31 ,
/// "Recall arrives above the cache breakpoint" — this system, this ship, this activity).
/// </summary>
public readonly record struct MemorySituation(string? System, string? Ship, AmbientSituation Doing)
{
    /// <summary>Nothing known.</summary>
    public static readonly MemorySituation Unknown = new(null, null, AmbientSituation.None);

    public const string SystemPrefix = "system:";

    public const string ShipPrefix = "ship:";

    public const string DoingPrefix = "doing:";

    /// <summary>The situation as it stands, from the two things that know it.</summary>
    public static MemorySituation Of(CommanderGameState? state, GameStatus status) => new(
        state?.Location.StarSystem,

        // The symbol rather than the localised name: "Krait_MkII" is the same string in every client
        // language, and a tag is matched on rather than read out.
        state?.Ship.Type,
        AmbientLines.Situate(status));

    /// <summary>The tags an entry written now carries.</summary>
    public IReadOnlyList<string> Tags()
    {
        var tags = new List<string>(3);

        if (!string.IsNullOrWhiteSpace(System))
        {
            tags.Add(SystemPrefix + System.Trim().ToLowerInvariant());
        }

        if (!string.IsNullOrWhiteSpace(Ship))
        {
            tags.Add(ShipPrefix + Ship.Trim().ToLowerInvariant());
        }

        if (Doing != AmbientSituation.None)
        {
            tags.Add(DoingPrefix + Doing.ToString().ToLowerInvariant());
        }

        return tags;
    }
}
