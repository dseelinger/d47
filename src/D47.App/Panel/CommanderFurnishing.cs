using Avalonia.Controls;
using D47.Core.Interface;

namespace D47.App.Panel;

/// <summary>The Commander tab: Checklist, Missions, Standing, Statistics, This session, BGS work and Commanders.</summary>
internal sealed class CommanderFurnishing(IPanelTabHost host)
{
    private Func<NavCrumb, Control>? _checklistBuild;
    private Func<NavCrumb, Control>? _missionsBuild;
    private Func<NavCrumb, Control>? _standingBuild;
    private Func<NavCrumb, Control>? _statisticsBuild;
    private Func<NavCrumb, Control>? _sessionBuild;
    private Func<NavCrumb, Control>? _bgsWorkBuild;
    private Func<NavCrumb, Control>? _commandersBuild;
    private MissionsPage? _missions;
    private StandingPage? _standing;
    private StatisticsPage? _statistics;
    private SessionPage? _session;
    private BgsWorkPage? _bgsWork;
    private CommandersPage? _commanders;
    private D47.App.Controls.JournalClock? _commanderClock;

    public void EnableChecklist(
        D47.Core.Checklists.ChecklistService checklists,
        D47.Core.Goals.GoalBook? goals = null,
        Action? backfill = null,
        D47.Core.Activities.ActivityLedger? activities = null)
    {
        ChecklistPage? page = null;
        var root = new NavCrumb(ChecklistRoot, "Checklist")
        {
            // The suggestions level drilled from here inherits it: a proposal is still the checklist's subject.
            Help = D47.Core.Capabilities.Builtin.ChecklistCapability.Id,
        };

        ChecklistPage Page(NavCrumb crumb) =>
            new(checklists, host.Nav, host.Prompts, goals, backfill, crumb: crumb, ask: question => host.Model?.AskNow(question));

        _checklistBuild = crumb => crumb.Key switch
        {
            // Suggestions opened by a switch or by voice may be drawn before the root ever was.
            ChecklistPage.SuggestionsKey => (page ??= Page(root)).BuildSuggestions(),
            ChecklistPage.ActivitiesKey => activities is null
                ? new TextBlock { Text = "No activity dates yet." }
                : new ActivitiesPage(activities, () => checklists.Document.CommanderFid),
            _ => page = Page(crumb),
        };

        host.Furnish(PanelTab.Commander, BuildCommander, root);

        host.Nav.Declare(
            ChecklistRoot,
            ChecklistPage.Suggestions,
            () => checklists.Proposals.PendingFor(checklists.Document.CommanderFid).Count > 0);
        host.Nav.Declare(ChecklistRoot, ChecklistPage.Activities);
        host.Nav.Declare(ChecklistRoot, ChecklistPage.AllLists);
    }

    private const string ChecklistRoot = "checklist";

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

    public void EnableStanding(Func<D47.Core.Journal.CommanderGameState?> state)
    {
        _commanderClock = new D47.App.Controls.JournalClock(() => state()?.Session.LastEventAt);

        _standingBuild = _ => _standing = new StandingPage(
            state, () => host.CommanderName?.Invoke(), _commanderClock, host.Portraits?.Pictures, () => host.Model?.CommanderPictureSource?.Invoke());

        host.Furnish(
            PanelTab.Commander,
            BuildCommander,
            new NavCrumb(StandingPage.RootKey, "Standing")
            {
                Help = D47.Core.Capabilities.Builtin.JournalCapability.Id,
            });
    }

    public void EnableStatistics(Func<D47.Core.Journal.CommanderGameState?> state)
    {
        _commanderClock ??= new D47.App.Controls.JournalClock(() => state()?.Session.LastEventAt);
        var clock = _commanderClock;

        _statisticsBuild = _ => _statistics = new StatisticsPage(state, clock, () => host.ClearSearch());

        host.Furnish(
            PanelTab.Commander,
            BuildCommander,
            new NavCrumb(StatisticsPage.RootKey, "Statistics")
            {
                Help = D47.Core.Capabilities.Builtin.JournalCapability.Id,
            });
    }

    public void EnableSession(Func<D47.Core.Journal.CommanderGameState?> state)
    {
        _commanderClock ??= new D47.App.Controls.JournalClock(() => state()?.Session.LastEventAt);
        var clock = _commanderClock;

        _sessionBuild = _ => _session = new SessionPage(state, clock);

        host.Furnish(
            PanelTab.Commander,
            BuildCommander,
            new NavCrumb(SessionPage.RootKey, "This session")
            {
                Help = D47.Core.Capabilities.Builtin.JournalCapability.Id,
            });
    }

    public void EnableBgsWork(
        Func<D47.Core.Journal.CommanderGameState?> state,
        Func<DateTimeOffset?> tick,
        Func<DateTimeOffset> now)
    {
        var headset = host.Headset;

        _bgsWorkBuild = _ => _bgsWork = new BgsWorkPage(state, tick, now, host.SettingsJump, headset);

        host.Furnish(
            PanelTab.Commander,
            BuildCommander,
            new NavCrumb(BgsWorkPage.RootKey, "BGS work")
            {
                Help = D47.Core.Capabilities.Builtin.JournalCapability.Id,
            });
    }

    public void EnableCommanders(CommanderRoster roster)
    {
        _commandersBuild = _ => _commanders = new CommandersPage(roster);

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

        if (_session is { } session && root == SessionPage.RootKey)
        {
            changed |= session.Tick();
        }

        if (_bgsWork is { } bgsWork && root == BgsWorkPage.RootKey)
        {
            changed |= bgsWork.Tick();
        }

        if (_commanders is { } commanders && root == CommandersPage.RootKey)
        {
            changed |= commanders.Tick();
        }

        return changed;
    }

    /// <summary>Draws a Commander level: Missions, Standing, Statistics, This session, BGS work, Commanders, or whatever the checklist roots draw.</summary>
    public Control BuildCommander(NavCrumb crumb) =>
        crumb.Key == MissionsPage.RootKey && _missionsBuild is { } missions ? missions(crumb)
        : crumb.Key == StandingPage.RootKey && _standingBuild is { } standing ? standing(crumb)
        : crumb.Key == StatisticsPage.RootKey && _statisticsBuild is { } statistics ? statistics(crumb)
        : crumb.Key == SessionPage.RootKey && _sessionBuild is { } session ? session(crumb)
        : crumb.Key == BgsWorkPage.RootKey && _bgsWorkBuild is { } bgsWork ? bgsWork(crumb)
        : crumb.Key == CommandersPage.RootKey && _commandersBuild is { } commanders ? commanders(crumb)
        : _checklistBuild is { } checklist ? checklist(crumb)
        : new TextBlock { Text = "Nothing here." };
}
