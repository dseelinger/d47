using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using D47.App.Theming;
using D47.Core.Capabilities;
using D47.Core.Debrief;
using D47.Core.Persona;

namespace D47.App.Controls;

/// <summary>
/// What the debrief drafted, and the one place a direction can be taken
/// (<a href="https://github.com/dseelinger/d47/issues/162">#162</a>).
/// <para>
/// <b>A window rather than a settings row, for the reason <see cref="MemoryWindow"/> is one.</b> A
/// direction is not a settings value, and taking one here is the act that produces
/// <see cref="D47.Core.Memory.MemoryTier.Stated"/>. The row that opens this is
/// <see cref="SettingKind.Info"/>, so <see cref="D47.Core.Configuration.SettingsService.Apply"/>
/// refuses it outright and nothing reachable from the tool surface can get here.
/// </para>
/// <para>
/// <b>It shows the exact text that would enter the prompt, and it shows it in an editor.</b> Not a
/// summary of the direction, not a description of what d47 learned — the characters themselves,
/// rendered by the same <see cref="StandingDirections"/> the prompt is built from, so the two
/// cannot drift. This is #160's rule about showing the exact bytes that leave the machine, applied
/// to the bytes that enter the model. And it is editable, because a draft nobody may touch is a
/// draft nobody adopts: the pass quotes the Commander, and the Commander gets the last word on
/// their own sentence.
/// </para>
/// <para>
/// <b>Each proposal carries the sentence it came from.</b> That is what makes it checkable rather
/// than merely plausible — a Commander reading "Shorter answers in combat." underneath their own
/// "no, shorter answers when I'm in a fight" can tell at a glance whether d47 understood them.
/// Where the audio recorder was running, the clip id is named too, so the proposal can be
/// checked against what was actually said rather than against a transcriber's best guess at it.
/// </para>
/// <para>
/// <b>Desktop only, and that is allowed</b> — parity between the surfaces is a nice-to-have rather
/// than a constraint (architecture.md §1). This is reading a dozen sentences and editing some of
/// them, which is a keyboard job.
/// </para>
/// </summary>
public sealed class DebriefWindow : Window
{
    private readonly DebriefBook _book;
    private readonly Func<DateTimeOffset> _now;
    private readonly Func<Persona> _core;
    private readonly StackPanel _waiting = new() { Spacing = 12 };
    private readonly StackPanel _taken = new() { Spacing = 12 };
    private readonly SelectableTextBlock _prompt;
    private readonly TextBlock _status;

    /// <param name="core">
    /// Which core is aboard, so a direction can be scoped to it. Read at draw time rather than
    /// captured: a Commander can switch core with this window open.
    /// </param>
    public DebriefWindow(DebriefBook book, Func<DateTimeOffset> now, Func<Persona> core)
    {
        ArgumentNullException.ThrowIfNull(book);
        ArgumentNullException.ThrowIfNull(now);
        ArgumentNullException.ThrowIfNull(core);

        _book = book;
        _now = now;
        _core = core;

        Title = "What D47 learned from your last session";
        Width = 680;
        Height = 640;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;

        Themed(this, BackgroundProperty, ThemeManager.BackgroundKey);

        _status = new TextBlock
        {
            FontSize = TypeScale.Secondary,
            TextWrapping = TextWrapping.Wrap,

            // Said before anything is pressed, because it is the one thing about this window that
            // is surprising: taking a direction does not change the conversation you are having.
            Text =
                "D47 drafts these from what you corrected it on, in your own words. Nothing here reaches "
                + "the model until you take it, and what you take arrives at the start of your next "
                + "session — never in the middle of this one.",
        };

        Themed(_status, TextBlock.ForegroundProperty, ThemeManager.TextMutedKey);

        _prompt = new SelectableTextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            FontFamily = new FontFamily("Consolas, Cascadia Mono, monospace"),
            FontSize = TypeScale.Secondary,
        };

        var promptBox = new Border { Padding = new Thickness(12, 10), CornerRadius = new CornerRadius(4), Child = _prompt };
        Themed(promptBox, Border.BackgroundProperty, ThemeManager.SurfaceAltKey);

        var body = new StackPanel
        {
            Margin = new Thickness(24),
            Spacing = 16,
            Children =
            {
                Heading("Waiting for you"),
                _status,
                _waiting,
                Heading("What you have taken"),
                _taken,
                Heading("Exactly what D47 will be told"),
                Muted(
                    "Word for word, at the start of your next session. This is the text itself, not a "
                    + "description of it."),
                promptBox,
            },
        };

        var close = new Button { Content = "Close", MinWidth = 110, HorizontalAlignment = HorizontalAlignment.Right };
        close.Click += (_, _) => Close();
        body.Children.Add(close);

        // Horizontal scrolling off, so the wrapping below it actually wraps (GitHub issue 87).
        Content = new ScrollViewer
        {
            Content = body,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        };

