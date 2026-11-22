using Avalonia.Controls;
using D47.Core.Interface;

namespace D47.App.Panel;

/// <summary>The Commander tab: Checklist, Assets, Missions, Watches, Progress, Statistics, BGS, Ledgers (with This session among them) and Commanders.</summary>
internal sealed class CommanderFurnishing(IPanelTabHost host)
{
    /// <summary>The sub-tabs in the order the row draws them, whichever order they are furnished in.</summary>
    public static IReadOnlyList<string> SubTabs { get; } =
    [
        ChecklistRoot,
        PanelTabNames.AssetsRoot,
        MissionsPage.RootKey,
        WatchesPage.RootKey,
        StandingPage.RootKey,
        StatisticsPage.RootKey,
        CampaignPage.RootKey,
        LedgersPage.RootKey,
        CommandersPage.RootKey,
    ];

    private Func<NavCrumb, Control>? _assetsBuild;

    /// <summary>Furnishes the Assets sub-tab, whose levels <paramref name="build"/> draws.</summary>
    public void FurnishAssets(NavCrumb root, Func<NavCrumb, Control> build)
    {
        _assetsBuild = build;
        host.Furnish(PanelTab.Commander, BuildCommander, root);
    }

    /// <summary>Whether a level being drawn is on the Assets trail: every level is drawn from the trail showing.</summary>
    private bool InAssets(NavCrumb crumb) =>
        host.Nav.TrailOf(PanelTab.Commander) is [{ Key: PanelTabNames.AssetsRoot }, ..] trail
        && trail.Any(level => level.Key == crumb.Key);

    private Func<NavCrumb, Control>? _checklistBuild;
    private Func<NavCrumb, Control>? _missionsBuild;
    private Func<NavCrumb, Control>? _standingBuild;
    private Func<NavCrumb, Control>? _statisticsBuild;
    private Func<NavCrumb, Control>? _sessionBuild;
    private Func<NavCrumb, Control>? _combatStatsBuild;
    private Func<NavCrumb, Control>? _creditsBuild;
    private Func<NavCrumb, Control>? _codexBuild;
    private Func<NavCrumb, Control>? _screenshotsBuild;
    private Func<NavCrumb, Control>? _commandersBuild;
    private Func<NavCrumb, Control>? _campaignBuild;
    private CampaignPage? _campaign;
    private string? _statisticsOpening;
    private StatisticsPage? _statisticsRoot;
    private MissionsPage? _missions;
    private StandingPage? _standing;
    private StatisticsPage? _statistics;
    private SessionPage? _session;
    private CombatStatsPage? _combatStats;
    private CreditsPage? _credits;
    private CreditTransactionsPage? _creditTransactions;
    private CodexPage? _codex;
    private CodexRegionPage? _codexRegion;
    private CodexPossibleFirstsPage? _codexPossibleFirsts;
    private CodexEntryPage? _codexEntry;
    private CodexCataloguePage? _codexCatalogue;
    private CommandersPage? _commanders;
    private Func<NavCrumb, Control>? _watchesBuild;
    private WatchesPage? _watches;
    private D47.App.Controls.JournalClock? _commanderClock;

    public void EnableChecklist(
        D47.Core.Checklists.ChecklistService checklists,
        D47.Core.Goals.GoalBook? goals = null)
    {
        ChecklistPage? page = null;
        var root = new NavCrumb(ChecklistRoot, "Checklist")
        {
            // The suggestions level drilled from here inherits it: a proposal is still the checklist's subject.
            Help = D47.Core.Capabilities.Builtin.ChecklistCapability.Id,
        };

        ChecklistPage Page(NavCrumb crumb) =>
            new(checklists, host.Nav, host.Prompts, goals, crumb: crumb);

        _checklistBuild = crumb => crumb.Key switch
        {
            // Suggestions opened by a switch or by voice may be drawn before the root ever was.
            ChecklistPage.SuggestionsKey => (page ??= Page(root)).BuildSuggestions(),
            _ => page = Page(crumb),
        };

        host.Furnish(PanelTab.Commander, BuildCommander, root);

        host.Nav.Declare(
            ChecklistRoot,
            ChecklistPage.Suggestions,
            () => checklists.Proposals.PendingFor(checklists.Document.CommanderFid).Count > 0);
        host.Nav.Declare(ChecklistRoot, ChecklistPage.AllLists);
    }

