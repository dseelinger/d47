using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using D47.App.Theming;
using D47.Core.Interface;

namespace D47.App.Panel;

/// <summary>
/// The two questions the panel can put to the Commander — pick one of these, and say or type
/// this — and the two places either can be drawn (list.md Phase 25, "Choosing takes the panel"
/// and "Say it, or type it").
/// <para>
/// One host for both, because they are one mechanism seen twice: a level that takes the panel
/// and behaves as a modal, with identical affordances so neither reads as a special case. The
/// difference between them is what fills the middle.
/// </para>
/// <para>
/// It draws with ordinary controls in the ordinary tree, which is the whole argument for taking
/// the panel rather than opening a popup: <c>OffscreenSurface</c>'s geometric hit test, its
/// activation and its draggable scrollbar all work on these without knowing they are special, so
/// the headset and the desktop window get the same chooser from one definition.
/// </para>
/// </summary>
public sealed class PanelPrompts
{
    private readonly PanelNavigator _nav;
    private readonly Avalonia.Controls.Panel _layer;

    /// <summary>
    /// The keys, in rows, as they are drawn. <b>The one table</b>: <c>OffscreenSurface</c> draws
    /// this same array rather than a copy of it, because two boards that are meant to be the same
    /// board and are declared twice are two boards that eventually are not.
    /// <para>
    /// <b>QWERTY</b> (remediation.md 10, item 18). It was a staggered alphabetic board, declared
    /// twice, with the same argument written out in both places: what is typed here is a system
    /// name or a commander name, hunted one key at a time with a ray, and alphabetical order is
    /// faster to hunt in than muscle memory that needs ten fingers on a desk.
    /// </para>
    /// <para>
    /// That argument is wrong about where the hunting starts. A Commander does not arrive at this
    /// board without a keyboard in their head — they have one, it is QWERTY, and it tells them
    /// roughly where a letter is before they start looking. An alphabetical board throws that away
    /// and makes every letter a fresh search of thirty boxes. The Commander asked for QWERTY, and
    /// the ordering that is already in their hands beats the one that is merely sorted.
    /// </para>
    /// </summary>
    public static readonly string[] Keys =
        ["1234567890", "qwertyuiop", "asdfghjkl", "zxcvbnm-_.", " "];

    /// <summary>
    /// True in every listening mode, and what is shown until a host says something better. The
    /// view model starts on this same string, so the two cannot drift.
    /// </summary>
    internal const string WaitingFallback = "Say it, or type it instead.";

    /// <summary>
    /// What a prompt waiting on speech says about itself — set by the surface from
    /// <see cref="PanelViewModel.ListeningPrompt"/>, which the host fills from the listening
    /// settings (remediation.md 10, item 12).
    /// </summary>
    public Func<string?>? Waiting { get; set; }

    /// <summary>What each open prompt is, by the crumb key it was pushed as.</summary>
    private readonly Dictionary<string, Func<Control>> _pages = [];

    /// <summary>The prompt currently taking speech, if any. At most one — a modal is a modal.</summary>
    private Action<Heard>? _listening;

    public PanelPrompts(PanelNavigator nav, Avalonia.Controls.Panel layer)
    {
        _nav = nav;
        _layer = layer;
    }

    /// <summary>
    /// Puts a question to the Commander and calls back with what they picked. Nothing is called
    /// back when they dismiss it: a chooser that commits something on the way out is a chooser
    /// with no way to change your mind.
    /// </summary>
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

