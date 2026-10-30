using System.Text.RegularExpressions;

namespace D47.Core.Callouts;

/// <summary>Whether a model's reply carries its reasoning, a restatement of its brief or a quotation of the line it was given.</summary>
public static partial class ModelReasoning
{
    private const int RunLength = 3;

    private const int MostFillerInARun = 1;

    private static readonly HashSet<string> Filler = new(StringComparer.Ordinal)
    {
        "a", "an", "and", "as", "at", "be", "but", "by", "do", "for", "from", "i", "if", "in", "is", "it",
        "its", "my", "no", "not", "of", "on", "or", "so", "that", "the", "this", "to", "you", "your", "with",
    };

    /// <summary>
    /// True when the reply puts commentary ahead of a delivery tag, quotes text that is in the prompt, or
    /// repeats a run of the prompt's own instruction words.
    /// </summary>
    /// <param name="prompt">Everything the model was told, or null when the caller has only the reply.</param>
    public static bool Shows(string reply, string? prompt)
    {
        ArgumentNullException.ThrowIfNull(reply);

        if (CommentaryBeforeATag(reply))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(prompt))
        {
            return false;
        }

        if (Quoted(reply).Any(said => prompt.Contains(said, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        var instruction = Words(QuotedSpan().Replace(prompt, " "));
        var asked = new HashSet<string>(Runs(instruction), StringComparer.Ordinal);

        return Runs(Words(reply)).Any(asked.Contains);
    }

    private static bool CommentaryBeforeATag(string reply)
    {
        var tag = DeliveryTag().Match(reply);

        return tag.Success && reply[..tag.Index].Contains(':', StringComparison.Ordinal);
    }

    private static IEnumerable<string> Quoted(string text) =>
        QuotedSpan().Matches(text).Select(match => match.Groups[1].Value.Trim().TrimEnd('.', ',', '!', '?'))
            .Where(said => said.Length >= 3);

    private static List<string> Words(string text) =>
        [.. WordPattern().Matches(text).Select(match => match.Value.ToLowerInvariant())];

    private static IEnumerable<string> Runs(List<string> words)
    {
        for (var start = 0; start + RunLength <= words.Count; start++)
        {
            var run = words.GetRange(start, RunLength);

            if (run.Count(Filler.Contains) <= MostFillerInARun)
            {
                yield return string.Join(' ', run);
            }
        }
    }

    [GeneratedRegex("[\"“]([^\"“”]+)[\"”]")]
    private static partial Regex QuotedSpan();

    [GeneratedRegex(@"\[[A-Za-z][A-Za-z ,'-]{1,30}\]")]
    private static partial Regex DeliveryTag();

    [GeneratedRegex(@"[\p{L}\p{N}']+")]
    private static partial Regex WordPattern();
}
