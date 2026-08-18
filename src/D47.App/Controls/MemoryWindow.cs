using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using D47.App.Theming;
using D47.Core.Capabilities;
using D47.Core.Memory;

namespace D47.App.Controls;

/// <summary>
/// Everything d47 remembers about the Commander, readable and editable by a person (list.md Phase
/// 31, "It forgets, and can be read and emptied").
/// <para>
/// <b>A window rather than a settings row, for the reason the notes window is one.</b> A fact is not
/// a settings value, and typing one here is the act that produces
/// <see cref="MemoryTier.Stated"/> — the only tier d47 will read back as the Commander's own word.
/// The row that opens this is <see cref="SettingKind.Info"/>, so
/// <see cref="D47.Core.Configuration.SettingsService.Apply"/> refuses it outright and nothing
/// reachable from the tool surface can get here.
/// </para>
/// <para>
/// <b>It shows the tier on every line, which is the whole point of there being one.</b> A store that
/// hedges when it speaks and looks uniform when it is read would hide exactly the distinction the
/// tiers exist to keep: <em>you told me</em>, <em>I noticed</em>, and <em>I worked this out myself
/// and nothing has checked it</em>.
/// </para>
/// <para>
/// Emptying is deliberately <b>not</b> here. It lives on the privacy row, which is where a Commander
/// looks for it, and putting a second one next to a list of things they might want to keep is how an
/// irreversible button gets pressed by accident.
/// </para>
/// </summary>
public sealed class MemoryWindow : Window
{
    private readonly MemoryBook _book;
    private readonly Func<DateTimeOffset> _now;
    private readonly StackPanel _entries = new() { Spacing = 10 };
    private readonly TextBox _fact;
    private readonly TextBlock _status;
    private readonly Button _add;

    public MemoryWindow(MemoryBook book, Func<DateTimeOffset> now)
    {
        ArgumentNullException.ThrowIfNull(book);
        ArgumentNullException.ThrowIfNull(now);

        _book = book;
        _now = now;

        Title = "What D47 remembers about you";
        Width = 620;
        Height = 560;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;

        Themed(this, BackgroundProperty, ThemeManager.BackgroundKey);

        _fact = new TextBox
        {
            Name = "MemoryFact",
            PlaceholderText = "Something about you worth keeping — a preference, a habit, a name",
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            MinHeight = 72,
        };

        _add = new Button
        {
            Name = "AddMemory",
            Content = "Remember this",
            MinWidth = 130,
            IsEnabled = false,
            HorizontalAlignment = HorizontalAlignment.Left,
        };

        _add.Click += (_, _) => Add();
        _fact.TextChanged += (_, _) => _add.IsEnabled = !string.IsNullOrWhiteSpace(_fact.Text);

        _status = new TextBlock
        {
            FontSize = TypeScale.Secondary,
            TextWrapping = TextWrapping.Wrap,

            // Said before anything is typed. What a Commander needs to know here is that this
            // route, and only this route, is recorded as their own word.
            Text =
                "Anything you write here is stored as your own word, and read back as your own word. "
                + "It is also sent to the model with your questions when it is relevant to where you are.",
        };

        Themed(_status, TextBlock.ForegroundProperty, ThemeManager.TextMutedKey);

        var body = new StackPanel
        {
            Margin = new Thickness(24),
            Spacing = 16,
            Children =
            {
                Heading("Tell D47 something"),
                _fact,
                _add,
                _status,
                Heading("What it has written down"),
                _entries,
            },
        };

        var close = new Button { Content = "Close", MinWidth = 110, HorizontalAlignment = HorizontalAlignment.Right };
        close.Click += (_, _) => Close();
        body.Children.Add(close);

        Content = new ScrollViewer { Content = body };

        Refresh();
    }

    private void Add()
    {
        if (string.IsNullOrWhiteSpace(_fact.Text))
        {
            return;
        }

        var entry = _book.Remember(_fact.Text, MemoryArrival.Panel, _now());

        _fact.Text = string.Empty;
        _status.Text = $"Stored as your word, keyed {entry.Key}.";

        Refresh();
    }

    private void Refresh()
    {
        _entries.Children.Clear();

        var mine = _book.Mine
            .OrderByDescending(entry => entry.AddedAt ?? DateTimeOffset.MinValue)
            .ThenBy(entry => entry.Key, StringComparer.Ordinal)
            .ToArray();

        if (mine.Length == 0)
        {
            _entries.Children.Add(Muted(
                "Nothing yet. What you add here is remembered between sessions, and D47 will add "
                + "things of its own as it notices them."));
        }

        foreach (var entry in mine)
        {
            _entries.Children.Add(Card(entry));
        }

        // The Commander's own words, so a line that could not be read back is reported rather than
        // dropped — the same rule the checklist and the notes file follow.
        foreach (var problem in _book.Store.Problems)
        {
            _entries.Children.Add(Muted($"Could not read {problem.What}: {problem.Why}"));
        }
    }

    private Control Card(MemoryEntry entry)
    {
        var stack = new StackPanel
        {
            Spacing = 6,
            Children =
            {
                new TextBlock
                {
                    Text = $"{entry.Key} — {Label(entry)}{Stamp(entry)}",
                    FontSize = TypeScale.Secondary,
                    FontWeight = FontWeight.SemiBold,
                },
                new SelectableTextBlock { Text = entry.Fact, TextWrapping = TextWrapping.Wrap },
            },
        };

        // No forget button on an observation. It is rewritten from the journal the moment the
        // Commander moves, so a button that removed it would look broken rather than decisive —
        // and the way to be rid of observations is to stop remembering, or to empty the store.
        if (!MemoryObserver.IsObservation(entry.Key))
        {
            var forget = new Button { Content = "Forget", MinWidth = 90 };

            forget.Click += (_, _) =>
            {
                _book.Forget(entry.Key);
                Refresh();
            };

            stack.Children.Add(forget);
        }

        var inset = new Border { Padding = new Thickness(12, 10), CornerRadius = new CornerRadius(4), Child = stack };
        Themed(inset, Border.BackgroundProperty, ThemeManager.SurfaceAltKey);

        return inset;
    }

    /// <summary>
    /// How an entry is labelled in the list — the same distinction the spoken sentence makes, shown
    /// rather than implied, because the whole value of a tier is that it is visible.
    /// </summary>
    private static string Label(MemoryEntry entry) => (entry.Tier, entry.Arrival) switch
    {
        (MemoryTier.Stated, _) => "your word",
        (MemoryTier.Observed, _) => "noticed in your journal",
        (_, MemoryArrival.Panel) => "written by hand, unverified",
        _ => "written by D47 itself, unverified",
    };

    private static string Stamp(MemoryEntry entry) =>
        entry.AddedAt is { } at ? $", {at.ToLocalTime():d MMM yyyy}" : string.Empty;

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
