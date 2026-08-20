using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using D47.App.Theming;
using D47.Core.Capabilities;
using D47.Core.Interface;
using D47.Core.Knowledge;

namespace D47.App.Panel;

/// <summary>
/// Where a route comes from: the three planners, as forms (list.md Phase 37, "Plan").
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

    private readonly StackPanel _cards = new() { Spacing = 12 };

    public RoutePlanPage(
        CapabilityRegistry registry,
        RoutePlanBook plans,
        PanelNavigator nav,
        Func<bool> lookupsEnabled,
        Action? openSettings = null)
    {
        _registry = registry;
        _plans = plans;
        _nav = nav;
        _lookupsEnabled = lookupsEnabled;
        _openSettings = openSettings;

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
        var to = Field("Destination", "Colonia");
        var from = Field("From", "where you are now");
        var range = Field("Jump range (ly)", "this ship's");
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
            },
        };

        return Plottable(
            "Jump route",
            form,
            RoutePlanKind.Jump,
            "plot_route",
            () => Arguments(
                ("to", to.Text),
                ("from", from.Text),
                ("jump_range", range.Text),
                ("efficiency", efficiency.Text)),
            () => string.IsNullOrWhiteSpace(to.Text) ? "Name a destination first." : null);
    }

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
            () => null);
    }

    private Control TradeCard()
    {
        var capital = Field("Credits to trade with", "required");
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
                : null);
    }

    /// <summary>
    /// One planner: its form, its button, whatever it last answered, and the pending state that
    /// a submitted job needs and a spoken answer never did.
    /// </summary>
    private Control Plottable(
        string title,
        Control form,
        RoutePlanKind kind,
        string tool,
        Func<ToolArguments> arguments,
        Func<string?> validate)
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

        return Card(title, body);
    }

    private static ToolArguments Arguments(params (string Name, string? Value)[] values) =>
        new(values
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Value))
            // Typed with separators because that is how a Commander writes fifty million, and
            // the tool wants a number.
            .ToDictionary(pair => pair.Name, pair => pair.Value!.Replace(",", string.Empty).Trim(), StringComparer.Ordinal));

    /// <summary>A labelled box, and the text in it. Two things, so the caller keeps both.</summary>
    private sealed class FormField(string label, string placeholder)
    {
        private readonly TextBox _box = new()
        {
            PlaceholderText = placeholder,
            Width = 190,
            MinHeight = 30,
            HorizontalAlignment = HorizontalAlignment.Left,
        };

        public string? Text => _box.Text;

        public Control Control => Draw(label, _box);

        private static Control Draw(string label, TextBox box)
        {
            var stack = new StackPanel { Spacing = 3 };

            stack.Children.Add(Text(label, TypeScale.Small, ThemeManager.TextMutedKey));
            stack.Children.Add(box);

            return stack;
        }
    }

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

    private static FormField Field(string label, string placeholder) => new(label, placeholder);

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

    private Control Card(string title, Control body)
    {
        var heading = Text(title, TypeScale.Subheading, ThemeManager.TextKey);
        heading.FontWeight = FontWeight.SemiBold;

        var stack = new StackPanel
        {
            Spacing = 10,
            Children = { heading, body },
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
