using D47.Core.Audio;
using D47.Core.Journal;

namespace D47.Core.Callouts;

/// <summary>
/// The Commander's fleet carrier, answering for itself (list.md Phase 11, "Carrier Captain").
/// <para>
/// Two people rather than one. The captain speaks about the carrier — its jump, its fuel — and
/// the tower handles the Commander's own arrivals and departures, because that is how it works
/// on a station and because a carrier where both jobs come out of the same voice is a carrier
/// with one person on it.
/// </para>
/// <para>
/// Silent for a Commander who does not own one, and it never guesses: <see cref="CarrierState"/>
/// is established by events only an owner receives, so "no carrier seen" is the honest state
/// and produces nothing rather than a captain talking about a ship that does not exist.
/// </para>
/// <para>
/// The lines here are the authored floor. The app replaces them with a model-generated line in
/// character when there is a model to ask (list.md Phase 11: "varied LLM arrival and departure
/// responses"), which is why each carries a <see cref="Announcement.Key"/> the app can match on
/// rather than being the final text.
/// </para>
/// </summary>
public sealed class CarrierCallout : ICallout
{
    public string Id => "carrier";

    /// <summary>Docked at, or departed from, the Commander's own carrier.</summary>
    public const string ArrivalKey = "carrier.arrival";

    public const string DepartureKey = "carrier.departure";

    public const string JumpKey = "carrier.jump";

    private string? _lastDockedAt;

    public IEnumerable<Announcement> Examine(CalloutContext context)
    {
        if (context.State is not { Carrier.Owned: true } state)
        {
            yield break;
        }

        foreach (var journalEvent in context.Events)
        {
            switch (journalEvent.Kind)
            {
                // Docking at the Commander's own carrier, rather than at any station. The
                // callsign is the carrier's stable identity and is what Elite writes as the
                // station name, so this is an equality check rather than a guess.
                case "Docked" when IsOwnCarrier(journalEvent.String("StationName"), state.Carrier):
                    _lastDockedAt = journalEvent.String("StationName");

                    if (!context.IsPriming)
                    {
                        yield return Tower(
                            ArrivalKey,
                            $"{Called(state.Carrier)}, docking granted. Welcome home, {Owner(state)}.");
                    }

                    break;

                case "Undocked" when IsOwnCarrier(journalEvent.String("StationName") ?? _lastDockedAt, state.Carrier):
                    _lastDockedAt = null;

                    if (!context.IsPriming)
                    {
                        yield return Tower(
                            DepartureKey,
                            $"{Called(state.Carrier)} clear. Safe flying, {Owner(state)}.");
                    }

                    break;

                // The captain's business rather than the tower's: this is about the carrier
                // itself moving, not about the Commander arriving.
                case "CarrierJumpRequest" when !context.IsPriming:
                    if (journalEvent.String("SystemName") is { Length: > 0 } destination)
                    {
                        yield return Captain(
                            JumpKey,
                            $"Jump plotted for {destination}, {Owner(state)}. We will be under way on your order.");
                    }

                    break;
            }
        }
    }

    private static bool IsOwnCarrier(string? stationName, CarrierState carrier) =>
        stationName is { Length: > 0 }
        && carrier.CallSign is { Length: > 0 } callsign
        && string.Equals(stationName, callsign, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// How the carrier's crew address the person who owns it.
    /// <para>
    /// By name, because they know it. The tower and the captain are the Commander's own crew on
    /// the Commander's own ship — an eight-billion-credit one they were hired onto — and
    /// "Welcome back, Commander" is how a stranger at a starport talks
    /// (remediation.md 9, "the Carrier Captain and Control Tower should give the Commander the
    /// respect he deserves as carrier owner").
    /// </para>
    /// <para>
    /// The name is the one the journal header states and is never invented; without it this is
    /// the bare rank, which is still correct and merely less familiar. No honorific beyond the
    /// rank: "sir" and "ma'am" are a guess about the Commander that nothing in the journal
    /// supports, and guessing wrong about somebody's own crew is worse than not guessing.
    /// </para>
    /// </summary>
    private static string Owner(CommanderGameState state) =>
        state.Identity.Name is { Length: > 0 } name ? $"Commander {name}" : "Commander";

    /// <summary>
    /// What to call it out loud: the name the Commander gave it, falling back to the callsign.
    /// Both come from the journal and neither is invented.
    /// </summary>
    private static string Called(CarrierState carrier) =>
        carrier.Name is { Length: > 0 } name ? name : carrier.CallSign ?? "Carrier";

    private static Announcement Tower(string key, string text) => new(key, text)
    {
        Voice = VoiceRole.TowerControl,
        Cooldown = TimeSpan.FromSeconds(30),
    };

    private static Announcement Captain(string key, string text) => new(key, text)
    {
        Voice = VoiceRole.CarrierCaptain,
        Cooldown = TimeSpan.FromSeconds(30),
    };
}
