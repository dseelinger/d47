using D47.Core.Audio;
using D47.Core.Journal;

namespace D47.Core.Callouts;

/// <summary>Which pairing an overheard exchange is (#244).</summary>
public enum NpcChatterKind
{
    /// <summary>Two invented people near the Commander, talking to each other.</summary>
    Passersby,

    /// <summary>An invented pilot and the controller of the station or carrier the Commander is docked at.</summary>
    Controller,

    /// <summary>One invented person saying a line or two to the Commander. Statements only — no reply is expected.</summary>
    Hail,
}

/// <summary>One parsed line of an exchange: who says it, what they say, and whose voice it is.</summary>
/// <param name="Role">
/// The cast role this line belongs to, or null for the invented nobody that every line used to
/// be. Set only for the Commander's own carrier's two posts (#249) — see
/// <see cref="NpcChatterCarrier"/>.
/// </param>
public sealed record NpcChatterLine(string Name, string Text, VoiceRole? Role = null);

/// <summary>
/// What d47 knows about the Commander's own fleet carrier while an exchange is composed
/// (<a href="https://github.com/dseelinger/d47/issues/249">#249</a>).
/// <para>
/// Two facts, and each answers a fault the Commander reported. <see cref="Present"/> decides
/// whether the tower and the captain in an exchange are <em>his</em> tower and captain — the
/// two people he has already cast a voice for — or a station's staff, who are invented nobodies
/// like everybody else. <see cref="JumpScheduled"/> decides whether anybody may talk about the
/// carrier going anywhere.
/// </para>
/// </summary>
public sealed record NpcChatterCarrier
{
    /// <summary>No carrier in the picture: the Commander does not own one, or none is known yet.</summary>
    public static readonly NpcChatterCarrier None = new();

    /// <summary>Whether the Commander owns a carrier at all. Nothing below matters when false.</summary>
    public bool Owned { get; init; }

    /// <summary>
    /// Whether the Commander is at it — set down on its deck, or sharing the space around it.
    /// <para>
    /// <b>Docked at anything else disqualifies it</b>, and that is the whole reason this is not
    /// just "same system". A carrier parked over Jameson Memorial while the Commander sits on a
    /// pad inside the station is near his carrier and being talked to by the station's tower;
    /// casting that tower as his own would put his carrier's voice in a stranger's mouth, which
    /// is the fault this exists to fix rather than a second helping of it.
    /// </para>
    /// </summary>
    public bool Present { get; init; }

    /// <summary>What to call it out loud: the name the Commander gave it, or the callsign.</summary>
    public string? Called { get; init; }

    /// <summary>Whether it actually has a jump scheduled — <see cref="CarrierState.JumpScheduled"/>.</summary>
    public bool JumpScheduled { get; init; }

    /// <summary>
    /// Read from the two parts of the game state that say it, rather than from the whole: both
    /// are records a test can build, and neither can be set on a
    /// <see cref="CommanderGameState"/> from outside the fold.
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

        // On its deck, which is the case the Commander named first. Checked on its own rather
        // than through the system match, because the deck is a fact about where the ship is
        // parked and the carrier's own system can be a jump behind.
        var onItsDeck = here.AtCarrier && IsMine(here, carrier);

        // Otherwise: the same system, out of the chair-bound modes where nothing is overheard
        // from anybody nearby, and not sitting inside somebody else's station.
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

    /// <summary>
    /// Whether the station the Commander is docked at is their own carrier. The market id is the
    /// carrier id for a carrier and is the answer when it is known; the three names are the same
    /// three <see cref="IncomingMessages"/> matches a sender against, so a carrier is identified
    /// here by exactly what identifies it there.
    /// </summary>
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
/// Invented background radio traffic (#244): made-up conversations between people who do not
/// exist — never the game's own NPC messages, which arrive through <see cref="IncomingMessages"/>
/// and are somebody else's words.
/// <para>
/// <b>The whole feature is theatre, and three rules keep it honest.</b> It is model-written or it
/// is nothing (#245) — there are no authored stand-in conversations, so the callout emits a
/// marker the app composes from, never a line to speak. It is one-way — nobody answers, the
/// Commander is never asked anything, and no line enters the conversation history or the comms
/// record. And an invented speaker is an invented <em>nobody</em>: the instruction forbids real
/// people and other players by name, and every line still passes
/// <see cref="FlavourBriefs.MayBeSpoken"/> on the way out.
/// </para>
/// <para>
/// <b>The Commander's own carrier is the one exception to that third rule</b>
/// (<a href="https://github.com/dseelinger/d47/issues/249">#249</a>), and it is an exception
/// because of the rule rather than despite it. Its tower and its captain are not invented
/// nobodies — they are two posts the Commander has already cast a voice for, and an exchange
/// that puts one of them on the radio in a pooled stranger's voice is the same person speaking
/// twice in two voices. So while the Commander is at their carrier the two posts are named
/// exactly, and <see cref="Parse"/> hands their lines back with the role that carries the cast
/// voice. Everybody else in the exchange is still nobody.
/// </para>
/// <para>
/// The prose here is what the model is asked; the mechanics — when, how often, which pairing —
/// live in <see cref="NpcChatterCallout"/>, where the replay harness can drive them.
/// </para>
/// </summary>
public static class NpcChatter
{
    public const string KeyPrefix = "npc.chatter.";

