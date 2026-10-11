using D47.Core.Capabilities.Builtin;
using D47.Core.Conversation;
using D47.Core.Knowledge;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Knowledge;

/// <summary>A saved search is a name, a search tool and its arguments, kept per Commander (#1078).</summary>
public class ASavedSearchRunsByItsNameTests
{
    private static readonly DateTimeOffset At = new(2026, 10, 10, 12, 0, 0, TimeSpan.Zero);

    private readonly MemoryFileSystem _files = new();

    private static string StorePath => Path.Combine(@"C:\d47-test", "saved-searches.json");

    private SavedSearchStore Store() => new(StorePath, _files, NullLogger<SavedSearchStore>.Instance);

    private static Dictionary<string, string> Repairs() =>
        new() { ["services"] = "Repair", ["landing_pad"] = "Large" };

    private static SavedSearchOutcome Save(SavedSearchStore store, string frontierId, string name, params string[] taken) =>
        store.Add(frontierId, name, "search_stations", Repairs(), At, taken);

    [Fact]
    public void ASavedSearchSurvivesAReloadWithItsArguments()
    {
        Assert.True(Save(Store(), "F1", "large pad repairs").Saved);

        var reloaded = Store();
        reloaded.Load();

        var found = Assert.Single(reloaded.For("F1"));
        Assert.Equal("large pad repairs", found.Name);
        Assert.Equal("search_stations", found.Tool);
        Assert.Equal("Large", found.Arguments["landing_pad"]);
        Assert.Equal(At, found.SavedAt);
    }

    [Fact]
    public void AnArgumentLeftOutStaysOut()
    {
        var store = Store();
        Save(store, "F1", "large pad repairs");

        Assert.False(Assert.Single(store.For("F1")).Arguments.ContainsKey("near"));
    }

    [Fact]
    public void ASearchForgottenIsGoneAfterAReload()
    {
        var store = Store();
        Save(store, "F1", "large pad repairs");

        Assert.True(store.Forget("F1", "Large Pad Repairs"));
        Assert.False(store.Forget("F1", "large pad repairs"));

        var reloaded = Store();
        reloaded.Load();

        Assert.Empty(reloaded.For("F1"));
    }

    [Fact]
    public void TwoCommandersKeepTheirOwnSearches()
    {
        var store = Store();
        Save(store, "F1", "large pad repairs");
        Save(store, "F2", "large pad repairs");
        store.Forget("F1", "large pad repairs");

        Assert.Empty(store.For("F1"));
        Assert.Single(store.For("F2"));
    }

    [Fact]
    public void TheNewestSearchComesFirst()
    {
        var store = Store();
        store.Add("F1", "older", "search_systems", new Dictionary<string, string>(), At, []);
        store.Add("F1", "newer", "find_body", new Dictionary<string, string>(), At.AddMinutes(1), []);

        Assert.Equal(["newer", "older"], store.For("F1").Select(search => search.Name));
    }

    [Fact]
    public void ANameTheCommanderAlreadyUsesIsRefused()
    {
        var store = Store();
        Save(store, "F1", "large pad repairs");

        var outcome = Save(store, "F1", "Large Pad Repairs");

        Assert.False(outcome.Saved);
        Assert.Equal("You already have a search named \"Large Pad Repairs\".", outcome.Message);
        Assert.Single(store.For("F1"));
    }

    [Fact]
    public void AQuickSearchNameIsRefused()
    {
        var outcome = Save(Store(), "F1", "interstellar factors");

        Assert.False(outcome.Saved);
        Assert.Equal("\"interstellar factors\" is a quick search already. Choose another name.", outcome.Message);
    }

    [Fact]
    public void AMacroPhraseIsRefused()
    {
        var store = Store();

        var outcome = Save(store, "F1", "rescue run", "Run the rescue run search");

        Assert.False(outcome.Saved);
        Assert.Equal("\"run the rescue run search\" already means something else. Choose another name.", outcome.Message);
        Assert.Empty(store.For("F1"));
    }

    [Fact]
    public void AnEmptyNameAndAnUnknownToolAreRefused()
    {
        var store = Store();

        Assert.Equal("Give the search a name.", Save(store, "F1", "  ").Message);
        Assert.False(store.Add("F1", "x", "plot_course", Repairs(), At, []).Saved);
        Assert.Empty(store.For("F1"));
    }

    [Theory]
    [InlineData("large pad repairs search")]
    [InlineData("run the large pad repairs search")]
    public void ASavedSearchIsRoutedToItsToolWithoutTheModel(string said)
    {
        var store = Store();
        Save(store, "F1", "large pad repairs");
        var router = new KeywordRouter(
            TestSurface.For(new MemoryInstall()).Registry,
            () => SavedSearchPhrases.Commands(store, () => "F1"));

        var match = router.MatchToolCommand(said);

        Assert.NotNull(match);
        Assert.Equal(GalaxyCapability.Id, match.CapabilityId);
        Assert.Equal("search_stations", match.ToolName);
        Assert.Equal("Repair", match.Arguments.Values["services"]);
        Assert.Equal("Large", match.Arguments.Values["landing_pad"]);
    }

    [Fact]
    public void AnotherCommandersSearchesGiveNoPhrases()
    {
        var store = Store();
        Save(store, "F1", "large pad repairs");

        Assert.Empty(SavedSearchPhrases.Commands(store, () => "F2"));
        Assert.Empty(SavedSearchPhrases.Commands(store, () => string.Empty));
    }
}
