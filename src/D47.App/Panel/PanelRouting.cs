using D47.Core.Capabilities;
using D47.Core.Interface;
using D47.Core.Ships;

namespace D47.App.Panel;

/// <summary>The panel as the switch path sees it: every page any surface registered, and the one showing.</summary>
public sealed record PanelSnapshot(IReadOnlyList<PanelDestination> Destinations, string? Showing);

/// <summary>Routes spoken and switch input to the panel surfaces: the window, the headset and the mini panel.</summary>
public sealed class PanelRouting(Func<bool> headsetShowing, Func<IReadOnlyList<FleetEntry>> fleet)
{
    private readonly List<Func<Heard, bool>> _prompts = [];
    private readonly List<(PanelPrompts Prompts, Action<Action> Post, bool Headset)> _promptSurfaces = [];
    private readonly List<PanelNavigator> _navigators = [];
    private readonly TranscriptMirror _transcript = new();
    private readonly List<(PanelNavigator Nav, Action<Action> Post, Action<long>? OpenSystem)> _surfaces = [];
    private readonly List<Func<PanelScrollStep, PanelScrollOutcome>> _scrollers = [];
    private volatile PanelSnapshot _panel = new([], null);

    /// <summary>Every page any surface offers and the one showing, read together.</summary>
    public PanelSnapshot Snapshot => _panel;

    /// <summary>Adds a surface to the places a spoken value may be destined for.</summary>
    public void RoutePrompts(Func<Heard, bool> surface) => _prompts.Add(surface);

    /// <summary>Adds a panel that can be opened on a keyboard entry; <paramref name="post"/> runs on the surface's own thread.</summary>
    public void RoutePromptSurface(PanelPrompts prompts, Action<Action> post, bool headset = false) =>
        _promptSurfaces.Add((prompts, post, headset));

    /// <summary>
    /// Adds a surface's navigator to the ones a spoken phrase moves. <paramref name="post"/> is called from
    /// the tick and must carry a dispatcher the surface captured on its own thread.
    /// </summary>
    public void RouteNavigation(
        PanelNavigator nav, Action<Action> post, bool leads = false, Action<long>? openSystem = null)
    {
        _navigators.Add(nav);
        _surfaces.Add((nav, post, openSystem));

        // Mirrored before the snapshot is hooked, so the first snapshot already reads the surfaces agreeing.
        if (leads)
        {
            _transcript.Lead(nav);
        }
        else
        {
            _transcript.Add(nav);
        }

        nav.Changed += (_, _) => SnapshotPanel();
        SnapshotPanel();
    }

    /// <summary>Adds a surface to the ones a spoken scroll moves (#34).</summary>
    public void RouteScrolling(Func<PanelScrollStep, PanelScrollOutcome> scroll) => _scrollers.Add(scroll);

    /// <summary>Offers what was heard to each surface in turn, and says whether one took it.</summary>
    public bool Prompted(Heard heard) => _prompts.Any(surface => surface(heard));

    /// <summary>Runs <paramref name="open"/> on the headset panel while the overlay shows, else on the window's.</summary>
    public void OnPromptSurface(Action<PanelPrompts> open)
    {
        var showing = headsetShowing();

        foreach (var (prompts, post, headset) in _promptSurfaces)
        {
            if (headset != showing)
            {
                continue;
            }

            post(() =>
            {
                // One entry at a time, on whichever surface holds it.
                if (!_promptSurfaces.Any(s => s.Prompts.IsOpen))
                {
                    open(prompts);
                }
            });

            return;
        }
    }

    /// <summary>Puts every surface on this page, each on its own thread (Phase 46).</summary>
    public void Show(string rootKey)
    {
        foreach (var (nav, post, _) in _surfaces)
        {
            post(() => nav.Show(rootKey));
        }
    }

    /// <summary>Opens the page a Commander's question was about on every surface (#575).</summary>
    public void Open(PageRef page) => PageTrail.OpenEverywhere(page, _surfaces, fleet);

    /// <summary>Moves the page on every surface and says so, or null when the phrase was not a scroll (#34).</summary>
    public string? Scroll(string spoken)
    {
        if (PanelScroll.Match(spoken) is not { } step)
        {
            return null;
        }

        return PanelScroll.Answer(step, _scrollers.Select(scroll => scroll(step)));
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

    private void SnapshotPanel()
    {
        var destinations = _navigators
            .SelectMany(nav => nav.Destinations)
            .DistinctBy(page => page.Root.Key)
            .ToList();

        // What the panel is showing is what every surface agrees it is showing.
        var showing = _navigators.Select(nav => nav.Root.Key).Distinct().ToList();

        _panel = new PanelSnapshot(destinations, showing.Count == 1 ? showing[0] : null);
    }
}
