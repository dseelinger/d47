using System.Text;

namespace D47.Core.Debrief;

/// <summary>
/// The debrief pass: a session's spoken corrections, turned into proposed standing directions
/// (<a href="https://github.com/dseelinger/d47/issues/162">#162</a>).
/// <para>
/// <b>Local and deterministic, with no model in it, and that is the issue's own requirement rather
/// than a shortcut.</b> The design says the loop is personal and local, always, and that nothing
/// leaves the machine at all — and a pass that sent an evening's transcript to a provider to be
/// summarised would be the largest single egress d47 has ever made, on a payload the Commander
/// never chose to send. So the extraction is arithmetic over words. It buys three things beyond
/// the privacy: it costs nothing to run, it cannot hallucinate a direction the Commander never
/// said, and it is testable line by line.
/// </para>
/// <para>
/// <b>What it gives up is real and is stated rather than hidden.</b> A rephrase after a wrong
/// answer — the third example in the issue — is not detectable this way: it looks like an ordinary
/// question. Cue-matching catches the explicit corrections, the implicit signals catch two patterns
/// nobody typed, and the rest is not caught. A pass that quietly claimed to catch everything would
/// be worse than one that names its own edge.
/// </para>
/// <para>
/// <b>Only <see cref="DebriefSpeaker.Commander"/> lines are read.</b> The record holds all three
/// voices so a reader can check a proposal against what provoked it, and
/// <see cref="DebriefSpeaker.Game"/> — an in-game message, a journal line, a quoted web result — is
/// skipped at the first filter here. That is the poisoning defence at extraction, and the adoption
/// gate stands behind it regardless: nothing this drafts reaches a prompt without a person taking
/// it.
/// </para>
/// <para>
/// <b>The Commander's own words, tidied and never reinterpreted.</b> A drafted direction is their
/// sentence with the address stripped and a full stop added — so the review pane can show the exact
/// text that would enter the prompt and a Commander can tell at a glance whether d47 understood
/// them. Anything cleverer would be a paraphrase presented as a quote.
/// </para>
/// </summary>
public static class DebriefExtractor
{
    /// <summary>
    /// How many times a pattern has to repeat before it is worth a question. One is noise — a
    /// Commander cuts a sentence off because something happened, not because d47 is too talkative
    /// — and three in one session is a habit.
    /// </summary>
    public const int SignalThreshold = 3;

    /// <summary>
    /// The most one pass may draft. A review pane with forty things in it is one nobody works
    /// through, and the newest corrections are the ones still worth acting on.
    /// </summary>
    public const int MaxProposals = 12;

    /// <summary>
    /// The longest utterance a direction may be drafted from. A standing direction is a sentence;
    /// a paragraph that happens to contain the word "stop" is a Commander thinking out loud.
    /// </summary>
    public const int MaxSourceLength = 200;

    /// <summary>The fewest words. "Stop" on its own is an interruption, not an instruction.</summary>
    private const int MinimumWords = 3;

    /// <summary>How far into an utterance a one-word cue still counts as the Commander leading with it.</summary>
    private const int LeadingWords = 4;

    /// <summary>
    /// Cues that mean a standing instruction wherever they appear. Every one of them is a phrase
    /// rather than a word, which is what makes position irrelevant: nobody says "from now on" in
    /// passing.
    /// </summary>
    private static readonly string[] Anywhere =
    [
        "from now on",
        "in future",
        "in the future",
        "stop calling",
        "don't call",
        "do not call",
        "stop saying",
        "don't say",
        "do not say",
        "no more",
    ];

    /// <summary>
    /// Cues that only count when the Commander led with them. Single words, and every one of them
    /// turns up harmlessly in the middle of an ordinary sentence — <em>I always fly solo</em> is not
    /// an instruction, and <em>less than an hour</em> is not either. Leading with one is what makes
    /// it a correction, and it is how people actually give them.
    /// </summary>
    private static readonly string[] Leading =
    [
        "stop",
        "don't",
        "do not",
        "never",
        "shorter",
        "quit",
    ];

    /// <summary>
    /// What a Commander puts in front of an instruction before the instruction starts. Stripped
    /// before the cues are looked for, so <em>hey d47, stop calling it that</em> leads with "stop"
    /// exactly as <em>stop calling it that</em> does.
    /// </summary>
    private static readonly string[] Address =
    [
        "d47",
        "hey",
        "ok",
        "okay",
        "no",
        "um",
        "uh",
        "look",
        "listen",
        "please",
        "and",
        "so",
        "well",
    ];

