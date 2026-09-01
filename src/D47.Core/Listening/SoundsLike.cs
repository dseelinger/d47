namespace D47.Core.Listening;

/// <summary>One thing the transcriber gets wrong, and what the Commander said it was.</summary>
/// <param name="Heard">The token as it came out of the transcriber. Matched case-insensitively.</param>
/// <param name="Meant">What it is replaced with.</param>
/// <param name="LearnedAt">When the Commander confirmed it, off the journal or the turn.</param>
public sealed record SoundsLikeEntry(string Heard, string Meant, DateTimeOffset LearnedAt);

/// <summary>
/// What this Commander's transcriber reliably gets wrong, and what they meant
/// (<a href="https://github.com/dseelinger/d47/issues/134">#134</a>).
/// <para>
/// <b>Recorded against the word, not against the answer</b> — which is the whole design, and the
/// Commander's own instruction. An alias held against the <em>system</em> fixes one question; an
/// alias held against the <em>token</em> fixes <i>"how far is Eurebia"</i>, <i>"the Eurebia Blue
/// Mafia"</i> and every other sentence that token ever turns up in.
/// </para>
/// <para>
/// <b>Never learned on a guess, because a wrong alias is worse than the mishearing it fixes.</b>
/// A mishearing is one failed question the Commander can see; a wrong alias is permanent,
/// invisible, and quietly rewrites every future utterance containing that token. So nothing is
/// captured until a correction has actually resolved to something real.
/// </para>
/// <para>
/// Three rules fall out of that, and all three are enforced in <see cref="MayLearn"/> rather than
/// remembered by a caller:
/// </para>
/// <list type="bullet">
///   <item>
///     <b>Never alias a word that already means something.</b> If the token names a place the
///     Commander has met, or a phrase the keyword router answers, it is not a mishearing.
///     <c>Eurebia</c> is safe to capture precisely because it is not a word.
///   </item>
///   <item>
///     <b>The Commander's own words only.</b> This learns from what they said and confirmed, never
///     from journal text, an in-game message or anything another player wrote.
///   </item>
///   <item>
///     <b>One token, not a phrase.</b> A multi-word alias is a rewrite rule, and a rewrite rule
///     applied to every future sentence is a much larger promise than this is making.
///   </item>
/// </list>
/// </summary>
public sealed record SoundsLike
{
    /// <summary>
    /// How many corrections one Commander may accumulate. A transcriber has a handful of words it
    /// reliably mangles; a table growing past this is evidence of something else going wrong, and
    /// an unbounded rewrite table applied to every utterance is not a thing to own.
    /// </summary>
    public const int Limit = 200;

    public static readonly SoundsLike Empty = new();

    /// <summary>Newest first, which is the order a Commander reading the row wants them in.</summary>
    public IReadOnlyList<SoundsLikeEntry> Entries { get; init; } = [];

    public bool IsKnown => Entries.Count > 0;