    internal const string ChecklistRoot = "checklist";

    public void EnableWatches(WatchesDesk desk)
    {
        _watchesBuild = crumb =>
        {
            if (crumb.Key == WatchesPage.NewStockKey)
            {
                return new WatchFormPage(desk, host.Nav, null, D47.Core.Watches.WatchKind.Stock);
            }

            if (crumb.Key == WatchesPage.NewNextKey)
            {
                return new WatchFormPage(desk, host.Nav, null, D47.Core.Watches.WatchKind.NextSystem);
            }

            if (crumb.Key == WatchesPage.NewGoalKey)
            {
                return new WatchFormPage(desk, host.Nav, null, D47.Core.Watches.WatchKind.NewCommunityGoal);
            }

            if (WatchesPage.EditOf(crumb.Key) is { } id)
            {
                return desk.Store.Watches.FirstOrDefault(watch => watch.Id == id) is { } watch
                    ? new WatchFormPage(desk, host.Nav, watch, watch.Kind)
                    : new TextBlock { Text = "That watch is gone." };
            }

            return _watches = new WatchesPage(desk, host.Nav, host.Headset);
        };

        host.Furnish(
            PanelTab.Commander,
            BuildCommander,
            new NavCrumb(WatchesPage.RootKey, "Watches")
            {
                Help = D47.Core.Capabilities.Builtin.WatchesCapability.Id,
            });
    }

    private static bool IsWatches(string key) =>
        key == WatchesPage.RootKey || key == WatchesPage.NewStockKey || key == WatchesPage.NewGoalKey || key == WatchesPage.NewNextKey || WatchesPage.EditOf(key) is not null;

    public void EnableMissions(
        Func<D47.Core.Journal.CommanderGameState?> state,
        Func<DateTimeOffset> now,
        D47.Core.Capabilities.CapabilityRegistry? registry = null,
        Func<string, Task<bool>>? copy = null)
    {
        var headset = host.Headset;

        _missionsBuild = _ => _missions = new MissionsPage(state, now, registry, copy, headset);

        host.Furnish(
            PanelTab.Commander,
            BuildCommander,
            new NavCrumb(MissionsPage.RootKey, "Missions")
            {
                Help = D47.Core.Capabilities.Builtin.MissionsCapability.Id,
            });
    }

    public void EnableStanding(
        Func<D47.Core.Journal.CommanderGameState?> state,
        D47.Core.Goals.GoalBook? goals = null,
        Action? backfill = null)
    {
        _commanderClock = new D47.App.Controls.JournalClock(() => state()?.Session.LastEventAt);

        _standingBuild = _ => _standing = new StandingPage(
            state, () => host.CommanderName?.Invoke(), _commanderClock, host.Portraits?.Pictures, () => host.Model?.CommanderPictureSource?.Invoke(),
            goals, backfill, question => host.Model?.AskNow(question));

        host.Furnish(
            PanelTab.Commander,
            BuildCommander,
            new NavCrumb(StandingPage.RootKey, "Progress")
            {
                Help = D47.Core.Capabilities.Builtin.JournalCapability.Id,
            });
    }

    public void EnableStatistics(Func<D47.Core.Journal.CommanderGameState?> state, D47.Core.Activities.ActivityLedger? activities = null)
    {
        _commanderClock ??= new D47.App.Controls.JournalClock(() => state()?.Session.LastEventAt);
        var clock = _commanderClock;

        _statisticsBuild = crumb =>
        {
            var opening = crumb.Key == StatisticsPage.ActivitiesKey ? StatisticsPage.ActivitiesKey : _statisticsOpening;
            _statisticsOpening = null;

            _statistics = new StatisticsPage(
                state, clock, () => host.ClearSearch(), activities, host.ColumnWidths, opening, activities is null ? null : NavigateStatistics, host.ColumnArrangements);

            if (crumb.Key == StatisticsPage.RootKey)
            {
                _statisticsRoot = _statistics;
            }

            return _statistics;
        };

        host.Furnish(
            PanelTab.Commander,
            BuildCommander,
            new NavCrumb(StatisticsPage.RootKey, "Statistics")
            {
                Help = D47.Core.Capabilities.Builtin.JournalCapability.Id,
            });

        if (activities is not null)
        {
            host.Nav.Declare(StatisticsPage.RootKey, new NavCrumb(StatisticsPage.ActivitiesKey, "Activities") { Whole = true });
        }
    }

