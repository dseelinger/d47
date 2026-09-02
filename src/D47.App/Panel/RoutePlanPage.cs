using Avalonia;
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

/// <summary>
/// Where a route comes from: the three planners, as forms (Phase 37, "Plan").
/// <para>
/// <b>One page of three cards rather than a second strip.</b> <c>PanelTabs.axaml</c> records that
/// two stacked strips is the thing the tab design avoids, and a drill to reach a form is two
/// presses to type in a box. A finished plot opens as a drill <em>level</em>, so the breadcrumb
/// carries it and Back returns to the forms.
/// </para>
/// <para>
/// <b>Nothing here plots anything itself.</b> Every button goes through
/// <see cref="CapabilityRegistry.InvokeAsync"/> — the same path the model's tool call takes, and
/// the same path the model-free keyword router already uses. Two callers of one path; not two
/// paths to one answer.
/// </para>
/// </summary>
public sealed class RoutePlanPage : UserControl
{
    /// <summary>
    /// How long a plot may run before it is given up on. The service's own budget is ninety
    /// seconds and the capability already holds it; this is the surface refusing to sit on a
    /// spinner for longer than the thing behind it can possibly take.
    /// </summary>
    private static readonly TimeSpan Budget = TimeSpan.FromSeconds(100);

    private readonly CapabilityRegistry _registry;
    private readonly RoutePlanBook _plans;
    private readonly PanelNavigator _nav;
    private readonly Func<bool> _lookupsEnabled;
    private readonly Action? _openSettings;
    private readonly Func<string?>? _here;
    private readonly Func<double?>? _jumpRange;

    /// <summary>
    /// The fields whose placeholder quotes a live figure (#253), kept so
    /// <see cref="Refresh"/> can re-read them. Jump range changes on a ship swap and on a refit,
    /// and where you are changes on every jump.
    /// </summary>
    private readonly List<FormField> _supplied = [];

    private readonly StackPanel _cards = new() { Spacing = 12 };

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

        if (!_lookupsEnabled())
        {
            // A capability that is off rather than an error, which is the same answer the tool
            // gives and for the reason Phase 3 states.
            _cards.Children.Add(SwitchedOff());
            return;
        }

        // Cleared with the cards, or a rebuild leaves the previous set of fields in here to be
        // refreshed forever after they stopped being on screen.
        _supplied.Clear();