        Open(request.Key, request.Word, request.Surface == ChoiceSurface.Page, Build);
    }

    /// <summary>
    /// Asks the Commander for a value, by voice or by keyboard, and calls back once with it.
    /// <para>
    /// <b>Once, on Done.</b> A settings row commits what it is given, and committing a system
    /// name letter by letter would be twelve writes and eleven wrong values on the way to the
    /// right one — which is the rule <c>69fbd3e</c> set and which a spoken value fits better than
    /// typing does, being naturally atomic.
    /// </para>
    /// </summary>
    public void Enter(EntryRequest request, Action<string> done)
    {
        Open(request.Key, request.Word, page: true, () => new EntryPage(this, request, value =>
        {
            Dismiss(request.Key, ChoiceSurface.Page);
            done(value);
        }));
    }

    /// <summary>
    /// Hands the panel what was heard, for whichever prompt is listening. Called by the host from
    /// the speech path; ignored when nothing is asking, which is almost always.
    /// </summary>
    public void Hear(Heard heard) => _listening?.Invoke(heard);

    /// <summary>Whether a prompt is waiting on speech, so the host knows to route it here.</summary>
    public bool IsListening => _listening is not null;

    /// <summary>
    /// The control for a level that takes the panel, or null for a crumb that is not one of these.
    /// The panel asks this for its modal level.
    /// </summary>
    public Control? Build(NavCrumb crumb) =>
        _pages.TryGetValue(crumb.Key, out var build) ? build() : null;

    private void Open(string key, string word, bool page, Func<Control> build)
    {
        if (page)
        {
            _pages[key] = build;
            _nav.Take(new NavCrumb(key, word));
            return;
        }

        // The in-tree layer. Not a popup: a popup has no top level to hang from on a window that
        // is never shown, and forcing one into the window's own overlay layer is the same crash.
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

        // Only if it is still the level showing. A chooser that opened a chooser is dismissed
        // from the top down, and popping blindly would take the parent with it.
        if (_nav.Trail.Count > 0 && _nav.Trail[^1].Key == key)
        {
            _nav.Back();
        }
    }

    /// <summary>
    /// Points what is heard next at one prompt, or at none.
    /// <para>
    /// It does not open the microphone, and deliberately does not try to. The Commander's
    /// listening mode is theirs — push-to-talk or continuous — and a prompt that armed the
    /// microphone for them would be a prompt that overrode a setting they chose, on a surface
    /// whose whole indicator row exists to say when the microphone is open. What this does is
    /// decide where the words go when they arrive.
    /// </para>
    /// </summary>
    private void Attend(Action<Heard>? heard) => _listening = heard;

    /// <summary>
    /// The shape both prompts share: a header saying what this is for, the body, and one way out.
    /// <para>
    /// The header is the part that earns taking the panel. A list floating over a page has the
    /// page behind it for context; a list that replaced the page has none unless it brings the
    /// context with it — the slot, its size, and what is fitted now.
    /// </para>
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

        // The one way out, and it is the same affordance every level has: back. Identical rather
        // than special, so a modal never reads as a different kind of thing to be in.
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
    /// <para>
    /// Marked rather than pre-selected on purpose: a chooser that opens with a row highlighted as
    /// the selection invites a press that changes nothing, and the Commander cannot tell whether
    /// it took.
    /// </para>
    /// </summary>
    private static Control Rows(ChoiceRequest request, Action<ChoiceOption> chosen)
    {
        var rows = new StackPanel { Spacing = 3 };

        foreach (var option in request.Options)
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
                // Said rather than only coloured. A fact carried only by a hue is a fact some of
                // the Commanders reading it do not have. The word is the caller's, because this
                // chooser is general and they are not.
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

                // Tall enough to be pressed by a ray at a metre. A row a Commander has to aim at
                // is a row they press twice, and the second press is on the one below it.
                MinHeight = 34,
                Padding = new Thickness(12, 6),
            };

            var picked = option;
            row.Click += (_, _) => chosen(picked);

            rows.Children.Add(row);
        }

        return new ScrollViewer
        {
            Content = rows,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        };
    }

    /// <summary>
    /// One value being said or typed, with the correction loop around it (list.md Phase 25, "Say
    /// it, or type it").
    /// <para>
    /// <b>Voice first with the drawn keyboard as the fallback</b>, and which one opens is
    /// declared per call site. A dismissed keyboard shows a <b>visible listening state with the
    /// partial transcription in it</b>, because a Commander talking at a blank page has no way to
    /// tell a microphone that is off from one that is not hearing them.
    /// </para>
    /// <para>
    /// <b>The keyboard returns by itself on the three failures d47 can detect</b> — nothing heard,
    /// a transcription below confidence, or a value that fails to resolve — and says which. Only
    /// <em>confident, valid and still wrong</em> needs the human, and that is what the readback
    /// is for.
    /// </para>
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

        /// <summary>
        /// The visible listening state, and it has to be visible or the Commander is talking at
        /// a blank page (list.md Phase 25).
        /// <para>
        /// Read per show rather than held as a constant. It used to be the one sentence "Say it —
        /// I am listening.", with a comment arguing it was worded for both modes; the Commander
        /// reported it as untrue under push-to-talk, and they are right — the microphone is shut
        /// until the key is held (remediation.md 10, item 12). The wording now comes from the
        /// host, which is the only thing here that knows the mode, and is chosen in Core where a
        /// test reads what a Commander reads.
        /// </para>
        /// </summary>
        private string Waiting => _host.Waiting?.Invoke() is { Length: > 0 } said
            ? said
            : WaitingFallback;

        private string _typed;
        private bool _keyboard;

        /// <summary>
        /// Whether the box is being filled from <see cref="_typed"/> rather than the other way
        /// round. A drawn key and a heard word both write the box, and the box now writes back;
        /// without this the two chase each other and a partial transcription lands in the value
        /// that only Done is allowed to set.
        /// </summary>
        private bool _writingBack;

        public EntryPage(PanelPrompts host, EntryRequest request, Action<string> done)
        {
            _host = host;
            _request = request;
            _done = done;
            _typed = request.Initial;

            // Writable, which it was not (remediation.md 10, item 11). It was read-only because
            // the drawn board and the microphone were the only two ways in, and on the surface
            // that has neither of those problems -- a window, with a keyboard in front of it and a
            // clipboard behind it -- that made the obvious thing to do the one thing that did not
            // work. Ctrl+V comes with the control; nothing had to be invented for it.
            //
            // It costs the headset nothing. Nothing there sends a keystroke at this box, so it
            // behaves exactly as it did; what changes is that the surface which can type, can.
            _shown = new TextBox
            {
                Text = _typed,
                FontSize = TypeScale.Heading,
                Padding = new Thickness(12, 10),
                Margin = new Thickness(0, 0, 0, 10),
            };

            // The box is the value when the Commander types into it. Without this, what they typed
            // is shown and then thrown away on Done, which reads as the keyboard being decorative.
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

            BuildBoard();

            var accept = new Button
            {
                Content = "Done",
                Padding = new Thickness(18, 7),
            };

            accept.Click += (_, _) => Commit(_typed);

            var actions = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 12, 0, 0),
                Children = { _swap, accept },
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

            Content = Frame(
                request.Title,
                request.Context,
                body,
                () =>
                {
                    _host.Dismiss(request.Key, ChoiceSurface.Page);
                });

            Show(request.Surface == EntrySurface.Keyboard);

            // Focused once it is actually in a tree, so a Commander at a desk can simply type or
            // paste (remediation.md 10, item 11). Harmless where there is no keyboard: nothing in
            // the headset sends a keystroke, and the panel already swallows the push-to-talk key
            // before any control sees it, which is what stops holding it filling the box with
            // brackets.
            AttachedToVisualTree += (_, _) => _shown.Focus();
        }

        /// <summary>Swaps between listening and typing, and says which one is on.</summary>
        private void Show(bool keyboard)
        {
            _keyboard = keyboard;

            _board.IsVisible = keyboard;
            _swap.Content = keyboard ? "Say it instead" : "Type it instead";

            if (keyboard)
            {
                _host.Attend(null);
                return;
            }

            _state.Text = Waiting;
            _host.Attend(OnHeard);
        }

        /// <summary>
        /// Puts the value into the box without the box putting it back. The drawn board edits
        /// <see cref="_typed"/> and shows the result; the keyboard edits the box and it is read
        /// back. Both are correct, and they must not chase each other.
        /// </summary>
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
            // A partial is shown and never committed. This is the visible listening state doing
            // its job: the words appearing as they are said is the whole of how a Commander knows
            // the microphone is on them.
            if (!heard.Final)
            {
                _state.Text = string.IsNullOrWhiteSpace(heard.Text)
                    ? Waiting
                    : $"{Waiting} {heard.Text}";

                return;
            }

            if (TextEntryLoop.Judge(heard, _request.Validate, out var verdict) is { } fallback)
            {
                _state.Text = TextEntryLoop.Explain(fallback, verdict?.Complaint);
                Show(keyboard: true);
                return;
            }

            Commit(heard.Text.Trim());
        }

        /// <summary>
        /// Takes the value, once, having asked whatever the caller wanted asking. A typed value
        /// goes through the same validation a spoken one does: the three detectable failures are
        /// about the value rather than about how it arrived.
        /// </summary>
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

        private void BuildBoard()
        {
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

                    var pressed = new Button
                    {
                        Content = character == ' ' ? "space" : character.ToString(),
                        Width = character == ' ' ? 220 : 46,
                        Height = 40,
                        HorizontalContentAlignment = HorizontalAlignment.Center,
                        VerticalContentAlignment = VerticalAlignment.Center,
                    };

                    pressed.Click += (_, _) =>
                    {
                        _typed += character;
                        WriteBack();
                    };

                    line.Children.Add(pressed);
                }

                _board.Children.Add(line);
            }

            var back = new Button { Content = "delete", Height = 40, Padding = new Thickness(16, 0) };
            back.Click += (_, _) =>
            {
                _typed = _typed.Length > 0 ? _typed[..^1] : _typed;
                WriteBack();
            };

            var clear = new Button { Content = "clear", Height = 40, Padding = new Thickness(16, 0) };
            clear.Click += (_, _) =>
            {
                _typed = string.Empty;
                WriteBack();
            };

            _board.Children.Add(new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 6,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 4, 0, 0),
                Children = { back, clear },
            });
        }
    }
}
