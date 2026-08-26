using Avalonia;
using Avalonia.Controls;
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
/// <para>
/// <b>This is where the turn line's figures went.</b> One row carrying outcome, route, effort,
/// three token counts, two dollar figures, a cache-regression counter, a character count and a
/// voice price is eleven numbers a Commander reads none of while flying. The line keeps what
/// answers "did that work"; everything that answers "what is this costing me" is here, laid out
/// so it can be read rather than scanned past.
/// </para>
/// <para>
/// Built in code rather than as an axaml pair, like <see cref="ConfirmWindow"/> beside it: one
/// layout, no state of its own, and a markup file plus a code-behind for a table is structure
/// that buys nothing.
/// </para>
/// </summary>
public sealed class SpendWindow : Window
{
    public SpendWindow(
        TurnCost? turn,
        SpendTracker session,
        SpeechSpend speech,
        SpendLedger ledger,
        D47Settings settings,
        TimeZoneInfo zone)
    {
        Title = "What this has cost";
        Width = 560;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        CanResize = false;
        ShowInTaskbar = false;

        Themed(this, BackgroundProperty, ThemeManager.BackgroundKey);

        var body = new StackPanel { Margin = new Thickness(24), Spacing = 18 };

        body.Children.Add(Estimates());
        body.Children.Add(Section("This turn", TurnRows(turn)));
        body.Children.Add(Section("This session", SessionRows(session, speech, settings)));

        // The four windows the request asked for. Two are elapsed durations and two are local
        // calendar ideas; the ledger works out which instants those are, against this zone.
        body.Children.Add(Section(
            "Running totals",
            [.. ledger.Summary(zone).Select(row => Row(
                row.Period.Name,
                row.Totals.Any ? Money(row.Totals) : "nothing yet"))]));

        var close = new Button { Content = "Close", MinWidth = 110, HorizontalAlignment = HorizontalAlignment.Right };
        close.Click += (_, _) => Close();
        body.Children.Add(close);

        Content = new ScrollViewer { Content = body };
    }

    /// <summary>
    /// A window's figure, and whether it is the whole of it. An unpriced model or a voice with no
    /// rate set makes the total a floor, and saying so is the point — a number presented as
    /// authoritative while covering part of the cost is worse than no number.
    /// </summary>
    private static string Money(SpendTotals totals)
    {
        var line = totals.Dollars.ToString("C4", System.Globalization.CultureInfo.CurrentCulture);

        if (totals.VoiceDollars > 0m)
        {
            line += $" — {totals.ModelDollars:C4} model, {totals.VoiceDollars:C4} voice";
        }

        return totals.Complete ? line : $"at least {line}, part of it unpriced";
    }

    private static IReadOnlyList<Control> TurnRows(TurnCost? turn)
    {
        if (turn is not { } cost)
        {
            return [Row("Nothing yet", "no turn has been answered this session")];
        }

        var usage = cost.Usage;

        List<Control> rows =
        [
            Row("Input", $"{usage.TotalInputTokens:N0} tokens"),
            Row("  of which cached", $"{usage.CacheReadInputTokens:N0} read, {usage.CacheCreationInputTokens:N0} written"),
            Row("Output", $"{usage.OutputTokens:N0} tokens"),
            Row("Cost", cost.Priced ? $"{cost.Dollars:C4}" : "unpriced model — no rate for it"),
        ];

        if (usage.WebSearchRequests > 0)
        {
            // Billed separately from tokens and not small: one search costs more than an entire
            // cheap turn, so it is named rather than folded into the figure above.
            rows.Insert(3, Row("Web searches", usage.WebSearchRequests.ToString("N0")));
        }

        return rows;
    }

    private static IReadOnlyList<Control> SessionRows(
        SpendTracker session,
        SpeechSpend speech,
        D47Settings settings)
    {
        List<Control> rows =
        [
            Row("Turns", session.TurnCount.ToString("N0")),
            Row("Model", session.RunningTotalDollars.ToString("C4")),
        ];

        if (speech.Describe(settings) is { } voice)
        {
            rows.Add(Row("Voice", voice));
        }

        if (session.UnexplainedColdPrefixes > 0)
        {
            // A caching regression rather than a cost curiosity: a profile switch is the only
            // sanctioned cause of a cold prefix, so a count here means something is defeating
            // the cache — a mutated descriptor, a prompt whose bytes vary per turn.
            rows.Add(Row(
                "Cold prefixes",
                $"{session.UnexplainedColdPrefixes:N0} with no cause — caching is being defeated"));
        }

        return rows;
    }

    /// <summary>
    /// Said once, at the top, rather than as a suffix on each of a dozen figures.
    /// <para>
    /// <b>Almost every figure in this window is an estimate, and the codebase already knew it.</b>
    /// <c>TtsProviderCatalog.ElevenLabs</c> argues the case in its own comment: a subscription
    /// burns bundled credits rather than paying a list rate, so a published $0.05 per thousand
    /// can be 3.6× too low or infinitely too high depending on account state no API reports. The
    /// model rates carry the same problem — a table is what is published, not what an account is
    /// charged.
    /// </para>
    /// <para>
    /// <b>At the top rather than as a footnote</b>, because a Commander who reads the first
    /// figure and closes the window never reaches the bottom. It qualifies before any number is
    /// read, and costs one line of muted type to do it.
    /// </para>
    /// <para>
    /// It does not hedge itself with "for the most part", true though that is: hedging the hedge
    /// reads as evasive, and where a figure genuinely is exact it is already visible — a local
    /// endpoint prices at zero and Edge is free, and nobody disputes a zero.
    /// </para>
    /// </summary>
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

        Themed(line, TextBlock.ForegroundProperty, ThemeManager.TextMutedKey);

        return line;
    }

    private static Control Section(string heading, IReadOnlyList<Control> rows)
    {
        var stack = new StackPanel { Spacing = 4 };

        var title = new TextBlock { Text = heading, FontSize = TypeScale.Body, FontWeight = FontWeight.SemiBold };
        Themed(title, TextBlock.ForegroundProperty, ThemeManager.AccentKey);

        stack.Children.Add(title);

        foreach (var row in rows)
        {
            stack.Children.Add(row);
        }

        return stack;
    }

    /// <summary>
    /// A caption and its figure, on one row. The figure is monospaced and right-aligned so a
    /// column of them can be compared down the page rather than read one at a time.
    /// </summary>
    private static Control Row(string caption, string value)
    {
        var grid = new Grid
        {
            ColumnDefinitions =
            [
                new ColumnDefinition(2, GridUnitType.Star),
                new ColumnDefinition(3, GridUnitType.Star),
            ],
        };

        var label = new TextBlock { Text = caption, FontSize = TypeScale.Secondary, TextWrapping = TextWrapping.Wrap };
        Themed(label, TextBlock.ForegroundProperty, ThemeManager.TextMutedKey);

        var figure = new TextBlock
        {
            Text = value,
            FontSize = TypeScale.Secondary,
            FontFamily = new FontFamily("Cascadia Mono,Consolas,monospace"),
            TextWrapping = TextWrapping.Wrap,
        };
        Themed(figure, TextBlock.ForegroundProperty, ThemeManager.TextKey);

        Grid.SetColumn(figure, 1);
        grid.Children.Add(label);
        grid.Children.Add(figure);

        return grid;
    }

    private static void Themed(AvaloniaObject target, AvaloniaProperty property, string key) =>
        target[!property] = new DynamicResourceExtension(key);
}
