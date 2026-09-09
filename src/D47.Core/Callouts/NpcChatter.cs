using D47.Core.Audio;
using D47.Core.Journal;

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
}

/// <summary>One parsed line of an exchange: who says it, what they say, and whose voice it is.</summary>
/// <param name="Role">
/// The cast role this line belongs to, or null for the invented nobody that every line used to be.
/// </param>
public sealed record NpcChatterLine(string Name, string Text, VoiceRole? Role = null);

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
/// Invented background radio traffic (#244): made-up conversations between people who do not exist —
/// never the game's own NPC messages, which arrive through <see cref="IncomingMessages"/> and are
/// somebody else's words.
/// </summary>
public static class NpcChatter
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
        + "or a model, and never break the fiction.";

    /// <summary>The format contract every kind shares.</summary>
    private const string Contract =
        "Write only the exchange, one line per speaker turn, each formatted exactly as "
        + "Name: words — an invented plain name or call sign, a colon, what they say. A speaker "
        + "keeps one name, written the same way on every line of theirs. No other "
        + "text, no quotation marks, no stage directions. Use the live game state only for where "
        + "this is happening; invent everything else. Never name or imitate a real person or "
        + "another player. Nobody asks the Commander to do anything, nobody asks the Commander a "
        + "question, and nobody expects an answer.";

    /// <summary>
    /// What the model is asked for one exchange of the given kind, in the situation the Commander's own
    /// carrier is in.
    /// </summary>
    public static string Instruction(NpcChatterKind kind, NpcChatterCarrier? carrier = null)
    {
        var about = carrier ?? NpcChatterCarrier.None;

        return Scene(kind, about) + Contract + Carrier(about);
    }

    private static string Scene(NpcChatterKind kind, NpcChatterCarrier carrier) => kind switch
    {
        // Docked is the only situation this pairing fires in, and while the Commander is at their own carrier
        // the only thing they can be docked at is that carrier — so the controller is named rather than left
        // for the model to guess at.
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
    /// The two rules the Commander's own carrier adds (#249): who its two posts are when he is at it,
    /// and that it is not going anywhere when it is not.
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

    /// <summary>The reply, read strictly.</summary>
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

            if (MovesTheCarrier(text, role, about))
            {
                return [];
            }

            lines.Add(new NpcChatterLine(name, text, role));
        }

        return lines.Count >= (kind == NpcChatterKind.Hail ? 1 : 2) ? lines : [];
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

        return text.Length > 0 && !MovesTheCarrier(text, role, carrier ?? NpcChatterCarrier.None)
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

    private static bool Mentions(string text, string? word) =>
        word is { Length: > 0 } && text.Contains(word, StringComparison.OrdinalIgnoreCase);
}
