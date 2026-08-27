using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using D47.App.Theming;
using D47.Core.Capabilities;
using D47.Core.Habits;

namespace D47.App.Controls;

/// <summary>
/// What d47 has noticed the Commander keeps doing, readable and refusable by a person (list.md
/// Phase 32).
/// <para>
/// <b>A window rather than a settings row, because a claim is not a settings value and refusing one
/// is an act.</b> The row that opens this is <see cref="SettingKind.Info"/>, so
/// <see cref="D47.Core.Configuration.SettingsService.Apply"/> refuses it outright and nothing
/// reachable from the tool surface can get here — the same arrangement
/// <see cref="MemoryWindow"/> uses.
/// </para>
/// <para>
/// <b>Every claim shows its arithmetic, not just its sentence.</b> Item 2 is explicit that a habit
/// reported without its frequency, window and sample size is an insult rather than an observation,
/// and a panel that showed the words while hiding the numbers would be doing exactly that in the
/// one place a Commander could have checked them.
/// </para>
/// <para>
/// <b>The patterns that found nothing are shown too.</b> A detector that declined is a different
/// answer from a detector that found no problem, and this is where the difference is legible.
/// </para>
/// </summary>
public sealed class HabitsWindow : Window
{
    private readonly HabitBook _book;
    private readonly Action? _mine;
    private readonly StackPanel _claims = new() { Spacing = 10 };
    private readonly TextBlock _status;

    public HabitsWindow(HabitBook book, Action? mine)
    {
        ArgumentNullException.ThrowIfNull(book);

        _book = book;
        _mine = mine;

        Title = "What D47 has noticed about you";
        Width = 660;
        Height = 600;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;

        Themed(this, BackgroundProperty, ThemeManager.BackgroundKey);

        _status = new TextBlock
        {
            FontSize = TypeScale.Secondary,
            TextWrapping = TextWrapping.Wrap,

            // Said before anything is pressed, because it is the thing a Commander would most
            // reasonably want reassurance about before letting anything read thirteen months of
            // their play.
            Text =
                "D47 reads the journals already on this disk. Nothing leaves the machine, and no journal "
                + "is ever sent to a model. Anything you drop here stays dropped, even after another read.",
        };

        Themed(_status, TextBlock.ForegroundProperty, ThemeManager.TextMutedKey);

        var body = new StackPanel
        {
            Margin = new Thickness(24),
            Spacing = 16,
            Children = { Heading("What D47 found"), _status },
        };

        if (mine is not null)
        {
            var read = new Button
            {
                Name = "MineHabits",
                Content = "Read my journals again",
                MinWidth = 180,
                HorizontalAlignment = HorizontalAlignment.Left,
            };

            read.Click += (_, _) =>
            {
                mine();

                // The pass is seconds long and runs off this thread, so the window says what it
                // started rather than pretending to have finished. The store raises Changed when
                // it lands, which is what refreshes the list.
                _status.Text = "Reading. It takes a few seconds — this list will fill in when it lands.";
            };

            body.Children.Add(read);
        }

        body.Children.Add(_claims);

        var close = new Button { Content = "Close", MinWidth = 110, HorizontalAlignment = HorizontalAlignment.Right };
        close.Click += (_, _) => Close();
        body.Children.Add(close);

        // Horizontal scrolling off, so wrapping below it actually wraps (GitHub issue 87). A
        // scroller that may scroll sideways measures its content with unbounded width, and every
        // TextWrapping under it stops meaning anything.
        Content = new ScrollViewer
        {
            Content = body,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        };

        book.Store.Changed += OnStoreChanged;
        Closed += (_, _) => book.Store.Changed -= OnStoreChanged;

        Refresh();
    }

    private void OnStoreChanged() => Avalonia.Threading.Dispatcher.UIThread.Post(Refresh);

    private void Refresh()
    {
        _claims.Children.Clear();

        var report = _book.Mine;

        if (report is null)
        {
            _claims.Children.Add(Muted(
                _mine is null
                    ? "D47 is not reading your journals in this configuration."
                    : "Nothing read yet. Press the button above and D47 will look."));

            return;
        }

        if (report.Refusal is { Length: > 0 } refusal)
        {
            _claims.Children.Add(Muted(refusal));
            return;
        }

        _claims.Children.Add(Muted(_book.Summarise()));

        foreach (var claim in _book.Claims)
        {
            _claims.Children.Add(Card(claim));
        }

        var dismissed = _book.Store.DismissedBy(_book.CommanderId);

        foreach (var silence in report.Quiet)
        {
            _claims.Children.Add(Muted($"{silence.Subject} — {silence.Why}."));
        }

        foreach (var key in dismissed)
        {
            _claims.Children.Add(Muted($"{key} — you told D47 to drop this one, and it has."));
        }

        foreach (var problem in _book.Store.Problems)
        {
            _claims.Children.Add(Muted($"Could not read {problem.What}: {problem.Why}"));
        }
    }

    /// <summary>
    /// One claim, with the whole of its working under it and the button that refuses it. The
    /// numbers are the point — see the type doc.
    /// </summary>
    private Control Card(HabitClaim claim)
    {
        var evidence = claim.Evidence;

        var drop = new Button { Content = "Never mention this again", MinWidth = 190 };

        drop.Click += (_, _) =>
        {
            _book.Dismiss(claim.Key);
            Refresh();
        };

        var stack = new StackPanel
        {
            Spacing = 6,
            Children =
            {
                new TextBlock
                {
                    Text = claim.Subject,
                    FontSize = TypeScale.Secondary,
                    FontWeight = FontWeight.SemiBold,
                },
                new SelectableTextBlock { Text = claim.Spoken(), TextWrapping = TextWrapping.Wrap },
                Muted(
                    $"{evidence.Occurrences} of {evidence.Opportunities}, "
                    + $"{evidence.From:d MMM yyyy} to {evidence.To:d MMM yyyy}, "
                    + $"over {evidence.Journals} journals"
                    + (evidence.Latest is { } at ? $". Last one {at.ToLocalTime():d MMM yyyy}." : ".")),
                drop,
            },
        };

        var inset = new Border { Padding = new Thickness(12, 10), CornerRadius = new CornerRadius(4), Child = stack };
        Themed(inset, Border.BackgroundProperty, ThemeManager.SurfaceAltKey);

        return inset;
    }

    private static TextBlock Heading(string text)
    {
        var block = new TextBlock { Text = text, FontSize = TypeScale.Body, FontWeight = FontWeight.SemiBold };
        Themed(block, TextBlock.ForegroundProperty, ThemeManager.TextKey);
        return block;
    }

    private static TextBlock Muted(string text)
    {
        var block = new TextBlock { Text = text, FontSize = TypeScale.Secondary, TextWrapping = TextWrapping.Wrap };
        Themed(block, TextBlock.ForegroundProperty, ThemeManager.TextMutedKey);
        return block;
    }

    private static void Themed(AvaloniaObject target, AvaloniaProperty property, string key) =>
        target[!property] = new DynamicResourceExtension(key);
}
