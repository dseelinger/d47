using System.Globalization;

namespace D47.Core.Callouts;

/// <summary>
/// Says "it" when it has just said the name (docs/plans/change-requests.md item 30).
/// <para>
/// <b>The condition is both halves or nothing.</b> The request is <i>recently</i> <b>and</b>
/// <i>it was the last one read</i> — a pronoun that reaches back past a second system is worse
/// than the repetition it replaces, because a Commander hearing "it" has no way to ask which one
/// was meant. So a name is only ever replaced while it is still the last one d47 named, and
/// anything else being named clears the referent outright.
/// </para>
/// <para>
/// <b>The voice takes the pronoun and the page keeps the name.</b> This rewrites what is spoken
/// and nothing that is written, which is not a compromise: a Commander scrolling back can always
/// see which system "it" was, so nothing is lost from the record. It is the same split
/// <see cref="Announcement.Transcript"/> already documents for the opposite reason.
/// </para>
/// <para>
/// <b>The first mention in a line always survives.</b> Within one line the second and later
/// occurrences go; across lines all of them do, provided the referent held. Getting that backwards
/// produces a line that opens with a dangling "it".
/// </para>
/// </summary>
public sealed class SpokenReferent
{
    /// <summary>
    /// How long a name stays the referent with nothing else said. Without a ceiling, a callout
    /// twenty minutes later says "it" about a system the Commander stopped thinking about.
    /// </summary>
    public TimeSpan Holds { get; set; } = TimeSpan.FromMinutes(3);

    private string? _last;

    private DateTimeOffset _at;

    /// <summary>
    /// What to say instead, given what the line was going to say. Also updates the referent, so
    /// this is called once per line and in the order the lines are spoken.
    /// </summary>
    /// <param name="text">The line as its callout composed it.</param>
    /// <param name="named">
    /// Every system this line is about, longest first. Supplied by the caller rather than found
    /// by pattern, because a procedural name is recognisable and a handcrafted one — Sol, Shinrarta
    /// Dezhra — is just words, and guessing at those would eventually pronoun a sentence that was
    /// never about a system at all.
    /// </param>
    public string Speak(string text, IReadOnlyCollection<string> named, DateTimeOffset now)
    {
        if (text.Length == 0)
        {
            return text;
        }

        // More than one system in play clears the referent rather than picking one. Two names in a
        // sentence is exactly when "it" stops being answerable.
        var distinct = named
            .Where(name => name is { Length: > 0 })
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (distinct.Length != 1)
        {
            _last = null;
            return text;
        }

        var subject = distinct[0];
        var held = _last is not null
            && string.Equals(_last, subject, StringComparison.OrdinalIgnoreCase)
            && now - _at <= Holds;

        _last = subject;
        _at = now;

        // Every occurrence when the referent held into this line; all but the first when it did
        // not, so a fresh mention still introduces itself.
        var spoken = Replace(text, subject, skipFirst: !held);

        return spoken;
    }

    /// <summary>Forgets the referent — a jump, or anything else that changes what "it" would mean.</summary>
    public void Forget() => _last = null;

    /// <summary>
    /// The pronoun. Deliberately one word and deliberately not <i>there</i> or <i>here</i>: those
    /// two are claims about where the Commander is, and a line built for a name would read as a
    /// different sentence with either of them in it.
    /// </summary>
    private const string Pronoun = "it";

    private static string Replace(string text, string name, bool skipFirst)
    {
        var at = text.IndexOf(name, StringComparison.OrdinalIgnoreCase);

        if (at < 0)
        {
            return text;
        }

        if (skipFirst)
        {
            at = text.IndexOf(name, at + name.Length, StringComparison.OrdinalIgnoreCase);

            if (at < 0)
            {
                return text;
            }
        }

        var built = new System.Text.StringBuilder(text.Length);
        var from = 0;

        while (at >= 0)
        {
            built.Append(text, from, at - from);
            built.Append(Fitted(text, at, Pronoun));

            from = at + name.Length;
            at = text.IndexOf(name, from, StringComparison.OrdinalIgnoreCase);
        }

        built.Append(text, from, text.Length - from);

        return built.ToString();
    }

    /// <summary>
    /// The pronoun with the capitalisation the position needs. A name at the start of a sentence
    /// is replaced by a word that has to start one too, and "it is 40 light years away" reading as
    /// "it" mid-sentence and "It" after a full stop is the difference between a line that sounds
    /// written and one that sounds substituted.
    /// </summary>
    private static string Fitted(string text, int at, string pronoun)
    {
        for (var back = at - 1; back >= 0; back--)
        {
            var letter = text[back];

            if (char.IsWhiteSpace(letter))
            {
                continue;
            }

            return letter is '.' or '!' or '?' or ':'
                ? Capitalised(pronoun)
                : pronoun;
        }

        // Nothing before it at all: this is the start of the line.
        return Capitalised(pronoun);
    }

    private static string Capitalised(string word) =>
        char.ToUpper(word[0], CultureInfo.InvariantCulture) + word[1..];
}
