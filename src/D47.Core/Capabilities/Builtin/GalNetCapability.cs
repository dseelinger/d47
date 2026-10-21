using System.Text;
using D47.Core.Knowledge;

namespace D47.Core.Capabilities.Builtin;

/// <summary>Reads and searches the GalNet articles the archive keeps.</summary>
public static class GalNetCapability
{
    public const string Id = "galnet";

    private const int FullTextCount = 3;

    private const int TitleOnlyCount = 7;

    private const int MinimumWordLength = 3;

    private const int TitleWeight = 3;

    private static readonly IReadOnlyDictionary<string, string> NoArguments = new Dictionary<string, string>();

    private static readonly char[] Separators =
        [' ', '\t', '\r', '\n', ',', '.', ';', ':', '!', '?', '"', '(', ')', '-', '–', '—'];

    public static CapabilityDescriptor Create(Func<GalNetArchive?> archive, Func<bool> enabled) => new()
    {
        Id = Id,
        Group = "Knowledge",
        Name = "GalNet",
        Summary = "Search the GalNet articles kept on this PC and read one aloud.",
        Examples = ["what's on galnet", "read the latest galnet article"],
        Tools =
        [
            new ToolDefinition
            {
                Name = "search_galnet",
                Description =
                    "Search the GalNet articles kept on this PC. With no query, names the three newest. "
                    + "With a query, gives the full text of the three best matches and the titles of up to seven more.",
                Parameters =
                [
                    new ToolParameter
                    {
                        Name = "query",
                        Type = ToolParameterType.String,
                        Description = "Words to look for in the article titles and text.",
                    },
                ],
                Commands =
                [
                    new ToolCommandPhrase("what's on galnet", NoArguments),
                    new ToolCommandPhrase("galnet headlines", NoArguments),
                    new ToolCommandPhrase("any galnet news", NoArguments),
                ],
                Handler = (arguments, _) =>
                {
                    arguments.TryGetString("query", out var query);
                    return Task.FromResult(Search(archive(), enabled(), query));
                },
            },
            new ToolDefinition
            {
                Name = "get_galnet_article",
                Description =
                    "Give the title, in-game date and full text of one kept GalNet article: the newest, "
                    + "or the one whose title best matches the title given.",
                Parameters =
                [
                    new ToolParameter
                    {
                        Name = "title",
                        Type = ToolParameterType.String,
                        Description = "All or part of the article's title.",
                    },
                ],
                Commands =
                [
                    new ToolCommandPhrase("read galnet", NoArguments),
                    new ToolCommandPhrase("read the galnet article", NoArguments),
                    new ToolCommandPhrase("read the latest galnet article", NoArguments),
                ],
                Handler = (arguments, _) =>
                {
                    arguments.TryGetString("title", out var title);
                    return Task.FromResult(Read(archive(), enabled(), title));
                },
            },
        ],
    };

    private static ToolResult Search(GalNetArchive? archive, bool enabled, string? query)
    {
        var all = archive?.All() ?? [];

        if (all.Count == 0)
        {
            return Empty(enabled);
        }

        var words = Words(query);

        if (words.Count == 0)
        {
            var latest = all.Take(FullTextCount).Select(article => $"{article.Title}, {article.GameDate}").ToList();
            return ToolResult.Ok($"The latest on GalNet: {JoinList(latest)}.");
        }

        var ranked = all
            .Select(article => (Article: article, Score: Score(article, words)))
            .Where(pair => pair.Score > 0)
            .OrderByDescending(pair => pair.Score)
            .ThenByDescending(pair => pair.Article.Published)
            .Select(pair => pair.Article)
            .ToList();

        if (ranked.Count == 0)
        {
            return ToolResult.Ok($"No kept GalNet article matches \"{query!.Trim()}\".");
        }

        var said = new StringBuilder();

        foreach (var article in ranked.Take(FullTextCount))
        {
            said.AppendLine(Full(article));
            said.AppendLine();
        }

        var others = ranked.Skip(FullTextCount).Take(TitleOnlyCount).ToList();

        if (others.Count > 0)
        {
            said.Append("Also: ").Append(string.Join("; ", others.Select(a => $"{a.Title}, {a.GameDate}"))).Append('.');
        }

        return ToolResult.Ok(said.ToString().TrimEnd());
    }

    private static ToolResult Read(GalNetArchive? archive, bool enabled, string? title)
    {
        var all = archive?.All() ?? [];

        if (all.Count == 0)
        {
            return Empty(enabled);
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            return ToolResult.Ok(Full(all[0]));
        }

        var asked = title.Trim();
        var words = Words(asked);

        var best = all
            .Select(article => (Article: article, Score: TitleScore(article, asked, words)))
            .Where(pair => pair.Score > 0)
            .OrderByDescending(pair => pair.Score)
            .ThenByDescending(pair => pair.Article.Published)
            .Select(pair => pair.Article)
            .FirstOrDefault();

        return best is null
            ? ToolResult.Ok($"No kept GalNet article has a title like \"{asked}\".")
            : ToolResult.Ok(Full(best));
    }

    private static ToolResult Empty(bool enabled) => ToolResult.Ok(enabled
        ? "No GalNet articles are kept yet."
        : "GalNet news is off. Turn it on under Galaxy search in Settings to keep the articles.");

    private static string Full(GalNetArticle article) =>
        string.Join(Environment.NewLine, [$"{article.Title}, {article.GameDate}", .. article.Body]);

    private static int Score(GalNetArticle article, List<string> words)
    {
        var title = Split(article.Title).ToList();
        var body = article.Body.SelectMany(Split).ToList();

        return words.Sum(word =>
            (title.Any(t => t.StartsWith(word, StringComparison.OrdinalIgnoreCase)) ? TitleWeight : 0)
            + (body.Any(b => b.StartsWith(word, StringComparison.OrdinalIgnoreCase)) ? 1 : 0));
    }

    private static int TitleScore(GalNetArticle article, string asked, List<string> words)
    {
        if (string.Equals(article.Title, asked, StringComparison.OrdinalIgnoreCase))
        {
            return int.MaxValue;
        }

        if (article.Title.Contains(asked, StringComparison.OrdinalIgnoreCase))
        {
            return int.MaxValue - 1;
        }

        var title = Split(article.Title).ToList();
        return words.Count(word => title.Any(t => t.StartsWith(word, StringComparison.OrdinalIgnoreCase)));
    }

    private static List<string> Words(string? query) =>
        [.. Split(query ?? string.Empty).Where(word => word.Length >= MinimumWordLength)];

    private static IEnumerable<string> Split(string text) =>
        text.Split(Separators, StringSplitOptions.RemoveEmptyEntries);

    private static string JoinList(List<string> items) => items.Count switch
    {
        1 => items[0],
        2 => $"{items[0]}; and {items[1]}",
        _ => string.Join("; ", items.Take(items.Count - 1)) + "; and " + items[^1],
    };
}