    /// <summary>
    /// Runs the pass.
    /// </summary>
    /// <param name="lines">The session, all three voices. Only the Commander's are read.</param>
    /// <param name="signals">Patterns nobody put into words. These become questions, never directions.</param>
    /// <param name="known">
    /// Everything already in the file for this Commander — adopted, waiting <em>and declined</em>.
    /// Declined entries are the reason this parameter is not just "what is live": the pass is
    /// deterministic, so a direction the Commander turned down would be redrafted from the same
    /// sentence every session, and a pane that keeps re-offering a refusal is one nobody opens
    /// twice.
    /// </param>
    /// <param name="now">When the pass ran. Supplied, because Core reads no clock.</param>
    /// <param name="saidUnder">Which core was aboard, recorded so the pane can offer "just for this core".</param>
    /// <param name="addressedAs">
    /// What the Commander calls d47 in this installation — the core's name, the ship name — so that
    /// those are stripped as address rather than read as part of the instruction.
    /// </param>
    public static IReadOnlyList<StandingDirection> Extract(
        IReadOnlyList<DebriefLine> lines,
        IReadOnlyList<DebriefSignal> signals,
        IReadOnlyCollection<StandingDirection> known,
        DateTimeOffset now,
        string? saidUnder = null,
        IReadOnlyCollection<string>? addressedAs = null)
    {
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentNullException.ThrowIfNull(signals);
        ArgumentNullException.ThrowIfNull(known);

        var taken = known.Select(entry => entry.Key).ToHashSet(StringComparer.Ordinal);
        var seen = known.Select(entry => Normalise(entry.Text)).ToHashSet(StringComparer.Ordinal);

        var drafted = new List<StandingDirection>();

        foreach (var (line, text) in Corrections(lines, addressedAs).TakeLast(MaxProposals))
        {
            var normalised = Normalise(text);

            if (!seen.Add(normalised))
            {
                continue;
            }

            var key = DirectionKeys.Next(DirectionKeys.DraftedPrefix, taken);
            taken.Add(key);

            drafted.Add(new StandingDirection(key, text)
            {
                State = DirectionState.Proposed,
                Kind = DirectionKind.Direction,
                Because = line.Text,
                SaidUnder = saidUnder,
                Clip = line.Clip,
                ProposedAt = now,
            });
        }

        foreach (var question in Questions(signals))
        {
            if (!seen.Add(Normalise(question.Text)))
            {
                continue;
            }

            var key = DirectionKeys.Next(DirectionKeys.AskedPrefix, taken);
            taken.Add(key);

            drafted.Add(question with { Key = key, SaidUnder = saidUnder, ProposedAt = now });
        }

        return drafted;
    }

    /// <summary>
    /// The Commander's corrections, in the order they were said, each with the sentence a direction
    /// would be drafted from.
    /// <para>
    /// The first filter is the poisoning defence and it is one line: anything that is not the
    /// Commander's own voice never reaches the cues.
    /// </para>
    /// </summary>
    private static IEnumerable<(DebriefLine Line, string Text)> Corrections(
        IReadOnlyList<DebriefLine> lines,
        IReadOnlyCollection<string>? addressedAs)
    {
        foreach (var line in lines.Where(line => line.Who == DebriefSpeaker.Commander))
        {
            if (Draft(line.Text, addressedAs) is { } text)
            {
                yield return (line, text);
            }
        }
    }

    /// <summary>
    /// One utterance turned into the exact text a direction would carry, or null where it is not a
    /// correction at all. Pure and total, which is what makes the cue rules checkable one at a time.
    /// </summary>
    public static string? Draft(string utterance, IReadOnlyCollection<string>? addressedAs = null)
    {
        if (string.IsNullOrWhiteSpace(utterance) || utterance.Length > MaxSourceLength)
        {
            return null;
        }

        var original = utterance.Trim();
        var stripped = StripAddress(original, addressedAs);

        if (stripped.Length == 0)
        {
            return null;
        }

        // Stripping can take the cue with it, and "no more speeches about that" is the case that
        // matters: "no" is address in front of an instruction and is the first word of one here.
        // Rather than teaching the address list about the cue list — two lists that would have to
        // be kept in step — the strip is simply undone where it destroyed the thing being looked
        // for. The Commander's own sentence is what gets drafted either way.
        if (!Cued(Words(stripped)) && Cued(Words(original)))
        {
            stripped = original;
        }

        // A question is a question. "Don't you think that is a long way?" carries a cue and is not
        // an instruction, and there is no reading of a trailing question mark that makes it one.
        if (stripped.EndsWith('?'))
        {
            return null;
        }

        var words = Words(stripped);

        if (words.Length < MinimumWords)
        {
            return null;
        }

        if (!Cued(words))
        {
            return null;
        }

        return Sentence(stripped);
    }

    /// <summary>Whether the words carry a cue, by the two rules the cue lists document.</summary>
    private static bool Cued(string[] words)
    {
        var whole = " " + string.Join(' ', words) + " ";

        if (Anywhere.Any(cue => whole.Contains($" {cue} ", StringComparison.Ordinal)))
        {
            return true;
        }

        var lead = " " + string.Join(' ', words.Take(LeadingWords)) + " ";

        return Leading.Any(cue => lead.Contains($" {cue} ", StringComparison.Ordinal));
    }