    /// <summary>Keeps the trail and the sidebar agreeing: Activities is a page of its own, the game's sections are the root.</summary>
    private bool NavigateStatistics(string section)
    {
        var onActivities = host.Nav.DestinationKey == StatisticsPage.ActivitiesKey;

        if (section == StatisticsPage.ActivitiesKey)
        {
            return !onActivities && host.Nav.Show(StatisticsPage.ActivitiesKey);
        }

        if (!onActivities)
        {
            return false;
        }

        _statisticsOpening = section;

        var moved = host.Nav.ToRoot();

        if (_statisticsRoot is { } root && root.Section != section)
        {
            _statisticsOpening = null;
            root.Open(section);
        }

        return moved;
    }

    public void EnableCampaign(CampaignSources sources)
    {
        var headset = host.Headset;
        var note = new CampaignNote();

        _campaignBuild = _ => _campaign = new CampaignPage(sources, host.Prompts, headset, host.ColumnWidths, host.Copy, note, host.ColumnArrangements);

        host.Furnish(
            PanelTab.Commander,
            BuildCommander,
            new NavCrumb(CampaignPage.RootKey, "BGS")
            {
                Help = D47.Core.Capabilities.Builtin.CampaignCapability.Id,
                Spoken = ["campaign", "BGS work"],
            });
    }

    public void EnableSession(Func<D47.Core.Journal.CommanderGameState?> state)
    {
        _commanderClock ??= new D47.App.Controls.JournalClock(() => state()?.Session.LastEventAt);
        var clock = _commanderClock;

        _sessionBuild = _ => _session = new SessionPage(state, clock);

        DeclareLedger(SessionPage.RootKey, "This session");
    }

    public void EnableCombatStats(
        Func<D47.Core.Journal.CommanderGameState?> state,
        Func<D47.Core.Journal.HistoryState> history,
        Func<DateTimeOffset> now)
    {
        var headset = host.Headset;

        _combatStatsBuild = _ => _combatStats = new CombatStatsPage(state, history, now, headset, host.ColumnWidths, host.Copy, host.ColumnArrangements);

        DeclareLedger(CombatStatsPage.PageKey, "Combat stats");
    }

    public void EnableCredits(
        Func<D47.Core.Journal.CommanderGameState?> state,
        Func<D47.Core.Journal.HistoryState> history,
        Func<DateTimeOffset> now)
    {
        var headset = host.Headset;
        var choice = new CreditsChoice();

        _creditsBuild = crumb =>
        {
            if (!CreditsPage.IsTransactions(crumb.Key))
            {
                return _credits = new CreditsPage(state, history, now, choice, host.Nav, headset, host.ColumnWidths, host.ColumnArrangements);
            }

            return _creditTransactions = new CreditTransactionsPage(state, history, now, choice, CreditsPage.KindOf(crumb.Key), headset, host.ColumnWidths, host.ColumnArrangements);
        };

        DeclareLedger(CreditsPage.PageKey, "Credits");
    }

    /// <summary>This surface's report press, once <see cref="EnableCodex"/> has been given the book and the tools.</summary>
    public RegionFirstPress? Press { get; private set; }

