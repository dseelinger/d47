using D47.Core.Audio;
using D47.Core.Configuration;
using D47.Core.Stories;

namespace D47.Core.Campaigns;

/// <summary>The council after a step, and the lines due.</summary>
public sealed record CouncilStep(CampaignCouncil? Council, IReadOnlyList<CampaignArcLine> Lines);

/// <summary>What the council reads: the faction, the seed key, its fronts and the front the Commander has chosen.</summary>
public sealed record CouncilView(string Faction, string SeedKey, IReadOnlyList<Front> Fronts, ChosenFront? Chosen, DateTimeOffset Now);

/// <summary>
/// The Internal politics arc: a Hawk and a Dove on the campaign faction's council, invented once from the seed key. They
/// appear when two or more fronts are open at once, each urging a different one. The front the Commander then chooses sides
/// with one, and the other, passed over, remembers it. Pure: the caller supplies the time and keeps the state.
/// </summary>
public static class CampaignInternalPolitics
{
    /// <summary>The link strength the councillors are heard through: a clear comms link.</summary>
    public const double Link = 1;

    /// <summary>The Hawk's and the Dove's names, drawn from <paramref name="seedKey"/>, the same on every PC that has the same key.</summary>
    public static (string Hawk, string Dove) Names(string seedKey)
    {
        ArgumentNullException.ThrowIfNull(seedKey);

        var hawk = CampaignArcs.Leader("hawk", seedKey);
        var dove = CampaignArcs.Leader("dove", seedKey);

        return (hawk, string.Equals(hawk, dove, StringComparison.Ordinal) ? CampaignArcs.Leader("dove council", seedKey) : dove);
    }

    /// <summary>Whether a front of <paramref name="kind"/> is the Hawk's kind: a conflict or a coup threat, a fight with a rival.</summary>
    public static bool Aggressive(FrontKind kind) => kind is FrontKind.Conflict or FrontKind.Coup;

    /// <summary>The fronts that are open, one per system, in rank order: every front but a stale one.</summary>
    public static IReadOnlyList<Front> Open(IReadOnlyList<Front> fronts)
    {
        ArgumentNullException.ThrowIfNull(fronts);

        return
        [
            .. fronts
                .Where(front => front.Kind != FrontKind.Stale)
                .DistinctBy(front => front.System, StringComparer.OrdinalIgnoreCase),
        ];
    }

    /// <summary>
    /// The front each councillor urges, or null when fewer than two fronts are open. The Hawk urges the first aggressive
    /// front, else the first front; the Dove the first other front in a different system, else the last front in a
    /// different system.
    /// </summary>
    public static (Front Hawk, Front Dove)? Picks(IReadOnlyList<Front> fronts)
    {
        var open = Open(fronts);

        if (open.Count < 2)
        {
            return null;
        }

        var hawk = open.FirstOrDefault(front => Aggressive(front.Kind)) ?? open[0];
        var dove = open.FirstOrDefault(front => !Aggressive(front.Kind) && !SameSystem(front, hawk))
                   ?? open.Last(front => !SameSystem(front, hawk));

        return (hawk, dove);
    }

    /// <summary>
    /// The council after one look, and the lines due. With no council, two open fronts bring the Hawk and the Dove, each
    /// urging a front. With one, a chosen front newer than the last answered one sides with the Hawk when it is aggressive and
    /// the Dove otherwise, and the other says it was passed over, counting the times and naming the last.
    /// </summary>
    public static CouncilStep Step(CampaignCouncil? council, CouncilView view)
    {
        ArgumentNullException.ThrowIfNull(view);

        if (council is null)
        {
            return Begin(view);
        }

        if (view.Chosen is not { } chosen || (council.Noted is { } noted && chosen.Since <= noted))
        {
            return new CouncilStep(council, []);
        }

        var side = Aggressive(chosen.Kind) ? CouncilSide.Hawk : CouncilSide.Dove;
        var passed = side == CouncilSide.Hawk ? CouncilSide.Dove : CouncilSide.Hawk;
        var picks = Picks(view.Fronts);
        var earlier = council.Sidings.Where(siding => siding.Sided == side).ToList();
        var count = earlier.Count + 1;
        var winnerName = NameOf(council, side);
        var loserName = NameOf(council, passed);
        var described = Describe(view.Fronts, chosen);
        var facts = new List<string>
        {
            $"The Commander has chosen to work the {described} in {chosen.System}, siding with {winnerName}, the {Role(side)} on {view.Faction}'s council.",
            $"{loserName} is the {Role(passed)} on {view.Faction}'s council and has been passed over.",
        };

        if (picks is { } pair && (passed == CouncilSide.Hawk ? pair.Hawk : pair.Dove) is { } urged
            && !string.Equals(urged.System, chosen.System, StringComparison.OrdinalIgnoreCase))
        {
            facts.Add($"{loserName} had urged the {CampaignLiaison.Named(urged)} in {urged.System}.");
        }

        if (count > 1)
        {
            facts.Add($"This is the {CampaignArcs.Ordinal(count)} time {loserName} has been passed over; the last time was over {earlier[^1].System}.");
        }

        var next = council with
        {
            Noted = chosen.Since,
            Sidings = [.. council.Sidings, new CouncilSiding(chosen.System, side, view.Now)],
        };

        return new CouncilStep(next, [new CampaignArcLine(ArcAct.Sided, view.Faction, string.Empty, loserName, chosen.System, facts)]);
    }

