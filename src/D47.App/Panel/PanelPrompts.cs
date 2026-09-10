using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using D47.App.Theming;
using D47.Core.Interface;

namespace D47.App.Panel;

/// <summary>
/// The two questions the panel can put to the Commander — pick one of these, and say or type this — and
/// the two places either can be drawn (Phase 25, "Choosing takes the panel" and "Say it, or type it").
/// </summary>
public sealed class PanelPrompts : IHearsText
{
    private readonly PanelNavigator _nav;
    private readonly Avalonia.Controls.Panel _layer;

    /// <summary>The keys, in rows, as they are drawn.</summary>
    public static readonly string[] Keys =
        ["1234567890", "qwertyuiop", "asdfghjkl", "zxcvbnm-_.", " "];

    /// <summary>True in every listening mode, and what is shown until a host says something better.</summary>
    internal const string WaitingFallback = "Say it, or type it instead.";

    /// <summary>
    /// What a prompt waiting on speech says about itself — set by the surface from <see
    /// cref="PanelViewModel.ListeningPrompt"/>, which the host fills from the listening settings
    /// (remediation.md 10, item 12).
    /// </summary>
    public Func<string?>? Waiting { get; set; }

    /// <summary>What each open prompt is, by the crumb key it was pushed as.</summary>
    private readonly Dictionary<string, Func<Control>> _pages = [];

    /// <summary>The prompt currently taking speech, if any.</summary>
    private Action<Heard>? _listening;

    public PanelPrompts(PanelNavigator nav, Avalonia.Controls.Panel layer)
    {
        _nav = nav;
        _layer = layer;
    }

    /// <summary>Puts a question to the Commander and calls back with what they picked.</summary>
    public void Choose(ChoiceRequest request, Action<ChoiceOption> chosen)
    {
        Control Build() => Frame(
            request.Title,
            request.Context,
            Rows(request, option =>
            {
                Dismiss(request.Key, request.Surface);
                chosen(option);
            }),
            () => Dismiss(request.Key, request.Surface));

        Open(request.Key, request.Word, request.Surface == ChoiceSurface.Page, Build, request.Help);
    }

    /// <summary>Asks the Commander for a value, by voice or by keyboard, and calls back once with it.</summary>
    public void Enter(EntryRequest request, Action<string> done)
    {
        Open(request.Key, request.Word, page: true, () => Answering(request, value =>
        {
            Dismiss(request.Key, ChoiceSurface.Page);
            done(value);
        }));
    }

    /// <summary>
    /// The page that takes the answer: a picker where the caller could name every value it would
    /// accept, and the box-and-keyboard everywhere else (#282).
    /// </summary>
    private Control Answering(EntryRequest request, Action<string> done) =>
        request.Suggestions is { Count: > 0 }
            ? new PickPage(this, request, done)
            : new EntryPage(this, request, done);

    /// <summary>Hands the panel what was heard, for whichever prompt is listening.</summary>
    public void Hear(Heard heard) => _listening?.Invoke(heard);

    /// <summary>Drops whatever prompt is open, committing nothing (remediation.md 13, item 5).</summary>
    public void Abandon()
    {
        Attend(null);

        _layer.Children.Clear();
        _layer.IsVisible = false;

        // Every prompt level, not only the top one: a chooser can open a chooser, and a tab press means the
        // Commander is done with the whole stack of them.
        while (_nav.Trail.Count > 0 && _nav.Trail[^1].Modal)
        {
            _pages.Remove(_nav.Trail[^1].Key);
            _nav.Back();
        }
    }

    /// <summary>Whether a prompt is waiting on speech, so the host knows to route it here.</summary>
    public bool IsListening => _listening is not null;

    /// <summary>
    /// The control for a level that takes the panel, or null for a crumb that is not one of these.
    /// </summary>
    public Control? Build(NavCrumb crumb) =>
        _pages.TryGetValue(crumb.Key, out var build) ? build() : null;

