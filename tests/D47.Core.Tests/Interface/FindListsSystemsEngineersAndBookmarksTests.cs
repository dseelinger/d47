using D47.Core.Conversation;
using D47.Core.Interface;
using Xunit;

namespace D47.Core.Tests.Interface;

/// <summary>Find lists star systems, engineers and bookmarks by name, ranked with pages and settings (#1971).</summary>
public class FindListsSystemsEngineersAndBookmarksTests
{
    private static PanelFind Find(bool galaxy, params Bookmark[] bookmarks)
    {
        var (_, settings, nav) = FindRanksPagesSettingsAndHelpTests.Build();
        settings.Replace("test", current => current with { Knowledge = current.Knowledge with { GalaxySearch = galaxy } });

        return new PanelFind(nav, settings, new Dictionary<PanelTab, string>(), () => bookmarks);
    }

    [Fact]
    public void ASystemThatStartsWithTheWordComesBeforeOneThatContainsIt()
    {
        var results = Find(true).Search("dark");

        var starts = results.ToList().FindIndex(r => r.Title == "Darkan");
        var contains = results.ToList().FindIndex(r => r.Title == "Beldarkri");

        Assert.InRange(starts, 0, contains - 1);
        Assert.All(results.Where(r => r.Title is "Darkan" or "Beldarkri"), r =>
        {
            Assert.Equal(PanelFindKind.StarSystem, r.Kind);
            Assert.Equal("Star system", r.Path);
        });
    }

    [Fact]
    public void NoSystemIsListedWhileGalaxySearchIsOff()
    {
        Assert.DoesNotContain(Find(false).Search("darkan"), r => r.Kind == PanelFindKind.StarSystem);
    }

    [Fact]
    public void SystemsDoNotChangeWhichPagesAndSettingsComeFirst()
    {
        var off = Find(false).Search("fuel").Select(r => r.Target).ToList();
        var on = Find(true).Search("fuel");

        Assert.Equal(off, on.Where(r => r.Kind != PanelFindKind.StarSystem).Select(r => r.Target));

        var system = on.Single(r => r.Kind == PanelFindKind.StarSystem);

        var at = on.ToList().IndexOf(system);

        Assert.All(on.Where(r => r.Rank <= system.Rank && r.Kind != PanelFindKind.StarSystem), r => Assert.True(on.ToList().IndexOf(r) < at));
    }

    [Fact]
    public void ASystemIsNotFoundByLettersInOrder()
    {
        Assert.DoesNotContain(Find(true).Search("drk"), r => r.Title == "Darkan");
    }

    [Fact]
    public void AnEngineerIsFoundByASurnameAndMarkedAsAnEngineer()
    {
        var result = Find(false).Search("farseer").Single(r => r.Kind == PanelFindKind.Engineer);

        Assert.Equal("Felicity Farseer", result.Title);
        Assert.Equal("Engineer", result.Path);
    }

    [Fact]
    public void ABookmarkIsFoundByItsNameAndSaysWhereItIs()
    {
        var bookmark = new Bookmark("Current CG", "Sol", DateTimeOffset.UnixEpoch);

        var result = Find(false, bookmark).Search("current").Single(r => r.Kind == PanelFindKind.Bookmark);

        Assert.Equal("Current CG", result.Title);
        Assert.Equal("Bookmark › Sol", result.Path);
    }

    [Fact]
    public void ASettingStillComesBeforeANameOnTheSameStep()
    {
        var results = Find(true).Search("push");

        Assert.Equal("Push-to-talk", results[0].Title);
    }

    [Fact]
    public void NamesTakeTurnsByKindWithinAStep()
    {
        var extra = Enumerable.Range(0, 20).Select(i => new PanelFindName(PanelFindKind.Station, $"Dark Port {i:00}", "Station", $"s{i}"))
            .Concat(Enumerable.Range(0, 8).Select(i => new PanelFindName(PanelFindKind.Faction, $"Dark Pact {i}", "Faction", $"f{i}")))
            .ToList();

        var first = Find(true).Search("dark", extra).Take(8).Select(r => r.Kind).ToList();

        Assert.Contains(PanelFindKind.StarSystem, first);
        Assert.Contains(PanelFindKind.Station, first);
        Assert.Contains(PanelFindKind.Faction, first);
    }

    [Fact]
    public void ANameReturnedByTheCallerIsRankedLikeAnyOther()
    {
        var extra = new[] { new PanelFindName(PanelFindKind.Faction, "Zzyzx Union", "Faction", "z") };

        var result = Find(false).Search("zzyzx", extra).Single();

        Assert.Equal(PanelFindKind.Faction, result.Kind);
        Assert.Equal(2, result.Rank);
    }
}