    /// <summary>
    /// Takes the address off the front — "hey d47," and whatever else this installation answers to
    /// — one word at a time, so a stacked one comes off too.
    /// </summary>
    private static string StripAddress(string text, IReadOnlyCollection<string>? addressedAs)
    {
        var names = addressedAs is { Count: > 0 }
            ? addressedAs.Select(name => name.Trim().ToLowerInvariant()).Where(name => name.Length > 0).ToArray()
            : [];

        while (true)
        {
            var span = text.AsSpan();
            var end = 0;

            while (end < span.Length && !char.IsWhiteSpace(span[end]))
            {
                end++;
            }

            if (end == 0)
            {
                return text;
            }

            var first = span[..end].ToString().Trim(',', '.', '!', ':', ';').ToLowerInvariant();

            if (!Address.Contains(first, StringComparer.Ordinal) && !names.Contains(first, StringComparer.Ordinal))
            {
                return text;
            }

            var rest = text[end..].TrimStart(' ', ',', '.', '!', ':', ';', '\t');

            // The whole utterance was address. "Hey d47" is not an instruction, and returning the
            // empty string here is what says so.
            if (rest.Length == 0)
            {
                return string.Empty;
            }

            text = rest;
        }
    }

    /// <summary>
    /// The utterance as one written sentence: a capital at the front, a full stop at the back, and
    /// nothing else touched. Clamped, because the store clamps and a proposal that showed one text
    /// and stored another would break the one promise the review pane makes.
    /// </summary>
    private static string Sentence(string text)
    {
        var built = new StringBuilder(text.Length + 1);

        built.Append(char.ToUpperInvariant(text[0])).Append(text.AsSpan(1));

        if (built.Length > 0 && built[^1] is not ('.' or '!' or '?'))
        {
            built.Append('.');
        }

        var sentence = built.ToString();

        return sentence.Length > StandingDirection.MaxText
            ? sentence[..StandingDirection.MaxText].TrimEnd() + "…"
            : sentence;
    }

    /// <summary>
    /// The questions the implicit signals earn. <b>Questions and never directions</b> — the whole
    /// of the second refinement in the issue, and the reason a suggestion is offered rather than
    /// applied.
    /// </summary>
    private static IEnumerable<StandingDirection> Questions(IReadOnlyList<DebriefSignal> signals)
    {
        var grouped = signals
            .Where(signal => !string.IsNullOrWhiteSpace(signal.What))
            .GroupBy(signal => (signal.Kind, What: signal.What.Trim()))
            .Select(group => (group.Key.Kind, group.Key.What, Count: group.Sum(signal => signal.Count)))
            .Where(group => group.Count >= SignalThreshold)
            .OrderBy(group => group.What, StringComparer.Ordinal);

        foreach (var (kind, what, _) in grouped)
        {
            // The count is deliberately not in the text. It changes every session, and a question
            // whose wording moved would be re-proposed after the Commander had already answered it.
            yield return kind switch
            {
                DebriefSignalKind.SpeechCutOff => new StandingDirection(
                    DirectionKeys.AskedPrefix,
                    $"You cut me off repeatedly while I was speaking — {what}. Shorter answers there?")
                {
                    Kind = DirectionKind.Question,
                    Suggested = "Keep answers to a sentence or two unless I ask for more.",
                },

                _ => new StandingDirection(
                    DirectionKeys.AskedPrefix,
                    $"{Capitalised(what)} was switched off within seconds of firing, more than once. "
                    + "Is it firing too eagerly?")
                {
                    Kind = DirectionKind.Question,

                    // No suggestion, and that is honest rather than lazy: the answer to this one is
                    // a threshold on a settings row, and the debrief writes one file that is not
                    // that row. If it is worth a direction, the Commander writes the direction.
                    Suggested = null,
                },
            };
        }
    }

    private static string Capitalised(string text) =>
        text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];

    private static string[] Words(string text) =>
        Flatten(text).Split(' ', StringSplitOptions.RemoveEmptyEntries);

    /// <summary>
    /// What two utterances have to share to be the same one: letters, digits and single spaces.
    /// Punctuation and case are how the same instruction said twice looks different.
    /// </summary>
    private static string Normalise(string text) => Flatten(text).Trim();

    private static string Flatten(string text)
    {
        var built = new StringBuilder(text.Length);
        var space = true;

        foreach (var character in text)
        {
            if (char.IsLetterOrDigit(character) || character == '\'')
            {
                built.Append(char.ToLowerInvariant(character));
                space = false;
                continue;
            }

            if (!space)
            {
                built.Append(' ');
                space = true;
            }
        }

        return built.ToString().TrimEnd();
    }
}
