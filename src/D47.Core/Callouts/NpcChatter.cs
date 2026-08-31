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

/// <summary>One parsed line of an exchange: who says it, and what they say.</summary>
public sealed record NpcChatterLine(string Name, string Text);

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
/// The prose here is what the model is asked; the mechanics — when, how often, which pairing —
/// live in <see cref="NpcChatterCallout"/>, where the replay harness can drive them.
/// </para>
/// </summary>
public static class NpcChatter
{
    public const string KeyPrefix = "npc.chatter.";

    /// <summary>The most lines one exchange may carry, however many the model writes.</summary>
    public const int MostLines = 4;

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

    /// <summary>What the model is asked for one exchange of the given kind.</summary>
    public static string Instruction(NpcChatterKind kind) => kind switch
    {
        NpcChatterKind.Controller =>
            "An invented pilot and the controller of the station or carrier where the Commander "
            + "is docked exchange 2 to 4 short lines of routine traffic — clearances, pad "
            + "assignments, a telling-off. Procedure with a human edge. The Commander is not "
            + "part of it. " + Contract,

        NpcChatterKind.Hail =>
            "One invented person nearby says one or two lines to the Commander over the open "
            + "channel — a compliment on the ship, a grumble about the queue, a rumour heard in "
            + "the bar. Statements only: they are not starting a conversation. " + Contract,

        _ =>
            "Two invented people near the Commander — crews on the local channel, a courier and "
            + "a dock hand — exchange 2 to 4 short lines about their own small business: cargo, "
            + "shifts, prices, a ship acting up. The Commander is not part of it, and is not "
            + "mentioned beyond perhaps being noticed in passing. " + Contract,
    };

    /// <summary>
    /// The reply, read strictly. Lines that do not parse are dropped; lines that fail
    /// <see cref="FlavourBriefs.MayBeSpoken"/> are dropped; at most <see cref="MostLines"/>
    /// survive. <b>Too few is nothing at all</b>: a hail is one line, but a one-line
    /// "conversation" between two people is a fragment, and silence beats a fragment — the same
    /// judgement the ambient drop makes (#245).
    /// </summary>
    public static IReadOnlyList<NpcChatterLine> Parse(string? script, NpcChatterKind kind)
    {
        if (string.IsNullOrWhiteSpace(script))
        {
            return [];
        }

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

            lines.Add(new NpcChatterLine(name, text));
        }

        return lines.Count >= (kind == NpcChatterKind.Hail ? 1 : 2) ? lines : [];
    }
}
