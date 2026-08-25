using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using D47.App.Theming;
using D47.Core.Capabilities;
using D47.Core.Journal;
using D47.Core.Knowledge;

namespace D47.App.Panel;

/// <summary>
/// Everything one build still needs, and where to buy it (list.md Phase 50).
/// <para>
/// <b>On the Checklist tab rather than on Routing.</b> The Commander is looking at what they owe,
/// and <em>where to get it</em> belongs beside <em>what is left</em> rather than beside route
/// plotting. It also costs no tool-surface bytes at all, which is the lever Phase 47 used and
/// Phase 49 used again — and with 103 bytes spare after the widening, it is the half that could
/// afford to be generous.
/// </para>
/// <para>
/// <b>Nothing here searches anything itself.</b> The button invokes <c>get_construction_needs</c>
/// through the registry, exactly as the model does and exactly as the keyword router does; the
/// table is read back from <see cref="SourcingBoard"/>, which the capability writes on its way out.
/// So a Commander who asks by voice and then looks at the tab sees the answer they were just given
/// rather than a second search that might disagree with it. The same arrangement
/// <see cref="RouteMarketPage"/> makes, pointed at a bigger question.
/// </para>
/// <para>
/// <b>And this is where the carrier is told.</b> What is on a fleet carrier is not derivable —
/// reconciling <c>CargoTransfer</c> against <c>CarrierStats</c> came out wrong 679 times against
/// right 347 — so d47 does not guess, and the Commander can simply say. A figure entered here is
/// taken off the shopping list and never off the site's own outstanding figures, which stay exactly
/// what the depot wrote.
/// </para>
/// </summary>
public sealed class SourcingPage : UserControl
{
    /// <summary>The Checklist tab's second root.</summary>
    public const string RootKey = "checklist.sourcing";

    /// <summary>
    /// How long a search may run. The sweep's own page budget is twenty seconds each across three
    /// pages, and this is the surface declining to sit on a spinner longer than that can take.
    /// </summary>
    private static readonly TimeSpan Budget = TimeSpan.FromSeconds(70);

    private readonly CapabilityRegistry _registry;
    private readonly SourcingBoard _board;
    private readonly CarrierManifest? _carrier;
    private readonly Func<CommanderGameState?> _commander;
    private readonly Func<bool> _lookupsEnabled;
    private readonly Action? _openSettings;

    private readonly StackPanel _body = new() { Spacing = 12 };

    /// <summary>
    /// The two boxes, <b>built fresh on every draw rather than kept</b>. A control belongs to
    /// exactly one visual tree, and this page redraws itself whenever a figure is entered — a kept
    /// box is then added to a second parent while the first still holds it, which Avalonia refuses
    /// outright. Found by the test that types into them twice.
    /// </summary>
    private TextBox _commodity = Box("Tritium", 190);

    private TextBox _tonnes = Box("how many", 120);

    /// <summary>The last thing said, kept as a string so the block itself can be rebuilt.</summary>
    private string _said = string.Empty;

    private TextBlock _status;

    public SourcingPage(
        CapabilityRegistry registry,
        SourcingBoard board,
        CarrierManifest? carrier,
        Func<CommanderGameState?> commander,
        Func<bool> lookupsEnabled,
        Action? openSettings = null)
    {
        _registry = registry;
        _board = board;
        _carrier = carrier;
        _commander = commander;
        _lookupsEnabled = lookupsEnabled;
        _openSettings = openSettings;

        _status = Text(string.Empty, TypeScale.Secondary, ThemeManager.TextMutedKey, wrap: true);
        _status.IsVisible = false;

        Content = new ScrollViewer
        {
            Padding = new Thickness(14),
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = _body,
        };

        Build();
    }

    /// <summary>Redraws, after a search, after a carrier figure, or after the setting moved.</summary>
    public void Refresh() => Dispatcher.UIThread.Post(Build);

    private void Build()
    {
        _body.Children.Clear();

        var site = Site();

        if (site is null)
        {
            _body.Children.Add(Card(
                "No site to source for",
                Text(
                    "Nothing in this journal is under construction. Dock at a colonisation "
                    + "construction site and D47 will read its manifest.",
                    TypeScale.Body,
                    ThemeManager.TextKey,
                    wrap: true)));

            return;
        }

        _body.Children.Add(SiteCard(site));
        _body.Children.Add(CarrierCard(site));

        if (!_lookupsEnabled())
        {
            _body.Children.Add(SwitchedOff());
            return;
        }

        _body.Children.Add(SearchCard(site));
        DrawPlan();
    }