    public void EnableCodex(
        Func<D47.Core.Journal.CommanderGameState?> state,
        Func<D47.Core.Journal.HistoryState> history,
        Func<DateTimeOffset> now,
        CodexPictureSet? pictures = null,
        D47.Core.Journal.RegionFirstBook? regionFirsts = null,
        D47.Core.Capabilities.CapabilityRegistry? tools = null)
    {
        var headset = host.Headset;
        var choice = new CodexChoice();
        var catalogue = new CodexCatalogueChoice();
        var work = new CodexPictureWork();
        var press = tools is not null && regionFirsts is not null
            ? new RegionFirstPress(
                (name, arguments, cancellation) => tools.InvokeAsync(name, arguments, cancellation, D47.Core.Capabilities.ToolCaller.Commander),
                regionFirsts,
                state,
                host.Model?.WaitSound)
            : null;

        Press = press;

        _codexBuild = crumb => CodexPage.EntryOf(crumb.Key) is { } entry
            ? _codexEntry = new CodexEntryPage(state, entry.Key, entry.RegionId, crumb.Word, pictures, headset, nav: host.Nav, bodySearch: () => host.BodySearch, copy: host.Copy, waitSound: host.Model?.WaitSound, regionFirsts: regionFirsts, press: press)
            : CodexPage.PictureOf(crumb.Key) is { } picture
            ? pictures is not null && !headset ? PicturePage(crumb.Key, picture, pictures, work) : new Border()
            : CodexPage.CatalogueNodeOf(crumb.Key) is { } node
            ? _codexCatalogue = new CodexCataloguePage(state, history, catalogue, D47.Core.Knowledge.CodexCatalogue.Tree.Node(node) ?? D47.Core.Knowledge.CodexCatalogue.Tree.Root, headset, host.Nav, pictures, host.ColumnWidths, host.ColumnArrangements, () => host.BodySearch, host.Copy, period: choice, now: now, regionFirsts: regionFirsts, prompts: host.Prompts)
            : CodexPage.IsPossibleFirsts(crumb.Key)
            ? _codexPossibleFirsts = new CodexPossibleFirstsPage(state, regionFirsts, host.Nav, headset, host.ColumnWidths, host.ColumnArrangements, host.Copy, press, pictures)
            : CodexPage.IsRegion(crumb.Key)
            ? _codexRegion = new CodexRegionPage(state, history, now, choice, CodexPage.RegionOf(crumb.Key), headset, host.Nav, columnWidths: host.ColumnWidths, copy: host.Copy, columnArrangements: host.ColumnArrangements, regionFirsts: regionFirsts, catalogue: catalogue, pictures: pictures)
            : _codex = new CodexPage(state, history, now, choice, host.Nav, headset, host.ColumnWidths, host.ColumnArrangements);

        DeclareLedger(CodexPage.PageKey, "Codex");
    }

    public void EnableScreenshots(Func<D47.Core.Journal.CommanderGameState?> state, ScreenshotSet screenshots)
    {
        var headset = host.Headset;
        var browse = new ScreenshotBrowse();

        _screenshotsBuild = crumb => ScreenshotsPage.IsView(crumb.Key)
            ? new ScreenshotPicturePage(screenshots, browse, ScreenshotsPage.StemOf(crumb.Key), () => state()?.Location.StarSystem)
            : new ScreenshotsPage(screenshots, () => state()?.Location.StarSystem, headset, host.Nav, browse);

        DeclareLedger(ScreenshotsPage.PageKey, "Screenshots");
    }

    private Control PicturePage(string crumb, CodexPictureKey picture, CodexPictureSet pictures, CodexPictureWork work) =>
        CodexPage.IsChoose(crumb)
            ? new CodexChoosePage(picture, pictures, host.Nav, work)
            : new CodexCropPage(picture, pictures, host.Nav, work, CodexPage.IsRecrop(crumb));

    /// <summary>Furnishes the Ledgers root, once, and declares a ledger under it, which the page sidebar lists.</summary>
    private void DeclareLedger(string key, string word)
    {
        host.Furnish(PanelTab.Commander, BuildCommander, LedgersPage.Root);
        host.Nav.Declare(LedgersPage.RootKey, LedgersPage.Ledger(key, word));
        host.Nav.HoldPages(LedgersPage.RootKey, LedgersPage.Order);
    }

    public void EnableCommanders(CommanderRoster roster)
    {
        _commandersBuild = _ => _commanders = new CommandersPage(roster, host.ColumnWidths, host.Copy, host.ColumnArrangements);

        host.Furnish(
            PanelTab.Commander,
            BuildCommander,
            new NavCrumb(CommandersPage.RootKey, "Commanders")
            {
                Help = D47.Core.Capabilities.Builtin.JournalCapability.Id,
            });
    }

