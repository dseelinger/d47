using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using D47.App.Theming;
using D47.Core.Audio;
using D47.Core.Configuration;
using D47.Core.Conversation;

namespace D47.App.Controls;

/// <summary>
/// What the last turn cost, and what d47 has cost over four running windows
/// (docs/plans/change-requests.md item 2).
/// </summary>
public sealed class SpendWindow : Window
{
    private readonly SpendTracker _session;
    private readonly SpeechSpend _speech;
    private readonly SpendLedger _ledger;
    private readonly D47Settings _settings;
    private readonly TimeZoneInfo _zone;

    /// <summary>
    /// When this process started, so "this session" is a window the ledger can be asked about (#197).
    /// </summary>
    private readonly DateTimeOffset? _launchedAt;

    /// <summary>The last turn, kept so the window can be redrawn after a reset.</summary>
    private readonly TurnCost? _turn;

    /// <summary>Where the sections live, so a reset can replace them rather than reopen the window.</summary>
    private readonly StackPanel _body = new() { Spacing = 18 };

    /// <summary>The footer's buttons, rebuilt with the sections.</summary>
    private readonly StackPanel _buttons = new() { Orientation = Orientation.Horizontal, Spacing = 10 };

    /// <summary>The session's running total, at the top right of the header.</summary>
    private readonly TextBlock _figure = new()
    {
        Name = "SpendFigure",
        FontFamily = new FontFamily(Fonts.MonoFamily),
        FontSize = TypeScale.Subheading,
        TextWrapping = TextWrapping.NoWrap,
    };

    /// <summary>The provider the drill-down is reading, kept across a redraw (#35).</summary>
    private ProviderPick? _provider;

    /// <summary>One entry in the provider picker: which kind, which id, and what to call it (#35).</summary>
    private readonly record struct ProviderPick(SpendKind Kind, string ProviderId, string Label);

    public SpendWindow(
        TurnCost? turn,
        SpendTracker session,
        SpeechSpend speech,
        SpendLedger ledger,
        D47Settings settings,
        TimeZoneInfo zone,
        DateTimeOffset? launchedAt = null)
    {
        _turn = turn;
        _session = session;
        _speech = speech;
        _ledger = ledger;
        _settings = settings;
        _zone = zone;
        _launchedAt = launchedAt;

        Title = "What this has cost";

        // 640 rather than 560 because the widest line here is a running total that names both providers and
        // both figures, and at 560 it wrapped to three lines in a two-column row.
        Width = 640;
        SizeToContent = SizeToContent.Height;

        // A ledger with five windows in it is taller than some screens, so the height is capped and the
        // scroller takes the rest.
        MaxHeight = 760;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        // Resizable, unlike ConfirmWindow beside it: that one asks a question and is done, and this one is
        // read.
        CanResize = true;
        ShowInTaskbar = false;

        Themed(_figure, TextBlock.ForegroundProperty, ThemeManager.AKey);

        Draw();

        // The modal's body scrolls vertically only; that is the whole of the fix for GitHub issue 87.
        Modal.Apply(this, "Spend", Title, _body, [_buttons], _figure);
    }

    /// <summary>Every section, from scratch.</summary>
    private void Draw()
    {
        _body.Children.Clear();

        _body.Children.Add(Estimates());

        // **Two groups, one row each** (#227).
        _body.Children.Add(Section(
            "Now",
            [
                TurnRow(_turn),
                SessionRow(_session, _speech, _settings),
                .. _ledger.Immediate(_zone).Select(window => WindowRow(window)),
                .. ColdPrefixRow(_session),
            ]));

        // Each calendar window beside its rolling twin: This week, Last 7 days, This month, Last 30 days.
        _body.Children.Add(Section(
            "Running totals",
            [.. _ledger.Windows(_zone).Select(window => WindowRow(window))]));

        _body.Children.Add(ByProviderSection());

        _figure.Text = SessionDollars(_session, _speech, _settings).ToString("C4");

        _buttons.Children.Clear();
        Buttons(_buttons);
    }