        Refresh();
    }

    private void Refresh()
    {
        _waiting.Children.Clear();
        _taken.Children.Clear();

        var mine = _book.Mine;

        var waiting = mine
            .Where(entry => entry.State == DirectionState.Proposed)
            .OrderByDescending(entry => entry.ProposedAt ?? DateTimeOffset.MinValue)
            .ThenBy(entry => entry.Key, StringComparer.Ordinal)
            .ToArray();

        if (waiting.Length == 0)
        {
            _waiting.Children.Add(Muted(
                "Nothing waiting. D47 reads the session back when you close it, and drafts something "
                + "only where you actually corrected it."));
        }

        foreach (var entry in waiting)
        {
            _waiting.Children.Add(Proposal(entry));
        }

        var taken = mine
            .Where(entry => entry.State == DirectionState.Adopted)
            .OrderBy(entry => entry.AdoptedAt ?? DateTimeOffset.MinValue)
            .ThenBy(entry => entry.Key, StringComparer.Ordinal)
            .ToArray();

        if (taken.Length == 0)
        {
            _taken.Children.Add(Muted("Nothing taken yet, so nothing of this is in the prompt."));
        }

        foreach (var entry in taken)
        {
            _taken.Children.Add(Taken(entry));
        }

        // The Commander's own words are in this file, so a line that could not be read back is
        // reported rather than dropped — the rule every hand-editable store here follows.
        foreach (var problem in _book.Store.Problems)
        {
            _taken.Children.Add(Muted($"Could not read {problem.What}: {problem.Why}"));
        }

        _prompt.Text = Rendered(taken);
    }

    /// <summary>
    /// The block as the prompt will carry it, general directions and this core's overlay both.
    /// Rendered by <see cref="StandingDirections"/> rather than assembled here, which is the whole
    /// of what makes "exactly what D47 will be told" a fact rather than a claim.
    /// </summary>
    private string Rendered(IReadOnlyList<StandingDirection> taken)
    {
        var core = _core();
        var general = StandingDirections.Render(taken);
        var overlay = StandingDirections.RenderFor(core.Id, taken);

        return (general, overlay) switch
        {
            (null, null) => "Nothing. The prompt carries no directions at all.",
            (not null, null) => general,
            (null, not null) => $"In {core.Name}'s own block:\n\n{overlay}",
            _ => $"{general}\n\nIn {core.Name}'s own block:\n\n{overlay}",
        };
    }

    private Control Proposal(StandingDirection entry)
    {
        var core = _core();

        var editor = new TextBox
        {
            Name = $"Direction_{entry.Key}",
            Text = entry.Suggested ?? (entry.Kind == DirectionKind.Question ? string.Empty : entry.Text),
            AcceptsReturn = false,
            TextWrapping = TextWrapping.Wrap,
            MaxLength = StandingDirection.MaxText,
            PlaceholderText = entry.Kind == DirectionKind.Question
                ? "Write the direction you want, or discard the question"
                : null,
        };

        var take = new Button { Content = "Take it", MinWidth = 100 };
        var takeForCore = new Button { Content = $"Just for {core.Name}", MinWidth = 140 };
        var discard = new Button { Content = "Discard", MinWidth = 100 };

        void Enable() =>
            take.IsEnabled = takeForCore.IsEnabled = !string.IsNullOrWhiteSpace(editor.Text);

        Enable();
        editor.TextChanged += (_, _) => Enable();

        take.Click += (_, _) => Adopt(entry, editor.Text, persona: null);
        takeForCore.Click += (_, _) => Adopt(entry, editor.Text, core.Id);

        discard.Click += (_, _) =>
        {
            _book.Decline(entry.Key);
            Refresh();
        };

        var stack = new StackPanel
        {
            Spacing = 8,
            Children =
            {
                new TextBlock
                {
                    Text = $"{entry.Key} — {entry.Label()}{Stamp(entry.ProposedAt)}",
                    FontSize = TypeScale.Secondary,
                    FontWeight = FontWeight.SemiBold,
                },
            },
        };

        if (entry.Kind == DirectionKind.Question)
        {
            // A question is shown as a question and cannot be taken as one. Adopting the
            // question's own text would put "shorter answers there?" into the prompt.
            stack.Children.Add(new SelectableTextBlock { Text = entry.Text, TextWrapping = TextWrapping.Wrap });
        }

        stack.Children.Add(editor);

        if (entry.Because.Length > 0)
        {
            stack.Children.Add(Muted($"You said: “{entry.Because}”"));
        }

        if (entry.Clip is { Length: > 0 } clip)
        {
            // Where the recorder was running. The transcript alone is enough to draft from; this
            // is what lets a Commander check the draft against the audio rather than against a
            // transcriber's guess at it (#164).
            stack.Children.Add(Muted($"Recorded as {clip} in the audio recorder."));
        }

        stack.Children.Add(new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Children = { take, takeForCore, discard },
        });

        var inset = new Border { Padding = new Thickness(12, 10), CornerRadius = new CornerRadius(4), Child = stack };
        Themed(inset, Border.BackgroundProperty, ThemeManager.SurfaceAltKey);

        return inset;
    }

    private void Adopt(StandingDirection entry, string? text, string? persona)
    {
        if (_book.Adopt(entry.Key, _now(), text, persona) is null)
        {
            return;
        }

        _status.Text =
            "Taken. It goes into the prompt at the start of your next session — this one carries what "
            + "it started with.";

        Refresh();
    }

    private Control Taken(StandingDirection entry)
    {
        var withdraw = new Button { Content = "Withdraw", MinWidth = 110 };

        withdraw.Click += (_, _) =>
        {
            _book.Decline(entry.Key);
            Refresh();
        };

        var scope = entry.Persona is { Length: > 0 } id ? $", only for {id}" : ", for every core";

        var stack = new StackPanel
        {
            Spacing = 6,
            Children =
            {
                new TextBlock
                {
                    Text = $"{entry.Key} — {entry.Label()}{scope}{Stamp(entry.AdoptedAt)}",
                    FontSize = TypeScale.Secondary,
                    FontWeight = FontWeight.SemiBold,
                },
                new SelectableTextBlock { Text = entry.Text, TextWrapping = TextWrapping.Wrap },
                withdraw,
            },
        };

        var inset = new Border { Padding = new Thickness(12, 10), CornerRadius = new CornerRadius(4), Child = stack };
        Themed(inset, Border.BackgroundProperty, ThemeManager.SurfaceAltKey);

        return inset;
    }

    private static string Stamp(DateTimeOffset? at) =>
        at is { } when ? $", {when.ToLocalTime():d MMM yyyy}" : string.Empty;

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
