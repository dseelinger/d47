using System.Text.RegularExpressions;
using D47.Core.Audio;
using D47.Core.Configuration;
using D47.Core.Conversation;
using D47.Core.Journal;
using D47.Core.Persona;

namespace D47.Core.Callouts;

/// <summary>Which pairing an overheard exchange is (#244).</summary>
public enum NpcChatterKind
{
    /// <summary>Two invented people near the Commander, talking to each other.</summary>
    Passersby,

    /// <summary>
    /// An invented pilot and the controller of the station or carrier the Commander is docked at.
    /// </summary>
    Controller,

    /// <summary>One invented person saying a line or two to the Commander.</summary>
    Hail,

    /// <summary>Two invented bystanders reacting to the Commander's last kill.</summary>
    Combat,

    /// <summary>The people of a settlement the Commander is on foot at, on their own radio.</summary>
    Scene,
}

/// <summary>One parsed line of an exchange: who says it, what they say, and whose voice it is.</summary>
/// <param name="Role">The carrier post this line belongs to, or null for an invented speaker.</param>
/// <param name="VoiceId">The voice its roster slot was cast with, or null where there was no roster.</param>
public sealed record NpcChatterLine(string Name, string Text, VoiceRole? Role = null, string? VoiceId = null);

/// <summary>A chatter line as it will be spoken, with its exchange and whether the Commander may answer it.</summary>
public sealed record NpcChatterHeard(NpcChatterLine Line, int ExchangeIndex, bool Answerable);

/// <summary>One voice an exchange may use, cast before the exchange is written (#415).</summary>
/// <param name="Tag">What the model tags this slot's lines with.</param>
/// <param name="Name">The NPC's name where they were already heard in this system, or null for a new one.</param>
/// <param name="Flavoured">Whether this exchange asks for the slot's words to suit its accent.</param>
public sealed record NpcChatterSlot(
    string Tag,
    string VoiceId,
    string? Accent,
    Audio.VoiceGender Gender,
    string? Name = null,
    bool Flavoured = true);

/// <summary>The voices an exchange is written for: the invented speakers' slots and the carrier's two posts.</summary>
public sealed record NpcChatterRoster(
    IReadOnlyList<NpcChatterSlot> Slots,
    string? TowerAccent = null,
    string? CaptainAccent = null,
    bool TowerFlavoured = true,
    bool CaptainFlavoured = true)
{
    public static readonly NpcChatterRoster None = new([]);

    /// <summary>The same roster with each accented speaker rolled once against <paramref name="percent"/>.</summary>
    public NpcChatterRoster Rolled(AccentRoll roll, int percent)
    {
        ArgumentNullException.ThrowIfNull(roll);

        return this with
        {
            Slots = [.. Slots.Select(slot => slot with { Flavoured = slot.Accent is not { Length: > 0 } || roll.Hits(percent) })],
            TowerFlavoured = TowerAccent is not { Length: > 0 } || roll.Hits(percent),
            CaptainFlavoured = CaptainAccent is not { Length: > 0 } || roll.Hits(percent),
        };
    }

    /// <summary>Whether the accent sentence goes with this line's speaker.</summary>
    public bool IsFlavoured(NpcChatterLine line) =>
        line.Role switch
        {
            VoiceRole.TowerControl => TowerFlavoured,
            VoiceRole.CarrierCaptain => CaptainFlavoured,
            _ => Slots.FirstOrDefault(slot => slot.VoiceId == line.VoiceId)?.Flavoured ?? true,
        };

    /// <summary>The most NPCs already heard in this system that one exchange is offered back.</summary>
    public const int MostMet = 3;

    /// <summary>
    /// Casts the slots for one exchange from the NPC cast: new voices for the speakers the kind needs,
    /// and the NPCs already heard here with the voices they already have, less any in <paramref name="dead"/>.
    /// </summary>
    public static NpcChatterRoster Cast(
        VoiceCast cast,
        NpcChatterKind kind,
        int exchangeIndex,
        string? system,
        string? allegiance = null,
        string? towerAccent = null,
        string? captainAccent = null,
        IReadOnlySet<string>? dead = null)
    {
        ArgumentNullException.ThrowIfNull(cast);

        var fresh = cast.Roster(kind == NpcChatterKind.Hail ? 1 : 2, VoiceCast.SeedOf(exchangeIndex, system), allegiance);
        var slots = new List<NpcChatterSlot>();

        foreach (var voice in fresh)
        {
            slots.Add(new NpcChatterSlot(TagOf(slots.Count), voice, cast.AccentOf(voice), cast.GenderOf(voice)));
        }

        var living = cast.MetHere.Where(met => dead is null || !dead.Contains(met.Name, StringComparer.OrdinalIgnoreCase));

        foreach (var (name, voice) in living.Take(MostMet))
        {
            slots.Add(new NpcChatterSlot(TagOf(slots.Count), voice, cast.AccentOf(voice), cast.GenderOf(voice), name));
        }

        return new NpcChatterRoster(slots, towerAccent, captainAccent);
    }

    private static string TagOf(int index) => ((char)('A' + index)).ToString();
}

/// <summary>What d47 knows about the Commander's own fleet carrier while an exchange is composed (#249).</summary>
public sealed record NpcChatterCarrier
{
    /// <summary>No carrier in the picture: the Commander does not own one, or none is known yet.</summary>
    public static readonly NpcChatterCarrier None = new();

    /// <summary>Whether the Commander owns a carrier at all.</summary>
    public bool Owned { get; init; }

    /// <summary>Whether the Commander is at it — set down on its deck, or sharing the space around it.</summary>
    public bool Present { get; init; }

    /// <summary>What to call it out loud: the name the Commander gave it, or the callsign.</summary>
    public string? Called { get; init; }

    /// <summary>Whether it actually has a jump scheduled — <see cref="CarrierState.JumpScheduled"/>.</summary>
    public bool JumpScheduled { get; init; }

    /// <summary>
    /// Read from the two parts of the game state that say it, rather than from the whole: both are
    /// records a test can build, and neither can be set on a <see cref="CommanderGameState"/> from
    /// outside the fold.
    /// </summary>
    public static NpcChatterCarrier Of(CarrierState? carrier, JournalLocation? location)
    {
        if (carrier is not { Owned: true })
        {
            return None;
        }

        var here = location ?? JournalLocation.Unknown;

        var sameSystem = carrier.StarSystem is { Length: > 0 } parked
            && here.StarSystem is { Length: > 0 } current
            && string.Equals(parked, current, StringComparison.OrdinalIgnoreCase);

        // On its deck, which is the case the Commander named first.
        var onItsDeck = here.AtCarrier && IsMine(here, carrier);

        // Otherwise: the same system, out of the chair-bound modes where nothing is overheard from anybody
        // nearby, and not sitting inside somebody else's station.
        var sharingItsSpace = sameSystem
            && here.Mode is not (FlightMode.Supercruise or FlightMode.Hyperspace)
            && (!here.Docked || onItsDeck);

        return new NpcChatterCarrier
        {
            Owned = true,
            Present = onItsDeck || sharingItsSpace,
            Called = carrier.Name is { Length: > 0 } name ? name : carrier.CallSign,
            JumpScheduled = carrier.JumpScheduled,
        };
    }