    /// <summary>Close, and — on a window that was told when the process started — Reset beside it.</summary>
    private void Buttons(StackPanel row)
    {
        var close = new Button { Content = "Close", MinWidth = 110 };
        close.Click += (_, _) => Close();

        // The mark, on the left of the row so it is not mistaken for one of the two controls that do
        // something (#252).
        row.Children.Add(SiteHelpMark.For(
            DocsSite.Capability(
                D47.Core.Capabilities.Builtin.ConversationCapability.Id, "running-totals"),
            "SpendHelp"));

        if (_launchedAt is { } launched)
        {
            var reset = new Button
            {
                Name = "SpendReset",
                Content = "Reset\u2026",
                MinWidth = 110,
                Flyout = Choices(launched),
            };

            row.Children.Add(reset);
        }

        row.Children.Add(close);
    }

    /// <summary>How far back to reset, as a menu over the button.</summary>
    private MenuFlyout Choices(DateTimeOffset launched)
    {
        var flyout = new MenuFlyout { Placement = PlacementMode.Top };

        foreach (var window in _ledger.Resettable(_zone, launched))
        {
            var item = new MenuItem { Header = window.Name };

            // The window is captured rather than re-derived on click, so what the Commander is asked about
            // and what is cleared are the same instants — a menu left open across a midnight would otherwise
            // reset a different span than it offered.
            item.Click += async (_, _) => await ResetAsync(window);

            flyout.Items.Add(item);
        }

        return flyout;
    }

    /// <summary>Asks, then resets, then redraws.</summary>
    private async Task ResetAsync(SpendPeriod window)
    {
        var standing = _ledger.Total(window);

        var asked = await new ConfirmWindow(
            "Reset the figures",
            standing.Any
                ? $"Stop counting {window.Name.ToLowerInvariant()} \u2014 {Money(standing)}?\n\n"
                  + "It leaves every running total that contained it. Nothing is deleted: a mark "
                  + "is added to data\\spend.jsonl, and removing that line by hand puts the "
                  + "figures back."
                : $"There is nothing counted {window.Name.ToLowerInvariant()}. Reset it anyway?",
            "Reset",
            "Cancel").AskAsync(this);

        if (!asked)
        {
            return;
        }

        _ledger.Reset(window);

        // Every reset clears these, even one narrower than the session: a TurnCost carries no instant, so
        // there is nothing here to filter by.
        _session.Forget();
        _speech.Forget();

        Draw();
    }

    /// <summary>A window's figure, and whether it is the whole of it.</summary>
    private static string Money(SpendTotals totals)
    {
        var line = totals.Dollars.ToString("C4", System.Globalization.CultureInfo.CurrentCulture);

        if (totals.VoiceDollars > 0m)
        {
            line += $" — {totals.ModelDollars:C4} model, {totals.VoiceDollars:C4} voice";
        }

        return totals.Complete ? line : $"at least {line}, part of it unpriced";
    }

    /// <summary>
    /// This turn, on one row (#227): what it cost, and the tokens behind it in the details cell.
    /// </summary>
    private static Control TurnRow(TurnCost? turn)
    {
        if (turn is not { } cost)
        {
            return Row("Turn", string.Empty, "no response has been given this session");
        }

        var usage = cost.Usage;

        var detail = $"{usage.TotalInputTokens:N0} in, {usage.OutputTokens:N0} out, "
                     + $"{usage.CacheReadInputTokens:N0} cached read, {usage.CacheCreationInputTokens:N0} written";

        if (usage.WebSearchRequests > 0)
        {
            // Billed separately from tokens and not small: one search costs more than an entire cheap turn,
            // so it is named rather than folded into the figure beside it.
            detail += $", {usage.WebSearchRequests:N0} web searches";
        }

        return Row("Turn", cost.Priced ? $"{cost.Dollars:C4}" : "unpriced", detail);
    }

    /// <summary>This session, on one row (#227).</summary>
    private static Control SessionRow(SpendTracker session, SpeechSpend speech, D47Settings settings) =>
        Row(
            "Session",
            SessionDollars(session, speech, settings).ToString("C4"),
            SessionDetail(session, speech, settings));

    /// <summary>The session's model turns and priced speech together (#432).</summary>
    public static decimal SessionDollars(SpendTracker session, SpeechSpend speech, D47Settings settings) =>
        session.RunningTotalDollars + speech.Dollars(settings);

