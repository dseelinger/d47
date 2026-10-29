using System.Globalization;

namespace D47.Core.Callouts;

/// <summary>Says "it" when it has just said the name (docs/plans/change-requests.md item 30).</summary>
public sealed class SpokenReferent
{
    /// <summary>How long a name stays the referent with nothing else said.</summary>
    public TimeSpan Holds { get; set; } = TimeSpan.FromMinutes(3);

    private string? _last;

    private DateTimeOffset _at;

    /// <summary>What to say instead, given what the line was going to say.</summary>
    /// <param name="text">The line as its callout composed it.</param>
    /// <param name="named">Every system this line is about, longest first.</param>
    public string Speak(string text, IReadOnlyCollection<string> named, DateTimeOffset now)
    {
        if (text.Length == 0)
        {
            return text;
        }

        // More than one system in play clears the referent rather than picking one.
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

        // Every occurrence when the referent held into this line; all but the first when it did not, so a
        // fresh mention still introduces itself.
        var spoken = Replace(text, subject, skipFirst: !held);

        return spoken;
    }

    /// <summary>The pronoun.</summary>
    private const string Pronoun = "it";

    private static string Replace(string text, string name, bool skipFirst)
    {
        var at = Find(text, name, 0);

        if (at < 0)
        {
            return text;
        }

        if (skipFirst)
        {
            at = Find(text, name, at + name.Length);

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
            built.Append(FollowsAPlace(text, at) ? text.Substring(at, name.Length) : Fitted(text, at, Pronoun));

            from = at + name.Length;
            at = Find(text, name, from);
        }

        built.Append(text, from, text.Length - from);

        return built.ToString();
    }

    /// <summary>The next place the name stands on its own, not as the start of a body name.</summary>
    private static int Find(string text, string name, int from)
    {
        var at = text.IndexOf(name, from, StringComparison.OrdinalIgnoreCase);

        while (at >= 0 && StartsABodyName(text, at + name.Length))
        {
            at = text.IndexOf(name, at + name.Length, StringComparison.OrdinalIgnoreCase);
        }

        return at;
    }

    /// <summary>True when a body designation (a digit or upper-case letter) follows after one space.</summary>
    private static bool StartsABodyName(string text, int end) =>
        end + 1 < text.Length
        && text[end] == ' '
        && (char.IsDigit(text[end + 1]) || char.IsUpper(text[end + 1]));

    /// <summary>True for "&lt;Name&gt; in/at &lt;system&gt;", where "it" would point at the name before it.</summary>
    private static bool FollowsAPlace(string text, int at)
    {
        var end = at;

        while (end > 0 && char.IsWhiteSpace(text[end - 1]))
        {
            end--;
        }

        var preposition = text.AsSpan(0, end);

        if (!preposition.EndsWith(" in", StringComparison.OrdinalIgnoreCase)
            && !preposition.EndsWith(" at", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var wordEnd = end - 3;

        while (wordEnd > 0 && char.IsWhiteSpace(text[wordEnd - 1]))
        {
            wordEnd--;
        }

        var wordStart = wordEnd;

        while (wordStart > 0 && !char.IsWhiteSpace(text[wordStart - 1]))
        {
            wordStart--;
        }

        return wordStart < wordEnd
            && char.IsUpper(text[wordStart])
            && !StartsASentence(text, wordStart);
    }

    private static bool StartsASentence(string text, int at)
    {
        for (var back = at - 1; back >= 0; back--)
        {
            if (!char.IsWhiteSpace(text[back]))
            {
                return text[back] is '.' or '!' or '?' or ':';
            }
        }

        return true;
    }

    /// <summary>The pronoun with the capitalisation the position needs.</summary>
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
