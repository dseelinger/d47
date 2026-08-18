using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using D47.App.Settings;
using D47.App.Theming;
using D47.Core.Capabilities.Builtin;
using D47.Core.Lore;

namespace D47.App.Controls;

/// <summary>
/// The Commander's own notes about systems, and where one is written (list.md Phase 23,
/// "Commander's Lore").
/// <para>
/// <b>A window rather than a settings row, for the reason the checklist's is.</b> A note is not a
/// settings value and never could be — and writing one is the act that produces the
/// <see cref="LoreTier.Commander"/> tier, which exists precisely because a person's hands were on
/// it. The row that opens this is <see cref="SettingKind.Info"/>, so
/// <see cref="D47.Core.Configuration.SettingsService.Apply"/> refuses it outright and nothing
/// reachable from the tool surface can get here.
/// </para>
/// <para>
/// <b>The lookup runs before the note is stored, and its answer is a label rather than a
/// verdict.</b> A search that turns something up files the note as corroborated; one that does
/// not files it as the Commander's word, which is a perfectly good thing for a note to be — an
/// obscure but real site finds nothing. Neither is ever revisited: no later search promotes an
/// entry, because promotion is the path by which an invention becomes a fact d47 states flatly.
/// </para>
/// <para>
/// Built in code rather than as an axaml pair, like <see cref="SpendWindow"/> beside it: one
/// layout and no state of its own.
/// </para>
/// </summary>
public sealed class LoreWindow : Window
{
    private readonly LoreEditing _editing;
    private readonly bool _canSearch;
    private readonly StackPanel _entries = new() { Spacing = 10 };
    private readonly TextBox _note;
    private readonly TextBlock _status;
    private readonly Button _add;

    public LoreWindow(LoreEditing editing)
    {
        ArgumentNullException.ThrowIfNull(editing);

        _editing = editing;

        // Read once, as the window opens. It decides what the window says it is about to do, and
        // a sentence that changed while somebody was typing under it would be worse than either.
        _canSearch = editing.CanSearch();

        Title = "What you have told me about systems";
        Width = 620;
        Height = 560;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;

        Themed(this, BackgroundProperty, ThemeManager.BackgroundKey);

        var place = _editing.Here();

        _note = new TextBox
        {
            Name = "LoreNote",
            PlaceholderText = place is null
                ? "D47 does not know which system you are in yet"
                : $"Something worth remembering about {place.SystemName}",
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            MinHeight = 72,
            IsEnabled = place is not null,
        };

        _add = new Button
        {
            Name = "AddLore",
            Content = "Remember this",
            MinWidth = 130,
            IsEnabled = false,
            HorizontalAlignment = HorizontalAlignment.Left,
        };

        _add.Click += async (_, _) => await AddAsync().ConfigureAwait(true);
        _note.TextChanged += (_, _) => _add.IsEnabled = !string.IsNullOrWhiteSpace(_note.Text);

        _status = new TextBlock
        {
            FontSize = TypeScale.Secondary,
            TextWrapping = TextWrapping.Wrap,

            // Said before anything is typed, so the Commander knows what pressing the button will
            // do — including that it may spend a fraction of a penny looking the system up.
            Text = place is null
                ? "Nothing to attach a note to until D47 has seen where you are."
                : !_canSearch
                    ? "Stored as your word. D47 cannot search from here, so nothing will corroborate it."
                    : "D47 searches once before storing, and records whether anything backed it up.",
        };

        Themed(_status, TextBlock.ForegroundProperty, ThemeManager.TextMutedKey);

        var body = new StackPanel
        {
            Margin = new Thickness(24),
            Spacing = 16,
            Children =
            {
                Heading("Add a note"),
                _note,
                _add,
                _status,
                Heading("What you have already said"),
                _entries,
            },
        };

        var close = new Button { Content = "Close", MinWidth = 110, HorizontalAlignment = HorizontalAlignment.Right };
        close.Click += (_, _) => Close();
        body.Children.Add(close);

        Content = new ScrollViewer { Content = body };

        Refresh();
    }

    private async Task AddAsync()
    {
        if (_editing.Here() is not { } place || string.IsNullOrWhiteSpace(_note.Text))
        {
            return;
        }

        var note = _note.Text.Trim();

        _add.IsEnabled = false;
        _note.IsEnabled = false;

        var corroborated = false;

        if (_canSearch)
        {
            _status.Text = $"Looking {place.SystemName} up…";

            using var budget = new CancellationTokenSource(LoreLookup.Budget);

            // A lookup that fails, times out or finds nothing all mean the same thing here: the
            // note is stored as the Commander's word. Nothing is refused for want of a search.
            corroborated = LoreLookup.Spoken(
                await _editing.LookUp(place.SystemName, budget.Token).ConfigureAwait(true)) is not null;
        }

        _editing.Book.Add(place.SystemAddress, place.SystemName, note, LoreArrival.Panel, _editing.Now(), place.FrontierId, corroborated);

        _note.Text = string.Empty;
        _note.IsEnabled = true;
        _status.Text = corroborated
            ? $"Stored, and the search agreed. That is recorded as a label, not as a verdict."
            : $"Stored as your word about {place.SystemName}.";

        Refresh();
    }

    private void Refresh()
    {
        _entries.Children.Clear();

        var mine = _editing.Book.Store.Entries
            .OrderByDescending(entry => entry.AddedAt ?? DateTimeOffset.MinValue)
            .ToArray();

        if (mine.Length == 0)
        {
            _entries.Children.Add(Muted("Nothing yet. What you add here is said back the next time you arrive."));
            return;
        }

        foreach (var entry in mine)
        {
            _entries.Children.Add(Card(entry));
        }

        // The Commander's own words, so a line that could not be read back is reported rather
        // than dropped — the same rule the checklist file follows.
        foreach (var problem in _editing.Book.Store.Problems)
        {
            _entries.Children.Add(Muted($"Could not read {problem.What}: {problem.Why}"));
        }
    }

    private Control Card(LoreEntry entry)
    {
        var forget = new Button { Content = "Forget", MinWidth = 90 };

        forget.Click += (_, _) =>
        {
            _editing.Book.Store.Forget(entry.SystemAddress);
            Refresh();
        };

        var heading = new TextBlock
        {
            Text = $"{(entry.Name.Length == 0 ? entry.SystemAddress.ToString() : entry.Name)} — {Label(entry)}",
            FontSize = TypeScale.Secondary,
            FontWeight = FontWeight.SemiBold,
        };

        var note = new SelectableTextBlock { Text = entry.Note, TextWrapping = TextWrapping.Wrap };

        var stack = new StackPanel { Spacing = 6, Children = { heading, note, forget } };

        var inset = new Border { Padding = new Thickness(12, 10), CornerRadius = new CornerRadius(4), Child = stack };
        Themed(inset, Border.BackgroundProperty, ThemeManager.SurfaceAltKey);

        return inset;
    }

    /// <summary>
    /// How an entry is labelled in the list, which is the same distinction the spoken sentence
    /// makes. Shown rather than implied, because the whole value of the tier is that it is
    /// visible.
    /// </summary>
    private static string Label(LoreEntry entry) => (entry.Tier, entry.Arrival) switch
    {
        (LoreTier.Corroborated, _) => "a search agreed at the time",
        (_, LoreArrival.Model) => "written by D47 itself, unverified",
        _ => "your word",
    };

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