    /// <summary>
    /// A councillor's voice on the NPC voices' provider: a pool voice picked by <paramref name="name"/>, stepped past the
    /// Liaison's and past the voices of <paramref name="before"/>, the same in every session. Null with no voice to give.
    /// </summary>
    public static PinnedVoice? Voice(string name, string faction, IReadOnlyList<string> before, SpeechSettings speech, Func<string, VoiceCast> casting, double link = Link)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(speech);
        ArgumentNullException.ThrowIfNull(casting);

        var provider = VoiceGroups.ProviderFor(speech, VoiceGroups.Of(VoiceRole.Comms));

        if (string.Equals(provider, TtsProviderCatalog.NoneId, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var cast = casting(provider);
        var liaison = CampaignLiaison.Voice(faction, speech, casting)?.VoiceId;
        var taken = new List<string>();

        if (liaison is not null)
        {
            taken.Add(liaison);
        }

        foreach (var other in before)
        {
            taken.Add(Pick(cast, other, taken));
        }

        var voice = Pick(cast, name, taken);

        return voice is { Length: > 0 } ? new PinnedVoice(provider, voice) { Link = link } : null;
    }

    private static string Pick(VoiceCast cast, string name, List<string> taken)
    {
        var voice = cast.Lasting(name) ?? cast.For(VoiceRole.Comms).VoiceId ?? string.Empty;

        for (var step = 0; step < cast.Pool.Count && taken.Contains(voice, StringComparer.OrdinalIgnoreCase); step++)
        {
            var at = cast.Pool.ToList().FindIndex(pooled => string.Equals(pooled, voice, StringComparison.OrdinalIgnoreCase));

            voice = cast.Pool[(Math.Max(at, 0) + 1) % cast.Pool.Count];
        }

        return voice;
    }

    private static CouncilStep Begin(CouncilView view)
    {
        if (Picks(view.Fronts) is not var (hawkFront, doveFront))
        {
            return new CouncilStep(null, []);
        }

        var (hawk, dove) = Names(view.SeedKey);
        var council = new CampaignCouncil(hawk, dove, view.Now, view.Chosen?.Since, []);

        return new CouncilStep(
            council,
            [
                Argument(view, hawk, CouncilSide.Hawk, hawkFront, dove, doveFront),
                Argument(view, dove, CouncilSide.Dove, doveFront, hawk, hawkFront),
            ]);
    }

    private static CampaignArcLine Argument(CouncilView view, string who, CouncilSide side, Front urged, string other, Front otherUrged) =>
        new(
            ArcAct.Argument,
            view.Faction,
            string.Empty,
            who,
            urged.System,
            [
                $"{who} is the {Role(side)} on {view.Faction}'s council: {Temper(side)}.",
                $"{who} urges the Commander to take the {CampaignLiaison.Named(urged)} in {urged.System}: {urged.Reason}",
                $"{other}, the {Role(side == CouncilSide.Hawk ? CouncilSide.Dove : CouncilSide.Hawk)}, urges the {CampaignLiaison.Named(otherUrged)} in {otherUrged.System} instead.",
            ]);

    private static string Describe(IReadOnlyList<Front> fronts, ChosenFront chosen) =>
        fronts.FirstOrDefault(front => front.Kind == chosen.Kind && SameSystem(front, chosen.System)) is { } found
            ? CampaignLiaison.Named(found)
            : CampaignFronts.Describe(chosen.Kind).ToLowerInvariant();

    private static string NameOf(CampaignCouncil council, CouncilSide side) => side == CouncilSide.Hawk ? council.Hawk : council.Dove;

    private static string Role(CouncilSide side) => side == CouncilSide.Hawk ? "Hawk" : "Dove";

    private static string Temper(CouncilSide side) =>
        side == CouncilSide.Hawk ? "the one who presses a fight with a rival" : "the one who counsels patience and holding what is held";

    private static bool SameSystem(Front front, Front other) => SameSystem(front, other.System);

    private static bool SameSystem(Front front, string system) => string.Equals(front.System, system, StringComparison.OrdinalIgnoreCase);
}