    /// <summary>The most lines one exchange may carry, however many the model writes.</summary>
    public const int MostLines = 4;

    /// <summary>The exact name the carrier's tower controller speaks under, and only it (#249).</summary>
    public const string TowerName = "Tower";

    /// <summary>And its captain's.</summary>
    public const string CaptainName = "Captain";

    /// <summary>
    /// The kind is read back off the key, the same way the ambient situation is: the callout has
    /// moved on by the time the app composes, and the key is the only thing that travelled.
    /// </summary>
    public static NpcChatterKind KindOf(string key) =>
        Enum.TryParse<NpcChatterKind>(key[KeyPrefix.Length..], ignoreCase: true, out var kind)
            ? kind
            : NpcChatterKind.Passersby;

    /// <summary>
    /// The persona-slot framing. A scriptwriter rather than a character, because no invented
    /// speaker is the core aboard — handing a Guardian core a dock worker's lines would put it
    /// in two places at once, which is the same rule the carrier's two roles follow.
    /// </summary>
    public const string Speaker =
        "You write background radio traffic overheard in the Elite Dangerous galaxy in 3311: "
        + "short exchanges between minor invented characters — freighter crews, couriers, dock "
        + "hands, controllers. Plain working speech, brief and human. Never mention being an AI "
        + "or a model, and never break the fiction.";

    /// <summary>
    /// The format contract every kind shares. Exact, because the reply is parsed rather than
    /// spoken whole: a line that does not read <c>Name: words</c> is dropped without ceremony.
    /// </summary>
    private const string Contract =
        "Write only the exchange, one line per speaker turn, each formatted exactly as "
        + "Name: words — an invented plain name or call sign, a colon, what they say. No other "
        + "text, no quotation marks, no stage directions. Use the live game state only for where "
        + "this is happening; invent everything else. Never name or imitate a real person or "
        + "another player. Nobody asks the Commander to do anything, nobody asks the Commander a "
        + "question, and nobody expects an answer.";

    /// <summary>
    /// What the model is asked for one exchange of the given kind, in the situation the
    /// Commander's own carrier is in.
    /// </summary>
    public static string Instruction(NpcChatterKind kind, NpcChatterCarrier? carrier = null)
    {
        var about = carrier ?? NpcChatterCarrier.None;

        return Scene(kind, about) + Contract + Carrier(about);
    }

    private static string Scene(NpcChatterKind kind, NpcChatterCarrier carrier) => kind switch
    {
        // Docked is the only situation this pairing fires in, and while the Commander is at
        // their own carrier the only thing they can be docked at is that carrier — so the
        // controller is named rather than left for the model to guess at.
        NpcChatterKind.Controller when carrier.Present =>
            "An invented pilot and the tower controller aboard the Commander's own fleet carrier "
            + $"{Called(carrier)}exchange 2 to 4 short lines of routine traffic — clearances, pad "
            + "assignments, a telling-off. Procedure with a human edge. The Commander is not "
            + "part of it. ",

        NpcChatterKind.Controller =>
            "An invented pilot and the controller of the station or carrier where the Commander "
            + "is docked exchange 2 to 4 short lines of routine traffic — clearances, pad "
            + "assignments, a telling-off. Procedure with a human edge. The Commander is not "
            + "part of it. ",

        NpcChatterKind.Hail =>
            "One invented person nearby says one or two lines to the Commander over the open "
            + "channel — a compliment on the ship, a grumble about the queue, a rumour heard in "
            + "the bar. Statements only: they are not starting a conversation. ",

        _ =>
            "Two invented people near the Commander — crews on the local channel, a courier and "
            + "a dock hand — exchange 2 to 4 short lines about their own small business: cargo, "
            + "shifts, prices, a ship acting up. The Commander is not part of it, and is not "
            + "mentioned beyond perhaps being noticed in passing. ",
    };

