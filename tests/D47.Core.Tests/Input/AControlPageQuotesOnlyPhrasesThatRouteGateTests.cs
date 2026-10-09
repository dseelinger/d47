using System.Text.RegularExpressions;
using D47.Core.Conversation;
using Xunit;

namespace D47.Core.Tests.Input;

/// <summary>
/// The pages for the game's controls tell the Commander what to say. Every phrase they quote must
/// reach something without the model.
/// </summary>
[Trait("Category", "Gate")]
public partial class AControlPageQuotesOnlyPhrasesThatRouteGateTests
{
    [Theory]
    [InlineData("flight-controls")]
    [InlineData("ship-systems")]
    [InlineData("panels")]
    [InlineData("srv")]
    [InlineData("on-foot-controls")]
    public void EveryQuotedPhraseRoutes(string page)
    {
        var phrases = Quoted(File.ReadAllText(Path.Combine(RepositoryRoot(), "docs", "capabilities", $"{page}.md")));

        Assert.NotEmpty(phrases);

        using var install = new TempInstall();
        var router = new KeywordRouter(TestSurface.For(install).Registry);

        var unrouted = phrases
            .Where(phrase => router.MatchSetting(phrase) is null
                && router.MatchToolCommand(phrase) is null
                && router.Match(phrase, InputSource.Spoken) is null)
            .ToList();

        Assert.True(unrouted.Count == 0, $"{page}.md quotes phrases that reach the model: {string.Join(", ", unrouted)}");
    }

    /// <summary>The phrases a page tells the Commander to say: the "Say" lines, the ask-row examples and the "Ask for it" lists.</summary>
    private static List<string> Quoted(string markdown)
    {
        var text = Comment().Replace(markdown, string.Empty);
        var phrases = new List<string>();

        foreach (Match say in SayLine().Matches(text))
        {
            phrases.AddRange(Quote().Matches(say.Groups[1].Value).Select(quote => quote.Groups[1].Value));
        }

        foreach (Match row in ExampleRow().Matches(text))
        {
            phrases.AddRange(Quote().Matches(row.Groups[1].Value).Select(quote => quote.Groups[1].Value));
        }

        phrases.AddRange(AskBox().Matches(text).Select(typed => typed.Groups[1].Value));
        phrases.AddRange(AskList().Matches(text).Select(asked => asked.Groups[1].Value));

        return [.. phrases.Distinct(StringComparer.OrdinalIgnoreCase)];
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "d47.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("No d47.slnx above the test binary.");
    }

    [GeneratedRegex(@"<!--.*?-->", RegexOptions.Singleline)]
    private static partial Regex Comment();

    [GeneratedRegex(@"<p class=""say"">Say (.*?)</p>")]
    private static partial Regex SayLine();

    /// <summary>The muted line under an ask row: quoted phrases joined by dashes and nothing else.</summary>
    [GeneratedRegex(@"<text [^>]*>((?:""[^""]+""(?: — )?)+)</text>")]
    private static partial Regex ExampleRow();

    /// <summary>The words typed into a drawn ask row, which sits beside its "Ask" label.</summary>
    [GeneratedRegex(@"<text x=""44"" y=""57"" [^>]*>([^<]+)</text>")]
    private static partial Regex AskBox();

    [GeneratedRegex(@"^> ""([^""]+)""\r?$", RegexOptions.Multiline)]
    private static partial Regex AskList();

    [GeneratedRegex(@"""([^""]+)""")]
    private static partial Regex Quote();
}
