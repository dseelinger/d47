using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using D47.App.Controls;
using D47.App.Theming;
using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Conversation;
using D47.Core.Journal;

namespace D47.App.Panel;

/// <summary>
/// Settings › Phrases: adding a phrase of the Commander's own, the ones already taught, and every built-in
/// phrase grouped by capability. Adding goes through <c>add_phrase</c>, so the page refuses what the voice
/// refuses.
/// </summary>
public sealed class PhrasesPage : UserControl, IFilterablePage
{
    /// <summary>The Settings tab's second root.</summary>
    public const string RootKey = "settings.phrases";

    public const string Word = "Phrases";

    public const string Lead = "What D47 understands straight away, without asking the model.";

    public const string AddLead = "Type a wording or a pattern, then pick the phrase it stands for.";

    public const string Hint =
        "Square brackets give choices: [boost|get clear] hears “boost” or “get clear”. "
        + "You can also teach one by voice: “" + LearnedPhrasesCapability.TeachPhrase + "”.";

    public const string SayPlaceholder = "[boost|get clear] and [jump|engage]";

    public const string PickPlaceholder = "Pick the phrase it stands for";

    public const string ClashLabel = "Clash · not added";

    public const string NothingToAddLabel = "Nothing to add";

    public const string NothingToAdd = "Type a wording or a pattern first.";

    public const string PickAPhraseLabel = "Pick a phrase";

    public const string PickAPhrase = "Pick the phrase it stands for from the list.";

    public const string NotAddedLabel = "Not added";

    public const string NothingTaught =
        "Nothing taught yet. Add one above, or say “" + LearnedPhrasesCapability.TeachPhrase + "”.";

    public const string NobodyFlying = "Nobody is flying, so there are no phrases of your own to show.";

    public const string Forget = "Forget this phrase";

    /// <summary>The own-phrase table: wording, the arrow, the phrase it stands for, the forget tile.</summary>
    private const string OwnColumns = "*,28,*,44";

    /// <summary>The built-in table: the phrase, then what it does.</summary>
    private const string BuiltInColumns = "260,*";

    /// <summary>One built-in phrase and the sentence saying what it does.</summary>
    public sealed record BuiltIn(string Phrase, string Sentence);

    /// <summary>The built-in phrases of one capability, under its panel title.</summary>
    public sealed record Group(string Title, IReadOnlyList<BuiltIn> Phrases);

    private readonly CapabilityRegistry _registry;
    private readonly LearnedPhrasesStore _store;
    private readonly Func<CommanderGameState?> _commander;
    private readonly Func<PhraseBook> _book;
    private readonly JournalClock _clock;

    private readonly TextBox _say;
    private readonly InlinePicker _picker = new() { ListMaxHeight = 280, Label = PickPlaceholder };
    private readonly Notice _notice = new() { IsVisible = false, Margin = new Thickness(0, 2, 0, 0) };
    private readonly StackPanel _lists = new();

    private string? _picked;
    private string _query = string.Empty;