    /// <summary>
    /// The two rules the Commander's own carrier adds (#249): who its two posts are when he is
    /// at it, and that it is not going anywhere when it is not.
    /// <para>
    /// <b>The second holds whether or not he is there.</b> The live game state names the carrier
    /// wherever it is parked, so an exchange can invent a departure for one three hundred light
    /// years away exactly as easily as for the one overhead.
    /// </para>
    /// </summary>
    private static string Carrier(NpcChatterCarrier carrier)
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
                + $"{TowerName} or exactly {CaptainName}, with nothing else in it, and no other "
                + "speaker may use those two names.";
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

    /// <summary>The carrier's name and a trailing space, or nothing when it has no name to give.</summary>
    private static string Called(NpcChatterCarrier carrier) =>
        carrier.Called is { Length: > 0 } name ? $"{name} " : string.Empty;

    /// <summary>
    /// The reply, read strictly. Lines that do not parse are dropped; lines that fail
    /// <see cref="FlavourBriefs.MayBeSpoken"/> are dropped; at most <see cref="MostLines"/>
    /// survive. <b>Too few is nothing at all</b>: a hail is one line, but a one-line
    /// "conversation" between two people is a fragment, and silence beats a fragment — the same
    /// judgement the ambient drop makes (#245).
    /// <para>
    /// <b>A line that moves the Commander's carrier takes the whole exchange with it</b> (#249),
    /// where an unsayable line only takes itself. The difference is what each one is: a refusal
    /// is a line that cannot be spoken, and dropping it leaves an exchange that still makes
    /// sense, while a fabricated departure is the <em>subject</em> of the scene around it — the
    /// replies to it are about it, and keeping them is keeping the lie with the evidence
    /// removed.
    /// </para>
    /// </summary>
    public static IReadOnlyList<NpcChatterLine> Parse(
        string? script,
        NpcChatterKind kind,
        NpcChatterCarrier? carrier = null)
    {
        if (string.IsNullOrWhiteSpace(script))
        {
            return [];
        }

        var about = carrier ?? NpcChatterCarrier.None;
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

            var name = raw[..split].Trim().Trim('*', '-', '#', '"');
            var text = raw[(split + 1)..].Trim().Trim('"');

            if (name.Length is < 2 or > 40 || text.Length == 0 || !FlavourBriefs.MayBeSpoken(text))
            {
                continue;
            }

            var role = RoleOf(name, about);

            if (MovesTheCarrier(text, role, about))
            {
                return [];
            }

            lines.Add(new NpcChatterLine(name, text, role));
        }

        return lines.Count >= (kind == NpcChatterKind.Hail ? 1 : 2) ? lines : [];
    }

    /// <summary>
    /// Which of the carrier's two posts this speaker is, if either. Only while the Commander is
    /// at their own carrier: everywhere else a controller called "Tower" is a station's, and
    /// giving it the cast voice would be the reported fault pointed the other way.
    /// </summary>
    private static VoiceRole? RoleOf(string name, NpcChatterCarrier carrier)
    {
        if (!carrier.Present)
        {
            return null;
        }

        // The model was told to write the bare word, and mostly does; these are the shapes it
        // reaches for instead when it decorates one — the carrier's own name in front, or the
        // post spelled out. Anything else is a person: "Captain Reyes" is a pilot with a rank,
        // not the captain of this ship, and does not match.
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

    /// <summary>
    /// Whether this line has the Commander's carrier going somewhere it is not. Two halves, both
    /// required: it has to be about that carrier, and it has to move it.
    /// <para>
    /// Its own tower and captain are about it by definition — "we cast off in twenty" names
    /// nothing and is still the lie. Everybody else has to say which carrier they mean, which is
    /// what keeps a freighter crew free to talk about their own jump.
    /// </para>
    /// </summary>
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

        // Stems, so "jumping" and "departure" are caught by the word they are made of; the third
        // has no stem worth having, so its three shapes are listed.
        return Mentions(text, "jump")
            || Mentions(text, "depart")
            || Mentions(text, "cast off")
            || Mentions(text, "casting off")
            || Mentions(text, "casts off");
    }

    private static bool Mentions(string text, string? word) =>
        word is { Length: > 0 } && text.Contains(word, StringComparison.OrdinalIgnoreCase);
}
