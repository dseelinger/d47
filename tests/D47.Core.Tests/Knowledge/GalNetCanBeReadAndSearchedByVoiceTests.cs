using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Conversation;
using D47.Core.Knowledge;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Knowledge;

public class GalNetCanBeReadAndSearchedByVoiceTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 21, 12, 0, 0, TimeSpan.Zero);

    private static GalNetArticle Article(int day, string title, string body = "Nothing of note.") =>
        new($"id{day}", title, [body], Start.AddDays(day), $"{day} OCT 3312");

    private static GalNetArchive Archive(params GalNetArticle[] articles)
    {
        var archive = new GalNetArchive(@"C:\d47\data\galnet.json", new MemoryFileSystem(), NullLogger<GalNetArchive>.Instance);
        archive.Merge(articles);
        return archive;
    }

    private static CapabilityRegistry Registry(GalNetArchive archive) =>
        CapabilityRegistry.Build([GalNetCapability.Create(() => archive)]);

    private static async Task<string> Ask(CapabilityRegistry registry, string tool, string? name = null, string? value = null)
    {
        var values = name is null ? new Dictionary<string, string>() : new Dictionary<string, string> { [name] = value! };
        var result = await registry.InvokeAsync(tool, new ToolArguments(values));
        Assert.False(result.IsError);
        return result.Content;
    }

    [Fact]
    public async Task SearchWithNoQueryNamesTheThreeNewest()
    {
        var registry = Registry(Archive(Article(1, "Old"), Article(2, "B"), Article(3, "C"), Article(4, "D")));

        Assert.Equal("The latest on GalNet: D, 4 OCT 3312; C, 3 OCT 3312; and B, 2 OCT 3312.", await Ask(registry, "search_galnet"));
    }

    [Fact]
    public async Task ASearchRanksATitleMatchAboveABodyMatchAndMatchesWordStarts()
    {
        var registry = Registry(Archive(
            Article(5, "Station news", "Thargoids were seen."),
            Article(1, "Thargoid sighting", "Pilots reported it.")));

        var said = await Ask(registry, "search_galnet", "query", "thargoid");

        Assert.True(said.IndexOf("Thargoid sighting", StringComparison.Ordinal) < said.IndexOf("Station news", StringComparison.Ordinal));
        Assert.Contains("Pilots reported it.", said);
        Assert.Contains("Thargoids were seen.", said);
    }

    [Fact]
    public async Task ASearchGivesThreeInFullAndNoMoreThanSevenTitles()
    {
        var registry = Registry(Archive([.. Enumerable.Range(1, 14).Select(day => Article(day, $"Thargoid {day}", $"Body {day}."))]));

        var said = await Ask(registry, "search_galnet", "query", "Thargoid");

        Assert.Equal(3, said.Split("Body ").Length - 1);
        var also = said[said.IndexOf("Also: ", StringComparison.Ordinal)..];
        Assert.Equal(7, also.Split("Thargoid ").Length - 1);
    }

    [Fact]
    public async Task ShortQueryWordsAreIgnored()
    {
        var registry = Registry(Archive(Article(1, "An announcement")));

        Assert.StartsWith("The latest on GalNet:", await Ask(registry, "search_galnet", "query", "an"));
    }

    [Fact]
    public async Task GetWithNoTitleReadsTheNewestInFull()
    {
        var registry = Registry(Archive(Article(1, "Old"), Article(2, "New", "All of it.")));

        Assert.Equal("New, 2 OCT 3312" + Environment.NewLine + "All of it.", await Ask(registry, "get_galnet_article"));
    }

    [Fact]
    public async Task GetWithPartOfATitleIgnoresCase()
    {
        var registry = Registry(Archive(Article(1, "Anaconda Mk II revealed", "Details."), Article(2, "Other")));

        Assert.StartsWith("Anaconda Mk II revealed", await Ask(registry, "get_galnet_article", "title", "anaconda mk"));
    }

    [Fact]
    public void ReadTheLatestGalNetArticleReachesTheToolWithNoModel()
    {
        var router = new KeywordRouter(Registry(Archive(Article(1, "A"))));

        var match = router.MatchToolCommand("read the latest galnet article");

        Assert.NotNull(match);
        Assert.Equal("get_galnet_article", match.ToolName);
    }

    [Fact]
    public async Task AnEmptyArchiveSaysWhy()
    {
        var registry = Registry(Archive());

        Assert.Equal("No GalNet articles are kept yet.", await Ask(registry, "search_galnet"));
        Assert.Equal("No GalNet articles are kept yet.", await Ask(registry, "get_galnet_article"));
    }
}
