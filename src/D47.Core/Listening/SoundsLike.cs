namespace D47.Core.Listening;

/// <summary>One thing the transcriber gets wrong, and what the Commander said it was.</summary>
/// <param name="Heard">The token as it came out of the transcriber.</param>
/// <param name="Meant">What it is replaced with.</param>
/// <param name="LearnedAt">When the Commander confirmed it, off the journal or the turn.</param>
public sealed record SoundsLikeEntry(string Heard, string Meant, DateTimeOffset LearnedAt);

/// <summary>What this Commander's transcriber reliably gets wrong, and what they meant (#134).</summary>
public sealed record SoundsLike
{
    /// <summary>How many corrections one Commander may accumulate.</summary>
    public const int Limit = 200;

    public static readonly SoundsLike Empty = new();

    /// <summary>Newest first, which is the order a Commander reading the row wants them in.</summary>
    public IReadOnlyList<SoundsLikeEntry> Entries { get; init; } = [];

    public bool IsKnown => Entries.Count > 0;

    /// <summary>Whether this token may be captured as a mishearing at all.</summary>
    /// <param name="heard">The token the transcriber produced.</param>
    /// <param name="meant">What it should have been.</param>
    /// <param name="known">Whether a token already names something this Commander has met.</param>
    /// <param name="reserved">Whether a token is a word d47's own routing uses.</param>
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

        // Nothing to learn, and a self-alias applied forever would be a rule that does nothing at a cost that
        // is not nothing.
        if (string.Equals(from, to, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // **Short tokens are where an English word hides.** "Sol" is three characters and a real system; so
        // is "for".
        if (from.Length < 4)
        {
            return false;
        }

        // It already means something.
        return !known(from) && !reserved(from);
    }

    /// <summary>
    /// Rewrites the tokens of an utterance this store knows about, and leaves everything else exactly
    /// as it was.
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

    /// <summary>Records a correction, replacing any earlier one for the same token.</summary>
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

    /// <summary>Drops one correction.</summary>
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

    /// <summary>Drops the lot.</summary>
    public SoundsLike ForgetAll() => Empty;

    /// <summary>What the settings row says.</summary>
    public string Summarise() =>
        Entries.Count == 0
            ? "Nothing yet. D47 learns one of these only when you correct a name it misheard, and "
              + "never on its own."
            : string.Join(
                "\n",
                Entries.Take(12).Select(entry => $"\"{entry.Heard}\" → {entry.Meant}"))
              + (Entries.Count > 12 ? $"\n…and {Entries.Count - 12} more." : string.Empty);

    /// <summary>One word, trimmed, or null for anything that is not one.</summary>
    internal static string? Token(string? text)
    {
        var trimmed = (text ?? string.Empty).Trim();

        return trimmed.Length > 0 && trimmed.All(character => char.IsLetterOrDigit(character))
            ? trimmed
            : null;
    }
}