    /// <summary>The turn count and the voice sentence behind the session figure.</summary>
    public static string SessionDetail(SpendTracker session, SpeechSpend speech, D47Settings settings)
    {
        var detail = $"{session.TurnCount:N0} {(session.TurnCount == 1 ? "turn" : "turns")}";

        // **The voice sentence is kept whole rather than reduced to a character count.** It already names
        // every provider that spoke and what each cost — which is what the details column is for — and it is
        // the only place a free provider is told apart from an unpriced one.
        if (speech.Describe(settings) is { Length: > 0 } voice)
        {
            detail += $", {voice}";
        }

        return detail;
    }

    /// <summary>
    /// One window's figure and the models behind it (#226). "nothing yet" sits in the details cell
    /// rather than the money one, so an empty window does not put words in a column of amounts.
    /// </summary>
    /// <param name="cap">
    /// The most shares to name before folding the rest into "and N more" — null reads every one, which
    /// is what a drill-down whose entire job is completeness needs (#35).
    /// </param>
    private static Control WindowRow((SpendPeriod Period, SpendTotals Totals) window, int? cap = 6)
    {
        var totals = window.Totals;

        return totals.Any
            ? Row(window.Period.Name, Amount(totals), Behind(totals, cap))
            : Row(window.Period.Name, string.Empty, "nothing yet");
    }

    /// <summary>Every model and voice provider used in a window, most expensive first (#226).</summary>
    private static string Behind(SpendTotals totals, int? cap = 6)
    {
        var most = cap ?? totals.Shares.Count;

        var said = totals.Shares.Take(most).Select(share =>
        {
            var what = share.Kind == SpendKind.Voice
                ? $"{share.Name} {share.Characters:N0} chars"
                : share.Name;

            if (!share.Priced)
            {
                return $"{what} (no rate for it)";
            }

            return share.Dollars > 0m ? $"{what} {share.Dollars:C4}" : $"{what} (free)";
        });

        var line = string.Join(", ", said);

        return totals.Shares.Count > most
            ? $"{line}, and {totals.Shares.Count - most:N0} more"
            : line;
    }

    /// <summary>
    /// One provider read down every period, uncapped — the question "Now" and "Running totals" read
    /// the other way round (#35).
    /// </summary>
    private Control ByProviderSection()
    {
        var stack = new StackPanel { Spacing = 4 };

        var title = new TextBlock
        {
            Text = "By provider",
            FontSize = TypeScale.Body,
            FontWeight = FontWeight.SemiBold,
        };
        Themed(title, TextBlock.ForegroundProperty, ThemeManager.AKey);
        stack.Children.Add(title);

        var picks = Providers();

        if (picks.Count == 0)
        {
            var empty = new TextBlock
            {
                Text = "nothing charged yet",
                FontSize = TypeScale.Secondary,
                TextWrapping = TextWrapping.Wrap,
            };
            Themed(empty, TextBlock.ForegroundProperty, ThemeManager.GreyKey);
            stack.Children.Add(empty);
            return stack;
        }

        if (_provider is not { } chosen || !picks.Contains(chosen))
        {
            _provider = chosen = picks[0];
        }

        var labels = picks.Select(pick => pick.Label).ToList();

        // Every provider ever charged, which only grows — always a stepper (#274).
        var (comboView, combo) = Choice.Build(labels, labels.IndexOf(chosen.Label), alwaysStepper: true);
        comboView.Name = "SpendProviderPicker";
        comboView.HorizontalAlignment = HorizontalAlignment.Left;
        comboView.MinWidth = 220;

        combo.SelectionChanged += (_, _) =>
        {
            if (combo.SelectedItem is { } label
                && picks.FirstOrDefault(pick => pick.Label == label) is { } picked)
            {
                _provider = picked;
                Draw();
            }
        };

        stack.Children.Add(comboView);

        foreach (var period in _ledger.Windows(_zone))
        {
            var totals = _ledger.Provider(period.Period, chosen.Kind, chosen.ProviderId);
            stack.Children.Add(WindowRow((period.Period, totals), cap: null));
        }

        return stack;
    }

    /// <summary>
    /// Every provider ever charged, most-recently-added-provider order collapsed into a stable
    /// alphabetical one, disambiguated where a model provider and a voice provider share a name (#35).
    /// </summary>
    private List<ProviderPick> Providers()
    {
        var named = _ledger.ProvidersCharged
            .Select(p => (p.Kind, p.ProviderId, Name: ProviderName(p.Kind, p.ProviderId)))
            .ToList();

        var collides = named
            .GroupBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return
        [
            .. named
                .Select(p => new ProviderPick(
                    p.Kind,
                    p.ProviderId,
                    collides.Contains(p.Name)
                        ? $"{p.Name} ({(p.Kind == SpendKind.Voice ? "voice" : "model")})"
                        : p.Name))
                .OrderBy(pick => pick.Label, StringComparer.OrdinalIgnoreCase),
        ];
    }