        _cards.Children.Add(JumpCard());
        _cards.Children.Add(RichesCard());
        _cards.Children.Add(TradeCard());
    }

    /// <summary>Redraws — after a plot, or after the setting behind the whole page moved.</summary>
    public void Refresh() => Dispatcher.UIThread.Post(Build);

    private Control SwitchedOff()
    {
        var body = new StackPanel { Spacing = 8 };

        body.Children.Add(Text(
            "Route planning is switched off. It shares the galaxy search setting, so turning on "
            + "“Look things up in the galaxy” switches both on.",
            TypeScale.Body,
            ThemeManager.TextKey,
            wrap: true));

        if (_openSettings is { } open)
        {
            var button = new Button { Content = "Open settings", Padding = new Thickness(12, 4) };
            button.Click += (_, _) => open();
            body.Children.Add(new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Children = { button },
            });
        }

        return Card("Plotting is off", body);
    }

    private Control JumpCard()
    {
        // Destination was "Colonia" — an example, drawn in the same grey and the same slot as the
        // 60 below it, where the grey text genuinely is what happens if you type nothing. So the
        // one field that had to be filled looked the most like it had been answered already (#253).
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
                Text(
                    "Efficiency is how strictly the plotter holds to the direct line, so a lower "
                    + "number wanders further, finds more neutron stars and finishes in fewer jumps.",
                    TypeScale.Small,
                    ThemeManager.TextMutedKey,
                    wrap: true),

                // Asked for 2026-08-22, alongside the rename: what the galaxy map does not do is
                // the reason this card exists, and it was only ever stated in the tool description
                // — which the model reads and the Commander does not.
                Text(
                    "The galaxy map plots this too, but only in short hops and only in a straight "
                    + "line. This one reaches across the galaxy and detours through neutron stars.",
                    TypeScale.Small,
                    ThemeManager.TextMutedKey,
                    wrap: true),
            },
        };

        // "Neutron Plotter" rather than "Jump route", asked for 2026-08-22: a jump route is what
        // the in-game galaxy map already plots, and the name said nothing about the one thing this
        // does that the map cannot. The name a Commander sees only — RoutePlanKind.Jump is
        // serialised into the plan book and is the nav crumb key, so renaming it would orphan
        // every stored plan to buy nothing visible.
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

    /// <summary>
    /// The page behind each planner's question mark. Named here rather than written at the call
    /// site so the page a card opens and the test that presses it cannot spell it differently.
    /// </summary>
    public const string NeutronPlotterHelp = D47.Core.Help.HelpLibrary.GeneralPrefix + "neutron-plotter";

    /// <inheritdoc cref="NeutronPlotterHelp"/>
    public const string RichesHelp = D47.Core.Help.HelpLibrary.GeneralPrefix + "road-to-riches";

    /// <inheritdoc cref="NeutronPlotterHelp"/>
    public const string TradeHelp = D47.Core.Help.HelpLibrary.GeneralPrefix + "trade-run";

    private Control RichesCard()
    {
        var stops = Field("Stops", "10");
        var radius = Field("Radius (ly)", "500");
        var minimum = Field("Least worth stopping for (cr)", "500,000");
        var loop = new CheckBox { Content = "Come back to the start", IsChecked = true };

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

    private Control TradeCard()
    {
        // The tell that started #253: somebody hit exactly this problem, had nowhere to put the
        // answer, and put the word "required" in the placeholder — where it vanishes the moment
        // the Commander types, which is when it still needs to be true.
        var capital = Field("Credits to trade with", "how much", FieldNeed.Required);
        var hops = Field("Hops", "5");
        var maxHop = Field("Longest leg (ly)", "40");
        var loop = new CheckBox { Content = "End where it started" };
        var largePad = new CheckBox { Content = "Large pads only" };

        var form = new StackPanel
        {
            Spacing = 8,
            Children =
            {
                Row(capital, hops),
                Row(maxHop, null),
                loop,
                largePad,

                // The one figure on this page that is about the Commander rather than their ship,
                // and the reason it is typed every time rather than remembered: what they are
                // worth is nobody's business but theirs. Cargo capacity comes from the journal
                // because it is a property of the hull.
                Text(
                    "Your balance is never read from the journal and never saved — say what you "
                    + "want to trade with. It plans from the station you are docked at.",
                    TypeScale.Small,
                    ThemeManager.TextMutedKey,
                    wrap: true),
            },
        };

        return Plottable(
            "Trade run",
            form,
            RoutePlanKind.Trade,
            "plot_trade_route",
            () => Arguments(
                ("capital", capital.Text),
                ("hops", hops.Text),
                ("max_hop_distance", maxHop.Text),
                ("loop", loop.IsChecked == true ? "true" : "false"),
                ("large_pad", largePad.IsChecked == true ? "true" : "false")),
            () => string.IsNullOrWhiteSpace(capital.Text)
                ? "Say how many credits to trade with. It is never inferred."
                : null,
            TradeHelp,
            FormField.Legend(required: true));
    }

    /// <summary>
    /// One planner: its form, its button, whatever it last answered, and the pending state that
    /// a submitted job needs and a spoken answer never did.
    /// </summary>
    /// <param name="legend">
    /// The key to the marks on this form (#253), or null where the form marks nothing — Road to
    /// Riches has four fields and none of them is required, and a legend naming a mark that is not
    /// on the card sends a Commander looking for something that is not there.
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
        var plot = new Button { Content = "Plot", Padding = new Thickness(14, 4), MinHeight = 30 };
        var cancel = new Button
        {
            Content = "Cancel",
            Padding = new Thickness(12, 4),
            MinHeight = 30,
            IsVisible = false,
        };

        var status = Text(string.Empty, TypeScale.Secondary, ThemeManager.TextMutedKey, wrap: true);
        status.IsVisible = false;

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Margin = new Thickness(0, 4, 0, 0),
            Children = { plot, cancel },
        };

        if (_plans.Last(kind) is { } kept)
        {
            var show = new Button
            {
                Content = "Show the last one",
                Padding = new Thickness(12, 4),
                MinHeight = 30,
            };

            show.Click += (_, _) => _nav.Drill(RoutingPages.ResultCrumb(kind, kept.Headline));
            actions.Children.Add(show);
        }

        CancellationTokenSource? inFlight = null;

        plot.Click += async (_, _) =>
        {
            if (validate() is { } complaint)
            {
                status.IsVisible = true;
                status.Text = complaint;
                return;
            }

            inFlight?.Cancel();
            inFlight = new CancellationTokenSource(Budget);

            plot.IsEnabled = false;
            cancel.IsVisible = true;
            status.IsVisible = true;

            // A plot is a submitted job rather than a request and a reply, so the surface has to
            // say it is waiting. The spoken path never needed this: a voice answer is awaited by
            // definition.
            status.Text = kind == RoutePlanKind.Trade
                ? "Working it out…"
                : "Plotting… this is a job the service queues, so it can take a moment.";

            try
            {
                var result = await _registry
                    .InvokeAsync(tool, arguments(), inFlight.Token)
                    .ConfigureAwait(true);

                status.Text = result.Content;

                // The book is what the result level draws, and the capability has just written
                // it — so redrawing the page is what puts "Show the last one" on the card.
                Refresh();
            }
            catch (OperationCanceledException)
            {
                status.Text = "Stopped.";
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

        var body = new StackPanel { Spacing = 8, Children = { form, actions, status } };

        if (legend is not null)
        {
            body.Children.Insert(1, legend);
        }

        return Card(title, body, help);
    }

    private static ToolArguments Arguments(params (string Name, string? Value)[] values) =>
        new(values
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Value))
            // Typed with separators because that is how a Commander writes fifty million, and
            // the tool wants a number.
            .ToDictionary(pair => pair.Name, pair => pair.Value!.Replace(",", string.Empty).Trim(), StringComparer.Ordinal));

    private static StackPanel Row(FormField left, FormField? right)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };

        row.Children.Add(left.Control);

        if (right is not null)
        {
            row.Children.Add(right.Control);
        }

        return row;
    }

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

    /// <summary>
    /// Re-reads every placeholder that quotes a live figure (#253). Called from
    /// <c>PanelView.TickRouting</c>, which already watches for the Commander having moved.
    /// <para>
    /// <b>Not <see cref="Refresh"/>, which rebuilds the page.</b> This runs whenever the
    /// Commander jumps, and rebuilding then would throw away whatever they were part way through
    /// typing into one of these forms.
    /// </para>
    /// </summary>
    public void RefreshSupplied()
    {
        foreach (var field in _supplied)
        {
            field.Refresh();
        }
    }

    private static TextBlock Text(string text, double size, string colourKey, bool wrap = false)
    {
        var block = new TextBlock
        {
            Text = text,
            FontSize = size,
            TextWrapping = wrap ? TextWrapping.Wrap : TextWrapping.NoWrap,
            MaxWidth = wrap ? 520 : double.PositiveInfinity,

            // A capped width in a stretching slot centres itself, which lands a wrapped line in
            // the middle of the card with nothing above it to line up with.
            HorizontalAlignment = HorizontalAlignment.Left,
        };

        block.Bind(
            TextBlock.ForegroundProperty,
            Application.Current!.Resources.GetResourceObservable(colourKey));

        return block;
    }

    /// <param name="help">
    /// The page this card's question mark opens, or null for a card that is not a planner (asked
    /// for 2026-08-23).
    /// <para>
    /// <b>One page per planner, not one page for the tab.</b> The mark used to be absent here and
    /// the tab's own mark opened <c>routes.md</c>, which describes all three at once — so a
    /// Commander asking what <em>Efficiency</em> does read three planners' worth of prose and had
    /// to work out which third was theirs. Reported as more confusing than helpful.
    /// </para>
    /// </param>
    private Control Card(string title, Control body, string? help = null)
    {
        var heading = Text(title, TypeScale.Subheading, ThemeManager.TextKey);
        heading.FontWeight = FontWeight.SemiBold;

        var headingRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            Children = { heading },
        };

        if (help is { Length: > 0 } page)
        {
            // The settings cards' mark, in the settings cards' place — same glyph, same size,
            // same drawn-in-the-panel behaviour, so it needs no learning twice.
            var mark = new Button
            {
                Content = "?",
                FontSize = TypeScale.Secondary,
                Padding = new Thickness(5, 0),
                MinWidth = 0,
                VerticalAlignment = VerticalAlignment.Center,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
            };

            mark.Bind(
                Button.ForegroundProperty,
                Application.Current!.Resources.GetResourceObservable(ThemeManager.TextMutedKey));

            ToolTip.SetTip(mark, $"What {title} does");

            mark.Click += (_, _) => D47.Core.Help.HelpLevel.Open(_nav, page);

            headingRow.Children.Add(mark);
        }

        var stack = new StackPanel
        {
            Spacing = 10,
            Children = { headingRow, body },
        };

        var card = new Border
        {
            Padding = new Thickness(14),
            CornerRadius = new CornerRadius(4),
            BorderThickness = new Thickness(1),
            Child = stack,
        };

        card.Bind(
            Border.BackgroundProperty,
            Application.Current!.Resources.GetResourceObservable(ThemeManager.SurfaceAltKey));

        card.Bind(
            Border.BorderBrushProperty,
            Application.Current!.Resources.GetResourceObservable(ThemeManager.BorderKey));

        return card;
    }
}
