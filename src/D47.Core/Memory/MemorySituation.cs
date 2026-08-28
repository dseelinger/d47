using D47.Core.Callouts;
using D47.Core.Journal;

namespace D47.Core.Memory;

/// <summary>
/// What was going on, reduced to the three things a memory can usefully be filed under (Phase 31
/// , "Recall arrives above the cache breakpoint" — <em>this system, this ship, this
/// activity</em>).
/// <para>
/// <b>Tags rather than fields, and the writer never chooses them.</b> A tag is a fact about the
/// moment d47 learned something, taken from the game state at that moment — so it costs nothing to
/// produce, it cannot be argued with, and it is out of reach of a model that read untrusted text.
/// A model that could tag its own entry could aim it at <see cref="MemoryRecall"/>, which is the
/// one thing in this phase that decides what reaches a prompt.
/// </para>
/// <para>
/// Prefixed — <c>system:deciat</c>, not <c>deciat</c> — because a ship called Deciat is a thing a
/// Commander would absolutely do.
/// </para>
/// </summary>
public readonly record struct MemorySituation(string? System, string? Ship, AmbientSituation Doing)
{
    /// <summary>Nothing known. What a memory written before the first journal event is filed under.</summary>
    public static readonly MemorySituation Unknown = new(null, null, AmbientSituation.None);

    public const string SystemPrefix = "system:";

    public const string ShipPrefix = "ship:";

    public const string DoingPrefix = "doing:";

    /// <summary>
    /// The situation as it stands, from the two things that know it. Lives here rather than at the
    /// wiring site so the App, the tests and the replay harness read it the same way — the shape
    /// <see cref="Capabilities.Builtin.LoreCapability.PlaceOf"/> already has.
    /// </summary>
    public static MemorySituation Of(CommanderGameState? state, GameStatus status) => new(
        state?.Location.StarSystem,

        // The symbol rather than the localised name: "Krait_MkII" is the same string in every
        // client language, and a tag is matched on rather than read out.
        state?.Ship.Type,
        AmbientLines.Situate(status));

    /// <summary>The tags an entry written now carries. Ordered, so two identical situations tag identically.</summary>
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
