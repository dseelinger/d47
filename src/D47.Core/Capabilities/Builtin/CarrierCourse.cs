using D47.Core.Conversation;
using D47.Core.Journal;

namespace D47.Core.Capabilities.Builtin;

/// <summary>
/// "Set course for my carrier" (docs/plans/change-requests.md item 31).
/// <para>
/// <b>Reported as an instruction answered with a report.</b> <i>"Set course for my carrier"</i>
/// came back with <i>"JOHN DEPARAGON is in Scorpii Sector BB-O a6-2. Currently in normal space."</i>
/// — because <c>my carrier</c> is a keyword on <see cref="JournalCapability"/> as well as a whole
/// phrase, keywords match anywhere in the input, and the router answered with that capability's
/// first argument-free tool before the model was ever consulted.
/// </para>
/// <para>
/// <b>The keywords stay.</b> Narrowing them was ruled against when remediation 16 fixed the same
/// hijack for <i>"where is my fleet carrier"</i> — they are what makes a capability reachable with
/// no model at all, and cutting them trades one wrong answer for a set of silences. The fix is
/// that <b>an instruction out-matches a topic</b>, which the router already supports: a
/// <see cref="DynamicCommand"/> is matched first, against the whole utterance, and carries the
/// arguments it means.
/// </para>
/// <para>
/// <b>Dynamic because the destination is not knowable when a descriptor is registered</b> — the
/// same reason the Commander's macros and the clipboard offer are, and the comment beside the
/// router already says so. It also costs nothing: commands are deliberately not part of a tool's
/// schema, so this cannot move a byte of the cached prefix.
/// </para>
/// <para>
/// <b>Offered only when there is somewhere to go.</b> With no carrier, or one whose system d47 has
/// not seen, no phrase exists and the sentence falls through to the model — which will say it does
/// not know, rather than plotting a course to nowhere.
/// </para>
/// </summary>
public static class CarrierCourse
{
    /// <summary>
    /// The ways a Commander says it. Whole utterances, because that is how a dynamic command is
    /// matched — so none of these can swallow a longer sentence the way a keyword can.
    /// </summary>
    public static readonly string[] Spellings =
    [
        "set course for my carrier",
        "set course to my carrier",
        "set a course for my carrier",
        "set course for my fleet carrier",
        "set course to my fleet carrier",
        "plot a course to my carrier",
        "plot a course to my fleet carrier",
        "plot me to my carrier",
        "plot a route to my carrier",
        "route to my carrier",
        "take me to my carrier",
        "take me to my fleet carrier",
        "take us to my carrier",
        "navigate to my carrier",
    ];

    /// <summary>
    /// The commands, given where the carrier is now. Empty when that is not known, which is the
    /// honest answer rather than a course to nowhere.
    /// </summary>
    public static IEnumerable<DynamicCommand> Phrases(Func<CarrierState?> carrier)
    {
        if (carrier() is not { StarSystem: { Length: > 0 } system })
        {
            yield break;
        }

        foreach (var phrase in Spellings)
        {
            yield return new DynamicCommand(
                phrase,
                RouteCapability.Id,
                "plot_route",
                new Dictionary<string, string>(StringComparer.Ordinal) { ["to"] = system });
        }
    }
}