    /// <summary>What each catalog calls a provider — the model provider's name where it is one.</summary>
    private static string ProviderName(SpendKind kind, string providerId) =>
        kind == SpendKind.Voice
            ? TtsProviderCatalog.Selected(providerId).Name
            : LlmProviderCatalog.Selected(providerId).Name;

    /// <summary>
    /// The one thing in this window that asks the Commander to do something, so it keeps a row of its
    /// own rather than being folded into a details cell beside token counts (#227).
    /// </summary>
    private static string Amount(SpendTotals totals)
    {
        var figure = totals.Dollars.ToString("C4", System.Globalization.CultureInfo.CurrentCulture);

        return totals.Complete ? figure : $"≥ {figure}";
    }

    private static IReadOnlyList<Control> ColdPrefixRow(SpendTracker session) =>
        session.UnexplainedColdPrefixes > 0
            ?
            [
                Row(
                    "Cold prefixes",
                    session.UnexplainedColdPrefixes.ToString("N0"),
                    "with no cause — caching is being defeated"),
            ]
            : [];

    /// <summary>Said once, at the top, rather than as a suffix on each of a dozen figures.</summary>
    private static Control Estimates()
    {
        var line = new TextBlock
        {
            Text =
                "Estimates. D47 knows each provider's published rates, not what your account is "
                + "actually billed — a subscription with bundled credits can make the real cost "
                + "anything from higher to nothing at all.",
            FontSize = TypeScale.Secondary,
            TextWrapping = TextWrapping.Wrap,
        };

        Themed(line, TextBlock.ForegroundProperty, ThemeManager.GreyKey);

        return line;
    }

    private static Control Section(string heading, IReadOnlyList<Control> rows)
    {
        var stack = new StackPanel { Spacing = 4 };

        var title = new TextBlock { Text = heading, FontSize = TypeScale.Body, FontWeight = FontWeight.SemiBold };
        Themed(title, TextBlock.ForegroundProperty, ThemeManager.AKey);

        stack.Children.Add(title);

        foreach (var row in rows)
        {
            stack.Children.Add(row);
        }

        return stack;
    }

    /// <summary>A label, an amount and the detail behind it, on one row (#226).</summary>
    private static Control Row(string caption, string money, string detail = "")
    {
        var grid = new Grid
        {
            ColumnDefinitions =
            [
                new ColumnDefinition(2, GridUnitType.Star),
                new ColumnDefinition(1.4, GridUnitType.Star),
                new ColumnDefinition(3, GridUnitType.Star),
            ],
        };

        var label = new TextBlock { Text = caption, FontSize = TypeScale.Secondary, TextWrapping = TextWrapping.Wrap };
        Themed(label, TextBlock.ForegroundProperty, ThemeManager.GreyKey);

        var figure = new TextBlock
        {
            Text = money,
            FontSize = TypeScale.Secondary,
            FontFamily = new FontFamily(Fonts.MonoFamily),
            HorizontalAlignment = HorizontalAlignment.Right,
            TextWrapping = TextWrapping.NoWrap,
            Margin = new Thickness(0, 0, 8, 0),
        };
        Themed(figure, TextBlock.ForegroundProperty, ThemeManager.WhiteKey);

        var behind = new TextBlock
        {
            Text = detail,
            FontSize = TypeScale.Secondary,
            FontFamily = new FontFamily(Fonts.MonoFamily),
            TextWrapping = TextWrapping.Wrap,
        };
        Themed(behind, TextBlock.ForegroundProperty, ThemeManager.GreyKey);

        Grid.SetColumn(figure, 1);
        Grid.SetColumn(behind, 2);

        grid.Children.Add(label);
        grid.Children.Add(figure);
        grid.Children.Add(behind);

        return grid;
    }

    private static void Themed(AvaloniaObject target, AvaloniaProperty property, string key) =>
        target[!property] = new DynamicResourceExtension(key);
}
