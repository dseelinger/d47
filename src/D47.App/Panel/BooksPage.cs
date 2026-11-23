using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Threading;
using D47.App.Controls;
using D47.App.Theming;
using D47.Core.Audio;
using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Interface;

namespace D47.App.Panel;

/// <summary>Media › Books: the book being read, the voice it is read with and the shelf.</summary>
public sealed class BooksPage : UserControl, IPageSummary, IPageChrome
{
    public const string RootKey = MediaFurnishing.BooksRoot;

    public const string PageSummary = "Public domain books, read aloud.";

    public const string NothingReading = "Nothing is being read. Press a book on your shelf to open it.";

    public const string EmptyShelf = "Your shelf is empty.";

    public const string ReadByName = "BookReadBy";

    public const string ChangeVoiceName = "BookChangeVoice";

    public const string RowName = "BookRow";

    public const string AddName = "BookAdd";

    public const string NotInstalled = "Not installed";

    /// <summary>The books the big panel lists at a time.</summary>
    public const int BigBooksPerPage = 3;

    private readonly BooksHost _host;
    private readonly PanelNavigator _nav;
    private readonly PanelPrompts _prompts;
    private readonly Action<string> _openSettings;
    private readonly bool _big;
    private readonly StackPanel _body = new() { Spacing = 16, MaxWidth = 900, HorizontalAlignment = HorizontalAlignment.Left };
    private readonly StackPanel _notices = new() { Spacing = 8 };
    private readonly StackPanel _now = new() { Spacing = 2 };
    private readonly ContentControl _by = new();
    private readonly StackPanel _shelf = new() { Spacing = Gaps.Tile };
    private readonly StatusLine _line = new() { Margin = new Thickness(0, 6, 0, 0) };

    private int _first;

    public BooksPage(BooksHost host, PanelNavigator nav, PanelPrompts prompts, Action<string> openSettings, Control? settingsStrip = null, bool big = false)
    {
        _host = host;
        _nav = nav;
        _prompts = prompts;
        _openSettings = openSettings;
        _big = big;
        BarTool = PageChrome.ToolOf(settingsStrip);

        _body.Children.Add(_notices);
        _body.Children.Add(_now);

        if (!_big)
        {
            _body.Children.Add(_by);
        }

        _body.Children.Add(_shelf);

        if (host.Folder is not null)
        {
            var add = BooksKit.Tile(AddName, "ADD A BOOK", "Add a book");
            add.HorizontalAlignment = HorizontalAlignment.Left;
            add.MinWidth = 160;
            add.Click += (_, _) => _nav.Drill(BookAddPage.Crumb);
            _body.Children.Add(add);
        }

        var root = new DockPanel { Margin = new Thickness(14) };

        if (settingsStrip is not null)
        {
            root.DockStripAtTop(settingsStrip);
        }

        root.Children.Add(new ScrollViewer
        {
            Content = _body,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
        });

        Content = root;
        DrawBy();
        Draw();
    }

    public Control? BarTool { get; }

    public string? HelpTopic => InterfaceCapability.Id;

    string IPageSummary.Summary => PageSummary;

    public event EventHandler? SummaryChanged
    {
        add { }
        remove { }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _host.Player.Changed += OnPlayerChanged;
        _host.Changed += OnPlayerChanged;
        DrawBy();
        Draw();
        _ = ListVoicesAsync();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _host.Player.Changed -= OnPlayerChanged;
        _host.Changed -= OnPlayerChanged;
    }

    /// <summary>Lists the chosen provider's voices so the mono line can name the voice.</summary>
    private async Task ListVoicesAsync()
    {
        var provider = _host.Settings.Provider;

        if (!_host.Installed(provider))
        {
            return;
        }

        await _host.Voices(provider);
        Draw();
    }

    private void OnPlayerChanged() => Dispatcher.UIThread.Post(Draw);

    private void OpenVoiceSettings() => _openSettings(BooksKit.VoicePlace);

    /// <summary>Redraws everything that follows the player and the shelf.</summary>
    internal void Draw()
    {
        var now = _host.Player.Now;

        DrawNotices(now);
        DrawNow(now);
        DrawShelf(now);
    }