    /// <summary>
    /// Whether this token may be captured as a mishearing at all.
    /// <para>
    /// <b>Default-deny, and the caller does not get to skip it.</b> Every reason to refuse is
    /// here: a token that is not a single word, one short enough to be an English word by
    /// accident, one the Commander has already met as a name, and one the keyword router answers
    /// to. A correction that fails any of these is simply not learned — the turn still works, and
    /// nothing is written down that would have to be found and undone later.
    /// </para>
    /// </summary>
    /// <param name="heard">The token the transcriber produced.</param>
    /// <param name="meant">What it should have been.</param>
    /// <param name="known">
    /// Whether a token already names something this Commander has met. Never guessed from the
    /// token's shape: see <see cref="SpokenNames.Knows"/>.
    /// </param>
    /// <param name="reserved">
    /// Whether a token is a word d47's own routing uses. A phrase the keyword router answers to
    /// must keep answering to it, and an alias over one would take a command away silently.
    /// </param>
    public static bool MayLearn(
        string? heard,
        string? meant,
        Func<string, bool> known,
        Func<string, bool> reserved)
    {
        ArgumentNullException.ThrowIfNull(known);
        ArgumentNullException.ThrowIfNull(reserved);

        if (Token(heard) is not { } from || Token(meant) is not { } to)
        {
            return false;
        }

        // Nothing to learn, and a self-alias applied forever would be a rule that does nothing at
        // a cost that is not nothing.
        if (string.Equals(from, to, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // **Short tokens are where an English word hides.** "Sol" is three characters and a real
        // system; so is "for". Four is the floor a transcriber's inventions clear and ordinary
        // little words do not.
        if (from.Length < 4)
        {
            return false;
        }

        // It already means something. Either of these makes it a word rather than a mishearing.
        return !known(from) && !reserved(from);
    }

    /// <summary>
    /// Rewrites the tokens of an utterance this store knows about, and leaves everything else
    /// exactly as it was.
    /// <para>
    /// <b>Token by token, matched whole.</b> Substring replacement would turn an alias for
    /// <c>"Sol"</c> into a rewrite of <c>"solid"</c>, and a rewrite nobody can see is the failure
    /// this whole store is built to avoid. Punctuation and spacing are preserved: only the letters
    /// of a matched word change.
    /// </para>
    /// </summary>
    public string Apply(string? spoken)
    {
        if (string.IsNullOrWhiteSpace(spoken) || Entries.Count == 0)
        {
            return spoken ?? string.Empty;
        }

        var by = Entries.ToDictionary(
            entry => entry.Heard, entry => entry.Meant, StringComparer.OrdinalIgnoreCase);

        var rewritten = new System.Text.StringBuilder(spoken.Length);
        var word = new System.Text.StringBuilder();

        foreach (var character in spoken)
        {
            if (char.IsLetterOrDigit(character) || character == '\'')
            {
                word.Append(character);
                continue;
            }

            Flush();
            rewritten.Append(character);
        }

        Flush();

        return rewritten.ToString();

        void Flush()
        {
            if (word.Length == 0)
            {
                return;
            }

            var said = word.ToString();

            rewritten.Append(by.TryGetValue(said, out var meant) ? meant : said);
            word.Clear();
        }
    }

    /// <summary>
    /// Records a correction, replacing any earlier one for the same token.
    /// <para>
    /// <b>Replacing rather than accumulating</b>, because a token has one right answer and a
    /// second correction is the Commander saying the first was wrong. The caller has already asked
    /// <see cref="MayLearn"/>; this does the writing.
    /// </para>
    /// </summary>
    public SoundsLike Learn(string heard, string meant, DateTimeOffset at)
    {
        if (Token(heard) is not { } from || Token(meant) is not { } to)
        {
            return this;
        }

        var kept = Entries
            .Where(entry => !string.Equals(entry.Heard, from, StringComparison.OrdinalIgnoreCase))
            .Take(Limit - 1);

        return this with { Entries = [new SoundsLikeEntry(from, to, at), .. kept] };
    }

    /// <summary>Drops one correction. Half of "readable and clearable".</summary>
    public SoundsLike Forget(string heard) =>
        Token(heard) is not { } from
            ? this
            : this with
            {
                Entries =
                [
                    .. Entries.Where(entry =>
                        !string.Equals(entry.Heard, from, StringComparison.OrdinalIgnoreCase)),
                ],
            };

    /// <summary>Drops the lot. The other half.</summary>
    public SoundsLike ForgetAll() => Empty;

    /// <summary>
    /// What the settings row says. <b>An alias table that cannot be read is a mystery
    /// generator</b> — a Commander whose words are being rewritten has to be able to see the rule
    /// doing it.
    /// </summary>
    public string Summarise() =>
        Entries.Count == 0
            ? "Nothing yet. D47 learns one of these only when you correct a name it misheard, and "
              + "never on its own."
            : string.Join(
                "\n",
                Entries.Take(12).Select(entry => $"\"{entry.Heard}\" → {entry.Meant}"))
              + (Entries.Count > 12 ? $"\n…and {Entries.Count - 12} more." : string.Empty);

    /// <summary>
    /// One word, trimmed, or null for anything that is not one.
    /// <para>
    /// <b>A single token is the unit on purpose</b> — see the note on this type. Letters and
    /// digits only, so a trailing full stop from a transcriber does not make a different word.
    /// </para>
    /// </summary>
    internal static string? Token(string? text)
    {
        var trimmed = (text ?? string.Empty).Trim();

        return trimmed.Length > 0 && trimmed.All(character => char.IsLetterOrDigit(character))
            ? trimmed
            : null;
    }
}
