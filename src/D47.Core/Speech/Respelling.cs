namespace D47.Core.Speech;

/// <summary>Applies the Commander's respellings to a line of text, for voices that take text rather than phonemes.</summary>
public static class Respelling
{
    /// <summary>
    /// <paramref name="text"/> with each matched word replaced by its respelling, keeping the punctuation
    /// around it. An IPA entry is passed to <paramref name="skipped"/> and the word left as written.
    /// Returns <paramref name="text"/> itself when nothing matched.
    /// </summary>
    public static string Apply(string text, PronunciationOverrides overrides, Action<Pronunciation>? skipped = null)
    {
        var raw = text.Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        var tokens = raw.Select(Phonemiser.Trim).ToList();
        var bodies = tokens.Select(token => token.Body.Replace('’', '\'')).ToList();
        var built = new System.Text.StringBuilder();
        var changed = false;

        for (var at = 0; at < tokens.Count;)
        {
            if (built.Length > 0)
            {
                built.Append(' ');
            }

            if (overrides.Match(bodies, at) is { } match)
            {
                if (match.Said.IsIpa)
                {
                    skipped?.Invoke(match.Said);
                }
                else
                {
                    var last = tokens[at + match.Words - 1];

                    built.Append(tokens[at].Lead).Append(match.Said.Value).Append(last.Tail);
                    at += match.Words;
                    changed = true;
                    continue;
                }
            }

            built.Append(raw[at]);
            at++;
        }

        return changed ? built.ToString() : text;
    }
}