    /// <summary>Redraws the Commander page showing and its footer when the journal has moved them on.</summary>
    public bool Tick()
    {
        if (host.Tab != PanelTab.Commander)
        {
            return false;
        }

        var changed = _commanderClock?.Tick() ?? false;
        var root = host.Nav.RootKeyOf(PanelTab.Commander);
        var trail = host.Nav.TrailOf(PanelTab.Commander);
        var leaf = trail.Count > 0 ? trail[^1].Key : string.Empty;

        if (_watches is { } watches && root == WatchesPage.RootKey && leaf == WatchesPage.RootKey)
        {
            changed |= watches.Tick();
        }

        if (_missions is { } missions && root == MissionsPage.RootKey)
        {
            changed |= missions.Tick();
        }

        if (_standing is { } standing && root == StandingPage.RootKey)
        {
            changed |= standing.Tick();
        }

        if (_statistics is { } statistics && root == StatisticsPage.RootKey)
        {
            changed |= statistics.Tick();
        }

        if (_campaign is { } campaign && root == CampaignPage.RootKey && leaf == CampaignPage.RootKey)
        {
            changed |= campaign.Tick();
        }

        var ledger = root == LedgersPage.RootKey;

        if (_session is { } session && ledger && leaf == SessionPage.RootKey)
        {
            changed |= session.Tick();
        }

        if (_combatStats is { } combatStats && ledger && leaf == CombatStatsPage.PageKey)
        {
            changed |= combatStats.Tick();
        }

        if (ledger && (leaf == CreditsPage.PageKey || CreditsPage.IsTransactions(leaf)))
        {
            changed |= _credits?.Tick() ?? false;
            changed |= _creditTransactions?.Tick() ?? false;
        }

        if (ledger && (leaf == CodexPage.PageKey || CodexPage.IsRegions(leaf) || CodexPage.IsRegion(leaf) || CodexPage.IsPossibleFirsts(leaf) || CodexPage.IsEntry(leaf) || CodexPage.IsCatalogue(leaf)))
        {
            changed |= _codexCatalogue?.Tick() ?? false;
            changed |= _codexEntry?.Tick() ?? false;
            changed |= _codex?.Tick() ?? false;
            changed |= _codexRegion?.Tick() ?? false;
            changed |= _codexPossibleFirsts?.Tick() ?? false;
        }

        if (_commanders is { } commanders && root == CommandersPage.RootKey)
        {
            changed |= commanders.Tick();
        }

        return changed;
    }

    /// <summary>Draws a Commander level: the Assets pages, Missions, Progress, Statistics, BGS, This session, Ledgers and its ledgers, Commanders, or whatever the checklist roots draw.</summary>
    public Control BuildCommander(NavCrumb crumb) =>
        _assetsBuild is { } assets && InAssets(crumb) ? assets(crumb)
        : IsLedger(crumb.Key) ? new LedgersPage(Page(crumb), crumb.Key)
        : Page(crumb);


    private static bool IsLedger(string key) =>
        key is CombatStatsPage.PageKey or CreditsPage.PageKey or CodexPage.PageKey or ScreenshotsPage.PageKey or SessionPage.RootKey;

    private Control Page(NavCrumb crumb) =>
        IsWatches(crumb.Key) && _watchesBuild is { } watches ? watches(crumb)
        : crumb.Key == MissionsPage.RootKey && _missionsBuild is { } missions ? missions(crumb)
        : crumb.Key == StandingPage.RootKey && _standingBuild is { } standing ? standing(crumb)
        : (crumb.Key == StatisticsPage.RootKey || crumb.Key == StatisticsPage.ActivitiesKey) && _statisticsBuild is { } statistics ? statistics(crumb)
        : crumb.Key == CampaignPage.RootKey && _campaignBuild is { } campaign ? campaign(crumb)
        : crumb.Key == SessionPage.RootKey && _sessionBuild is { } session ? session(crumb)
        : crumb.Key == CombatStatsPage.PageKey && _combatStatsBuild is { } combatStats ? combatStats(crumb)
        : (crumb.Key == CreditsPage.PageKey || CreditsPage.IsTransactions(crumb.Key)) && _creditsBuild is { } credits ? credits(crumb)
        : (crumb.Key == CodexPage.PageKey || CodexPage.IsRegions(crumb.Key) || CodexPage.IsRegion(crumb.Key) || CodexPage.IsPossibleFirsts(crumb.Key) || CodexPage.IsEntry(crumb.Key) || CodexPage.IsPicturePage(crumb.Key) || CodexPage.IsCatalogue(crumb.Key)) && _codexBuild is { } codex ? codex(crumb)
        : (crumb.Key == ScreenshotsPage.PageKey || ScreenshotsPage.IsView(crumb.Key)) && _screenshotsBuild is { } screenshots ? screenshots(crumb)
        : crumb.Key == CommandersPage.RootKey && _commandersBuild is { } commanders ? commanders(crumb)
        : _checklistBuild is { } checklist ? checklist(crumb)
        : new TextBlock { Text = "Nothing here." };
}
