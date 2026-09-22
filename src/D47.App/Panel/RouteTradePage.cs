using System.Globalization;
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
using D47.Core.Configuration;
using D47.Core.Interface;
using D47.Core.Knowledge;

namespace D47.App.Panel;

/// <summary>The Trade route page (#311): the plotter's own saved values, on a page of their own.</summary>
public sealed class RouteTradePage : UserControl
{
    private static readonly TimeSpan Budget = TimeSpan.FromSeconds(100);

    private readonly CapabilityRegistry _registry;
    private readonly RoutePlanBook _plans;
    private readonly PanelNavigator _nav;
    private readonly Func<bool> _lookupsEnabled;
    private readonly SettingsService _settings;
    private readonly Action? _openSettings;

    private readonly StackPanel _cards = new() { Spacing = 12 };

    public RouteTradePage(
        CapabilityRegistry registry,
        RoutePlanBook plans,
        PanelNavigator nav,
        Func<bool> lookupsEnabled,
        SettingsService settings,
        Action? openSettings = null)
    {
        _registry = registry;
        _plans = plans;
        _nav = nav;
        _lookupsEnabled = lookupsEnabled;
        _settings = settings;
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

    /// <summary>Redraws — after a plot, or after the setting behind the whole page moved.</summary>
    public void Refresh() => Dispatcher.UIThread.Post(Build);

    private void Build()
    {
        _cards.Children.Clear();

        if (!_lookupsEnabled())
        {
            _cards.Children.Add(SwitchedOff());
            return;
        }

        _cards.Children.Add(TradeCard());
    }

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

    /// <summary>The page behind this card's question mark.</summary>
    public const string TradeHelp = D47.Core.Help.HelpLibrary.GeneralPrefix + "trade-run";

    private Control TradeCard()
    {
        var trade = _settings.Current.Trade;

        var capital = Field("Credits to trade with", "how much", FieldNeed.Required);
        var hops = Field("Hops", "5");
        var maxJumps = Field("Most jumps per leg", "2");
        var maxDistance = Field("Max distance from star (ls)", "1,000");
        var maxAge = Field("Max price age (hours)", "720");

        hops.Box.Text = trade.Hops.ToString(CultureInfo.InvariantCulture);
        maxJumps.Box.Text = trade.MaxJumps.ToString(CultureInfo.InvariantCulture);
        maxDistance.Box.Text = trade.MaxStationDistance.ToString("N0", CultureInfo.InvariantCulture);
        maxAge.Box.Text = trade.MaxPriceAgeHours.ToString("N0", CultureInfo.InvariantCulture);

        var (loopRow, _, loop) = LabeledSwitch.Build("End where it started");
        var (largePadRow, _, largePad) = LabeledSwitch.Build("Large pads only");
        var (planetaryRow, _, planetary) = LabeledSwitch.Build("Planetary ports");
        var (permitRow, _, avoidPermit) = LabeledSwitch.Build("Avoid permit systems");

        loop.IsChecked = trade.Loop;
        largePad.IsChecked = trade.LargePadOnly;
        planetary.IsChecked = trade.Planetary;
        avoidPermit.IsChecked = trade.AvoidPermitSystems;

        // Everything below here saves as it is typed or switched — the credits box is the one exception,
        // wired to nothing (#311).
        void Save() => _settings.Replace(
            "the Trade route page's settings changed",
            s => s with
            {
                Trade = s.Trade with
                {
                    Hops = ParseInt(hops.Text, s.Trade.Hops, 1, 10),
                    MaxJumps = ParseInt(maxJumps.Text, s.Trade.MaxJumps, 1, 10),
                    MaxStationDistance = ParseDouble(maxDistance.Text, s.Trade.MaxStationDistance, 1, 1_000_000),
                    MaxPriceAgeHours = ParseInt(maxAge.Text, s.Trade.MaxPriceAgeHours, 1, 8_760),
                    Loop = loop.IsChecked == true,
                    LargePadOnly = largePad.IsChecked == true,
                    Planetary = planetary.IsChecked == true,
                    AvoidPermitSystems = avoidPermit.IsChecked == true,
                },
            });

        hops.Box.TextChanged += (_, _) => Save();
        maxJumps.Box.TextChanged += (_, _) => Save();
        maxDistance.Box.TextChanged += (_, _) => Save();
        maxAge.Box.TextChanged += (_, _) => Save();
        loop.IsCheckedChanged += (_, _) => Save();
        largePad.IsCheckedChanged += (_, _) => Save();
        planetary.IsCheckedChanged += (_, _) => Save();
        avoidPermit.IsCheckedChanged += (_, _) => Save();

        var form = new StackPanel
        {
            Spacing = 8,
            Children =
            {
                Row(capital, hops),
                Row(maxJumps, maxDistance),
                Row(maxAge, null),
                loopRow,
                largePadRow,
                planetaryRow,
                permitRow,
                Text(
                    "Your balance is never read from the journal and never saved — say what you "
                    + "want to trade with. It plans from the station you are docked at. Everything "
                    + "else on this page is saved, and a voice plot that says only the credits uses "
                    + "it.",
                    TypeScale.Small,
                    ThemeManager.TextMutedKey,
                    wrap: true),
            },
        };

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

        if (_plans.Last(RoutePlanKind.Trade) is { } kept)
        {
            var show = new Button
            {
                Content = "Show most recent",
                Padding = new Thickness(12, 4),
                MinHeight = 30,
            };

            show.Click += (_, _) => _nav.Drill(RoutingPages.ResultCrumb(RoutePlanKind.Trade, kept.Headline));
            actions.Children.Add(show);
        }

        CancellationTokenSource? inFlight = null;

        plot.Click += async (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(capital.Text))
            {
                status.IsVisible = true;
                status.Text = "Say how many credits to trade with. It is never inferred.";
                return;
            }

            inFlight?.Cancel();
            inFlight = new CancellationTokenSource(Budget);

            plot.IsEnabled = false;
            cancel.IsVisible = true;
            status.IsVisible = true;
            status.Text = "Working it out…";

            var before = _plans.Last(RoutePlanKind.Trade);

            try
            {
                var result = await _registry
                    .InvokeAsync(
                        "plot_trade_route",
                        Arguments(capital.Text),
                        inFlight.Token)
                    .ConfigureAwait(true);

                status.Text = result.Content;

                // The book is what the result level draws, and the capability has just written it — so
                // redrawing the page is what puts "Show most recent" on the card.
                Refresh();

                if (_plans.Last(RoutePlanKind.Trade) is { } after && !ReferenceEquals(before, after))
                {
                    _nav.Drill(RoutingPages.ResultCrumb(RoutePlanKind.Trade, after.Headline));
                }
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
        body.Children.Insert(1, FormField.Legend(required: true));

        return Card("Trade run", body, TradeHelp);
    }

    private static int ParseInt(string? text, int fallback, int min, int max) =>
        int.TryParse(
            (text ?? string.Empty).Replace(",", string.Empty).Trim(),
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var value)
            ? Math.Clamp(value, min, max)
            : fallback;

    private static double ParseDouble(string? text, double fallback, double min, double max) =>
        double.TryParse(
            (text ?? string.Empty).Replace(",", string.Empty).Trim(),
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out var value)
            ? Math.Clamp(value, min, max)
            : fallback;

    private static ToolArguments Arguments(string? capital) =>
        new(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["capital"] = (capital ?? string.Empty).Replace(",", string.Empty).Trim(),
        });

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

    private static FormField Field(string label, string placeholder, FieldNeed need = FieldNeed.Optional) =>
        new(label, placeholder, need);

    private static TextBlock Text(string text, double size, string colourKey, bool wrap = false)
    {
        var block = new TextBlock
        {
            Text = text,
            FontSize = size,
            TextWrapping = wrap ? TextWrapping.Wrap : TextWrapping.NoWrap,
            MaxWidth = wrap ? 520 : double.PositiveInfinity,
            HorizontalAlignment = HorizontalAlignment.Left,
        };

        block.Bind(
            TextBlock.ForegroundProperty,
            Application.Current!.Resources.GetResourceObservable(colourKey));

        return block;
    }

    private Control Card(string title, Control body, string? help = null)
    {
        var heading = new TextBlock { FontWeight = FontWeight.SemiBold };
        TitleText.Style(heading, TypeScale.Caption, TitleRank.Subgroup);
        TitleText.Show(heading, title);

        var headingRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            Children = { heading },
        };

        if (help is { Length: > 0 } page)
        {
            var mark = D47.App.Controls.Glyphs.Quiet(
                new Button { VerticalAlignment = VerticalAlignment.Center }, "HELP", $"About {title}");

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
            Child = stack,
        };

        CardChrome.Card(card);

        return card;
    }
}