    private void Open(string key, string word, bool page, Func<Control> build, string? help = null)
    {
        if (page)
        {
            _pages[key] = build;
            _nav.Take(new NavCrumb(key, word) { Help = help });
            return;
        }

        // The in-tree layer.
        _layer.Children.Clear();
        _layer.Children.Add(build());
        _layer.IsVisible = true;
    }

    private void Dismiss(string key, ChoiceSurface surface)
    {
        Attend(null);

        if (surface == ChoiceSurface.Layer)
        {
            _layer.Children.Clear();
            _layer.IsVisible = false;
            return;
        }

        _pages.Remove(key);

        // Only if it is still the level showing.
        if (_nav.Trail.Count > 0 && _nav.Trail[^1].Key == key)
        {
            _nav.Back();
        }
    }

    /// <summary>Points what is heard next at one prompt, or at none.</summary>
    private void Attend(Action<Heard>? heard) => _listening = heard;

    /// <summary>
    /// The shape both prompts share: a header saying what this is for, the body, and one way out.
    /// </summary>
    private static Control Frame(string title, string? context, Control body, Action dismissed)
    {
        var heading = new TextBlock
        {
            Text = title,
            FontSize = TypeScale.Heading,
            FontWeight = FontWeight.SemiBold,
            TextWrapping = TextWrapping.Wrap,
        };

        heading.Bind(TextBlock.ForegroundProperty, App.Current!.GetResourceObservable(ThemeManager.TextKey));

        var header = new StackPanel { Spacing = 3, Children = { heading } };

        if (!string.IsNullOrWhiteSpace(context))
        {
            var second = new TextBlock
            {
                Text = context,
                FontSize = TypeScale.Secondary,
                TextWrapping = TextWrapping.Wrap,
            };

            second.Bind(
                TextBlock.ForegroundProperty,
                App.Current!.GetResourceObservable(ThemeManager.TextMutedKey));

            header.Children.Add(second);
        }

        // The one way out, and it is the same affordance every level has: back.
        var back = new Button
        {
            Content = "Back",
            Padding = new Thickness(16, 7),
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 12, 0, 0),
        };

        back.Click += (_, _) => dismissed();

        var frame = new DockPanel { LastChildFill = true, Margin = new Thickness(16) };

        DockPanel.SetDock(header, Dock.Top);
        DockPanel.SetDock(back, Dock.Bottom);

        header.Margin = new Thickness(0, 0, 0, 14);

        frame.Children.Add(header);
        frame.Children.Add(back);
        frame.Children.Add(body);

        var card = new Border
        {
            Child = frame,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
        };

        card.Bind(Border.BackgroundProperty, App.Current!.GetResourceObservable(ThemeManager.SurfaceKey));
        card.Bind(Border.BorderBrushProperty, App.Current!.GetResourceObservable(ThemeManager.BorderKey));