    /// <summary>Whether the station the Commander is docked at is their own carrier.</summary>
    private static bool IsMine(JournalLocation location, CarrierState carrier) =>
        (carrier.CarrierId is { } id && location.MarketId == id)
        || Named(location.StationName, carrier.CallSign)
        || Named(location.StationName, carrier.Name)
        || Named(location.StationName, carrier.DisplayName);

    private static bool Named(string? station, string? carrier) =>
        station is { Length: > 0 }
        && carrier is { Length: > 0 }
        && station.Contains(carrier, StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// Whether one more exchange at the Commander's own carrier may make his owning it the subject rather
/// than only colour (#88). True at most once a visit: the first exchange found present claims it, and
/// leaving — <see cref="NpcChatterCarrier.Present"/> going false — returns it for the next one.
/// </summary>
public sealed class NpcChatterOwnershipSpotlight
{
    private bool _spent;

    /// <summary>Whether this exchange may spend the visit's one spotlight.</summary>
    public bool Claim(bool present)
    {
        if (!present)
        {
            _spent = false;
            return false;
        }

        if (_spent)
        {
            return false;
        }

        _spent = true;
        return true;
    }
}

/// <summary>
/// Invented background radio traffic (#244): made-up conversations between people who do not exist —
/// never the game's own NPC messages, which arrive through <see cref="IncomingMessages"/> and are
/// somebody else's words. Each speaker is drawn a manner of speaking from <see cref="Manners"/>; a
/// manner describes how someone talks, never where they are from or how well they speak English, and
/// no accent is ever rendered through spelling or grammar (#57).
/// </summary>
public static partial class NpcChatter
{
    public const string KeyPrefix = "npc.chatter.";

    /// <summary>The key every spoken line of an exchange goes out under.</summary>
    public const string LineKey = KeyPrefix + "line";

    /// <summary>The most lines one exchange may carry, however many the model writes.</summary>
    public const int MostLines = 4;

    /// <summary>The tightest a handover may be (#259).</summary>
    public static readonly TimeSpan ShortestBeat = TimeSpan.FromMilliseconds(600);

    /// <summary>And the loosest.</summary>
    public static readonly TimeSpan LongestBeat = TimeSpan.FromMilliseconds(1700);

    /// <summary>
    /// The air to leave in front of a line of an exchange (#259), reported as "it's like watching an
    /// episode of the Gilmore Girls".
    /// </summary>
    /// <param name="line">Which line of the exchange this is.</param>
    public static TimeSpan Beat(int line)
    {
        if (line <= 0)
        {
            return TimeSpan.Zero;
        }

        var fraction = unchecked((uint)line * 2654435761u) / 4294967296.0;

        return ShortestBeat + (LongestBeat - ShortestBeat) * fraction;
    }

    /// <summary>The exact name the carrier's tower controller speaks under, and only it (#249).</summary>
    public const string TowerName = "Tower";

    /// <summary>And its captain's.</summary>
    public const string CaptainName = "Captain";

    /// <summary>
    /// The kind is read back off the key, the same way the ambient situation is: the callout has moved
    /// on by the time the app composes, and the key is the only thing that travelled.
    /// </summary>
    public static NpcChatterKind KindOf(string key) =>
        Enum.TryParse<NpcChatterKind>(key[KeyPrefix.Length..], ignoreCase: true, out var kind)
            ? kind
            : NpcChatterKind.Passersby;

    /// <summary>The persona-slot framing.</summary>
    public const string Speaker =
        "You write background radio traffic overheard in the Elite Dangerous galaxy in 3311: "
        + "short exchanges between minor invented characters — freighter crews, couriers, dock "
        + "hands, controllers. Plain working speech, brief and human. Never mention being an AI "
        + "or a model, and never break the fiction. A speaker's manner is how they talk, never where "
        + "they are from: every line is in standard English grammar, and no accent or origin is ever "
        + "shown through phonetic spelling, dropped articles, broken or non-native grammar, or "
        + "imperfect English.";

    /// <summary>
    /// How a speaker talks: pace, register, formality, verbal habits. An entry never names a
    /// nationality, a language, a region or a level of fluency.
    /// </summary>
    public static readonly IReadOnlyList<string> Manners =
    [
        "terse to the point of rudeness",
        "over-explains everything",
        "talks in questions",
        "formal to the point of stiffness",
        "cheerful and too familiar",
        "distracted, half attending to something else",
        "dry and deadpan",
        "nervous, hedging every statement",
        "wanders off the point",
        "precise, correcting small details",
        "weary, saying no more than the job needs",
        "enthusiastic about small things",
    ];

    /// <summary>
    /// The manners drawn for one exchange's speakers, in speaker order: distinct from each other while
    /// there are no more speakers than manners, and the same for the same exchange every time.
    /// </summary>
    public static IReadOnlyList<string> MannersFor(int exchangeIndex, int speakers)
    {
        var fraction = unchecked((uint)(exchangeIndex + MannerOffset) * 2654435761u) / 4294967296.0;
        var first = (int)(fraction * Manners.Count);

        return [.. Enumerable.Range(0, speakers).Select(speaker => Manners[(first + speaker * MannerStep) % Manners.Count])];
    }

    /// <summary>The stride between one speaker's manner and the next; coprime with the table's length.</summary>
    private const int MannerStep = 5;

    private const int MannerOffset = 31;

    private const string MannerMeaning =
        "A manner is how that speaker talks — pace, register, verbal habits — and shapes every line of "
        + "theirs. ";

    /// <summary>The manners for the speakers of an exchange nobody was cast for ahead of time.</summary>
    private static string UnslottedManners(NpcChatterKind kind, int exchangeIndex)
    {
        var manners = MannersFor(exchangeIndex, kind == NpcChatterKind.Hail ? 1 : 2);

        return manners.Count == 1
            ? $"The speaker's manner: {manners[0]}. " + MannerMeaning
            : $"The speakers' manners, in the order they first speak: first, {manners[0]}; second, "
                + $"{manners[1]}. " + MannerMeaning;
    }

    /// <summary>The line format when no voices were cast ahead of the exchange.</summary>
    private const string Unslotted =
        "Write only the exchange, one line per speaker turn, each formatted exactly as "
        + "Name: words — an invented plain name or call sign, a colon, what they say. A speaker "
        + "keeps one name, written the same way on every line of theirs. ";

    /// <summary>The line format when each speaker is a slot in a cast roster (#415).</summary>
    private const string Slotted =
        "Write only the exchange, one line per speaker turn, each formatted exactly as "
        + "Name [slot]: words — the speaker's name, their slot letter in square brackets, a colon, "
        + "what they say. Each slot is one person with one name, written the same way on every line "
        + "of theirs, and no two slots share a name. Not every slot has to speak. ";

    /// <summary>The rest of the format contract every kind shares.</summary>
    private const string Contract =
        "No other text, no quotation marks, no stage directions. Use the live game state only for "
        + "where this is happening; invent everything else. Never name or imitate a real person or "
        + "another player. Nobody asks the Commander to do anything, nobody asks the Commander a "
        + "question, and nobody expects an answer. Nobody scans, interdicts, targets, fines or puts a "
        + "bounty on the Commander, grants or denies them docking, gives them or takes their cargo, or "
        + "sends them a wing or friend invite.";

    /// <summary>The cast slots, described by accent and gender, or nothing where there is no roster.</summary>
    private static string Roster(NpcChatterRoster roster, int exchangeIndex)
    {
        if (roster.Slots.Count == 0)
        {
            return string.Empty;
        }

        var manners = MannersFor(exchangeIndex, roster.Slots.Count);

        var described = roster.Slots.Select((slot, index) =>
        {
            var who = slot.Gender switch
            {
                Audio.VoiceGender.Feminine => "a woman",
                Audio.VoiceGender.Masculine => "a man",
                _ => "a person",
            };

            var accent = slot.Accent is { Length: > 0 } heard
                ? $" with {SpeakerAccent.Article(heard)} {heard} accent"
                : string.Empty;

            var manner = $", manner: {manners[index]}";

            return slot.Name is { Length: > 0 } name
                ? $"[{slot.Tag}] {name}, already heard in this system, {who}{accent}{manner} — if they "
                    + "speak, use exactly that name"
                : $"[{slot.Tag}] {who}{accent}{manner} — invent a plain name or call sign";
        });

        var withAccent = roster.Slots.Where(slot => slot.Accent is { Length: > 0 }).ToList();
        var flavoured = withAccent.Where(slot => slot.Flavoured).ToList();

        var accented = flavoured.Count == 0
            ? string.Empty
            : (flavoured.Count == withAccent.Count
                ? " Each speaker's words suit their slot's accent. "
                : $" The words of {string.Join(" and ", flavoured.Select(slot => $"[{slot.Tag}]"))} suit their slot's accent. ")
                + SpeakerAccent.Rules;

        return "The voices are already cast; every invented speaker is one of these slots: "
            + string.Join("; ", described) + ". " + MannerMeaning
            + (accented.Length > 0 ? accented.Trim() + " " : string.Empty);
    }

    /// <summary>
    /// What the model is asked for one exchange of the given kind, in the situation the Commander's own
    /// carrier is in.
    /// </summary>
    /// <param name="docked">
    /// Whether the Commander's ship is currently docked — read live, not off the kind, so a scene never
    /// keeps dock vocabulary the ship has since left behind.
    /// </param>
    /// <param name="spotlight">
    /// Whether this exchange may make the Commander's owning his carrier the subject rather than only
    /// colour — decided once per visit by <see cref="NpcChatterOwnershipSpotlight"/> (#88).
    /// </param>
    /// <param name="exchangeIndex">
    /// This exchange's place in the sequence <see cref="NpcChatterCallout"/> counts — the same number
    /// that picks its kind — used to rotate the passers-by cast, topic and opening beat so a replayed
    /// session picks the same script every time (#45).
    /// </param>
    /// <param name="stationType">
    /// The journal's <c>StationType</c> for the dock, or null when it is not known — used to say
    /// whether the station has a mail slot (#314) rather than leave the model to guess at one.
    /// </param>
    /// <param name="roster">The voices cast for this exchange, or null to let the model name them freely.</param>
    /// <param name="fight">The fight the <see cref="NpcChatterKind.Combat"/> scene reacts to.</param>
    public static string Instruction(
        NpcChatterKind kind,
        NpcChatterCarrier? carrier = null,
        bool docked = false,
        bool spotlight = false,
        int exchangeIndex = 0,
        string? stationType = null,
        NpcChatterRoster? roster = null,
        FightSnapshot? fight = null,
        string? scenario = null)
    {
        var about = carrier ?? NpcChatterCarrier.None;
        var cast = roster ?? NpcChatterRoster.None;

        return Situation(docked, stationType)
            + Scene(kind, about, docked, exchangeIndex, fight)
            + (cast.Slots.Count > 0 ? Roster(cast, exchangeIndex) : UnslottedManners(kind, exchangeIndex))
            + (cast.Slots.Count > 0 ? Slotted : Unslotted)
            + Contract
            + Carrier(about, spotlight, cast)
            + Scenario(scenario);
    }

    /// <summary>The scenario chatter may carry: only when everyone is told, and only when there is one.</summary>
    public static string? ScenarioFor(ScenarioAudience audience, string? scenario) =>
        audience == ScenarioAudience.Public && !string.IsNullOrWhiteSpace(scenario) ? scenario : null;

    private static string Scenario(string? scenario) =>
        string.IsNullOrWhiteSpace(scenario)
            ? string.Empty
            : " The pilots around the Commander can see what the Commander is doing, as follows: "
                + scenario.Trim()
                + " An exchange may be coloured by this where it fits; most exchanges do not mention it, "
                + "and nobody asks the Commander about it.";

    /// <summary>
    /// What the model is asked for one exchange of a scene, at a settlement, in a ship fight or over a mission.
    /// The scenario is given whatever the audience setting, as who the speakers are.
    /// </summary>
    public static string SceneInstruction(
        SceneBeat beat, string scenario, NpcChatterRoster? roster = null, int exchangeIndex = 0)
    {
        ArgumentNullException.ThrowIfNull(beat);

        var cast = roster ?? NpcChatterRoster.None;

        if (beat.Place == ScenePlace.Mission)
        {
            return MissionSituation(beat)
                + "The Commander's current scenario: " + scenario.Trim()
                + " Take from it who these people are and what is going on around them. They do not know who the "
                + "Commander is or the Commander's real purpose. If these missions do not bear on the scenario, reply "
                + "with nothing at all: no lines and no other text. "
                + (cast.Slots.Count > 0 ? Roster(cast, exchangeIndex) : UnslottedManners(NpcChatterKind.Scene, exchangeIndex))
                + (cast.Slots.Count > 0 ? Slotted : Unslotted)
                + SceneContract;
        }

        var aboard = beat.Place == ScenePlace.Ship;

        return (aboard ? FightSituation(beat) : SceneSituation(beat))
            + $"The Commander's current scenario, which this {(aboard ? "fight" : "settlement")} is part of: " + scenario.Trim()
            + " Take from it who these people are and what is going on around them. They do not know who the "
            + "Commander is or why the Commander is there. "
            + (aboard ? FightMoment(beat) : SceneMoment(beat))
            + (cast.Slots.Count > 0 ? Roster(cast, exchangeIndex) : UnslottedManners(NpcChatterKind.Scene, exchangeIndex))
            + (cast.Slots.Count > 0 ? Slotted : Unslotted)
            + SceneContract;
    }

    private static string SceneSituation(SceneBeat beat)
    {
        var body = beat.Body is { Length: > 0 } planet ? $" on {planet}" : string.Empty;
        var faction = beat.Faction is { Length: > 0 } owner ? $", run by {owner}" : string.Empty;
        var government = beat.Government is { Length: > 0 } rule ? $" Its government is {rule}." : string.Empty;

        var missions = beat.Missions is { Count: > 0 } held
            ? $"The Commander holds these missions concerning this settlement, which the people here do not know of: "
                + $"{MissionList(held, 0)}. "
            : string.Empty;

        return $"The Commander is on foot at {beat.Settlement}, a settlement{body}{faction}.{government} "
            + missions
            + "People who live and work there, such as guards, workers and staff, exchange 2 to 4 short lines "
            + "with each other on the settlement's own radio about what has just happened. ";
    }

    private static string MissionSituation(SceneBeat beat)
    {
        var missions = beat.Missions ?? [];

        var factions = missions
            .Select(mission => mission.Faction)
            .OfType<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var speakers = factions.Count > 0
            ? $"People of {string.Join(" or of ", factions)}"
            : "People of the faction that gave the work";

        var moment = beat.Kind switch
        {
            SceneBeatKind.MissionTaken => "has just taken on",
            SceneBeatKind.MissionDone => "has just completed",
            _ => "has just failed or abandoned",
        };

        return $"{speakers} exchange 2 to 4 short lines with each other on their own channel about a pilot they do "
            + $"not know, who {moment} work they gave out: {MissionList(missions, beat.MoreMissions)}. ";
    }

    /// <summary>Each mission by name, giver, target and destination where known, then the count of the rest.</summary>
    private static string MissionList(IReadOnlyList<Mission> missions, int more)
    {
        var named = missions.Select(mission =>
            mission.Title
            + (mission.Faction is { Length: > 0 } faction ? $" for {faction}" : string.Empty)
            + (mission.TargetFaction is { Length: > 0 } target ? $", against {target}" : string.Empty)
            + (mission.Destination is { Length: > 0 } destination ? $", to {destination}" : string.Empty));

        return string.Join("; ", named) + (more > 0 ? $"; and {more} more" : string.Empty);
    }

    private static string SceneMoment(SceneBeat beat) => beat.Kind switch
    {
        SceneBeatKind.Arrived =>
            "Nothing has happened yet: they are going about their shift and do not know anyone has arrived. "
            + "Nobody mentions an intruder or a visitor. ",

        SceneBeatKind.Spotted =>
            "They have just found an intruder in the settlement and are shooting at them. They do not know who "
            + "it is. ",

        SceneBeatKind.Down => SceneKill(beat),

        SceneBeatKind.CommanderDown =>
            "They have just killed the intruder they were fighting."
            + (beat.Killer is { Length: > 0 } killer ? $" {killer} fired the shot and may be one of the speakers. " : " "),

        _ => "The intruder has just got away in a ship or a vehicle"
            + (beat.Kills > 0 ? $", after killing {beat.Kills} of their people. " : ". "),
    };

    private static string SceneKill(SceneBeat beat)
    {
        var named = beat.Victim is { Length: > 0 } name ? name : null;

        var killed = (beat.Merged > 1, named) switch
        {
            (true, null) => $"{beat.Merged} of their people have just been killed.",
            (true, _) => $"{beat.Merged} of their people have just been killed, the last of them {named}.",
            (false, null) => "One of their people has just been killed.",
            _ => $"{named}, one of their people, has just been killed.",
        };

        var total = beat.Kills > beat.Merged ? $" That makes {beat.Kills} dead since the scene began." : string.Empty;

        var who = beat.Seen
            ? " The intruder they are fighting did it. "
            : " Nobody has seen who did it, and nobody has seen the Commander. ";

        return killed + total + who + "The dead do not speak. ";
    }

    private static string FightSituation(SceneBeat beat)
    {
        var site = beat.Site is { Length: > 0 } kind ? $" at a {kind}" : string.Empty;
        var system = beat.System is { Length: > 0 } star ? $" in {star}" : string.Empty;

        var speakers = beat.Sides is [var one, var other, ..]
            ? $"Pilots fighting there for {one} or for {other}, some from each side, "
            : beat.Faction is { Length: > 0 } faction
                ? $"Pilots flying for {faction}, the side the Commander's ship is fighting, "
                : "Pilots flying out there ";

        return $"The Commander is flying a ship{site}{system}. "
            + speakers
            + "exchange 2 to 4 short lines with each other on their own channel about what has just happened. ";
    }

    private static string FightMoment(SceneBeat beat) => beat.Kind switch
    {
        SceneBeatKind.Site =>
            "A ship has just dropped in. Nobody has noticed it yet, and nobody mentions a newcomer. ",

        SceneBeatKind.Interdicted =>
            (beat.Interdictor is { Length: > 0 } interdictor ? $"{interdictor}" : "One of them")
            + " has just pulled a ship they do not know out of supercruise"
            + (beat.Submitted ? ", and its pilot gave in without a struggle. " : ", against its pilot's struggle. ")
            + (beat.Interdictor is { Length: > 0 } ? "The interdictor may be one of the speakers. " : string.Empty),

        SceneBeatKind.Engaged =>
            "They have just started shooting at a ship they do not know. ",

        SceneBeatKind.ShipDown => FightKill(beat),

        SceneBeatKind.CommanderDown =>
            "They have just destroyed the ship they were fighting."
            + (beat.Killer is { Length: > 0 } killer
                ? $" {killer}{(beat.Ship is { Length: > 0 } ship ? $", flying a {ship}," : string.Empty)} fired the last shot and may be one of the speakers. "
                : " "),

        _ => "The ship they were fighting has just escaped into supercruise"
            + (beat.Kills > 0 ? $", after destroying {beat.Kills} of their ships. " : ". "),
    };

    private static string FightKill(SceneBeat beat)
    {
        var named = (beat.Victim, beat.Ship) switch
        {
            ({ Length: > 0 } pilot, { Length: > 0 } ship) => $"{pilot}'s {ship}",
            ({ Length: > 0 } pilot, _) => pilot,
            (_, { Length: > 0 } ship) => $"a {ship}",
            _ => null,
        };

        var whose = beat.Faction is { Length: > 0 } faction ? $" flying for {faction}" : string.Empty;

        var killed = (beat.Merged > 1, named) switch
        {
            (true, null) => $"{beat.Merged} ships{whose} have just been destroyed.",
            (true, _) => $"{beat.Merged} ships{whose} have just been destroyed, the last of them {named}.",
            (false, null) => $"A ship{whose} has just been destroyed.",
            _ => $"{named}{whose} has just been destroyed.",
        };

        var total = beat.Kills > beat.Merged ? $" That makes {beat.Kills} ships down since the fight began." : string.Empty;

        return killed + total + " A ship they do not know did it. The dead do not speak. ";
    }

    /// <summary>The format contract for a scene: the speakers talk among themselves about the Commander.</summary>
    private const string SceneContract =
        "No other text, no quotation marks, no stage directions. Use the live game state only for where this is "
        + "happening; invent everything else. Never name or imitate a real person or another player. They talk "
        + "to each other, never to the Commander, and nobody expects an answer. Nobody fines the Commander or "
        + "puts a bounty on them, and no dead person speaks.";

    /// <summary>
    /// The instruction with humor rolled once for the invented speakers and once for the carrier's crew
    /// when they are present, each result stated beside its speakers.
    /// </summary>
    public static string WithHumor(
        string instruction,
        NpcChatterCarrier? carrier,
        PersonaSettings settings,
        HumorRoll roll,
        bool canBeDirected)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(roll);

        var crewPresent = carrier?.Present == true;
        var npcs = Humor.DialFor(settings, HumorGroup.Npcs);
        var crew = Humor.DialFor(settings, HumorGroup.Carrier);

        var invented = roll.Hits(npcs) ? npcs.Level : 0;
        var own = crewPresent && roll.Hits(crew) ? crew.Level : 0;

        if (invented == 0 && own == 0)
        {
            return instruction;
        }

        var text = instruction + " " + HumorFor("the invented speakers", invented);

        if (crewPresent)
        {
            text += " " + HumorFor($"{TowerName} and {CaptainName}", own);
        }

        return canBeDirected ? $"{text} {Humor.Laughter}" : text;
    }

    private static string HumorFor(string who, int level) =>
        Humor.Describe(level, canBeDirected: false) is { } described
            ? $"Humor for {who}, level {level} of {Humor.Top}: {described}"
            : $"No humor for {who}; they play it straight.";

    /// <summary>The journal's <c>StationType</c> values for a dock with a mail slot.</summary>
    private static readonly HashSet<string> StationsWithAMailSlot = new(StringComparer.OrdinalIgnoreCase)
    {
        "Coriolis", "Orbis", "Ocellus", "Dodec", "AsteroidBase",
    };

    /// <summary>
    /// The one sentence of live state the model cannot misread (#43): said in words, because a prompt
    /// handed dock vocabulary invents a dock even where the scene never asked for one.
    /// </summary>
    private static string Situation(bool docked, string? stationType) => docked
        ? "The Commander's ship is docked at a station. " + MailSlot(stationType)
        : "The Commander's ship is in normal space, under its own power, with no station nearby. ";

    /// <summary>
    /// Whether the dock has a mail slot, said in words (#314): a fleet carrier, an outpost, a surface
    /// port and a settlement have none, and the model invented one anyway when left to guess.
    /// </summary>
    private static string MailSlot(string? stationType) =>
        stationType is { } type && StationsWithAMailSlot.Contains(type)
            ? string.Empty
            : "This station has no mail slot. Nobody mentions a mail slot, a letterbox or a docking "
                + "slot. ";

    private static string Scene(
        NpcChatterKind kind, NpcChatterCarrier carrier, bool docked, int exchangeIndex, FightSnapshot? fight) =>
        kind switch
        {
            NpcChatterKind.Combat => CombatScene(fight ?? FightSnapshot.None),

            // Docked is the only situation this pairing fires in, and while the Commander is at their own
            // carrier the only thing they can be docked at is that carrier — so the controller is named
            // rather than left for the model to guess at.
            NpcChatterKind.Controller when carrier.Present =>
                "An invented pilot and the tower controller aboard the Commander's own fleet carrier "
                + $"{Called(carrier)}exchange 2 to 4 short lines of routine traffic — "
                + $"{ControllerScene(exchangeIndex)} Procedure with a human edge. The Commander is not "
                + "part of it. ",

            NpcChatterKind.Controller =>
                "An invented pilot and the controller of the station or carrier where the Commander "
                + "is docked exchange 2 to 4 short lines of routine traffic — "
                + $"{ControllerScene(exchangeIndex)} Procedure with a human edge. The Commander is not "
                + "part of it. ",

            NpcChatterKind.Hail when !docked =>
                "One invented person nearby says one or two lines to the Commander over the open "
                + "channel — a compliment on the ship, a warning about a system ahead, a rumour "
                + "picked up on the way. No pad, no dock, no queue: there is no station here. "
                + "Statements only: they are not starting a conversation. ",

            NpcChatterKind.Hail =>
                "One invented person nearby says one or two lines to the Commander over the open "
                + "channel — a compliment on the ship, a grumble about the queue, a rumour heard in "
                + "the bar. Statements only: they are not starting a conversation. ",

            _ => PassersbyScene(exchangeIndex, docked),
        };

    private static string CombatScene(FightSnapshot fight)
    {
        var ship = fight.LastShip is { Length: > 0 } hull ? $"a {hull}" : "a ship";
        var faction = fight.LastVictimFaction is { Length: > 0 } owner ? $" of {owner}" : string.Empty;

        var pilot = fight.LastVictim is { Length: > 0 } name && fight.Dead.Contains(name)
            ? $" Its pilot, {name}, was destroyed with it. "
            : " ";

        var kills = fight.Kills == 1 ? "one ship" : $"{fight.Kills} ships";

        return "Two invented bystanders near the Commander react over the open channel to a fight that has "
            + "just ended — exchange 2 to 4 short lines. The Commander has destroyed " + kills
            + $" here; the last was {ship}{faction}." + pilot
            + "No destroyed pilot speaks. The Commander is not addressed, and the bystanders are not "
            + "in the fight. No pad, no dock, no queue: there is no station here. ";
    }

    /// <summary>
    /// A dock hand noticing a pad and a courier saying "not my problem" was one script wearing
    /// different names (#45): the cast, the topic and the opening beat now each rotate off
    /// <paramref name="exchangeIndex"/>, and the completions that shape is known to reach for are
    /// named off limits.
    /// </summary>
    private static string PassersbyScene(int exchangeIndex, bool docked)
    {
        var cast = Pick(exchangeIndex, CastOffset, docked ? DockedCasts : OpenSpaceCasts);
        var topic = Pick(exchangeIndex, TopicOffset, Topics);
        var opening = Pick(exchangeIndex, OpeningOffset, Openings);

        var noStation = docked
            ? string.Empty
            : "No pad, no dock, no queue: there is no station here. ";

        var noticed = Notices(exchangeIndex)
            ? "The Commander may be noticed in passing, nothing more. "
            : "The Commander is not part of it, and is not mentioned. ";

        return $"Two invented people near the Commander — {cast} — exchange 2 to 4 short lines, "
            + $"opening on {opening}, about {topic}. "
            + noStation
            + noticed
            + PassersbyBans;
    }

    /// <summary>
    /// The controller scene's subject and opening, rotated off <paramref name="exchangeIndex"/> the way
    /// <see cref="PassersbyScene"/> rotates cast, topic and opening (#291): a fixed "clearances, pad
    /// assignments, a telling-off" was the same line spoken at every visit.
    /// </summary>
    private static string ControllerScene(int exchangeIndex)
    {
        var subject = Pick(exchangeIndex, ControllerSubjectOffset, ControllerSubjects);
        var opening = Pick(exchangeIndex, ControllerOpeningOffset, ControllerOpenings);

        return $"{subject}, opening on {opening}.";
    }

    /// <summary>
    /// What the traffic is about — routine business only, never a jump (#291's own carrier drops any
    /// exchange that mentions one while none is scheduled, so a rotated subject must not risk it).
    /// </summary>
    private static readonly string[] ControllerSubjects =
    [
        "clearances and pad assignments",
        "a delayed pad assignment and a docking request",
        "a customs check and a paperwork snag",
        "a fuel allocation and a queue dispute",
        "a maintenance hold and a curt correction",
        "a beacon check and a pad swap",
        "a telling-off over a sloppy approach",
    ];

    private static readonly string[] ControllerOpenings =
    [
        "a queue backing up",
        "a pad running late",
        "a manifest flagged for review",
        "a beacon out of sync",
        "a pad mix-up",
        "a hold nobody wants",
    ];

    private const int ControllerSubjectOffset = 23;
    private const int ControllerOpeningOffset = 29;

    /// <summary>Cast pairs for a scene with a station around it.</summary>
    private static readonly string[] DockedCasts =
    [
        "a dock hand and a courier",
        "two freighter crews on the local channel",
        "a miner and a refinery hand",
        "a fuel rat and a bored escort pilot",
        "two passengers' liner crew",
        "a salvage crew",
    ];

    /// <summary>Cast pairs for a scene with no station in it (#43): no dock hand among them.</summary>
    private static readonly string[] OpenSpaceCasts =
    [
        "two freighter crews on the open channel",
        "a miner and a wingmate",
        "a fuel rat and a bored escort pilot",
        "two passengers' liner crew",
        "a salvage crew",
        "a courier and a scout",
    ];

    /// <summary>What the pair's own small business is about.</summary>
    private static readonly string[] Topics =
    [
        "a cargo manifest",
        "prices at the last stop",
        "a pilot's licence exam",
        "a bad landing somebody else made",
        "a rumour going around",
        "food",
        "weather on a planet nobody has been to",
        "a bet",
    ];

    /// <summary>What the exchange opens on — never the Commander's ship.</summary>
    private static readonly string[] Openings =
    [
        "a shift that will not end",
        "a delivery running late",
        "a manifest that has come up short",
        "a rumour just heard",
        "a bet neither of them has settled",
        "the weather rolling in",
    ];

    private const int CastOffset = 3;
    private const int TopicOffset = 7;
    private const int OpeningOffset = 13;
    private const int NoticeOffset = 19;

    /// <summary>
    /// The stock deflections the measured logs showed — asked for once here rather than caught after
    /// the fact, because a line already spoken cannot be unheard (#45).
    /// </summary>
    private const string PassersbyBans =
        "Do not write the Commander's ship blocking, hogging or taking up a pad — nobody here has "
        + "noticed it beyond perhaps a glance in passing. Do not write \"not my problem\", \"not my "
        + "run\", \"just here for the ... run\", or \"third time this week\" — overused lines, "
        + "off limits here. ";

    /// <summary>
    /// One pick off a fixed list, spaced by the same Knuth multiplicative hash <see
    /// cref="NpcChatterCallout"/> spreads exchanges with and <see cref="Beat"/> paces lines with, so a
    /// replayed session lands on the same pick every time (#45).
    /// </summary>
    private static T Pick<T>(int exchangeIndex, int offset, T[] options)
    {
        var fraction = unchecked((uint)(exchangeIndex + offset) * 2654435761u) / 4294967296.0;

        return options[(int)(fraction * options.Length)];
    }

    /// <summary>Whether the Commander may answer an exchange: every hail, and a passers-by exchange that notices them.</summary>
    public static bool MayNotice(NpcChatterKind kind, int exchangeIndex) =>
        kind switch
        {
            NpcChatterKind.Hail => true,
            NpcChatterKind.Passersby => Notices(exchangeIndex),
            _ => false,
        };

    /// <summary>The name an invented speaker is shown under, so they read apart from a real Commander.</summary>
    public static string Invented(string name) => $"{name} (invented)";

    /// <summary>Whether this exchange is one of the few allowed to notice the Commander at all.</summary>
    private static bool Notices(int exchangeIndex) =>
        unchecked((uint)(exchangeIndex + NoticeOffset) * 2654435761u) % 5 == 0;

    /// <summary>
    /// The rules the Commander's own carrier adds (#249, #88): who its two posts are when he is at it,
    /// how the people around him regard that, and that it is not going anywhere when it is not.
    /// </summary>
    private static string Carrier(NpcChatterCarrier carrier, bool spotlight, NpcChatterRoster roster)
    {
        if (!carrier.Owned)
        {
            return string.Empty;
        }

        var rules = string.Empty;

        if (carrier.Present)
        {
            rules +=
                " The Commander is at their own fleet carrier "
                + $"{Called(carrier)}— two people aboard it are not invented, its tower "
                + $"controller and its captain. If either speaks, that line's name is exactly "
                + $"{TowerName} or exactly {CaptainName}, with nothing else in it"
                + (roster.Slots.Count > 0 ? " and no slot" : string.Empty)
                + ", and no other speaker may use those two names. "
                + PostAccents(roster)
                + "Everybody here knows whose deck this is. His own crew — the tower, the "
                + "captain, anyone working for him — are not surprised he is aboard; it is "
                + "their job to be here, so write deference, easy familiarity, or a grumble made "
                + "to him rather than about him. A visiting pilot docked at somebody else's "
                + "carrier is the one who can be surprised, careful, chancing their arm, or "
                + "embarrassed to have been overheard."
                + (spotlight
                    ? " This exchange may make his owning the place the thing being talked "
                      + "about."
                    : " Do not make his owning the place the subject of this exchange. Let it "
                      + "colour what people are willing to say in front of him, or what they "
                      + "stop saying when he walks past, without a line remarking on it.");
        }

        if (!carrier.JumpScheduled)
        {
            rules +=
                " The Commander's fleet carrier has no jump scheduled and is going nowhere. "
                + "Nobody says or implies that it is jumping, departing or casting off, and "
                + "nobody asks when it does — not its crew, and not anybody talking about it.";
        }

        return rules;
    }

    /// <summary>What the tower's and the captain's voices sound like, where their listing says.</summary>
    private static string PostAccents(NpcChatterRoster roster)
    {
        var posts = new[] { (Post: TowerName, Accent: roster.TowerAccent), (Post: CaptainName, Accent: roster.CaptainAccent) }
            .Where(post => post.Accent is { Length: > 0 })
            .Select(post => $"{post.Post}'s voice has {SpeakerAccent.Article(post.Accent!)} {post.Accent} accent")
            .ToList();

        if (posts.Count == 0)
        {
            return string.Empty;
        }

        var suit = (
            roster.TowerFlavoured && roster.TowerAccent is { Length: > 0 },
            roster.CaptainFlavoured && roster.CaptainAccent is { Length: > 0 }) switch
        {
            (true, true) => " Their words suit it the same way. ",
            (true, false) => $" {TowerName}'s words suit it. ",
            (false, true) => $" {CaptainName}'s words suit it. ",
            _ => " ",
        };

        return string.Join(" and ", posts) + "." + suit;
    }

    /// <summary>The carrier's name and a trailing space, or nothing when it has no name to give.</summary>
    private static string Called(NpcChatterCarrier carrier) =>
        carrier.Called is { Length: > 0 } name ? $"{name} " : string.Empty;

    /// <summary>The reply, read strictly.</summary>
    /// <param name="roster">
    /// The voices the exchange was written for. With slots, a line's slot decides its voice, and a line
    /// with a missing or unknown slot is dropped.
    /// </param>
    public static IReadOnlyList<NpcChatterLine> Parse(
        string? script,
        NpcChatterKind kind,
        NpcChatterCarrier? carrier = null,
        NpcChatterRoster? roster = null)
    {
        if (string.IsNullOrWhiteSpace(script))
        {
            return [];
        }

        var about = carrier ?? NpcChatterCarrier.None;

        if (roster is { Slots.Count: > 0 })
        {
            return ParseSlotted(script, kind, about, roster);
        }
        var heard = new List<(string Spelled, string Text)>();

        foreach (var raw in script.Split('\n'))
        {
            if (heard.Count == MostLines)
            {
                break;
            }

            var split = raw.IndexOf(':', StringComparison.Ordinal);

            if (split <= 0)
            {
                continue;
            }

            var name = raw[..split].Trim().Trim('*', '-', '#', '"');
            var text = raw[(split + 1)..].Trim().Trim('"');

            if (name.Length is < 2 or > 40 || text.Length == 0 || !FlavourBriefs.MayBeSpoken(text))
            {
                continue;
            }

            heard.Add((name, text));
        }

        // Names are settled over the whole exchange before any line is judged, because the exchange is the
        // only place where two spellings are knowably one person (#256).
        var named = OneNamePerPerson(heard.Select(line => line.Spelled), about);
        var lines = new List<NpcChatterLine>(heard.Count);

        foreach (var (spelled, text) in heard)
        {
            var name = named[spelled];
            var role = RoleOf(name, about);

            if (MovesTheCarrier(text, role, about) || EscalatesIn(text, kind))
            {
                return [];
            }

            lines.Add(new NpcChatterLine(name, text, role));
        }

        return lines.Count >= (kind == NpcChatterKind.Hail ? 1 : 2) ? lines : [];
    }

    /// <summary>Every invented speaker a slot, and every slot one name (#415).</summary>
    private static IReadOnlyList<NpcChatterLine> ParseSlotted(
        string script,
        NpcChatterKind kind,
        NpcChatterCarrier carrier,
        NpcChatterRoster roster)
    {
        var slotNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var nameSlots = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var lines = new List<NpcChatterLine>();

        foreach (var raw in script.Split('\n'))
        {
            if (lines.Count == MostLines)
            {
                break;
            }

            var split = raw.IndexOf(':', StringComparison.Ordinal);

            if (split <= 0)
            {
                continue;
            }

            var (name, tag) = Tagged(raw[..split]);
            var text = raw[(split + 1)..].Trim().Trim('"');

            if (name.Length is < 2 or > 40 || text.Length == 0 || !FlavourBriefs.MayBeSpoken(text))
            {
                continue;
            }

            var role = RoleOf(name, carrier);
            string? voice = null;

            if (role is null)
            {
                // Somebody already heard here keeps the voice they have, whichever slot they were written into.
                var slot = roster.Slots.FirstOrDefault(slot => Is(name, slot.Name ?? string.Empty))
                    ?? roster.Slots.FirstOrDefault(slot => tag is not null && Is(tag, slot.Tag));

                if (slot is null)
                {
                    continue;
                }

                if (!slotNames.TryGetValue(slot.Tag, out var settled))
                {
                    settled = slot.Name ?? name;

                    if (nameSlots.ContainsKey(settled))
                    {
                        continue;
                    }

                    slotNames[slot.Tag] = settled;
                    nameSlots[settled] = slot.Tag;
                }

                name = settled;
                voice = slot.VoiceId;
            }

            if (MovesTheCarrier(text, role, carrier) || EscalatesIn(text, kind))
            {
                return [];
            }

            lines.Add(new NpcChatterLine(name, text, role, voice));
        }

        return lines.Count >= (kind == NpcChatterKind.Hail ? 1 : 2) ? lines : [];
    }

    /// <summary>A speaker's name and the slot tag written after it in square brackets, if any.</summary>
    private static (string Name, string? Tag) Tagged(string head)
    {
        var trimmed = head.Trim().Trim('*', '-', '#', '"').Trim();
        var open = trimmed.LastIndexOf('[');

        if (open < 0 || !trimmed.EndsWith(']'))
        {
            return (trimmed, null);
        }

        var tag = trimmed[(open + 1)..^1].Trim();

        return (trimmed[..open].Trim().Trim('*', '-', '#', '"').Trim(), tag.Length > 0 ? tag : null);
    }

    /// <summary>One replacement beat, read back the way <see cref="Parse"/> reads a script (#338).</summary>
    /// <param name="role">
    /// The cast role of the line being replaced — its own crew is about it by definition.
    /// </param>
    public static string? Rewritten(string? reply, VoiceRole? role, NpcChatterCarrier? carrier = null)
    {
        if (string.IsNullOrWhiteSpace(reply))
        {
            return null;
        }

        // One line only was asked for; the first that has anything in it is the answer.
        var text = reply
            .Split('\n')
            .Select(raw => raw.Trim())
            .FirstOrDefault(raw => raw.Length > 0);

        if (text is null)
        {
            return null;
        }

        var split = text.IndexOf(':', StringComparison.Ordinal);

        if (split > 0 && text[..split].Trim().Trim('*', '-', '#', '"').Length is >= 2 and <= 40)
        {
            text = text[(split + 1)..].Trim();
        }

        text = text.Trim('"');

        return text.Length > 0 && !MovesTheCarrier(text, role, carrier ?? NpcChatterCarrier.None) && !Escalates(text)
            ? text
            : null;
    }

    /// <summary>
    /// One name per person across an exchange (#256): what each spelling the model wrote is folded
    /// onto.
    /// </summary>
    private static Dictionary<string, string> OneNamePerPerson(
        IEnumerable<string> spellings,
        NpcChatterCarrier carrier)
    {
        var names = spellings.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var named = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var name in names)
        {
            named[name] = name;

            if (RoleOf(name, carrier) is not null)
            {
                continue;
            }

            var longer = names
                .Where(other => other.Length > name.Length
                    && RoleOf(other, carrier) is null
                    && IsAWordOf(name, other))
                .OrderByDescending(other => other.Length)
                .ThenBy(other => other, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (longer.Count > 0 && longer.Skip(1).All(other => IsAWordOf(other, longer[0])))
            {
                named[name] = longer[0];
            }
        }

        return named;
    }

    /// <summary>
    /// Whether <paramref name="shorter"/> is the leading or trailing whole word(s) of <paramref
    /// name="longer"/>.
    /// </summary>
    private static bool IsAWordOf(string shorter, string longer) =>
        longer.StartsWith(shorter + " ", StringComparison.OrdinalIgnoreCase)
        || longer.EndsWith(" " + shorter, StringComparison.OrdinalIgnoreCase);

    /// <summary>Which of the carrier's two posts this speaker is, if either.</summary>
    private static VoiceRole? RoleOf(string name, NpcChatterCarrier carrier)
    {
        if (!carrier.Present)
        {
            return null;
        }

        // The model was told to write the bare word, and mostly does; these are the shapes it reaches for
        // instead when it decorates one — the carrier's own name in front, or the post spelled out.
        var bare = Undecorated(name, carrier);

        if (Is(bare, TowerName) || Is(bare, "Tower Control") || Is(bare, "Control"))
        {
            return VoiceRole.TowerControl;
        }

        return Is(bare, CaptainName) ? VoiceRole.CarrierCaptain : null;
    }

    private static string Undecorated(string name, NpcChatterCarrier carrier)
    {
        var bare = name.Trim();

        foreach (var prefix in new[] { carrier.Called, "Fleet Carrier", "Carrier" })
        {
            if (prefix is { Length: > 0 }
                && bare.Length > prefix.Length
                && bare.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return bare[prefix.Length..].Trim();
            }
        }

        return bare;
    }

    private static bool Is(string name, string post) =>
        string.Equals(name, post, StringComparison.OrdinalIgnoreCase);

    /// <summary>Whether this line has the Commander's carrier going somewhere it is not.</summary>
    private static bool MovesTheCarrier(string text, VoiceRole? role, NpcChatterCarrier carrier)
    {
        if (!carrier.Owned || carrier.JumpScheduled)
        {
            return false;
        }

        var itsOwnCrew = role is VoiceRole.TowerControl or VoiceRole.CarrierCaptain;

        if (!itsOwnCrew
            && !Mentions(text, "carrier")
            && !Mentions(text, carrier.Called))
        {
            return false;
        }

        // Stems, so "jumping" and "departure" are caught by the word they are made of; the third has no stem
        // worth having, so its three shapes are listed.
        return Mentions(text, "jump")
            || Mentions(text, "depart")
            || Mentions(text, "cast off")
            || Mentions(text, "casting off")
            || Mentions(text, "casts off");
    }

    /// <summary>
    /// Whether this line says, threatens or promises something Elite would write a journal event for about
    /// the Commander — a scan, an interdiction, weapons, a fine or bounty, a docking decision, cargo given or
    /// taken, a wing or friend invite. "You" is read as the Commander.
    /// </summary>
    public static bool Escalates(string text) =>
        !string.IsNullOrEmpty(text) && Escalation().IsMatch(text.Replace('’', '\''));

    /// <summary>
    /// <see cref="Escalates"/> for a line of an exchange of <paramref name="kind"/>. In a controller
    /// exchange and a scene "you" is another invented speaker, so there the line must name the Commander as well.
    /// </summary>
    private static bool EscalatesIn(string text, NpcChatterKind kind) =>
        Escalates(text)
        && (kind is not (NpcChatterKind.Controller or NpcChatterKind.Scene) || NamesTheCommander().IsMatch(text));

    private const string Them = @"(?:you|ya|your|yours|yourself|(?:the\s+)?commander(?:'s)?|cmdr)\b";

    private const string TheirOwn = @"(?:your|the\s+commander's)";

    private const string Subject = @"\b(?:you|(?:the\s+)?commander|cmdr)"
        + @"(?:\s*'(?:re|ve|ll|s)\b|\s+(?:are|were|was|is|have|has|had|been|being|be|get|gets|got|getting|gonna|going|to|will|just|now|already|about|not))+\s+";

    private const string DockingDecision =
        @"(?:docking|landing)\s+(?:request\s+|permission\s+|clearance\s+|access\s+)?(?:is\s+|has\s+been\s+)?(?:denied|granted|refused|approved|rejected)";

    [GeneratedRegex(
        // Interdicted
        @"\binterdict(?:s|ed|ing|ion)?\s+(?:on\s+)?" + Them
        + "|" + Subject + @"interdicted\b"
        + @"|\b(?:pull|pulls|pulling|pulled|drag|drags|dragging|dragged|yank\w*|rip|ripping)\s+" + Them + @"\s+(?:out\s+of|from)\s+(?:super\s*cruise|frame\s*shift|the\s+jump)"
        // Scanned
        + @"|\bscan(?:s|ned|ning)?\s+(?:on\s+)?" + Them
        + "|" + Subject + @"scanned\b"
        // UnderAttack
        + @"|\b(?:open(?:s|ed|ing)?\s+fire|fir(?:e|es|ed|ing)|shoot(?:s|ing)?|shot)\s+(?:at|on|upon)\s+" + Them
        + @"|\b(?:shoot|shooting|attack|attacks|attacking|attacked|blast|blasting|kill|killing|destroy|destroying|target|targets|targeting|targetting)\s+" + Them
        + @"|\b(?:weapons|guns|lasers|missiles|torpedoes)\s+(?:are\s+)?(?:locked\s+|trained\s+|hot\s+)?(?:on|onto|at)\s+" + Them
        + @"|\block(?:s|ed|ing)?\s+(?:on|onto|on\s+to)\s+" + Them
        + "|" + Subject + @"(?:under\s+(?:attack|fire)|attacked|targeted|shot\s+at|fired\s+(?:on|upon))"
        // CommitCrime, Bounty
        + @"|\bfin(?:ing|ed)\s+" + Them
        + @"|\bfine\s+" + Them + @"\s+(?:for|\d)"
        + "|" + Subject + @"fined\b"
        + @"|\b(?:a|the|another)\s+fine\s+(?:on|for|to)\s+" + Them
        + @"|\bbounty\s+(?:on|for)\s+" + Them
        + @"|\bprice\s+on\s+" + TheirOwn + @"\s+head"
        + @"|\b" + TheirOwn + @"\s+bounty\b"
        + "|" + Subject + @"(?:a\s+)?bounty\b"
        + @"|\b(?:you|commander|cmdr)(?:\s*'re|\s+are|\s+is)(?:\s+now)?\s+wanted\b"
        // DockingDenied, DockingGranted
        + @"|\b" + TheirOwn + @"\s+(?:docking|landing)\s+(?:request|permission|clearance|access)"
        + "|" + Subject + @"(?:cleared|authori[sz]ed|permitted|allowed|clear)\s+(?:to|for)\s+(?:dock|land|docking|landing|approach)"
        + @"|\b(?:deny|denies|denying|denied|refuse\w*|grant\w*|clear|clearing)\s+" + Them + @"\s+(?:for\s+)?(?:docking|landing|a\s+pad|to\s+(?:dock|land))"
        + @"|\b" + DockingDecision + @"\s+(?:for|to)\s+" + Them
        + @"|\b(?:commander|cmdr)\b[^.!?]{0,24}?\b" + DockingDecision
        // CollectCargo, EjectCargo
        + @"|\b(?:drop|drops|dropping|dump|dumping|jettison\w*|eject\w*|hand\w*\s+over|surrender\w*|give\s+(?:me|us)|turn\w*\s+over)\s+" + TheirOwn + @"\s+(?:cargo|hold|goods|canisters?|cans|load|haul)"
        + @"|\b(?:take|taking|took|steal\w*|stole|grab\w*|scoop\w*|seiz\w*|confiscat\w*|impound\w*)\s+" + TheirOwn + @"\s+(?:cargo|goods|canisters?|cans|load|haul)"
        + @"|\b(?:drop|dropping|dropped|eject\w*|jettison\w*|leave|leaving|left|give|giving|gave|send|sending|sent)\s+(?:you|(?:the\s+)?commander|cmdr)\s+(?:some\s+|a\s+few\s+|a\s+couple\s+(?:of\s+)?|a\s+|the\s+)?(?:cargo|canisters?|cans|goods|crates?)"
        + @"|\b(?:cargo|canisters?|cans|goods|crates?)\s+(?:for|to)\s+(?:you|(?:the\s+)?commander|cmdr)\b"
        // WingInvite, Friends
        + @"|\b(?:wing|friend|friends|squadron)\s+(?:invite|invites|invitation|request)\s+(?:to|for)\s+" + Them
        + @"|\b" + TheirOwn + @"\s+(?:wing|friend)\s+(?:invite|invitation|request)"
        + @"|\b(?:send|sends|sending|sent|ping\w*)\s+" + Them + @"\s+an?\s+(?:wing|friend|squadron)\s+(?:invite|invitation|request)"
        + @"|\b(?:invite|invites|inviting|invited|add|adding|added)\s+" + Them + @"\s+(?:to\s+(?:my|our|the)\s+(?:wing|friends?\s+list)|as\s+(?:a\s+)?friend)"
        + @"|\bjoin\s+(?:my|our)\s+wing\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Escalation();

    [GeneratedRegex(@"\b(?:commander|cmdr)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex NamesTheCommander();

    private static bool Mentions(string text, string? word) =>
        word is { Length: > 0 } && text.Contains(word, StringComparison.OrdinalIgnoreCase);
}