    /// <summary>
    /// Says something, and remembers it — the page rebuilds itself on every change, so a block
    /// holding the only copy would lose the sentence the moment a carrier figure was entered.
    /// </summary>
    private void Say(string said)
    {
        _said = said;
        _status.Text = said;
        _status.IsVisible = said.Length > 0;
    }

    /// <summary>
    /// The site this page is about: the selected one, the way <c>get_construction_needs</c> already
    /// chooses. <b>One site at a time</b>, because sourcing two builds at once is not two answers
    /// side by side — a station covering four commodities for one and two for the other is not
    /// comparable to one covering six for either — and nobody has asked for it.
    /// </summary>
    private ConstructionSite? Site() =>
        _commander()?.Colonisation.Active is { Count: > 0 } open ? open[0] : null;

    private Control SiteCard(ConstructionSite site)
    {
        var body = new StackPanel { Spacing = 6 };

        var left = site.Outstanding.Sum(resource => resource.Remaining);

        body.Children.Add(Text(
            $"{site.Where} — {site.Progress * 100:0}% built, "
            + $"{site.Outstanding.Count} commodit{(site.Outstanding.Count == 1 ? "y" : "ies")} outstanding, "
            + $"{left:N0} t in all.",
            TypeScale.Body,
            ThemeManager.TextKey,
            wrap: true));

        body.Children.Add(Text(
            "These figures are as of your last visit to the site, not live.",
            TypeScale.Small,
            ThemeManager.TextMutedKey,
            wrap: true));

        return Card("The build", body);
    }

    /// <summary>
    /// What the Commander says is aboard the carrier, and the one place they can say it.
    /// </summary>
    private Control CarrierCard(ConstructionSite site)
    {
        var body = new StackPanel { Spacing = 8 };

        if (_carrier is null)
        {
            body.Children.Add(Text(
                "Carrier figures are not available on this surface.",
                TypeScale.Body,
                ThemeManager.TextMutedKey,
                wrap: true));

            return Card("On the carrier", body);
        }

        body.Children.Add(Text(
            "D47 cannot see inside a fleet carrier — the journal's transfer events do not add up to "
            + "an inventory, so it does not guess. Tell it what is aboard and it comes off the "
            + "shopping list below. What the site itself still owes is untouched.",
            TypeScale.Small,
            ThemeManager.TextMutedKey,
            wrap: true));

        _commodity = Box("Tritium", 190);
        _tonnes = Box("how many", 120);

        var add = new Button { Content = "It's aboard", Padding = new Thickness(12, 4), MinHeight = 30 };

        add.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(_commodity.Text)
                || !int.TryParse(
                    (_tonnes.Text ?? string.Empty).Replace(",", string.Empty).Trim(),
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var tonnes))
            {
                Say("Name a commodity and a tonnage.");
                return;
            }

            _carrier.Set(
                _commander()?.Identity.FrontierId,
                _commodity.Text!.Trim(),
                tonnes,
                DateTimeOffset.Now);