    private void DrawNotices(D47.App.Voice.BookPlayback? now)
    {
        _notices.Children.Clear();

        if (now is { Problem: not null })
        {
            _notices.Children.Add(BooksKit.Stopped(_host, now, OpenVoiceSettings));
        }
    }

    private void DrawNow(D47.App.Voice.BookPlayback? now)
    {
        _now.Children.Clear();
        _now.Children.Add(ListRow.Head("Now reading"));

        if (now is null)
        {
            _now.Children.Add(Typeset.Prose(NothingReading, 15, ThemeManager.GreyKey, wrap: true, margin: new Thickness(0, 6, 0, 6)));
            return;
        }

        _now.Children.Add(BooksKit.NowBlock(_host, now, titled: true, _big));

        var pause = BooksKit.Tile(BooksKit.PauseName, BooksKit.PauseText(now), "Pause or resume the book");
        pause.Click += (_, _) => BooksKit.Pause(_host);

        var back = BooksKit.Tile(BooksKit.BackName, "BACK A PARAGRAPH", "Back a paragraph");
        back.Click += (_, _) => _host.BackAParagraph();

        var next = BooksKit.Tile(BooksKit.NextName, "NEXT CHAPTER", "Next chapter");
        next.IsEnabled = _host.HasNextChapter;
        next.Click += (_, _) => _host.NextChapter();

        var chapters = BooksKit.Tile(BooksKit.ChaptersName, "CHAPTERS", "Chapters");
        chapters.Click += (_, _) => _nav.Drill(BookPage.CrumbOf(now.Book.Source, now.Book.Title));

        _now.Children.Add(new WrapPanel
        {
            ItemSpacing = Gaps.Tile,
            LineSpacing = Gaps.Tile,
            Margin = new Thickness(0, 2, 0, 0),
            Children = { pause, back, next, chapters },
        });

        BooksKit.Say(_line, _host, now);

        if (_line.Parent is null)
        {
            _now.Children.Add(_line);
        }
    }

    /// <summary>Redraws the Read by choice, its status lines and Change voice, after the provider or the voice changed.</summary>
    private void DrawBy()
    {
        var settings = _host.Settings;
        var providers = BooksHost.Providers;
        var statuses = new List<ChoiceStatus?>();

        foreach (var id in providers)
        {
            var chosen = string.Equals(id, settings.Provider, StringComparison.OrdinalIgnoreCase);

            var edge = string.Equals(id, TtsProviderCatalog.EdgeId, StringComparison.OrdinalIgnoreCase);
            var where = edge ? "needs an internet connection" : "this computer · free";

            statuses.Add(!_host.Installed(id)
                ? new ChoiceStatus(NotInstalled, ChoiceTone.Grey)
                : new ChoiceStatus(chosen ? $"{_host.VoiceName(settings)} · {where}" : Capitalised(where), ChoiceTone.Grey));
        }

        var segment = new Segment
        {
            Name = ReadByName,
            ItemsSource = [.. providers.Select(id => TtsProviderCatalog.Selected(id).Name)],
            Statuses = statuses,
        };

        segment.SelectedIndex = Math.Max(0, providers.ToList().FindIndex(id => string.Equals(id, settings.Provider, StringComparison.OrdinalIgnoreCase)));

        for (var i = 0; i < providers.Count; i++)
        {
            segment.SetOptionEnabled(i, _host.Installed(providers[i]));
        }

        segment.SelectionChanged += (_, _) =>
        {
            if (segment.SelectedIndex >= 0 && segment.SelectedIndex < providers.Count && _host.Installed(providers[segment.SelectedIndex]))
            {
                _host.Choose(providers[segment.SelectedIndex]);
                DrawBy();
                Draw();
                _ = ListVoicesAsync();
            }
        };

        var change = BooksKit.Tile(ChangeVoiceName, "CHANGE VOICE", "Change voice");
        change.IsEnabled = _host.Installed(settings.Provider);
        change.Click += (_, _) => _ = ChangeVoiceAsync();

        _by.Content = new StackPanel
        {
            Spacing = 6,
            Children =
            {
                Typeset.Chrome("READ BY", 13, ThemeManager.AKey, FontWeightSemiBold, wrap: false),
                segment,
                new StackPanel { Orientation = Orientation.Horizontal, Children = { change } },
            },
        };
    }

