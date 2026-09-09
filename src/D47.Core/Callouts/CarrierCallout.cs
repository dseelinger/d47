using D47.Core.Audio;
using D47.Core.Journal;

namespace D47.Core.Callouts;

/// <summary>The Commander's fleet carrier, answering for itself (Phase 11, "Carrier Captain").</summary>
public sealed class CarrierCallout : ICallout
{
    public string Id => "carrier";

    /// <summary>
    /// Docked at the Commander's own carrier — the ship down and made fast, which is what the
    /// <c>Docked</c> event actually is (#220).
    /// </summary>
    public const string SecuredKey = "carrier.secured";

    public const string HomeKey = "carrier.home";

    public const string DepartureKey = "carrier.departure";

    public const string JumpKey = "carrier.jump";

    /// <summary>
    /// The Commander has dropped out of supercruise at their own carrier, and the tower tells the
    /// captain before the captain says anything to anyone (asked for 2026-08-21).
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
                // Docking at the Commander's own carrier, rather than at any station.
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

                // Dropping out of supercruise at the Commander's own carrier. **`SupercruiseDestinationDrop`
                // is the event and `SupercruiseExit` is not.** Measured over the 920-journal corpus: not one
                // of 1,889 station-type SupercruiseExit events names a carrier, because the body a drop is
                // recorded against is whatever the carrier is orbiting.
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

                // The captain's business rather than the tower's: this is about the carrier itself moving,
                // not about the Commander arriving.
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
    /// callsign together — <c>"Arx Dei B0X-79X"</c>.
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

    /// <summary>How the carrier's crew address the person who owns it.</summary>
    private static string Owner(CommanderGameState state) =>
        CommanderAddress.Said(state.Identity.Name);

    /// <summary>What to call it out loud: the name the Commander gave it, falling back to the callsign.</summary>
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
