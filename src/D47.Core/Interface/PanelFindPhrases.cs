namespace D47.Core.Interface;

/// <summary>What a Find phrase says, and the result every surface opens, or null when it asks which.</summary>
public sealed record PanelFindAnswer(string Said, PanelFindResult? Open);

/// <summary>Finding a setting or page by saying so, without the model (#1473).</summary>
public static class PanelFindPhrases
{
    /// <summary>Each phrase as its words before and after the query. Only phrases naming a setting or a page.</summary>
    private static readonly IReadOnlyList<(string Before, string After)> Forms =
    [
        ("where is the ", " setting"),
        ("where are the ", " settings"),
        ("where do i change ", string.Empty),
        ("where do i set ", string.Empty),
        ("where do i turn on ", string.Empty),
        ("where do i turn off ", string.Empty),
        ("show me the ", " setting"),
        ("show me the ", " settings"),
        ("show me the ", " page"),
        ("open the ", " setting"),
        ("open the ", " settings"),
        ("find the ", " setting"),
        ("find the ", " settings"),
        ("find the ", " page"),
    ];

    /// <summary>The most places an unclear answer names.</summary>
    private const int MostNamed = 3;

    /// <summary>What the phrase asks to find, or null when it is not a Find phrase.</summary>
    public static string? Query(string spoken)
    {
        var input = spoken.Trim().Trim('.', '!', '?', ',').Trim().ToLowerInvariant();

        foreach (var (before, after) in Forms)
        {
            if (input.Length > before.Length + after.Length
                && input.StartsWith(before, StringComparison.Ordinal)
                && input.EndsWith(after, StringComparison.Ordinal))
            {
                var query = input[before.Length..^after.Length].Trim();

                if (query.Length > 0)
                {
                    return query;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// The answer to a Find phrase. Among the results at the best rank, the best kind wins, then a row that can be
    /// changed over a read-only one; one winner is opened, and several are named with a question. Null when the
    /// phrase is not a Find phrase or finds nothing.
    /// </summary>
    public static PanelFindAnswer? Answer(string spoken, PanelFind find)
    {
        if (Query(spoken) is not { } query)
        {
            return null;
        }

        var results = find.Search(query);

        if (results.Count == 0)
        {
            return null;
        }

        var best = results.Where(result => result.Rank == results[0].Rank).ToList();
        var kind = best.Min(result => result.Kind);
        var readOnly = best.Where(result => result.Kind == kind).All(result => result.ReadOnly);
        var tied = best.Where(result => result.Kind == kind && result.ReadOnly == readOnly).ToList();

        if (tied.Count == 1)
        {
            return new PanelFindAnswer($"{tied[0].Where}.", tied[0]);
        }

        var named = tied.Take(MostNamed).Select(result => result.Where).ToArray();

        var count = tied.Count switch
        {
            2 => "Two",
            3 => "Three",
            _ => "Several",
        };

        var listed = named.Length == 2
            ? $"{named[0]}, or {named[1]}"
            : $"{named[0]}, {named[1]}, or {named[2]}";

        return new PanelFindAnswer($"{count} places: {listed}. Which one?", null);
    }
}
