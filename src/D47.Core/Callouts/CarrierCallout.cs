using D47.Core.Audio;
using D47.Core.Journal;

namespace D47.Core.Callouts;

/// <summary>
/// The Commander's fleet carrier, answering for itself (Phase 11, "Carrier Captain").
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
/// character when there is a model to ask (Phase 11: "varied LLM arrival and departure
/// responses"), which is why each carries a <see cref="Announcement.Key"/> the app can match on
/// rather than being the final text.
/// </para>
/// </summary>
public sealed class CarrierCallout : ICallout
{
    public string Id => "carrier";

    /// <summary>
    /// Docked at the Commander's own carrier — the ship down and made fast, which is what the
    /// <c>Docked</c> event actually is (<a href="https://github.com/dseelinger/d47/issues/220">#220</a>).
    /// <para>
    /// It was one tower line saying <i>"docking granted"</i>, which was wrong twice over: the
    /// grant happened minutes earlier, and Elite's own docking-granted message from the carrier
    /// is already re-voiced as the tower, so d47 was inventing a second tower saying the same
    /// thing. Two keys now, the <see cref="InboundKey"/> exchange shape applied to the deck:
    /// the tower acknowledges the moment this event is, and the captain makes it a welcome.
    /// </para>
    /// </summary>
    public const string SecuredKey = "carrier.secured";

    public const string HomeKey = "carrier.home";

    public const string DepartureKey = "carrier.departure";

    public const string JumpKey = "carrier.jump";

    /// <summary>
    /// The Commander has dropped out of supercruise at their own carrier, and the tower tells the
    /// captain before the captain says anything to anyone (asked for 2026-08-21).
    /// <para>
    /// <b>Two keys because it is two people talking to each other.</b> The tower's line is
    /// addressed to the captain and the captain's answers the tower and then the Commander, which
    /// is the whole of what makes it an exchange rather than a greeting said twice.
    /// </para>
    /// </summary>
    public const string InboundKey = "carrier.inbound";

    public const string WelcomeKey = "carrier.welcome";

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
                            SecuredKey,
                            $"Ship secured aboard {Called(state.Carrier)}, {Owner(state)}.");

                        yield return Captain(
                            HomeKey,
                            $"Welcome home, {Owner(state)}. I'll meet you in the hangar.");
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

                // Dropping out of supercruise at the Commander's own carrier.
                //
                // **`SupercruiseDestinationDrop` is the event and `SupercruiseExit` is not.**
                // Measured over the 920-journal corpus: not one of 1,889 station-type
                // SupercruiseExit events names a carrier, because the body a drop is recorded
                // against is whatever the carrier is orbiting. The destination drop names the
                // target outright — `"ETERNAL FLAME BNH-T2F"` — and 779 of them are at a
                // callsign the same journal establishes as the Commander's own.
                //
                // Matched on the callsign for the reason `Docked` is: the same 804 events include
                // drops at other people's carriers, and a welcome home from somebody else's crew
                // is worse than silence.
                case "SupercruiseDestinationDrop" when !context.IsPriming
                                                       && IsOwnCarrier(
                                                           Target(journalEvent.String("Type"), state.Carrier),
                                                           state.Carrier):
                    yield return Tower(
                        InboundKey,
                        "Captain, our Commander is inbound. Just thought you'd like to know.");

                    yield return Captain(
                        WelcomeKey,
                        $"Priority routing, Tower Control. Welcome home, {Owner(state)}.");

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

    /// <summary>
    /// The callsign out of a supercruise target, which Elite writes as the carrier's name and its
    /// callsign together — <c>"Arx Dei B0X-79X"</c>. Null for a target that is not a carrier at
    /// all, which is most of the 3,819 drops in the corpus.
    /// <para>
    /// The <em>last</em> word rather than a pattern for a callsign, because a callsign is
    /// Frontier's format to change and the carrier's own name is free text a Commander chose —
    /// one that ends in something callsign-shaped is a name d47 must not read as an identity.
    /// The comparison below is against the callsign this Commander's journal already stated, so
    /// the only question here is which word to hand it.
    /// </para>
    /// </summary>
    private static string? Target(string? said, CarrierState carrier)
    {
        if (said is not { Length: > 0 } || carrier.CallSign is not { Length: > 0 })
        {
            return null;
        }

        var at = said.LastIndexOf(' ');

        return at < 0 ? said : said[(at + 1)..];
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
