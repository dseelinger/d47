using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using D47.App.Controls;
using D47.App.Theming;
using D47.Core.Capabilities;
using D47.Core.Interface;
using D47.Core.Knowledge;

namespace D47.App.Panel;

/// <summary>Where a route comes from: the three planners, as forms (Phase 37, "Plan").</summary>
public sealed class RoutePlanPage : UserControl
{
    /// <summary>How long a plot may run before it is given up on.</summary>
    private static readonly TimeSpan Budget = TimeSpan.FromSeconds(100);

    private readonly CapabilityRegistry _registry;
    private readonly RoutePlanBook _plans;
    private readonly PanelNavigator _nav;
    private readonly Func<bool> _lookupsEnabled;
    private readonly Action? _openSettings;
    private readonly Func<string?>? _here;
    private readonly Func<double?>? _jumpRange;

    /// <summary>
    /// The fields whose placeholder quotes a live figure (#253), kept so <see cref="Refresh"/> can
    /// re-read them.
    /// </summary>
    private readonly List<FormField> _supplied = [];

    private readonly StackPanel _cards = new();

    /// <summary>The narrowest a planner is laid out before the two stack.</summary>
    private const double PlannerWidth = 440;

    public RoutePlanPage(
        CapabilityRegistry registry,
        RoutePlanBook plans,
        PanelNavigator nav,
        Func<bool> lookupsEnabled,
        Action? openSettings = null,
        Func<string?>? here = null,
        Func<double?>? jumpRange = null)
    {
        _registry = registry;
        _plans = plans;
        _nav = nav;
        _lookupsEnabled = lookupsEnabled;
        _openSettings = openSettings;
        _here = here;
        _jumpRange = jumpRange;

        Content = new ScrollViewer
        {
            Padding = new Thickness(14),
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = _cards,
        };

        Build();
    }

    private void Build()
    {
        _cards.Children.Clear();
        _cards.Children.Add(RoutingKit.Title("Plan").Row);

        if (!_lookupsEnabled())
        {
            // A capability that is off rather than an error, which is the same answer the tool gives and for
            // the reason Phase 3 states.
            _cards.Children.Add(SwitchedOff());
            return;
        }

        // Cleared with the cards, or a rebuild leaves the previous set of fields in here to be refreshed
        // forever after they stopped being on screen.
        _supplied.Clear();

        _cards.Children.Add(Reflow.Grid([JumpCard(), RichesCard()], PlannerWidth, 28, 12, 2));
    }

    /// <summary>Redraws — after a plot, or after the setting behind the whole page moved.</summary>
    public void Refresh() => Dispatcher.UIThread.Post(Build);

    private Control SwitchedOff() =>
        RoutingKit.SwitchedOff(
            "Plotting is off",
            "Route planning is switched off. It shares the galaxy search setting, so turning on "
            + "“Look things up in the galaxy” switches both on.",
            _openSettings);

    private Control JumpCard()
    {
        // Destination was "Colonia" — an example, drawn in the same grey and the same slot as the 60 below
        // it, where the grey text genuinely is what happens if you type nothing.
        var to = Field("Destination", "a system", FieldNeed.Required);

        // The two d47 answers for itself, quoting the figure the tool call will actually use —
        // RouteCapability resolves `from` and `jump_range` from exactly these.
        var from = Field("From", "where you are now", FieldNeed.Supplied, () => _here?.Invoke(), width: 300);
        var range = Field(
            "Jump range (ly)",
            "this ship's",
            FieldNeed.Supplied,
            () => _jumpRange?.Invoke() is { } ly
                ? ly.ToString("0.##", System.Globalization.CultureInfo.CurrentCulture) + " ly"
                : null);

        var efficiency = Field("Efficiency", "60");

        var form = new StackPanel
        {
            Spacing = 8,
            Children =
            {
                Row(to, from),
                Row(range, efficiency),
                RoutingKit.Prose(
                    "Efficiency is how strictly the plotter holds to the direct line, so a lower "
                    + "number wanders further, finds more neutron stars and finishes in fewer jumps."),

                // Asked for 2026-08-22, alongside the rename: what the galaxy map does not do is the reason
                // this card exists, and it was only ever stated in the tool description — which the model
                // reads and the Commander does not.
                RoutingKit.Prose(
                    "The galaxy map plots this too, but only in short hops and only in a straight "
                    + "line. This one reaches across the galaxy and detours through neutron stars."),
            },
        };

        // "Neutron Plotter" rather than "Jump route", asked for 2026-08-22: a jump route is what the in-game
        // galaxy map already plots, and the name said nothing about the one thing this does that the map
        // cannot.
        return Plottable(
            "Neutron Plotter",
            form,
            RoutePlanKind.Jump,
            "plot_route",
            () => Arguments(
                ("to", to.Text),
                ("from", from.Text),
                ("jump_range", range.Text),
                ("efficiency", efficiency.Text)),
            () => string.IsNullOrWhiteSpace(to.Text) ? "Name a destination first." : null,
            NeutronPlotterHelp,
            FormField.Legend(required: true, supplied: true));
    }

    /// <summary>The page behind each planner's question mark.</summary>
    public const string NeutronPlotterHelp = D47.Core.Help.HelpLibrary.GeneralPrefix + "neutron-plotter";