    private static string Capitalised(string text) => char.ToUpperInvariant(text[0]) + text[1..];

    private static Avalonia.Media.FontWeight FontWeightSemiBold => Avalonia.Media.FontWeight.SemiBold;

    private async Task ChangeVoiceAsync()
    {
        var settings = _host.Settings;
        var provider = TtsProviderCatalog.Selected(settings.Provider);
        var listed = await _host.Voices(settings.Provider);

        _prompts.Pick(
            "books.voice",
            "Voice",
            new PickerRequest
            {
                Prompt = $"The {provider.Name} voice books are read with",
                Choices = [.. listed.Voices.Select(voice => voice.Id)],
                Describe = id => listed.LabelFor(id, provider),
                Current = settings.Voice,
                DefaultDisplay = string.Equals(settings.Provider, TtsProviderCatalog.EdgeId, StringComparison.OrdinalIgnoreCase)
                    ? D47.Core.Speech.LocalVoiceStandIn.EdgeVoice
                    : "the first voice",
                WhyEmpty = listed.WhyEmpty(provider.Name),
                Sorts = [PickerSort.Name],
            },
            result =>
            {
                _host.ChooseVoice(result.Value);
                DrawBy();
                Draw();
            });
    }

    private void DrawShelf(D47.App.Voice.BookPlayback? now)
    {
        _shelf.Children.Clear();

        var books = _host.Shelf.Books;
        var count = books.Count;

        _shelf.Children.Add(ListRow.Head($"On your shelf · {(count == 1 ? "1 book" : $"{count} books")}"));

        if (count == 0)
        {
            _shelf.Children.Add(Typeset.Prose(EmptyShelf, 15, ThemeManager.GreyKey, wrap: true, margin: new Thickness(0, 6, 0, 6)));
            return;
        }

        _first = Math.Clamp(_first, 0, Math.Max(0, (count - 1) / BigBooksPerPage * BigBooksPerPage));

        IEnumerable<D47.Core.Books.ShelfBook> shown = _big ? books.Skip(_first).Take(BigBooksPerPage) : books;

        foreach (var entry in shown)
        {
            _shelf.Children.Add(Row(entry, now));
        }

        if (_big && count > BigBooksPerPage)
        {
            _shelf.Children.Add(BooksKit.Pager(_first, BigBooksPerPage, count, first =>
            {
                _first = first;
                Draw();
            }));
        }
    }

    private Button Row(D47.Core.Books.ShelfBook entry, D47.App.Voice.BookPlayback? now)
    {
        var reading = now is not null && now.Book.Source == entry.Source;
        var finished = entry.Finished && !reading;
        var name = ListRow.Name(new TextBlock { Text = entry.Title, TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis });
        var main = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = { name } };

        if (reading)
        {
            ThemeInk.Bind(name, TextBlock.ForegroundProperty, ThemeManager.CyanKey);
        }

        if (!_big)
        {
            var sub = ListRow.Sub(new TextBlock
            {
                Text = $"{entry.Author} · {BooksKit.SourceLabel(entry.Source)}{(reading ? " · reading now" : string.Empty)}",
                TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis,
            });

            if (reading)
            {
                ThemeInk.Bind(sub, TextBlock.ForegroundProperty, ThemeManager.CyanKey);
            }

            main.Children.Add(sub);
        }

        var aside = Typeset.Mono(
            BooksKit.PlaceOf(entry, now),
            13,
            reading ? ThemeManager.CyanKey : finished ? ThemeManager.BlueKey : ThemeManager.GreyKey,
            vertical: VerticalAlignment.Center,
            margin: new Thickness(10, 0, 0, 0));
        DockPanel.SetDock(aside, Dock.Right);

        var row = new Button
        {
            Name = RowName,
            Tag = entry.Source.Id,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            MinHeight = TypeScale.MinimumTarget,
            Content = new DockPanel { LastChildFill = true, Children = { aside, main } },
        };
        Avalonia.Automation.AutomationProperties.SetName(row, entry.Title);
        ListRow.Dress(row, reading);
        row.Click += (_, _) => _nav.Drill(BookPage.CrumbOf(entry.Source, entry.Title));

        return row;
    }
}