    public PhrasesPage(
        CapabilityRegistry registry,
        LearnedPhrasesStore store,
        Func<CommanderGameState?> commander,
        Func<PhraseBook>? book = null)
    {
        _registry = registry;
        _store = store;
        _commander = commander;
        _book = book ?? (() => PhraseBook.From(registry, []));
        _clock = new JournalClock(() => commander()?.Session.LastEventAt);

        _say = new TextBox
        {
            PlaceholderText = SayPlaceholder,
            FontFamily = new FontFamily(Fonts.MonoFamily),
            FontSize = TypeScale.Control,
            MinHeight = 40,
            VerticalContentAlignment = VerticalAlignment.Center,
            InnerLeftContent = Prefix("Say"),
        };
        AutomationProperties.SetName(_say, "Say");
        _say.TextChanged += (_, _) => HideNotice();
        _say.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                _ = AddAsync();
            }
        };

        _picker.Picked += (_, phrase) =>
        {
            _picked = phrase;
            HideNotice();
            ShowPicker();
        };

        var add = new Button
        {
            Content = "Add",
            Height = 40,
            MinHeight = 40,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top,
        };
        add.Click += (_, _) => _ = AddAsync();

        _say.VerticalAlignment = VerticalAlignment.Top;
        _picker.VerticalAlignment = VerticalAlignment.Top;
        Grid.SetColumn(_picker, 1);
        Grid.SetColumn(add, 2);

        var form = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,*,110"),
            ColumnSpacing = Gaps.Tile,
            Children = { _say, _picker, add },
        };

        var body = new StackPanel
        {
            Children =
            {
                TitleText.Block(TitleText.Build(Word, TypeScale.Title, TitleRank.Screen), TitleText.Context("Settings ›")),
                Muted(Lead, new Thickness(0, 8, 0, 0), TypeScale.Tip),
                new StackPanel
                {
                    Margin = new Thickness(0, 28, 0, 0),
                    Spacing = 2,
                    Children =
                    {
                        GroupHead("Add a phrase", AddLead, chrome: false),
                        form,
                        _notice,
                    },
                },
                Muted(Hint, new Thickness(0, 8, 0, 0), TypeScale.Small),
                _lists,
            },
        };

        var root = new DockPanel { Margin = new Thickness(14) };
        var footer = new PageFooter(LearnedPhrasesCapability.TeachPhrase, _clock) { Margin = new Thickness(0, 12, 0, 0) };
        DockPanel.SetDock(footer, Dock.Bottom);
        root.Children.Add(footer);
        root.Children.Add(LoadoutPages.Scrolling(new Grid
        {
            ColumnDefinitions = [new ColumnDefinition(GridLength.Star) { MaxWidth = 1000 }, new ColumnDefinition(GridLength.Auto)],
            Children = { body },
        }));

        Content = root;

        _store.Changed += Refresh;

        ShowPicker();
        Build();
    }

    public bool Filters => true;

    public string FilterPlaceholder => "Search every phrase";

    public void Filter(string? query)
    {
        var trimmed = query?.Trim() ?? string.Empty;

        if (string.Equals(trimmed, _query, StringComparison.Ordinal))
        {
            return;
        }

        _query = trimmed;
        Build();
    }

    /// <summary>Moves the footer's time on when the journal has.</summary>
    public bool Tick() => _clock.Tick();

    /// <summary>Redraws after a phrase is added or forgotten, from the page or by voice.</summary>
    public void Refresh() => Dispatcher.UIThread.Post(Build);

    /// <summary>The empty state while a search hides every phrase.</summary>
    public static string NoMatch(string query) => $"No phrase matches “{query}”.";

    /// <summary>
    /// Every phrase in the book once, under the panel title of the first capability it reaches, with the
    /// sentence saying what it does; only those whose phrase, sentence or title contains
    /// <paramref name="query"/>.
    /// </summary>
    public static IReadOnlyList<Group> Groups(PhraseBook book, CapabilityRegistry registry, string query = "")
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var groups = new List<(string Title, List<BuiltIn> Phrases)>();

        foreach (var entry in book.Entries)
        {
            if (!seen.Add(entry.Phrase))
            {
                continue;
            }

            var title = Title(registry, entry.CapabilityId);
            var phrase = new BuiltIn(entry.Phrase, PhraseBook.Describe(entry, registry));

            if (query.Length > 0 && !Contains(phrase.Phrase, query) && !Contains(phrase.Sentence, query) && !Contains(title, query))
            {
                continue;
            }

            var index = groups.FindIndex(group => string.Equals(group.Title, title, StringComparison.Ordinal));

            if (index < 0)
            {
                groups.Add((title, [phrase]));
            }
            else
            {
                groups[index].Phrases.Add(phrase);
            }
        }

        return [.. groups.Select(group => new Group(group.Title, group.Phrases))];
    }

    /// <summary>The notice text and mono detail line for a refused clash.</summary>
    public static (string Text, string Detail) Describe(PhraseClash clash, PhraseBook book, CapabilityRegistry registry)
    {
        if (clash.Kind == PhraseClashKind.OwnPhrase)
        {
            return (
                $"“{clash.Wording}” already stands for “{clash.StandsFor}”. Choose another wording.",
                $"Your phrase · {clash.Pattern}");
        }

        var entry = book.Entries.FirstOrDefault(candidate =>
            string.Equals(candidate.Phrase, clash.StandsFor, StringComparison.OrdinalIgnoreCase)
            && string.Equals(candidate.CapabilityId, clash.CapabilityId, StringComparison.Ordinal));

        var known = entry is null
            ? $"“{clash.Wording}” is already a phrase D47 knows."
            : $"“{clash.Wording}” is already a phrase D47 knows: {LowerFirst(PhraseBook.Describe(entry, registry))}";

        return (
            $"{known} Choose another wording.",
            $"Built-in phrase · {Title(registry, clash.CapabilityId ?? string.Empty)}");
    }

    private static string Title(CapabilityRegistry registry, string capabilityId) =>
        registry.Find(capabilityId)?.Descriptor is { } descriptor
            ? descriptor.Display.PanelTitle ?? descriptor.Name
            : capabilityId;

    private static bool Contains(string text, string query) =>
        text.Contains(query, StringComparison.OrdinalIgnoreCase);

    private static string LowerFirst(string sentence) =>
        sentence.Length > 1 && char.IsUpper(sentence[0]) && !char.IsUpper(sentence[1])
            ? char.ToLower(sentence[0], CultureInfo.InvariantCulture) + sentence[1..]
            : sentence;

    private string FrontierId => _commander()?.Identity.FrontierId ?? string.Empty;

    private async Task AddAsync()
    {
        var pattern = _say.Text?.Trim() ?? string.Empty;

        if (pattern.Length == 0)
        {
            ShowNotice(NothingToAddLabel, NothingToAdd, null, invalid: true);
            return;
        }

        if (_picked is not { } phrase)
        {
            ShowNotice(PickAPhraseLabel, PickAPhrase, null, invalid: false);
            return;
        }

        var result = await _registry.InvokeAsync(
            LearnedPhrasesCapability.AddTool,
            new ToolArguments(new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["pattern"] = pattern,
                ["phrase"] = phrase,
            }),
            CancellationToken.None,
            ToolCaller.Commander);

        if (!result.IsError)
        {
            _picked = null;
            _say.Text = string.Empty;
            HideNotice();
            ShowPicker();
            return;
        }

        var book = _book();

        if (PhrasePattern.TryExpand(pattern, out var wordings, out _)
            && _store.FindClash(FrontierId, wordings, phrase, book) is { } clash)
        {
            var (text, detail) = Describe(clash, book, _registry);
            ShowNotice(ClashLabel, text, detail, invalid: true);
        }
        else
        {
            ShowNotice(NotAddedLabel, result.Content, null, invalid: true);
        }
    }

    private void ShowNotice(string label, string text, string? detail, bool invalid)
    {
        _notice.Label = label;
        _notice.Text = text;
        _notice.Detail = detail;
        _notice.IsVisible = true;

        if (invalid)
        {
            _say.Classes.Add(FieldMessage.ErrorClass);
        }
    }

    private void HideNotice()
    {
        _notice.IsVisible = false;
        _say.Classes.Remove(FieldMessage.ErrorClass);
    }

    private void ShowPicker()
    {
        var options = Groups(_book(), _registry)
            .SelectMany(group => group.Phrases.Select(phrase =>
                (InlinePickerEntry)new InlinePickerOption(phrase.Phrase, phrase.Phrase, ThemeManager.WhiteKey, group.Title)))
            .ToList();

        _picker.Show(
            options,
            _picked ?? string.Empty,
            _picked ?? PickPlaceholder,
            _picked is null ? ThemeManager.Grey2Key : ThemeManager.WhiteKey,
            null);
    }

    private void Build()
    {
        _lists.Children.Clear();

        var fid = FrontierId;

        IReadOnlyList<LearnedPhrase> own = fid.Length > 0
            ? [.. _store.For(fid)
                .OrderByDescending(phrase => phrase.LearnedAt)
                .Where(phrase => _query.Length == 0 || Contains(phrase.Said, _query) || Contains(phrase.Phrase, _query))]
            : [];

        var groups = Groups(_book(), _registry, _query);

        if (_query.Length > 0 && own.Count == 0 && groups.Count == 0)
        {
            _lists.Children.Add(Muted(NoMatch(_query), new Thickness(0, 28, 0, 0)));
            return;
        }

        if (_query.Length == 0 || own.Count > 0)
        {
            var rows = new StackPanel { Spacing = 2 };

            foreach (var phrase in own)
            {
                rows.Children.Add(OwnRow(phrase));
            }

            if (own.Count == 0)
            {
                rows.Children.Add(Muted(fid.Length > 0 ? NothingTaught : NobodyFlying, new Thickness(0, 4, 0, 0)));
            }

            _lists.Children.Add(Section("Your phrases", own.Count, rows));
        }

        foreach (var group in groups)
        {
            var rows = new StackPanel { Spacing = 2 };

            foreach (var phrase in group.Phrases)
            {
                rows.Children.Add(BuiltInRow(phrase));
            }

            _lists.Children.Add(Section(group.Title, group.Phrases.Count, rows));
        }
    }

    private Border OwnRow(LearnedPhrase phrase)
    {
        var said = new TextBlock
        {
            Text = phrase.Said,
            FontFamily = new FontFamily(Fonts.MonoFamily),
            FontSize = TypeScale.Tip,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        LoadoutPages.Themed(said, TextBlock.ForegroundProperty, ThemeManager.CyanKey);

        var arrow = MaterialsPage.Chrome("›", ThemeManager.GreyKey);
        arrow.HorizontalAlignment = HorizontalAlignment.Center;
        Grid.SetColumn(arrow, 1);

        var standsFor = Prose(phrase.Phrase, ThemeManager.WhiteKey, TypeScale.Secondary);
        Grid.SetColumn(standsFor, 2);

        var forget = new Button
        {
            Width = 44,
            MinWidth = 44,
            Height = 40,
            MinHeight = 40,
            Padding = new Thickness(0),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            Content = Glyphs.Text("✕", TypeScale.Glyph),
            Classes = { "destructive" },
        };
        ToolTip.SetTip(forget, Forget);
        AutomationProperties.SetName(forget, Forget);
        Grid.SetColumn(forget, 3);

        forget.Click += (_, _) =>
        {
            _ = _registry.InvokeAsync(
                LearnedPhrasesCapability.ForgetTool,
                new ToolArguments(
                    new Dictionary<string, string>(StringComparer.Ordinal) { ["said"] = phrase.Said }),
                CancellationToken.None,
                ToolCaller.Commander);
        };

        return Slab(new Grid
        {
            ColumnDefinitions = new ColumnDefinitions(OwnColumns),
            Children = { said, arrow, standsFor, forget },
        }, new Thickness(14, 0, 2, 0));
    }

    private static Border BuiltInRow(BuiltIn phrase)
    {
        var said = Prose(phrase.Phrase, ThemeManager.WhiteKey, TypeScale.Secondary);

        var sentence = Prose(phrase.Sentence, ThemeManager.GreyKey, TypeScale.Small);
        Grid.SetColumn(sentence, 1);

        return Slab(new Grid
        {
            ColumnDefinitions = new ColumnDefinitions(BuiltInColumns),
            ColumnSpacing = 12,
            Children = { said, sentence },
        }, new Thickness(14, 0));
    }

    private static Border Slab(Control content, Thickness padding)
    {
        var row = new Border { MinHeight = TypeScale.MinimumTarget, Padding = padding, Child = content };
        LoadoutPages.Themed(row, Border.BackgroundProperty, ThemeManager.SlabKey);
        return row;
    }

    private static StackPanel Section(string title, int count, Control rows) => new()
    {
        Margin = new Thickness(0, 28, 0, 0),
        Spacing = 2,
        Children =
        {
            GroupHead(title, $"{count.ToString(CultureInfo.InvariantCulture)} {(count == 1 ? "phrase" : "phrases")}", chrome: true),
            rows,
        },
    };

    private static Control GroupHead(string title, string detail, bool chrome)
    {
        var name = TitleText.Build(title, TypeScale.Section, TitleRank.Group);
        name.VerticalAlignment = VerticalAlignment.Center;

        var aside = chrome
            ? MaterialsPage.Chrome(detail, ThemeManager.GreyKey)
            : Prose(detail, ThemeManager.GreyKey, TypeScale.Small);

        var head = TitleText.GroupRow(new WrapPanel { ItemSpacing = 12, LineSpacing = 4, Children = { name, aside } });
        head.Margin = new Thickness(0, 0, 0, 8);

        return head;
    }

    private static TextBlock Prose(string text, string key, double size)
    {
        var block = new TextBlock
        {
            Text = text,
            FontSize = size,
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
        };
        LoadoutPages.Themed(block, TextBlock.ForegroundProperty, key);
        return block;
    }

    private static TextBlock Muted(string text, Thickness margin, double size = TypeScale.Tip)
    {
        var block = LoadoutPages.Muted(text);
        block.FontSize = size;
        block.TextWrapping = TextWrapping.Wrap;
        block.Margin = margin;
        return block;
    }

    private static TextBlock Prefix(string text)
    {
        var block = new TextBlock
        {
            Text = text.ToUpperInvariant(),
            FontFamily = new FontFamily(Fonts.ChromeFamily),
            FontSize = TypeScale.Control,
            FontWeight = FontWeight.SemiBold,
            LetterSpacing = TypeScale.Control * Fonts.ChromeTracking,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(12, 0, 0, 0),
        };
        LoadoutPages.Themed(block, TextBlock.ForegroundProperty, ThemeManager.AKey);

        return block;
    }
}
