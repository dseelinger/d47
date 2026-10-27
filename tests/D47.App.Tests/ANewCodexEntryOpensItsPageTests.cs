using D47.App.Panel;
using D47.Core.Capabilities;
using D47.Core.Interface;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Avalonia.Headless.XUnit;
using Xunit;

namespace D47.App.Tests;

/// <summary>A new codex entry opens its page on every surface that is free to move (#1782).</summary>
public class ANewCodexEntryOpensItsPageTests
{
    private const long Entry = 1101301;

    private static PanelNavigator Surface()
    {
        var nav = new PanelNavigator();

        nav.Register(PanelTab.Transcript, new NavCrumb("transcript", "Transcript"));
        nav.Register(PanelTab.Commander, LedgersPage.Root);

        return nav;
    }

    private static JournalEvent Discovery(long entryId, string name, bool isNew = true)
    {
        var json = $$"""{"timestamp":"2026-10-27T09:00:00Z","event":"CodexEntry","EntryID":{{entryId}},"Name_Localised":"{{name}}","Region":"$Codex_RegionName_18;","IsNewEntry":{{(isNew ? "true" : "false")}}}""";

        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static PanelRouting Route(params (PanelNavigator Nav, PanelPrompts? Prompts)[] surfaces)
    {
        var routing = new PanelRouting(() => false, () => []);

        foreach (var (nav, prompts) in surfaces)
        {
            routing.RouteNavigation(nav, action => action());

            if (prompts is not null)
            {
                routing.RoutePromptSurface(prompts, action => action());
            }
        }

        return routing;
    }

    [Fact]
    public void ACodexEntryIsResolvedToItsTrail()
    {
        var target = PageTrail.For(PageRef.CodexEntry(Entry, 18, "Anemone"), () => []);

        Assert.NotNull(target);
        Assert.Equal(LedgersPage.RootKey, target.Value.Root);
        Assert.Equal(
            [CodexPage.PageKey, CodexPage.RegionCrumb(18).Key, CodexPage.EntryCrumb(18, Entry, "Anemone").Key],
            target.Value.Crumbs.Select(crumb => crumb.Key));
        Assert.Equal("Anemone", target.Value.Crumbs[^1].Word);
    }

    [Fact]
    public void ANewEntryOpensItsPageOnEverySurface()
    {
        var first = Surface();
        var second = Surface();

        Route((first, null), (second, null)).OpenDiscovered([Discovery(Entry, "Anemone")], first: false, enabled: true);

        Assert.All([first, second], nav =>
        {
            Assert.Equal(PanelTab.Commander, nav.Tab);
            Assert.Equal(CodexPage.EntryCrumb(18, Entry, "Anemone").Key, nav.Trail[^1].Key);
        });
    }

    [Fact]
    public void TheLastOfSeveralEntriesInATickIsShown()
    {
        var nav = Surface();

        Route((nav, null)).OpenDiscovered([Discovery(Entry, "Anemone"), Discovery(Entry + 1, "Bark Mound")], first: false, enabled: true);

        Assert.Equal(CodexPage.EntryCrumb(18, Entry + 1, "Bark Mound").Key, nav.Trail[^1].Key);
    }

    [Fact]
    public void TheReplayedBacklogMovesNothing()
    {
        var nav = Surface();

        Route((nav, null)).OpenDiscovered([Discovery(Entry, "Anemone")], first: true, enabled: true);

        Assert.Equal(PanelTab.Transcript, nav.Tab);
    }

    [Fact]
    public void AnEntryAlreadyLoggedMovesNothing()
    {
        var nav = Surface();

        Route((nav, null)).OpenDiscovered([Discovery(Entry, "Anemone", isNew: false)], first: false, enabled: true);

        Assert.Equal(PanelTab.Transcript, nav.Tab);
    }

    [Fact]
    public void WithTheSettingOffNothingMoves()
    {
        var nav = Surface();

        Route((nav, null)).OpenDiscovered([Discovery(Entry, "Anemone")], first: false, enabled: false);

        Assert.Equal(PanelTab.Transcript, nav.Tab);
    }

    [Fact]
    public void ASurfaceHoldingAModalPageStaysAndTheOtherMoves()
    {
        var held = Surface();
        held.Take(new NavCrumb("chooser", "Chooser"));
        var free = Surface();

        Route((held, null), (free, null)).OpenDiscovered([Discovery(Entry, "Anemone")], first: false, enabled: true);

        Assert.Equal("chooser", held.Trail[^1].Key);
        Assert.Equal(CodexPage.EntryCrumb(18, Entry, "Anemone").Key, free.Trail[^1].Key);
    }

    [AvaloniaFact]
    public void ASurfaceWithAPromptOpenStaysAndTheOtherMoves()
    {
        var asking = Surface();
        var prompts = new PanelPrompts(asking, new Avalonia.Controls.Panel());
        prompts.Choose(
            new ChoiceRequest("pick", "Pick", "Pick one", null, [new ChoiceOption("a", "A")], null, ChoiceSurface.Layer),
            _ => { });
        Assert.True(prompts.IsOpen);
        var free = Surface();

        Route((asking, prompts), (free, null)).OpenDiscovered([Discovery(Entry, "Anemone")], first: false, enabled: true);

        Assert.Equal(PanelTab.Transcript, asking.Tab);
        Assert.Equal(CodexPage.EntryCrumb(18, Entry, "Anemone").Key, free.Trail[^1].Key);
    }
}
