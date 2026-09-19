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

    /// <summary>Which line of each key's pool is due next, so a replayed journal says the same lines.</summary>
    private readonly Dictionary<string, int> _nextLine = new();

    /// <summary>
    /// The next line of <paramref name="pool"/> for <paramref name="key"/>, cycling in order rather than
    /// off <see cref="Random.Shared"/> — the order two consecutive fires never repeat, and a replay of
    /// the same journal always picks the same line.
    /// </summary>
    private string Pick(string key, string[] pool)
    {
        var index = _nextLine.GetValueOrDefault(key);
        _nextLine[key] = (index + 1) % pool.Length;
        return pool[index];
    }

    /// <summary>Tower: the ship set down and made fast. Names the carrier and the owner.</summary>
    private static readonly string[] SecuredLines =
    [
        "Ship secured aboard {0}, {1}.",
        "{0} log shows you docked and secured, {1}.",
        "Secured and clamped down, {1}. Welcome to {0}.",
        "You're secured on the deck, {1}. {0} confirms it.",
        "Deck crew confirms the ship secured aboard {0}, {1}.",
        "That's you secured, {1}. {0}'s deck log shows it.",
    ];

    /// <summary>Captain: the docked welcome. Addresses the owner only.</summary>
    private static readonly string[] HomeLines =
    [
        "Welcome home, {0}. I'll meet you in the hangar.",
        "Good to have you back, {0}. I'll be waiting in the hangar.",
        "Welcome aboard, {0}. Meet you down in the hangar.",
        "Glad you're back, {0}. I'll see you in the hangar.",
        "Home again, {0}. I'll meet you at the hangar.",
        "Welcome back, {0}. Down to the hangar — I'll be there.",
    ];

    /// <summary>Tower: the ship clear of the deck. Names the carrier and the owner.</summary>
    private static readonly string[] DepartureLines =
    [
        "{0} clear. Safe flying, {1}.",
        "You're clear of {0}, {1}. Fly safe.",
        "Clear of the deck, {1}. {0} wishes you safe flying.",
        "{0} confirms you clear, {1}. Safe flying out there.",
        "You're away clean, {1}. Safe flying from {0}.",
        "Clear of {0}'s deck, {1}. Fly safe.",
    ];

    /// <summary>Tower, addressed to the captain: the Commander dropping in from supercruise.</summary>
    private static readonly string[] InboundLines =
    [
        "Captain, our Commander is inbound. Just thought you'd like to know.",
        "Captain, the Commander is inbound now.",
        "Captain, heads up — the Commander is inbound.",
        "Captain, inbound traffic: it's the Commander.",
        "Captain, the Commander's dropping in, inbound.",
        "Captain, inbound at range. It's the Commander.",
    ];

    /// <summary>Captain, answering the tower: the Commander welcomed home. Names the owner.</summary>
    private static readonly string[] WelcomeLines =
    [
        "Priority routing, Tower Control. Welcome home, {0}.",
        "Copy that, Tower Control. Good to have you back, {0}.",
        "Acknowledged, Tower Control. Meet you on the deck, {0}.",
        "Understood, Tower Control. Coming down to greet you, {0}.",
        "Copy, Tower Control. On my way down to meet you, {0}.",
        "Received, Tower Control. Glad you're back, {0}.",
    ];

    /// <summary>Captain: a jump plotted. Names the destination and the owner.</summary>
    private static readonly string[] JumpLines =
    [
        "Jump plotted for {0}, {1}. We will be under way on your order.",
        "Course laid in for {0}, {1}. Ready when you give the word.",
        "Plotted and ready for {0}, {1}. Say the word and we go.",
        "{0} is plotted, {1}. Standing by for your order.",
        "Jump set for {0}, {1}. We'll move on your order.",
        "Ready to jump for {0}, {1}. Awaiting your word.",
    ];

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
                            string.Format(Pick(SecuredKey, SecuredLines), Called(state.Carrier), Owner(state)));

                        yield return Captain(
                            HomeKey,
                            string.Format(Pick(HomeKey, HomeLines), Owner(state)));
                    }

                    break;

                case "Undocked" when IsOwnCarrier(journalEvent.String("StationName") ?? _lastDockedAt, state.Carrier):
                    _lastDockedAt = null;

                    if (!context.IsPriming)
                    {
                        yield return Tower(
                            DepartureKey,
                            string.Format(Pick(DepartureKey, DepartureLines), Called(state.Carrier), Owner(state)));
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
                        Pick(InboundKey, InboundLines));

                    yield return Captain(
                        WelcomeKey,
                        string.Format(Pick(WelcomeKey, WelcomeLines), Owner(state)));

                    break;

                // The captain's business rather than the tower's: this is about the carrier itself moving,
                // not about the Commander arriving.
                case "CarrierJumpRequest" when !context.IsPriming:
                    if (journalEvent.String("SystemName") is { Length: > 0 } destination)
                    {
                        yield return Captain(
                            JumpKey,
                            string.Format(Pick(JumpKey, JumpLines), destination, Owner(state)));
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
