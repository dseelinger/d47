using D47.Core.Capabilities;
using D47.Core.Hotas;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Ships;
using Microsoft.Extensions.Logging;

namespace D47.App.Panel;

/// <summary>The panel as the switch path sees it: every page any surface registered, the one showing, and each surface's own.</summary>
public sealed record PanelSnapshot(IReadOnlyList<PanelDestination> Destinations, string? Showing)
{
    /// <summary>The destination each surface is on, in the order the surfaces were routed.</summary>
    public IReadOnlyList<string> Shown { get; init; } = [];
}

/// <summary>What <see cref="PanelRouting.Toggle"/> did.</summary>
public enum PageToggle
{
    Opened,
    Closed,
    AlreadyOpen,
    AlreadyClosed,
}

/// <summary>Routes spoken and switch input to the panel surfaces: the window, the headset and the mini panel.</summary>
public sealed class PanelRouting(
    Func<bool> headsetShowing,
    Func<IReadOnlyList<FleetEntry>> fleet,
    ILogger<PanelRouting>? logger = null)
{
    private readonly object _registration = new();
    private readonly TranscriptMirror _transcript = new();
    private volatile Func<Heard, bool>[] _prompts = [];
    private volatile (PanelPrompts Prompts, Action<Action> Post, bool Headset)[] _promptSurfaces = [];
    private volatile PanelNavigator[] _navigators = [];
    private volatile (PanelNavigator Nav, Action<Action> Post, Action<long>? OpenSystem, Action<StarChartKind>? OpenChart)[] _surfaces = [];
    private volatile Func<PanelScrollStep, PanelScrollSide, PanelScrollOutcome>[] _scrollers = [];
    private volatile (PanelNavigator Nav, Action<Action> Post, Action<PanelFindResult>? OpenFound)[] _finders = [];
    private volatile PanelFind? _find;
    private volatile PanelSnapshot _panel = new([], null);

    /// <summary>Every page any surface offers and the one showing, read together.</summary>
    public PanelSnapshot Snapshot => _panel;

    /// <summary>Adds a surface to the places a spoken value may be destined for.</summary>
    public void RoutePrompts(Func<Heard, bool> surface)
    {
        lock (_registration)
        {
            _prompts = [.. _prompts, surface];
        }
    }

    /// <summary>Adds a panel that can be opened on a keyboard entry; <paramref name="post"/> runs on the surface's own thread.</summary>
    public void RoutePromptSurface(PanelPrompts prompts, Action<Action> post, bool headset = false)
    {
        lock (_registration)
        {
            _promptSurfaces = [.. _promptSurfaces, (prompts, post, headset)];
        }
    }

    /// <summary>
    /// Adds a surface's navigator to the ones a spoken phrase moves. <paramref name="post"/> is called from
    /// the tick and must carry a dispatcher the surface captured on its own thread. A surface with no
    /// <paramref name="openFound"/> is moved by a Find phrase only when the result is a page.
    /// </summary>
    public void RouteNavigation(
        PanelNavigator nav,
        Action<Action> post,
        bool leads = false,
        Action<long>? openSystem = null,
        Action<PanelFindResult>? openFound = null,
        Action<StarChartKind>? openChart = null)
    {
        lock (_registration)
        {
            _navigators = [.. _navigators, nav];
            _surfaces = [.. _surfaces, (nav, post, openSystem, openChart)];
            _finders = [.. _finders, (nav, post, openFound)];

            // Mirrored before the snapshot is hooked, so the first snapshot already reads the surfaces agreeing.
            if (leads)
            {
                _transcript.Lead(nav);
            }
            else
            {
                _transcript.Add(nav);
            }
        }

        nav.Changed += (_, _) => SnapshotPanel();
        SnapshotPanel();
    }

    /// <summary>Sets the index a spoken Find phrase searches (#1473).</summary>
    public void RouteFind(PanelFind find) => _find = find;

    /// <summary>Adds a surface to the ones a spoken scroll moves (#34).</summary>
    public void RouteScrolling(Func<PanelScrollStep, PanelScrollSide, PanelScrollOutcome> scroll)
    {
        lock (_registration)
        {
            _scrollers = [.. _scrollers, scroll];
        }
    }

    /// <summary>Offers what was heard to each surface in turn, and says whether one took it.</summary>
    public bool Prompted(Heard heard) => _prompts.Any(surface => surface(heard));

    /// <summary>Runs <paramref name="open"/> on the headset panel while the overlay shows, else on the window's.</summary>
    public void OnPromptSurface(Action<PanelPrompts> open)
    {
        var showing = headsetShowing();
        var surfaces = _promptSurfaces;

        foreach (var (prompts, post, headset) in surfaces)
        {
            if (headset != showing)
            {
                continue;
            }

            post(() =>
            {
                // One entry at a time, on whichever surface holds it.
                if (!surfaces.Any(s => s.Prompts.IsOpen))
                {
                    open(prompts);
                }
            });

            return;
        }
    }

    /// <summary>Puts every surface on this destination, each on its own thread (Phase 46).</summary>
    public void Show(string key)
    {
        if (RelativeTab.Direction(key) is { } direction)
        {
            foreach (var (nav, post, _, _) in _surfaces)
            {
                post(() => Step(nav, direction));
            }

            return;
        }

        foreach (var (nav, post, _, _) in _surfaces)
        {
            post(() => nav.Show(key));
        }
    }

    private void Step(PanelNavigator nav, int direction)
    {
        if (nav.Modal)
        {
            logger?.LogInformation("A dialog holds the panel, so the switch's tab step was ignored");
            return;
        }

        nav.Step(direction);
    }

    /// <summary>
    /// Opens a destination on every surface, or closes it by putting each surface on it back where it was;
    /// <paramref name="open"/> null toggles, opening it unless every surface already shows it.
    /// </summary>
    public PageToggle Toggle(string key, bool? open)
    {
        var shown = _panel.Shown;
        var anywhere = shown.Contains(key);
        var everywhere = anywhere && shown.All(showing => showing == key);
        var opening = open ?? !everywhere;

        if (opening ? everywhere : !anywhere)
        {
            return opening ? PageToggle.AlreadyOpen : PageToggle.AlreadyClosed;
        }

        if (opening)
        {
            Show(key);
            return PageToggle.Opened;
        }

        Leave(key);
        return PageToggle.Closed;
    }

    /// <summary>
    /// Puts every surface showing this destination back on the place it showed before, each on its own thread;
    /// surfaces elsewhere stay where they are.
    /// </summary>
    public void Leave(string key)
    {
        foreach (var (nav, post, _, _) in _surfaces)
        {
            post(() =>
            {
                if (nav.DestinationKey == key)
                {
                    nav.Previous();
                }
            });
        }
    }

    /// <summary>Opens the page a Commander's question was about on every surface (#575).</summary>
    public void Open(PageRef page) => PageTrail.OpenEverywhere(page, _surfaces, fleet, PromptHolds);

    /// <summary>
    /// Opens on every surface the page of <paramref name="newestFirst"/>, the last possible first among
    /// <paramref name="arrived"/>, whether or not <paramref name="enabled"/>; with none, the page of the last new codex
    /// entry while <paramref name="enabled"/>. The first tick replays the backlog and moves nothing.
    /// </summary>
    public void OpenDiscovered(IEnumerable<JournalEvent> arrived, bool first, bool enabled, CodexFirstSeen? newestFirst = null)
    {
        if (first)
        {
            return;
        }

        if (newestFirst is { } found)
        {
            Open(PageRef.CodexEntry(D47.Core.Knowledge.CodexKeys.Of(found.EntryId), found.RegionId, found.Name));
            return;
        }

        if (!enabled)
        {
            return;
        }

        if (arrived.Select(CodexDiscovery.Of).LastOrDefault(found => found is not null) is { } discovery)
        {
            Open(PageRef.CodexEntry(D47.Core.Knowledge.CodexKeys.Of(discovery.EntryId), discovery.RegionId, discovery.Name));
        }
    }

    private bool PromptHolds(PanelNavigator nav) =>
        _promptSurfaces.Any(surface => surface.Prompts.Nav == nav && surface.Prompts.IsOpen);

    /// <summary>Moves the page on every surface and says so, or null when the phrase was not a scroll (#34).</summary>
    public string? Scroll(string spoken)
    {
        if (PanelScroll.Match(spoken) is not { } heard)
        {
            return null;
        }

        return PanelScroll.Answer(heard.Step, _scrollers.Select(scroll => scroll(heard.Step, heard.Side)));
    }

    /// <summary>Moves every surface the phrase named somewhere and says what happened, or null when it named nowhere.</summary>
    public string? Navigate(string spoken)
    {
        string? said = null;

        foreach (var nav in _navigators)
        {
            var moved = PanelPhrases.Apply(spoken, nav);

            said ??= moved;
        }

        return said;
    }

    /// <summary>
    /// Opens what a Find phrase named on every surface and says its path, or says which places tied; null when
    /// the phrase was not a Find phrase or found nothing (#1473).
    /// </summary>
    public string? Find(string spoken)
    {
        if (_find is not { } find || PanelFindPhrases.Answer(spoken, find) is not { } answer)
        {
            return null;
        }

        if (answer.Open is { } found)
        {
            foreach (var (nav, post, openFound) in _finders)
            {
                post(() =>
                {
                    if (openFound is not null)
                    {
                        openFound(found);
                    }
                    else if (found.Kind == PanelFindKind.Page)
                    {
                        nav.Show(found.Target);
                    }
                });
            }
        }

        return answer.Said;
    }

    private void SnapshotPanel()
    {
        var navigators = _navigators;
        var destinations = navigators
            .SelectMany(nav => nav.Destinations)
            .DistinctBy(page => page.Key)
            .ToList();

        // What the panel is showing is what every surface agrees it is showing.
        var shown = navigators.Select(nav => nav.DestinationKey).ToList();
        var showing = shown.Distinct().ToList();

        _panel = new PanelSnapshot(destinations, showing.Count == 1 ? showing[0] : null) { Shown = shown };
    }
}