        return card;
    }

    /// <summary>
    /// The options, one pressable row each, with what is fitted now marked rather than selected.
    /// </summary>
    private static Control Rows(ChoiceRequest request, Action<ChoiceOption> chosen)
    {
        var rows = new StackPanel { Spacing = 3 };
        var query = string.Empty;

        void Draw()
        {
            rows.Children.Clear();

            var shown = request.Options.Where(option => Matches(option, query)).ToList();

            if (shown.Count == 0)
            {
                var nothing = new TextBlock
                {
                    Text = $"Nothing here matches “{query}”.",
                    FontSize = TypeScale.Body,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(12, 6),
                };

                nothing.Bind(
                    TextBlock.ForegroundProperty,
                    App.Current!.GetResourceObservable(ThemeManager.TextMutedKey));

                rows.Children.Add(nothing);
                return;
            }

            // A heading wherever the group changes, and only over options that survived the search — so
            // narrowing the list cannot leave a heading standing over nothing.
            var group = (string?)null;

            foreach (var option in shown)
            {
                if (option.Group is { Length: > 0 } heading && heading != group)
                {
                    group = heading;
                    rows.Children.Add(Heading(heading));
                }

                rows.Children.Add(Option(request, option, chosen));
            }
        }

        Draw();

        var list = new ScrollViewer
        {
            Content = rows,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        };

        if (!request.Searchable)
        {
            return list;
        }

        var box = new TextBox
        {
            PlaceholderText = "Search",
            FontSize = TypeScale.Body,
            Padding = new Thickness(10, 8),
        };

        // Focused once it is actually in a tree, so a Commander who opened a chooser to look something up can
        // simply type it — reported against the module chooser as "I should not have to click it"
        // (remediation 15 item 3).
        box.AttachedToVisualTree += (_, _) =>
            Dispatcher.UIThread.Post(() => box.Focus(), DispatcherPriority.Input);

        var board = new StackPanel { Spacing = 6, IsVisible = false, Margin = new Thickness(0, 8, 0, 0) };

        void Typed(string text)
        {
            query = text;

            if (box.Text != text)
            {
                box.Text = text;
                box.CaretIndex = text.Length;
            }

            Draw();
        }

        box.TextChanged += (_, _) =>
        {
            query = box.Text ?? string.Empty;
            Draw();
        };

        board.Children.Add(Board(
            character => Typed(query + character),
            () => Typed(query.Length > 0 ? query[..^1] : query),
            () => Typed(string.Empty)).Control);

        var swap = new Button { Content = "Type it instead", Padding = new Thickness(14, 6) };

        swap.Click += (_, _) =>
        {
            board.IsVisible = !board.IsVisible;
            swap.Content = board.IsVisible ? "Hide the keyboard" : "Type it instead";
        };

        var head = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };

        DockPanel.SetDock(swap, Dock.Right);
        swap.Margin = new Thickness(8, 0, 0, 0);

        head.Children.Add(swap);
        head.Children.Add(box);

        var body = new DockPanel { LastChildFill = true };

        DockPanel.SetDock(head, Dock.Top);
        DockPanel.SetDock(board, Dock.Bottom);

        body.Children.Add(head);
        body.Children.Add(board);
        body.Children.Add(list);

        return body;
    }

    /// <summary>Whether a row survives the query.</summary>
    private static bool Matches(ChoiceOption option, string query) =>
        query.Trim().Length == 0
        || option.Label.Contains(query.Trim(), StringComparison.OrdinalIgnoreCase)
        || (option.Detail?.Contains(query.Trim(), StringComparison.OrdinalIgnoreCase) ?? false);

    /// <summary>One option, as the pressable row it is drawn as.</summary>
    private static Control Heading(string text)
    {
        var said = new TextBlock
        {
            Text = text,
            FontSize = TypeScale.Secondary,
            FontWeight = FontWeight.Bold,
            Margin = new Thickness(12, 10, 12, 4),
        };

        said.Bind(
            TextBlock.ForegroundProperty,
            App.Current!.GetResourceObservable(ThemeManager.TextMutedKey));

        return said;
    }

    private static Control Option(
        ChoiceRequest request, ChoiceOption option, Action<ChoiceOption> chosen)
    {
        var current = option.Key == request.Current;

        var label = new TextBlock
        {
            Text = option.Label,
            FontSize = TypeScale.Body,
            FontWeight = current ? FontWeight.SemiBold : FontWeight.Normal,
            TextWrapping = TextWrapping.Wrap,
        };

        var stack = new StackPanel { Spacing = 1, Children = { label } };

        if (!string.IsNullOrWhiteSpace(option.Detail))
        {
            var detail = new TextBlock
            {
                Text = option.Detail,
                FontSize = TypeScale.Secondary,
                TextWrapping = TextWrapping.Wrap,
            };

            detail.Bind(
                TextBlock.ForegroundProperty,
                App.Current!.GetResourceObservable(ThemeManager.TextMutedKey));

            stack.Children.Add(detail);
        }

        if (current)
        {
            // Said rather than only coloured.
            stack.Children.Add(new TextBlock
            {
                Text = request.CurrentWord,
                FontSize = TypeScale.Small,
                [!TextBlock.ForegroundProperty] =
                    App.Current!.GetResourceObservable(ThemeManager.AccentKey).ToBinding(),
            });
        }

        var row = new Button
        {
            Content = stack,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Left,

            // Tall enough to be pressed by a ray at a metre.
            MinHeight = 34,
            Padding = new Thickness(12, 6),
        };

        row.Click += (_, _) => chosen(option);

        return row;
    }

    /// <summary>
    /// Every key of the drawn board, so a spelled word can press the one it named (#51).
    /// </summary>
    /// <param name="Control">The board itself.</param>
    private sealed record BoardKeys(
        Control Control,
        IReadOnlyDictionary<char, Button> Characters,
        Button Delete,
        Button Clear);

    /// <summary>
    /// The drawn keyboard, as a control rather than as a method on the page that first needed one.
    /// </summary>
    private static BoardKeys Board(Action<char> pressed, Action back, Action clear)
    {
        var board = new StackPanel { Spacing = 6 };
        var characters = new Dictionary<char, Button>();

        foreach (var row in Keys)
        {
            var line = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 6,
                HorizontalAlignment = HorizontalAlignment.Center,
            };

            foreach (var key in row)
            {
                var character = key;

                var button = new Button
                {
                    Content = character == ' ' ? "space" : character.ToString(),
                    Width = character == ' ' ? 220 : 46,
                    Height = 40,
                    HorizontalContentAlignment = HorizontalAlignment.Center,
                    VerticalContentAlignment = VerticalAlignment.Center,
                };

                button.Click += (_, _) => pressed(character);

                characters[character] = button;
                line.Children.Add(button);
            }

            board.Children.Add(line);
        }

        var erase = new Button { Content = "delete", Height = 40, Padding = new Thickness(16, 0) };
        var empty = new Button { Content = "clear", Height = 40, Padding = new Thickness(16, 0) };

        erase.Click += (_, _) => back();
        empty.Click += (_, _) => clear();

        board.Children.Add(new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 4, 0, 0),
            Children = { erase, empty },
        });

        return new BoardKeys(board, characters, erase, empty);
    }

    /// <summary>
    /// One value being said or typed, with the correction loop around it (Phase 25, "Say it, or type
    /// it").
    /// </summary>
    private sealed class EntryPage : UserControl
    {
        private readonly PanelPrompts _host;
        private readonly EntryRequest _request;
        private readonly Action<string> _done;

        private readonly TextBox _shown;
        private readonly TextBlock _state;
        private readonly StackPanel _board = new() { Spacing = 6 };
        private readonly Button _swap;
        private readonly Button _accept;

        /// <summary>The keys, so a spelled word can press the one it named (#51).</summary>
        private readonly BoardKeys _keys;

        /// <summary>The one way out, which the Back button raises and the cancel key says.</summary>
        private readonly Action _dismiss;

        /// <summary>
        /// The visible listening state, and it has to be visible or the Commander is talking at a blank
        /// page (Phase 25).
        /// </summary>
        private string Waiting => _host.Waiting?.Invoke() is { Length: > 0 } said
            ? said
            : WaitingFallback;

        private string _typed;
        private bool _keyboard;

        /// <summary>
        /// Whether the box is being filled from <see cref="_typed"/> rather than the other way round.
        /// </summary>
        private bool _writingBack;

        public EntryPage(PanelPrompts host, EntryRequest request, Action<string> done)
        {
            _host = host;
            _request = request;
            _done = done;
            _typed = request.Initial;

            // Writable, which it was not (remediation.md 10, item 11).
            _shown = new TextBox
            {
                Text = _typed,
                FontSize = TypeScale.Heading,
                Padding = new Thickness(12, 10),
                Margin = new Thickness(0, 0, 0, 10),
            };

            // The box is the value when the Commander types into it.
            _shown.TextChanged += (_, _) =>
            {
                if (!_writingBack)
                {
                    _typed = _shown.Text ?? string.Empty;
                }
            };

            _state = new TextBlock
            {
                FontSize = TypeScale.Secondary,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 12),
            };

            _state.Bind(
                TextBlock.ForegroundProperty,
                App.Current!.GetResourceObservable(ThemeManager.TextMutedKey));

            _swap = new Button { Padding = new Thickness(14, 6) };
            _swap.Click += (_, _) => Show(!_keyboard);

            _keys = BuildBoard();

            _accept = new Button
            {
                Content = "Done",
                Padding = new Thickness(18, 7),
            };

            _accept.Click += (_, _) => Commit(_typed);

            var actions = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 12, 0, 0),
                Children = { _swap, _accept },
            };

            var body = new DockPanel { LastChildFill = true };
            DockPanel.SetDock(_shown, Dock.Top);
            DockPanel.SetDock(_state, Dock.Top);
            DockPanel.SetDock(actions, Dock.Bottom);

            body.Children.Add(_shown);
            body.Children.Add(_state);
            body.Children.Add(actions);
            body.Children.Add(new ScrollViewer
            {
                Content = _board,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            });

            _dismiss = () => _host.Dismiss(request.Key, ChoiceSurface.Page);

            Content = Frame(request.Title, request.Context, body, _dismiss);

            Show(request.Surface == EntrySurface.Keyboard);

            // Focused once it is actually in a tree, so a Commander at a desk can simply type or paste
            // (remediation.md 10, item 11).
            AttachedToVisualTree += (_, _) => _shown.Focus();
        }

        /// <summary>Swaps between listening and typing, and says which one is on.</summary>
        /// <param name="say">
        /// What to put above the board instead of the standing line, for the keyboard coming back on its
        /// own.
        /// </param>
        private void Show(bool keyboard, string? say = null)
        {
            _keyboard = keyboard;

            _board.IsVisible = keyboard;
            _swap.Content = keyboard ? "Say it instead" : "Type it instead";

            // Listening either way (#51): with the keys drawn, what is heard is spelled onto them.
            _state.Text = say ?? (keyboard ? Spelling.Shape : Waiting);
            _host.Attend(OnHeard);
        }

        /// <summary>Puts the value into the box without the box putting it back.</summary>
        private void WriteBack()
        {
            _writingBack = true;

            try
            {
                _shown.Text = _typed;
                _shown.CaretIndex = _typed.Length;
            }
            finally
            {
                _writingBack = false;
            }
        }

        private void OnHeard(Heard heard)
        {
            // With the keys drawn, an utterance is spelling before it is anything else (#51).
            if (_keyboard)
            {
                Spell(heard);
                return;
            }

            // A partial is shown and never committed.
            if (!heard.Final)
            {
                _state.Text = string.IsNullOrWhiteSpace(heard.Text)
                    ? Waiting
                    : $"{Waiting} {heard.Text}";

                return;
            }

            if (TextEntryLoop.Judge(heard, _request.Validate, out var verdict) is { } fallback)
            {
                Show(keyboard: true, TextEntryLoop.Explain(fallback, verdict?.Complaint));
                return;
            }

            Commit(heard.Text.Trim());
        }

        /// <summary>
        /// What was heard while the keys are drawn: pressed onto them when every word is a key, and
        /// taken whole as the value when any word is not. Presses go through the same <see
        /// cref="Button.Click"/> a press on the drawn key raises (#51).
        /// </summary>
        private void Spell(Heard heard)
        {
            var read = Spelling.Hear(heard);

            switch (read.Outcome)
            {
                case SpelledOutcome.Waiting:
                    _state.Text = read.Text.Length > 0 ? read.Text : Spelling.Shape;
                    return;

                case SpelledOutcome.NotCaught:
                    _state.Text = read.Say;
                    return;

                case SpelledOutcome.Dictation:
                    _state.Text = read.Say;
                    _typed = read.Text;
                    WriteBack();
                    return;

                default:
                    _state.Text = read.Text;
                    break;
            }

            foreach (var key in read.Keys)
            {
                if (key.Press == SpelledPress.Cancel)
                {
                    _dismiss();
                    return;
                }

                var button = key.Press switch
                {
                    SpelledPress.Delete => _keys.Delete,
                    SpelledPress.Clear => _keys.Clear,
                    SpelledPress.Done => _accept,
                    _ => _keys.Characters.GetValueOrDefault(key.Character),
                };

                if (button is null)
                {
                    continue;
                }

                button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent) { Source = button });

                // Done takes the value, and there is nothing left to press into.
                if (key.Press == SpelledPress.Done)
                {
                    return;
                }
            }
        }

        /// <summary>Takes the value, once, having asked whatever the caller wanted asking.</summary>
        private void Commit(string value)
        {
            if (_request.Validate?.Invoke(value) is { Accepted: false } refused)
            {
                _state.Text = TextEntryLoop.Explain(
                    EntryFallback.DidNotResolve, refused.Complaint);

                Show(keyboard: true);
                return;
            }

            _host.Attend(null);
            _done(value);
        }

        /// <summary>The one board, drawn by the host rather than here (remediation.md 12, item 5).</summary>
        private BoardKeys BuildBoard()
        {
            var keys = Board(
                character =>
                {
                    _typed += character;
                    WriteBack();
                },
                () =>
                {
                    _typed = _typed.Length > 0 ? _typed[..^1] : _typed;
                    WriteBack();
                },
                () =>
                {
                    _typed = string.Empty;
                    WriteBack();
                });

            _board.Children.Add(keys.Control);

            return keys;
        }
    }

    /// <summary>One value picked from every value there is (#282).</summary>
    private sealed class PickPage : UserControl
    {
        private readonly PanelPrompts _host;
        private readonly EntryRequest _request;
        private readonly Action<string> _done;

        private readonly ComboBox _pick;
        private readonly TextBlock _state;

        /// <summary>Whether the selection is being set rather than made.</summary>
        private bool _settling;

        public PickPage(PanelPrompts host, EntryRequest request, Action<string> done)
        {
            _host = host;
            _request = request;
            _done = done;

            _pick = new ComboBox
            {
                ItemsSource = request.Suggestions,
                PlaceholderText = "Pick one, or say it",
                FontSize = TypeScale.Body,
                HorizontalAlignment = HorizontalAlignment.Left,

                // A floor, and a generous one.
                MinWidth = 300,
            };

            _settling = true;

            if (request.Initial is { Length: > 0 } initial
                && request.Suggestions!.FirstOrDefault(value =>
                    string.Equals(value, initial, StringComparison.OrdinalIgnoreCase)) is { } already)
            {
                _pick.SelectedItem = already;
            }

            _settling = false;

            _pick.SelectionChanged += (_, _) =>
            {
                if (!_settling && _pick.SelectedItem is string picked)
                {
                    Commit(picked);
                }
            };

            _state = new TextBlock
            {
                Text = string.Empty,
                FontSize = TypeScale.Secondary,
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(14, 0, 0, 0),
            };

            _state.Bind(
                TextBlock.ForegroundProperty,
                App.Current!.GetResourceObservable(ThemeManager.TextMutedKey));

            var row = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Top,
                Children = { _pick, _state },
            };

            Content = Frame(
                request.Title,
                request.Context,
                row,
                () => _host.Dismiss(request.Key, ChoiceSurface.Page));

            _host.Attend(OnHeard);

            // Focused once it is in a tree, and posted for the reason the searchable chooser's box already
            // is: focusing during the attach event runs before the page under it has finished building, and
            // whatever settles focus last wins.
            AttachedToVisualTree += (_, _) =>
                Dispatcher.UIThread.Post(() => _pick.Focus(), DispatcherPriority.Input);
        }

        private void OnHeard(Heard heard)
        {
            // A partial is shown and never committed, which is the visible listening state doing its job —
            // the words appearing as they are said is how a Commander knows the microphone is on them.
            if (!heard.Final)
            {
                _state.Text = heard.Text;

                return;
            }

            if (TextEntryLoop.Judge(heard, _request.Validate, out var verdict) is { } fallback)
            {
                // Said, and then the list is the way out.
                _state.Text = TextEntryLoop.Explain(fallback, verdict?.Complaint);
                return;
            }

            Commit(heard.Text.Trim());
        }

        /// <summary>Takes the value, once.</summary>
        private void Commit(string value)
        {
            if (_request.Validate?.Invoke(value) is { Accepted: false } refused)
            {
                _state.Text = TextEntryLoop.Explain(EntryFallback.DidNotResolve, refused.Complaint);
                return;
            }

            _host.Attend(null);
            _done(value);
        }
    }
}