    /// <inheritdoc cref="NeutronPlotterHelp"/>
    public const string RichesHelp = D47.Core.Help.HelpLibrary.GeneralPrefix + "road-to-riches";

    private Control RichesCard()
    {
        var stops = Field("Stops", "10");
        var radius = Field("Radius (ly)", "500");
        var minimum = Field("Least worth stopping for (cr)", "500,000");
        var loop = RoutingKit.Switch("Come back to the start");
        loop.IsChecked = true;

        var form = new StackPanel
        {
            Spacing = 8,
            Children = { Row(stops, radius), Row(minimum, null), loop },
        };

        return Plottable(
            "Road to Riches",
            form,
            RoutePlanKind.Riches,
            "plot_exploration_route",
            () => Arguments(
                ("stops", stops.Text),
                ("radius", radius.Text),
                ("minimum_value", minimum.Text),
                ("loop", loop.IsChecked == true ? "true" : "false")),
            () => null,
            RichesHelp);
    }

    /// <summary>
    /// One planner: its form, its button, whatever it last answered, and the pending state that a
    /// submitted job needs and a spoken answer never did.
    /// </summary>
    /// <param name="legend">
    /// The key to the marks on this form (#253), or null where the form marks nothing — Road to Riches
    /// has four fields and none of them is required, and a legend naming a mark that is not on the card
    /// sends a Commander looking for something that is not there.
    /// </param>
    private Control Plottable(
        string title,
        Control form,
        RoutePlanKind kind,
        string tool,
        Func<ToolArguments> arguments,
        Func<string?> validate,
        string help,
        Control? legend = null)
    {
        var plot = new Button { Content = "Plot" };
        var cancel = new Button { Content = "Cancel", IsVisible = false };

        var status = RoutingKit.Status();
        var actions = RoutingKit.Actions(plot, cancel);

        if (_plans.Last(kind) is { } kept)
        {
            var show = new Button { Content = "Show most recent", VerticalAlignment = VerticalAlignment.Top };

            show.Click += (_, _) => _nav.Drill(RoutingPages.ResultCrumb(kind, kept.Headline));
            actions.Children.Add(show);
        }

        CancellationTokenSource? inFlight = null;

        plot.Click += async (_, _) =>
        {
            if (validate() is { } complaint)
            {
                RoutingKit.Say(status, complaint, error: true);
                return;
            }

            inFlight?.Cancel();
            inFlight = new CancellationTokenSource(Budget);

            plot.IsEnabled = false;
            cancel.IsVisible = true;

            // A plot is a submitted job rather than a request and a reply, so the surface has to say it is
            // waiting.
            RoutingKit.Say(status, "Plotting… this is a job the service queues, so it can take a moment.");

            var before = _plans.Last(kind);

            try
            {
                var result = await _registry
                    .InvokeAsync(tool, arguments(), inFlight.Token)
                    .ConfigureAwait(true);

                RoutingKit.Say(status, result.Content, result.IsError);

                // The book is what the result level draws, and the capability has just written it — so
                // redrawing the page is what puts "Show most recent" on the card.
                Refresh();

                // A recorded plan is a new record in the book rather than the old one mutated (#200), so
                // reference identity says whether this call actually plotted something — ToolResult.IsError
                // does not: "No route from…", nothing worth mapping and an unseen market all come back as Ok
                // with nothing recorded (#212). A plot that recorded nothing leaves the surface on the form.
                if (_plans.Last(kind) is { } after && !ReferenceEquals(before, after))
                {
                    _nav.Drill(RoutingPages.ResultCrumb(kind, after.Headline));
                }
            }
            catch (OperationCanceledException)
            {
                RoutingKit.Say(status, "Stopped.");
            }
            finally
            {
                plot.IsEnabled = true;
                cancel.IsVisible = false;
                inFlight?.Dispose();
                inFlight = null;
            }
        };

        cancel.Click += (_, _) => inFlight?.Cancel();

        var body = new StackPanel
        {
            Spacing = 8,
            Children = { RoutingKit.Section(title, RoutingKit.Help(_nav, help, title)), form, actions, status },
        };

        if (legend is not null)
        {
            body.Children.Insert(2, legend);
        }

        return body;
    }

    private static ToolArguments Arguments(params (string Name, string? Value)[] values) =>
        new(values
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Value))
            // Typed with separators because that is how a Commander writes fifty million, and the tool wants
            // a number.
            .ToDictionary(pair => pair.Name, pair => pair.Value!.Replace(",", string.Empty).Trim(), StringComparer.Ordinal));

    private static WrapPanel Row(FormField left, FormField? right) =>
        right is null ? RoutingKit.Fields(left.Control) : RoutingKit.Fields(left.Control, right.Control);

    private FormField Field(
        string label,
        string placeholder,
        FieldNeed need = FieldNeed.Optional,
        Func<string?>? supplied = null,
        double width = 190)
    {
        var field = new FormField(label, placeholder, need, supplied, width);

        if (need == FieldNeed.Supplied)
        {
            _supplied.Add(field);
        }

        return field;
    }

    /// <summary>Re-reads every placeholder that quotes a live figure (#253).</summary>
    public void RefreshSupplied()
    {
        foreach (var field in _supplied)
        {
            field.Refresh();
        }
    }
}
