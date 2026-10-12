using D47.Core.Capabilities.Builtin;
using D47.Core.Conversation;
using D47.Core.Knowledge;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Knowledge;

/// <summary>A saved search is a name, a search tool and its arguments, shared by every Commander (#1078).</summary>
public class ASavedSearchRunsByItsNameTests
{
    private static readonly DateTimeOffset At = new(2026, 10, 10, 12, 0, 0, TimeSpan.Zero);

    private readonly MemoryFileSystem _files = new();

    private static string StorePath => Path.Combine(@"C:\d47-test", "saved-searches.json");

    private SavedSearchStore Store() => new(StorePath, _files, NullLogger<SavedSearchStore>.Instance);

    private static Dictionary<string, string> Repairs() =>
        new() { ["services"] = "Repair", ["landing_pad"] = "Large" };

    private static SavedSearchOutcome Save(SavedSearchStore store, string name, params string[] taken) =>
        store.Add(name, "search_stations", Repairs(), At, taken);

    [Fact]
    public void ASavedSearchSurvivesAReloadWithItsArguments()
    {
        Assert.True(Save(Store(), "large pad repairs").Saved);

        var reloaded = Store();
        reloaded.Load();

        var found = Assert.Single(reloaded.All());
        Assert.Equal("large pad repairs", found.Name);
        Assert.Equal("search_stations", found.Tool);
        Assert.Equal("Large", found.Arguments["landing_pad"]);
        Assert.Equal(At, found.SavedAt);
    }

    [Fact]
    public void AnArgumentLeftOutStaysOut()
    {
        var store = Store();
        Save(store, "large pad repairs");

        Assert.False(Assert.Single(store.All()).Arguments.ContainsKey("near"));
    }

    [Fact]
    public void ASearchForgottenIsGoneAfterAReload()
    {
        var store = Store();
        Save(store, "large pad repairs");

        Assert.True(store.Forget("Large Pad Repairs"));
        Assert.False(store.Forget("large pad repairs"));

        var reloaded = Store();
        reloaded.Load();

        Assert.Empty(reloaded.All());
    }

    [Fact]
    public void ASearchSavedWhileOneCommanderIsShownIsListedForAnother()
    {
        var store = Store();
        Save(store, "large pad repairs");

        var other = Store();
        other.Load();

        Assert.Single(other.All());
        Assert.Equal(2, SavedSearchPhrases.Commands(other).Count());
    }

    [Fact]
    public void AFileOfPerCommanderRecordsLoadsMergedAndIsRewrittenInTheNewForm()
    {
        _files.WriteText(StorePath, """
            {"commanders":[
              {"frontierId":"F1","searches":[
                {"name":"repairs","tool":"search_stations","arguments":{"services":"Repair"},"savedAt":"2026-10-01T00:00:00+00:00"},
                {"name":"only one","tool":"find_body","arguments":{},"savedAt":"2026-10-02T00:00:00+00:00"}]},
              {"frontierId":"F2","searches":[
                {"name":"Repairs","tool":"search_stations","arguments":{"services":"Refuel"},"savedAt":"2026-10-05T00:00:00+00:00"},
                {"name":"only two","tool":"search_systems","arguments":{},"savedAt":"2026-10-03T00:00:00+00:00"}]}]}
            """);

        var store = Store();
        store.Load();

        Assert.Equal(["Repairs", "only two", "only one"], store.All().Select(search => search.Name));
        Assert.Equal("Refuel", store.All()[0].Arguments["services"]);

        var text = _files.ReadText(StorePath)!;
        Assert.DoesNotContain("commanders", text, StringComparison.Ordinal);
        Assert.DoesNotContain("frontierId", text, StringComparison.Ordinal);

        var reloaded = Store();
        reloaded.Load();

        Assert.Equal(3, reloaded.All().Count);
    }

    [Fact]
    public void TheNewestSearchComesFirst()
    {
        var store = Store();
        store.Add("older", "search_systems", new Dictionary<string, string>(), At, []);
        store.Add("newer", "find_body", new Dictionary<string, string>(), At.AddMinutes(1), []);

        Assert.Equal(["newer", "older"], store.All().Select(search => search.Name));
    }

    [Fact]
    public void ANameTheCommanderAlreadyUsesIsRefused()
    {
        var store = Store();
        Save(store, "large pad repairs");

        var outcome = Save(store, "Large Pad Repairs");

        Assert.False(outcome.Saved);
        Assert.Equal("You already have a search named \"Large Pad Repairs\".", outcome.Message);
        Assert.Single(store.All());
    }

    [Fact]
    public void AQuickSearchNameIsRefused()
    {
        var outcome = Save(Store(), "interstellar factors");

        Assert.False(outcome.Saved);
        Assert.Equal("\"interstellar factors\" is a quick search already. Choose another name.", outcome.Message);
    }

    [Fact]
    public void AMacroPhraseIsRefused()
    {
        var store = Store();

        var outcome = Save(store, "rescue run", "Run the rescue run search");

        Assert.False(outcome.Saved);
        Assert.Equal("\"run the rescue run search\" already means something else. Choose another name.", outcome.Message);
        Assert.Empty(store.All());
    }

    [Fact]
    public void AnEmptyNameAndAnUnknownToolAreRefused()
    {
        var store = Store();

        Assert.Equal("Give the search a name.", Save(store, "  ").Message);
        Assert.False(store.Add("x", "plot_course", Repairs(), At, []).Saved);
        Assert.Empty(store.All());
    }

    [Theory]
    [InlineData("large pad repairs search")]
    [InlineData("run the large pad repairs search")]
    public void ASavedSearchIsRoutedToItsToolWithoutTheModel(string said)
    {
        var store = Store();
        Save(store, "large pad repairs");
        var router = new KeywordRouter(
            TestSurface.For(new MemoryInstall()).Registry,
            () => SavedSearchPhrases.Commands(store));

        var match = router.MatchToolCommand(said);

        Assert.NotNull(match);
        Assert.Equal(GalaxyCapability.Id, match.CapabilityId);
        Assert.Equal("search_stations", match.ToolName);
        Assert.Equal("Repair", match.Arguments.Values["services"]);
        Assert.Equal("Large", match.Arguments.Values["landing_pad"]);
    }
}
