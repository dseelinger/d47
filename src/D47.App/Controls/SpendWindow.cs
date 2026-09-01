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
    private readonly SpendTracker _session;
    private readonly SpeechSpend _speech;
    private readonly SpendLedger _ledger;
    private readonly D47Settings _settings;
    private readonly TimeZoneInfo _zone;

    /// <summary>
    /// When this process started, so "this session" is a window the ledger can be asked about
    /// (<a href="https://github.com/dseelinger/d47/issues/197">#197</a>). Null in a fixture that
    /// is not about resetting, which leaves the Reset button off.
    /// </summary>
    private readonly DateTimeOffset? _launchedAt;

    /// <summary>The last turn, kept so the window can be redrawn after a reset.</summary>
    private readonly TurnCost? _turn;

    /// <summary>Where the sections live, so a reset can replace them rather than reopen the window.</summary>
    private readonly StackPanel _body = new() { Margin = new Thickness(24), Spacing = 18 };

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

        // 640 rather than 560 because the widest line here is a running total that names both
        // providers and both figures, and at 560 it wrapped to three lines in a two-column row.
        Width = 640;
        SizeToContent = SizeToContent.Height;

        // A ledger with five windows in it is taller than some screens, so the height is capped
        // and the scroller takes the rest. Without this, SizeToContent grows the window past the
        // bottom of the display and the Close button is the part that goes.
        MaxHeight = 760;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        // Resizable, unlike ConfirmWindow beside it: that one asks a question and is done, and
        // this one is read. A Commander who wants the figures wider should be able to have them.
        CanResize = true;
        ShowInTaskbar = false;

        Themed(this, BackgroundProperty, ThemeManager.BackgroundKey);

        Draw();

        // **Horizontal scrolling disabled, and it is the whole of the fix** (GitHub issue 87).
        // A ScrollViewer that may scroll horizontally measures its content with *unconstrained*
        // width, so every `TextWrapping.Wrap` below it becomes a no-op and the two star columns
        // in `Row` have no width to divide. The window then lays every figure out on one endless
        // line and offers a horizontal scrollbar for it, which is what a Commander was shown:
        // the left of every line off-screen and the totals unreadable.
        //
        // So the rows were never the problem and neither was the width. `ChangelogWindow` has
        // had this line since it was written; the four windows beside it had not, and now do.
        Content = new ScrollViewer
        {
            Name = "SpendScroller",
            Content = _body,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        };
    }

    /// <summary>
    /// Every section, from scratch.
    /// <para>
    /// <b>Called again after a reset rather than reopening the window</b> (#197). Every figure
    /// here is a query — over the ledger, or over two in-memory counters — so redrawing is the
    /// whole of showing the new answer, and a window that closed and came back would lose the
    /// Commander's place and their resize.
    /// </para>
    /// </summary>
    private void Draw()
    {
        _body.Children.Clear();

        _body.Children.Add(Estimates());

        // **Two groups, one row each** (#227). The turn, the session and today answer one
        // question — what is this costing me right now — and the four below answer another, so
        // they are read together rather than as five sections down a page. Which windows belong
        // in which group is SpendPeriods' answer and not this file's.
        _body.Children.Add(Section(
            "Now",
            [
                TurnRow(_turn),
                SessionRow(_session, _speech, _settings),
                .. _ledger.Immediate(_zone).Select(WindowRow),
                .. ColdPrefixRow(_session),
            ]));

        // Each calendar window beside its rolling twin: This week, Last 7 days, This month, Last
        // 30 days. On the 3rd of a month a pair differs by an order of magnitude, and adjacency
        // is what makes that legible — see SpendPeriods.Windows, which owns the order.
        _body.Children.Add(Section(
            "Running totals",
            [.. _ledger.Windows(_zone).Select(WindowRow)]));

        _body.Children.Add(Buttons());
    }

    /// <summary>
    /// Close, and — on a window that was told when the process started — Reset beside it.
    /// <para>
    /// <b>This is the one eraser in the app that asks first, and the departure is deliberate.</b>
    /// Every other one is an <c>Info</c> settings row with a <c>Press</c> and no confirmation:
    /// memory, flight recordings, personas. Their safety is that the tool surface cannot reach
    /// them and no spoken phrase does — not a dialog. This one was asked for here, which is the
    /// right place because it is where the numbers are, and that puts a control that erases money
    /// history somewhere a stray click reaches. So it names the window and the figure before it
    /// does anything, and <see cref="ConfirmWindow"/> already defaults to no.
    /// </para>
    /// </summary>
    private Control Buttons()
    {
        var close = new Button { Content = "Close", MinWidth = 110 };
        close.Click += (_, _) => Close();

        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 10,
        };

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

        return row;
    }

    /// <summary>
    /// How far back to reset, as a menu over the button.
    /// <para>
    /// <b>The five windows the figures list shows, plus the session, and no others.</b> The
    /// ask named a 31-day option; the Commander settled it at thirty on 2026-08-30, because the
    /// reason 31 was wanted — being at the end of a month — is what <c>This month</c> already
    /// does. A reset list offering a span the figures list does not show would be two lists
    /// disagreeing about what a window is. <c>SpendPeriods.Resettable</c> owns the set.
    /// </para>
    /// </summary>
    private MenuFlyout Choices(DateTimeOffset launched)
    {
        var flyout = new MenuFlyout { Placement = PlacementMode.Top };

        foreach (var window in _ledger.Resettable(_zone, launched))
        {
            var item = new MenuItem { Header = window.Name };

            // The window is captured rather than re-derived on click, so what the Commander is
            // asked about and what is cleared are the same instants — a menu left open across a
            // midnight would otherwise reset a different span than it offered.
            item.Click += async (_, _) => await ResetAsync(window);

            flyout.Items.Add(item);
        }

        return flyout;
    }

    /// <summary>
    /// Asks, then resets, then redraws.
    /// <para>
    /// <b>The ledger and the session counters go together, from here</b> (#197). Doing one without
    /// the other is the confusing outcome the issue names: clearing only the counters leaves the
    /// running totals counting charges the session block says are gone, and clearing only the
    /// ledger leaves this block quoting figures nothing below it includes. The mark is appended
    /// rather than the rows deleted — see <c>SpendEntry.ResetFrom</c>.
    /// </para>
    /// </summary>
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

        // Every reset clears these, even one narrower than the session: a TurnCost carries no
        // instant, so there is nothing here to filter by. SpendTracker.Forget carries the whole
        // reasoning, including what it costs in the one case where it over-clears.
        _session.Forget();
        _speech.Forget();

        Draw();
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

    /// <summary>
    /// This turn, on one row (#227): what it cost, and the tokens behind it in the details cell.
    /// <para>
    /// <b>Four rows became one, and nothing was dropped.</b> Input, cached, output and cost were
    /// four labelled lines because there was nowhere else for them; with a details column they
    /// are one line that reads left to right. The Commander confirmed one row each on 2026-08-30.
    /// </para>
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
            // Billed separately from tokens and not small: one search costs more than an entire
            // cheap turn, so it is named rather than folded into the figure beside it.
            detail += $", {usage.WebSearchRequests:N0} web searches";
        }

        return Row("Turn", cost.Priced ? $"{cost.Dollars:C4}" : "unpriced", detail);
    }

    /// <summary>
    /// This session, on one row (#227). The money is the model and the voice together, rendered by
    /// <see cref="Money"/> so the split reads the same here as it does for every window below.
    /// </summary>
    private static Control SessionRow(SpendTracker session, SpeechSpend speech, D47Settings settings)
    {
        var detail = $"{session.TurnCount:N0} {(session.TurnCount == 1 ? "turn" : "turns")}";

        // **The voice sentence is kept whole rather than reduced to a character count.** It already
        // names every provider that spoke and what each cost — which is what the details column is
        // for — and it is the only place a free provider is told apart from an unpriced one.
        if (speech.Describe(settings) is { Length: > 0 } voice)
        {
            detail += $", {voice}";
        }

        return Row("Session", session.RunningTotalDollars.ToString("C4"), detail);
    }

    /// <summary>
    /// One window's figure and the models behind it (#226). "nothing yet" sits in the details
    /// cell rather than the money one, so an empty window does not put words in a column of
    /// amounts.
    /// </summary>
    private static Control WindowRow((SpendPeriod Period, SpendTotals Totals) window)
    {
        var totals = window.Totals;

        return totals.Any
            ? Row(window.Period.Name, Amount(totals), Behind(totals))
            : Row(window.Period.Name, string.Empty, "nothing yet");
    }

    /// <summary>
    /// Every model and voice provider used in a window, most expensive first (#226).
    /// <para>
    /// <b>A free provider is named rather than implied.</b> Kokoro and Edge Neural cost nothing,
    /// and a provider that was used and shows no figure looks like a provider that was not used —
    /// so it says <c>(free)</c> and how much it spoke.
    /// </para>
    /// <para>
    /// <b>And an unpriced model can finally say which one it is.</b> The window's own caveat is
    /// "part of it unpriced", which does not name the part; a row per model does.
    /// </para>
    /// <para>
    /// <b>Capped, and it says when it caps.</b> A period holds however many models it holds, and
    /// a silent truncation in a cost report is the one place that must not happen.
    /// </para>
    /// </summary>
    private static string Behind(SpendTotals totals)
    {
        const int Most = 6;

        var said = totals.Shares.Take(Most).Select(share =>
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

        return totals.Shares.Count > Most
            ? $"{line}, and {totals.Shares.Count - Most:N0} more"
            : line;
    }

    /// <summary>
    /// <b>The one thing in this window that asks the Commander to do something</b>, so it keeps a
    /// row of its own rather than being folded into a details cell beside token counts (#227).
    /// <para>
    /// A caching regression rather than a cost curiosity: a profile switch is the only sanctioned
    /// cause of a cold prefix, so a count here means something is defeating the cache — a mutated
    /// descriptor, a prompt whose bytes vary per turn. It appears only when the count is non-zero,
    /// as it already did.
    /// </para>
    /// </summary>
    /// <summary>
    /// A window's figure and nothing else, for the money column
    /// (<a href="https://github.com/dseelinger/d47/issues/226">#226</a>).
    /// <para>
    /// <b>The column holds one amount, and the first capture of the three-column layout is why.</b>
    /// <see cref="Money"/> renders the model-and-voice split as a sentence — <c>"$1.5021 — $1.4411
    /// model, $0.0610 voice"</c> — and a sentence in a right-aligned mono column that does not wrap
    /// came out as <c>"$1.5021 — $1"</c>. A cost figure clipped mid-string is the one thing a
    /// window about money must never do, and the split is not lost: the details cell names every
    /// model and every provider with its own figure, which is more than the sentence said.
    /// </para>
    /// <para>
    /// <b>An incomplete window says so with a sign rather than a clause.</b> "at least $X, part of
    /// it unpriced" does not say <em>which</em> part; the details cell now does, model by model, so
    /// this only has to mark the figure as a floor.
    /// </para>
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
    /// A label, an amount and the detail behind it, on one row
    /// (<a href="https://github.com/dseelinger/d47/issues/226">#226</a>).
    /// <para>
    /// <b>Money and detail used to share a cell</b>, so the voice line read as three facts wrapped
    /// into a paragraph with the figure buried in the middle of it — while a running total held
    /// <c>$0.0196</c> in a column three units wide and nothing else. A column of amounts is
    /// readable as a column only when it is one.
    /// </para>
    /// <para>
    /// The amount is monospaced and <b>right-aligned</b>, which is what lines the decimal points
    /// up; the window is already set in a mono face, so this costs nothing. Both text cells wrap
    /// rather than clip — the details cell holds a model per line and the window scrolls
    /// downward, which is the direction growth is safe in (#87).
    /// </para>
    /// </summary>
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
        Themed(label, TextBlock.ForegroundProperty, ThemeManager.TextMutedKey);

        var figure = new TextBlock
        {
            Text = money,
            FontSize = TypeScale.Secondary,
            FontFamily = new FontFamily("Cascadia Mono,Consolas,monospace"),
            HorizontalAlignment = HorizontalAlignment.Right,
            TextWrapping = TextWrapping.NoWrap,
            Margin = new Thickness(0, 0, 8, 0),
        };
        Themed(figure, TextBlock.ForegroundProperty, ThemeManager.TextKey);

        var behind = new TextBlock
        {
            Text = detail,
            FontSize = TypeScale.Secondary,
            FontFamily = new FontFamily("Cascadia Mono,Consolas,monospace"),
            TextWrapping = TextWrapping.Wrap,
        };
        Themed(behind, TextBlock.ForegroundProperty, ThemeManager.TextMutedKey);

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
