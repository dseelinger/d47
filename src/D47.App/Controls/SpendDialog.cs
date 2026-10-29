using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using D47.App.Panel;
using D47.App.Theming;
using D47.Core.Audio;
using D47.Core.Configuration;
using D47.Core.Conversation;

namespace D47.App.Controls;

/// <summary>
/// What the last turn cost, and what d47 has cost over four running windows
/// (docs/plans/change-requests.md item 2).
/// </summary>
public sealed class SpendDialog : ModalDialog
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

    /// <summary>The last turn, kept so the dialog can be redrawn after a reset.</summary>
    private readonly TurnCost? _turn;

    /// <summary>Where the sections live, so a reset can replace them rather than reopen the dialog.</summary>
    private readonly StackPanel _body = new() { Spacing = Modal.BlockGap };

    /// <summary>The footer's buttons, rebuilt with the sections.</summary>
    private readonly StackPanel _buttons = new() { Orientation = Orientation.Horizontal, Spacing = Gaps.Tile };

    /// <summary>The session's running total, at the top right of the header.</summary>
    private readonly TextBlock _figure = new()
    {
        Name = "SpendFigure",
        FontFamily = new FontFamily(Fonts.MonoFamily),
        FontSize = TypeScale.Subheading,
        TextWrapping = TextWrapping.NoWrap,
    };

    public SpendDialog(
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

        // A ledger with five windows in it is taller than some screens, so the height is capped and the
        // scroller takes the rest.
        MaxHeight = 740;

        ThemeInk.Bind(_figure, TextBlock.ForegroundProperty, ThemeManager.AKey);

        Draw();

        // The modal's body scrolls vertically only; that is the whole of the fix for GitHub issue 87.
        Modal.Apply(this, "Spend", Title, _body, [_buttons], _figure);
    }

    /// <summary>Every section, from scratch.</summary>
    private void Draw()
    {
        _body.Children.Clear();

        _body.Children.Add(Estimates());

        _body.Children.Add(Section(
            "Now",
            [
                TurnRow(_turn),
                SessionRow(_session, _speech, _settings),
                .. ColdPrefixRow(_session),
            ]));

        _body.Children.Add(Section("Totals", [Totals()], "≥ means part of it has no rate"));

        if (_launchedAt is { } launched)
        {
            _body.Children.Add(ResetSection(launched));
        }

        _figure.Text = SessionDollars(_session, _speech, _settings).ToString("C4");

        _buttons.Children.Clear();
        Buttons(_buttons);
    }

    /// <summary>The help mark and Close.</summary>
    private void Buttons(StackPanel row)
    {
        var close = new Button { Content = "Close", MinWidth = 110, MinHeight = TypeScale.MinimumTarget };
        close.Click += (_, _) => Close();

        // The mark, on the left of the row so it is not mistaken for one of the two controls that do
        // something (#252).
        var help = SiteHelpMark.For(
            DocsSite.Capability(
                D47.Core.Capabilities.Builtin.ConversationCapability.Id, "running-totals"),
            "SpendHelp");

        help.MinHeight = TypeScale.MinimumTarget;
        row.Children.Add(help);

        row.Children.Add(close);
    }

    /// <summary>How far back to reset, as a list that opens in the page.</summary>
    private Control ResetSection(DateTimeOffset launched)
    {
        // The windows are captured here rather than re-derived on pick, so what the Commander is asked
        // about and what is cleared are the same instants.
        var windows = _ledger.Resettable(_zone, launched).ToDictionary(window => window.Name);

        var picker = new InlinePicker { Name = "SpendReset", Label = "Reset" };
        picker.Show(
            [.. windows.Keys.Select(name => new InlinePickerOption(name, name, ThemeManager.WhiteKey))],
            string.Empty,
            "Choose a window to reset",
            ThemeManager.GreyKey,
            null);

        picker.Picked += async (_, id) =>
        {
            if (windows.TryGetValue(id, out var window))
            {
                await ResetAsync(window);
            }
        };

        return Section("Reset", [picker]);
    }

    /// <summary>Asks, then resets, then redraws.</summary>
    private async Task ResetAsync(SpendPeriod window)
    {
        if (TopLevel.GetTopLevel(this) is not Window owner)
        {
            return;
        }

        var standing = _ledger.Total(window);

        var asked = await new ConfirmDialog(
            "Reset the figures",
            standing.Any
                ? $"Stop counting {window.Name.ToLowerInvariant()} \u2014 {Money(standing)}?\n\n"
                  + "It leaves every running total that contained it. Nothing is deleted: a mark "
                  + "is added to data\\spend.jsonl, and removing that line by hand puts the "
                  + "figures back."
                : $"There is nothing counted {window.Name.ToLowerInvariant()}. Reset it anyway?",
            "Reset",
            "Cancel").AskAsync(owner);

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
            $"{session.TurnCount:N0} {(session.TurnCount == 1 ? "turn" : "turns")} · {BilledCharacters(speech):N0} chars");

    /// <summary>Characters spoken through voice providers that charge.</summary>
    private static long BilledCharacters(SpeechSpend speech) =>
        speech.Charges
            .Where(charge => TtsProviderCatalog.Selected(charge.ProviderId).Billed)
            .Sum(charge => charge.Characters);

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

    private const double FigureColumn = 86;

    private static readonly string Dash = "—";

    private static bool IsFree(SpendShare share) => share.Priced && share.Dollars == 0m;

    private static string Money(decimal dollars) =>
        dollars.ToString("C4", System.Globalization.CultureInfo.CurrentCulture);

    private readonly record struct FigureCell(string Text, string Key, string? Use = null);

    /// <summary>One row per window total, then each charged provider with its models under it.</summary>
    private Control Totals()
    {
        var windows = new List<(SpendPeriod Period, SpendTotals Totals)>([.. _ledger.Immediate(_zone), .. _ledger.Windows(_zone)]);
        var charged = windows.Select(w => w.Totals.Shares.Where(share => !IsFree(share)).ToList()).ToList();

        if (charged.All(shares => shares.Count == 0))
        {
            var empty = new TextBlock { Text = "nothing charged yet", FontSize = TypeScale.Secondary };
            ThemeInk.Bind(empty, TextBlock.ForegroundProperty, ThemeManager.GreyKey);
            return empty;
        }

        var stack = new StackPanel { Spacing = 0 };
        stack.Children.Add(TableRow(null, [.. windows.Select(w => w.Period.Name)]));

        var total = windows.Select((w, i) => w.Totals.Complete && charged[i].Count == 0
            ? new FigureCell(Dash, ThemeManager.Grey2Key)
            : new FigureCell(
                (w.Totals.Complete ? string.Empty : "≥ ") + Money(w.Totals.Dollars),
                ThemeManager.WhiteKey)).ToList();
        stack.Children.Add(TableRow(Label("All", ThemeManager.WhiteKey, 0, FontWeight.SemiBold), total));

        var last = windows.Count - 1;

        var providers = charged
            .SelectMany(shares => shares)
            .Select(share => (share.Kind, Provider: share.Provider.ToLowerInvariant()))
            .Distinct()
            .OrderByDescending(key => charged[last]
                .Where(share => Is(share, key.Kind, key.Provider))
                .Sum(share => share.Dollars))
            .ThenBy(key => ProviderName(key.Kind, key.Provider), StringComparer.OrdinalIgnoreCase);

        foreach (var (kind, provider) in providers)
        {
            var own = charged
                .Select(shares => shares.Where(share => Is(share, kind, provider)).ToList())
                .ToList();

            var cells = own.Select(shares => ProviderCell(kind, shares)).ToList();

            stack.Children.Add(TableRow(
                Label(ProviderName(kind, provider), ThemeManager.WhiteKey, 0, FontWeight.Medium),
                cells,
                rule: true));

            if (kind == SpendKind.Voice)
            {
                continue;
            }

            var models = own
                .SelectMany(shares => shares)
                .Select(share => share.Name)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(name => own[last].Where(share => share.Name == name).Sum(share => share.Dollars))
                .ThenBy(name => name, StringComparer.OrdinalIgnoreCase);

            foreach (var model in models)
            {
                var row = own.Select(shares => shares.FirstOrDefault(share => share.Name == model) is { } share
                    ? share.Priced
                        ? new FigureCell(Money(share.Dollars), ThemeManager.GreyKey)
                        : new FigureCell("no rate", ThemeManager.GreyKey)
                    : new FigureCell(Dash, ThemeManager.Grey2Key)).ToList();

                stack.Children.Add(TableRow(Label(model, ThemeManager.GreyKey, 14), row));
            }
        }

        return stack;
    }

    private static bool Is(SpendShare share, SpendKind kind, string provider) =>
        share.Kind == kind && string.Equals(share.Provider, provider, StringComparison.OrdinalIgnoreCase);

    private static FigureCell ProviderCell(SpendKind kind, List<SpendShare> shares)
    {
        if (shares.Count == 0)
        {
            return new FigureCell(Dash, ThemeManager.Grey2Key);
        }

        var use = kind == SpendKind.Voice ? $"{shares.Sum(share => share.Characters):N0} ch" : null;

        if (shares.All(share => !share.Priced))
        {
            return new FigureCell("no rate", ThemeManager.GreyKey, use);
        }

        var floor = shares.Any(share => !share.Priced) ? "≥ " : string.Empty;

        return new FigureCell(floor + Money(shares.Sum(share => share.Dollars)), ThemeManager.AKey, use);
    }

    private static TextBlock Label(string text, string key, double indent, FontWeight weight = FontWeight.Normal)
    {
        var label = new TextBlock
        {
            Text = text,
            FontSize = TypeScale.Secondary,
            FontWeight = weight,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(indent, 0, 0, 0),
        };
        ThemeInk.Bind(label, TextBlock.ForegroundProperty, key);
        return label;
    }

    private static Control TableRow(Control? label, IReadOnlyList<string> heads) =>
        TableRow(label, [.. heads.Select(text => new FigureCell(text.ToUpperInvariant(), ThemeManager.GreyKey))], head: true);

    private static Control TableRow(Control? label, IReadOnlyList<FigureCell> cells, bool rule = false, bool head = false)
    {
        var grid = new Grid { ColumnDefinitions = [new ColumnDefinition(1, GridUnitType.Star)] };

        foreach (var _ in cells)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition(FigureColumn, GridUnitType.Pixel));
        }

        if (label is not null)
        {
            grid.Children.Add(label);
        }

        for (var i = 0; i < cells.Count; i++)
        {
            var cell = cells[i];

            var figure = new TextBlock
            {
                Text = cell.Text,
                FontSize = head ? TypeScale.Meta : TypeScale.Secondary,
                FontFamily = new FontFamily(head ? Fonts.ChromeFamily : Fonts.MonoFamily),
                HorizontalAlignment = HorizontalAlignment.Right,
                TextAlignment = TextAlignment.Right,
                TextWrapping = head ? TextWrapping.Wrap : TextWrapping.NoWrap,
            };
            ThemeInk.Bind(figure, TextBlock.ForegroundProperty, cell.Key);

            Control content = figure;

            if (cell.Use is { } use)
            {
                var uses = new TextBlock
                {
                    Text = use,
                    FontSize = TypeScale.Meta,
                    FontFamily = new FontFamily(Fonts.MonoFamily),
                    HorizontalAlignment = HorizontalAlignment.Right,
                    TextWrapping = TextWrapping.NoWrap,
                };
                ThemeInk.Bind(uses, TextBlock.ForegroundProperty, ThemeManager.GreyKey);
                content = new StackPanel { Children = { figure, uses } };
            }

            Grid.SetColumn(content, i + 1);
            grid.Children.Add(content);
        }

        var border = new Border
        {
            Child = grid,
            Padding = new Thickness(0, rule ? 8 : 2, 0, 2),
            BorderThickness = new Thickness(0, rule ? 1 : 0, 0, 0),
            Margin = new Thickness(0, rule ? 6 : 0, 0, 0),
        };
        ThemeInk.Bind(border, Border.BorderBrushProperty, ThemeManager.Line2Key);

        return border;
    }

    /// <summary>What each catalog calls a provider — the model provider's name where it is one.</summary>
    private static string ProviderName(SpendKind kind, string providerId) =>
        kind == SpendKind.Voice
            ? TtsProviderCatalog.Selected(providerId).Name
            : LlmProviderCatalog.Selected(providerId).Name;

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

        ThemeInk.Bind(line, TextBlock.ForegroundProperty, ThemeManager.GreyKey);

        return line;
    }

    private static Control Section(string heading, IReadOnlyList<Control> rows, string note = "")
    {
        var stack = new StackPanel { Spacing = 4 };

        stack.Children.Add(StarSystemPage.GroupHead(heading, note));

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
        ThemeInk.Bind(label, TextBlock.ForegroundProperty, ThemeManager.GreyKey);

        var figure = new TextBlock
        {
            Text = money,
            FontSize = TypeScale.Secondary,
            FontFamily = new FontFamily(Fonts.MonoFamily),
            HorizontalAlignment = HorizontalAlignment.Right,
            TextWrapping = TextWrapping.NoWrap,
            Margin = new Thickness(0, 0, 8, 0),
        };
        ThemeInk.Bind(figure, TextBlock.ForegroundProperty, ThemeManager.WhiteKey);

        var behind = new TextBlock
        {
            Text = detail,
            FontSize = TypeScale.Secondary,
            FontFamily = new FontFamily(Fonts.MonoFamily),
            TextWrapping = TextWrapping.Wrap,
        };
        ThemeInk.Bind(behind, TextBlock.ForegroundProperty, ThemeManager.GreyKey);

        Grid.SetColumn(figure, 1);
        Grid.SetColumn(behind, 2);

        grid.Children.Add(label);
        grid.Children.Add(figure);
        grid.Children.Add(behind);

        return grid;
    }

}