            _commodity.Text = string.Empty;
            _tonnes.Text = string.Empty;
            Refresh();
        };

        body.Children.Add(new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            Children = { Labelled("Commodity", _commodity), Labelled("Tonnes", _tonnes), Bottom(add) },
        });

        var aboard = _carrier.For(_commander()?.Identity.FrontierId);

        if (aboard.Count == 0)
        {
            return Card("On the carrier", body);
        }

        var rows = new StackPanel { Spacing = 4 };

        foreach (var stock in aboard)
        {
            var forget = new Button
            {
                Content = "Forget",
                Padding = new Thickness(8, 2),
                FontSize = TypeScale.Small,
            };

            var held = stock;

            forget.Click += (_, _) =>
            {
                _carrier.Set(_commander()?.Identity.FrontierId, held.Commodity, 0, DateTimeOffset.Now);
                Refresh();
            };

            // Dated on every row, because this is the one number in a sourcing plan D47 has no way
            // of checking: a week-old "300 tritium" is a week-old memory of a carrier that has been
            // flown since.
            rows.Children.Add(new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 10,
                Children =
                {
                    Cell($"{stock.Tonnes:N0} t", 80),
                    Cell(stock.Commodity, 200),
                    Cell($"you said so {Ago(DateTimeOffset.Now - stock.SaidAt)}", 190, muted: true),
                    forget,
                },
            });
        }

        body.Children.Add(rows);

        return Card("On the carrier", body);
    }

    private Control SwitchedOff()
    {
        var body = new StackPanel { Spacing = 8 };

        body.Children.Add(Text(
            "Looking up markets is switched off. It shares the galaxy search setting, so turning on "
            + "“Look things up in the galaxy” switches both on.",
            TypeScale.Body,
            ThemeManager.TextKey,
            wrap: true));

        if (_openSettings is { } open)
        {
            var button = new Button { Content = "Open settings", Padding = new Thickness(12, 4) };
            button.Click += (_, _) => open();
            body.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Children = { button } });
        }

        return Card("Market lookups are off", body);
    }

    private Control SearchCard(ConstructionSite site)
    {
        _status = Text(_said, TypeScale.Secondary, ThemeManager.TextMutedKey, wrap: true);
        _status.IsVisible = _said.Length > 0;

        var find = new Button { Content = "Where to buy it", Padding = new Thickness(12, 4), MinHeight = 30 };

        var cancel = new Button
        {
            Content = "Cancel",
            Padding = new Thickness(12, 4),
            MinHeight = 30,
            IsVisible = false,
        };

        CancellationTokenSource? inFlight = null;

        find.Click += async (_, _) =>
        {
            inFlight?.Cancel();
            inFlight = new CancellationTokenSource(Budget);

            find.IsEnabled = false;
            cancel.IsVisible = true;
            Say("Reading the markets nearby…");

            try
            {
                var result = await _registry
                    .InvokeAsync(
                        "get_construction_needs",
                        new ToolArguments(new Dictionary<string, string>(StringComparer.Ordinal)
                        {
                            ["site"] = site.StationName ?? site.StarSystem ?? string.Empty,
                            ["where_to_buy"] = "true",
                        }),
                        inFlight.Token)
                    .ConfigureAwait(true);

                // **The sentence does not stay.** That is the Market page's arrangement and it is
                // right there, where the spoken answer is three lines; a build's is twenty and says
                // exactly what the table below is about to say again. The caveats it carries — what
                // could not be priced, what ran short, how many markets were too old — are all on
                // the table, so what is left for this line is failure and progress.
                Say(string.Empty);
                Refresh();
            }
            catch (OperationCanceledException)
            {
                Say("Stopped.");
            }
            finally
            {
                find.IsEnabled = true;
                cancel.IsVisible = false;
                inFlight?.Dispose();
                inFlight = null;
            }
        };

        cancel.Click += (_, _) => inFlight?.Cancel();

        var body = new StackPanel
        {
            Spacing = 8,
            Children =
            {
                Text(
                    "Which stations between them stock the whole list, fewest stops first. Not a "
                    + "course — you will fly this loop a dozen times, and the order is on the "
                    + "Routing tab.",
                    TypeScale.Small,
                    ThemeManager.TextMutedKey,
                    wrap: true),
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 8,
                    Children = { find, cancel },
                },
                _status,
            },
        };

        return Card("The shopping list", body);
    }

    private void DrawPlan()
    {
        if (_board.Last is not { } posting)
        {
            return;
        }

        var stack = new StackPanel { Spacing = 10 };

        var heading = Text(
            $"{posting.Site}, out of {posting.Near}",
            TypeScale.Subheading,
            ThemeManager.TextKey);

        heading.FontWeight = FontWeight.SemiBold;
        stack.Children.Add(heading);

        if (posting.Carrier.Count > 0)
        {
            stack.Children.Add(Text(
                "Taken off for the carrier: "
                + string.Join(", ", posting.Carrier.Select(stock => $"{stock.Tonnes:N0} t {stock.Commodity}"))
                + ".",
                TypeScale.Secondary,
                ThemeManager.TextKey,
                wrap: true));
        }

        if (posting.Answer.Plan.Stops.Count == 0)
        {
            stack.Children.Add(Text(
                "Nothing in range is selling any of it.",
                TypeScale.Body,
                ThemeManager.TextKey,
                wrap: true));
        }

        foreach (var stop in posting.Answer.Plan.Stops)
        {
            stack.Children.Add(Stop(stop, posting.Answer.OriginKnown));
        }

        // **Nothing is dropped in silence.** Every outstanding row either resolves to a station
        // above or is named here, and found-but-short is separate from never-found: "widen the
        // search" is the right advice for one and useless for the other.
        if (posting.Answer.Plan.Unpriced.Count > 0)
        {
            stack.Children.Add(Text(
                "Nothing in range prices: " + string.Join(", ", posting.Answer.Plan.Unpriced) + ".",
                TypeScale.Secondary,
                ThemeManager.DangerKey,
                wrap: true));
        }

        if (posting.Answer.Plan.Shortfalls.Count > 0)
        {
            stack.Children.Add(Text(
                "Stocked but not enough: "
                + string.Join(
                    ", ",
                    posting.Answer.Plan.Shortfalls
                        .OrderByDescending(pair => pair.Value)
                        .Select(pair => $"{pair.Key} by {pair.Value:N0} t"))
                + ".",
                TypeScale.Secondary,
                ThemeManager.DangerKey,
                wrap: true));
        }

        stack.Children.Add(Text(
            $"Worked out {Ago(DateTimeOffset.UtcNow - posting.AskedAt)}"
            + (posting.Answer.DroppedAsStale > 0
                ? $", with {posting.Answer.DroppedAsStale} market"
                  + $"{(posting.Answer.DroppedAsStale == 1 ? string.Empty : "s")} left out for quoting "
                  + "prices too old to trust"
                : string.Empty)
            + ". Prices are other Commanders' reports, and supply is stripped fastest during a rush.",
            TypeScale.Small,
            ThemeManager.TextMutedKey,
            wrap: true));

        _body.Children.Add(Card("What came back", stack));
    }

    private static Control Stop(SourcingStop stop, bool distances)
    {
        var body = new StackPanel { Spacing = 4 };

        var head = Text(
            $"{stop.Market.Station} ({stop.Market.System})"
            + (distances ? $", {stop.Distance:0.#} ly" : string.Empty)
            + $" — covers {stop.Covers}, {stop.Total:N0} cr",
            TypeScale.Body,
            ThemeManager.TextKey,
            wrap: true);

        head.FontWeight = FontWeight.SemiBold;
        body.Children.Add(head);

        foreach (var lot in stop.Lots.OrderByDescending(lot => lot.Tonnes))
        {
            body.Children.Add(new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 10,
                Children =
                {
                    Cell($"{lot.Tonnes:N0} t", 80),
                    Cell(lot.Commodity, 200),
                    Cell($"{lot.UnitPrice:N0} cr", 100, muted: true),
                    Cell($"{lot.Total:N0} cr", 120, muted: true),
                },
            });
        }

        return body;
    }

    private static string Ago(TimeSpan old) => old switch
    {
        { TotalMinutes: < 2 } => "just now",
        { TotalHours: < 1 } => "within the hour",
        { TotalHours: < 2 } => "an hour ago",
        { TotalHours: < 24 } => $"{old.TotalHours:0} hours ago",
        { TotalDays: < 2 } => "yesterday",
        { TotalDays: < 14 } => $"{old.TotalDays:0} days ago",
        { TotalDays: < 21 } => "a fortnight ago",
        _ => $"{old.TotalDays / 7:0} weeks ago",
    };

    private static Control Cell(string text, double width, bool muted = false)
    {
        var block = Text(text, TypeScale.Secondary, muted ? ThemeManager.TextMutedKey : ThemeManager.TextKey);

        block.Width = width;
        block.TextTrimming = TextTrimming.CharacterEllipsis;

        return block;
    }

    private static TextBox Box(string placeholder, double width) => new()
    {
        PlaceholderText = placeholder,
        Width = width,
        MinHeight = 30,
        HorizontalAlignment = HorizontalAlignment.Left,
    };

    private static Control Bottom(Control control)
    {
        control.VerticalAlignment = VerticalAlignment.Bottom;

        return control;
    }

    private static Control Labelled(string label, Control box)
    {
        var stack = new StackPanel { Spacing = 3 };

        stack.Children.Add(Text(label, TypeScale.Small, ThemeManager.TextMutedKey));
        stack.Children.Add(box);

        return stack;
    }

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

    private static Control Card(string title, Control body)
    {
        var heading = Text(title, TypeScale.Subheading, ThemeManager.TextKey);
        heading.FontWeight = FontWeight.SemiBold;

        var card = new Border
        {
            Padding = new Thickness(14),
            CornerRadius = new CornerRadius(4),
            BorderThickness = new Thickness(1),
            Child = new StackPanel { Spacing = 10, Children = { heading, body } },
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
