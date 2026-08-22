using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using D47.App.Theming;
using D47.Core.Adventures;
using D47.Core.Interface;

namespace D47.App.Panel;

/// <summary>
/// The Adventures tab (list.md Phase 47).
/// <para>
/// <b>The root is cards, and no card carries a number.</b> Each says the story's name, who wrote
/// it, and where it is — the current beat's title, <em>not begun</em>, <em>waiting for your yes</em>,
/// <em>finished</em>. A generated draft sits at the top with Accept, Decline and <em>Change
/// something</em>, exactly as the checklist draws a proposal; the stories set aside — abandoned or
/// finished — sit under a fold at the foot.
/// </para>
/// <para>
/// Three levels below the root, each a drill rather than a window: the reading level, which shows
/// the story so far and nothing ahead; the editor (<see cref="AdventureEditor"/>); and the ask
/// form, three choosers with defaults and a brief, so pressing Go on an untouched form is a
/// complete ask.
/// </para>
/// </summary>
public sealed class AdventuresPage : UserControl
{
    public const string RootKey = "adventures";

    public const string ReadPrefix = "adventure.read.";

    public const string EditPrefix = "adventure.edit.";

    public const string AskKey = "adventure.ask";

    /// <summary>The editor's key for a story that does not exist yet.</summary>
    public const string NewKey = "new";

    private const double TouchTarget = 30;

    private readonly AdventureSurface _surface;
    private readonly PanelNavigator _nav;
    private readonly PanelPrompts _prompts;

    private readonly StackPanel _list = new() { Spacing = 8 };
    private readonly TextBlock _problems = new()
    {
        TextWrapping = TextWrapping.Wrap,
        FontSize = TypeScale.Secondary,
        IsVisible = false,
    };

    private readonly Button _ask = new()
    {
        Content = "Ask for one",
        Padding = new Thickness(12, 4),
        MinHeight = TouchTarget,
    };

    /// <summary>The revision conversation per draft, for this session. Never written to the file.</summary>
    private readonly Dictionary<string, List<AdventureRemark>> _exchanges = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>What each draft was asked with, so a revision keeps the reach and the length.</summary>
    private readonly Dictionary<string, AdventureAsk> _asks = new(StringComparer.OrdinalIgnoreCase);

    private bool _showAside;

    public AdventuresPage(AdventureSurface surface, PanelNavigator nav, PanelPrompts prompts)
    {
        _surface = surface;
        _nav = nav;
        _prompts = prompts;

        Themed(_problems, TextBlock.ForegroundProperty, ThemeManager.DangerKey);

        var write = new Button { Content = "Write an adventure", Padding = new Thickness(12, 4), MinHeight = TouchTarget };
        write.Click += (_, _) => _nav.Drill(new NavCrumb(EditPrefix + NewKey, "Write"));

        _ask.Click += (_, _) => _nav.Drill(new NavCrumb(AskKey, "Ask"));

        var bar = new DockPanel { Margin = new Thickness(0, 0, 0, 10) };
        var right = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { _ask, write } };
        DockPanel.SetDock(right, Dock.Right);
        bar.Children.Add(right);
        bar.Children.Add(Muted("Stories you fly, told by the ship's AI. Progress comes from your own journal."));

        var root = new DockPanel { Margin = new Thickness(14) };
        DockPanel.SetDock(bar, Dock.Top);
        DockPanel.SetDock(_problems, Dock.Top);
        _problems.Margin = new Thickness(0, 0, 0, 10);

        root.Children.Add(bar);
        root.Children.Add(_problems);
        root.Children.Add(new ScrollViewer
        {
            Content = _list,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        });

        Content = root;
        Rebuild();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _surface.Book.Store.Changed += OnChanged;
        Rebuild();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _surface.Book.Store.Changed -= OnChanged;
    }

    /// <summary>The levels below the root, by crumb. The view asks for these; the root is the page itself.</summary>
    public Control? Build(NavCrumb crumb)
    {
        if (crumb.Key == AskKey)
        {
            return BuildAsk();
        }

        if (crumb.Key.StartsWith(ReadPrefix, StringComparison.Ordinal))
        {
            return BuildReading(crumb.Key[ReadPrefix.Length..]);
        }

        if (crumb.Key.StartsWith(EditPrefix, StringComparison.Ordinal))
        {
            var key = crumb.Key[EditPrefix.Length..];
            var existing = key == NewKey ? null : _surface.Book.Store.Find(_surface.Commander(), key);

            return new AdventureEditor(_surface, _nav, _prompts, existing);
        }

        return null;
    }

    private void OnChanged() => Dispatcher.UIThread.Post(Rebuild);

    // ---- the root ----------------------------------------------------------------------------

    private void Rebuild()
    {
        _list.Children.Clear();

        var problems = _surface.Book.Store.Problems;
        _problems.IsVisible = problems.Count > 0;
        _problems.Text = string.Join("\n", problems.Select(problem => $"{problem.Where}: {problem.Reason}"));

        _ask.IsEnabled = _surface.ModelAvailable() && _surface.GalaxySearchOn();

        var standings = _surface.Book.Standings(_surface.Commander());

        if (standings.Count == 0)
        {
            _list.Children.Add(Muted(
                "No adventures yet. Write one, or ask the ship's AI for one — it will propose a story and "
                + "wait for your yes."));

            if (!_ask.IsEnabled)
            {
                _list.Children.Add(Muted(AskShutBecause()));
            }

            return;
        }

        var drafts = standings.Where(standing => standing.Adventure.IsDraft).ToList();
        var live = standings.Where(standing => !standing.Adventure.IsDraft && standing.Adventure.IsActive && !standing.IsDone).ToList();
        var unbegun = standings.Where(standing => !standing.Adventure.IsDraft && !standing.Adventure.IsBegun).ToList();
        var aside = standings.Where(standing => standing.Adventure.IsAbandoned || standing.IsDone).ToList();

        foreach (var standing in drafts)
        {
            _list.Children.Add(DraftCard(standing));
        }

        foreach (var standing in live.Concat(unbegun))
        {
            _list.Children.Add(Card(standing));
        }

        if (aside.Count > 0)
        {
            var fold = new Button
            {
                Content = _showAside
                    ? "Hide set aside"
                    : $"Set aside ({aside.Count.ToString(CultureInfo.InvariantCulture)})",
                Padding = new Thickness(12, 4),
                MinHeight = TouchTarget,
                Margin = new Thickness(0, 8, 0, 0),
            };

            fold.Click += (_, _) =>
            {
                _showAside = !_showAside;
                Rebuild();
            };

            _list.Children.Add(fold);

            if (_showAside)
            {
                foreach (var standing in aside)
                {
                    _list.Children.Add(Card(standing));
                }
            }
        }
    }

    private string AskShutBecause() => !_surface.ModelAvailable()
        ? "Asking for one needs a language model, and none is configured."
        : "Asking for one needs galaxy search, so the story's places can be checked. It is off in Settings.";

    private Control Card(AdventureStanding standing)
    {
        var adventure = standing.Adventure;
        var card = CardShell();
        var body = (StackPanel)card.Child!;

        body.Children.Add(Title(adventure.Name));
        body.Children.Add(Muted($"{By(adventure)} — {standing.Place()}"));

        if (standing.CurrentBeat is { } current && adventure.IsActive)
        {
            body.Children.Add(Text($"Waiting to {current.Trigger.Describe()}.", TypeScale.Secondary));
        }

        card.PointerPressed += (_, _) => _nav.Drill(new NavCrumb(ReadPrefix + adventure.Key, adventure.Name));
        return card;
    }

    private Control DraftCard(AdventureStanding standing)
    {
        var adventure = standing.Adventure;
        var card = CardShell();
        var body = (StackPanel)card.Child!;

        body.Children.Add(Title(adventure.Name));
        body.Children.Add(Muted($"{By(adventure)} — waiting for your yes"));

        if (!string.IsNullOrWhiteSpace(adventure.Spine?.Premise))
        {
            body.Children.Add(Text(adventure.Spine.Premise, TypeScale.Body));
        }

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(0, 6, 0, 0) };

        var accept = new Button { Content = "Accept", Padding = new Thickness(12, 4), MinHeight = TouchTarget };
        accept.Click += (_, _) => Begin(adventure.Key);

        var change = new Button { Content = "Change something", Padding = new Thickness(12, 4), MinHeight = TouchTarget };
        change.Click += (_, _) => Revise(adventure);

        var decline = new Button { Content = "Decline", Padding = new Thickness(12, 4), MinHeight = TouchTarget };
        decline.Click += (_, _) => Remove(adventure, confirm: false);

        buttons.Children.Add(accept);
        buttons.Children.Add(change);

        if (adventure.Previous is not null)
        {
            var back = new Button { Content = "Put it back", Padding = new Thickness(12, 4), MinHeight = TouchTarget };
            back.Click += (_, _) =>
            {
                var refusal = _surface.Book.Write(_surface.Commander(), adventure.Previous with { Previous = null });

                if (refusal is not null)
                {
                    _surface.Say(refusal);
                }
            };

            buttons.Children.Add(back);
        }

        buttons.Children.Add(decline);

        var read = new Button { Content = "Read it", Padding = new Thickness(12, 4), MinHeight = TouchTarget };
        read.Click += (_, _) => _nav.Drill(new NavCrumb(ReadPrefix + adventure.Key, adventure.Name));
        buttons.Children.Add(read);

        body.Children.Add(buttons);
        return card;
    }

    // ---- the reading level -------------------------------------------------------------------

    private Control BuildReading(string key)
    {
        var page = new StackPanel { Spacing = 8, Margin = new Thickness(14) };

        void Fill()
        {
            page.Children.Clear();

            if (_surface.Book.Standing(_surface.Commander(), key) is not { } standing)
            {
                page.Children.Add(Muted("That adventure is no longer on file."));
                return;
            }

            var adventure = standing.Adventure;

            page.Children.Add(Title(adventure.Name, TypeScale.Heading));
            page.Children.Add(Muted($"{By(adventure)} — {standing.Place()}"));

            if (adventure.Spine is { } spine && !string.IsNullOrWhiteSpace(spine.Premise))
            {
                page.Children.Add(Text(spine.Premise, TypeScale.Body));
            }

            if (!string.IsNullOrWhiteSpace(adventure.Opening))
            {
                page.Children.Add(Labelled("Opening", adventure.Opening));
            }

            // The story so far, and nothing ahead — for a generated one that is the point, and the
            // editor shows the whole of an authored one.
            var shown = adventure.IsBegun ? standing.Fired.Count : 0;

            for (var index = 0; index < shown && index < adventure.Beats.Count; index++)
            {
                var beat = adventure.Beats[index];
                page.Children.Add(Labelled(
                    $"{beat.Title} — {standing.Fired[index].ToLocalTime():d MMM HH:mm}",
                    beat.Line));
            }

            if (adventure.IsActive && standing.CurrentBeat is { } current)
            {
                page.Children.Add(Labelled(current.Title, $"Waiting to {current.Trigger.Describe()}."));
            }

            if (standing.IsDone && adventure.Beats.Count > 0)
            {
                page.Children.Add(Muted("The end."));
            }

            page.Children.Add(ReadingBar(standing));
        }

        Fill();

        var scroller = new ScrollViewer
        {
            Content = page,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        };

        void Follow() => Dispatcher.UIThread.Post(Fill);

        scroller.AttachedToVisualTree += (_, _) =>
        {
            _surface.Book.Store.Changed += Follow;
            Fill();
        };

        scroller.DetachedFromVisualTree += (_, _) => _surface.Book.Store.Changed -= Follow;

        return scroller;
    }

    private Control ReadingBar(AdventureStanding standing)
    {
        var adventure = standing.Adventure;
        var bar = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(0, 10, 0, 0) };

        if (adventure.IsDraft)
        {
            bar.Children.Add(Action("Accept", () => Begin(adventure.Key)));
            bar.Children.Add(Action("Change something", () => Revise(adventure)));
            bar.Children.Add(Action("Decline", () => Remove(adventure, confirm: false)));
            return bar;
        }

        if (!adventure.IsBegun)
        {
            bar.Children.Add(Action("Begin", () => Begin(adventure.Key)));
            bar.Children.Add(Action("Edit", () => _nav.Drill(new NavCrumb(EditPrefix + adventure.Key, "Edit"))));
        }
        else if (adventure.IsAbandoned)
        {
            bar.Children.Add(Action("Begin again", () => Begin(adventure.Key)));
            bar.Children.Add(Action("Edit", () => _nav.Drill(new NavCrumb(EditPrefix + adventure.Key, "Edit"))));
        }
        else if (!standing.IsDone)
        {
            bar.Children.Add(Action("Abandon", () => Abandon(adventure)));
            bar.Children.Add(Action("Edit", () => _surface.Say(
                $"{adventure.Name} is under way. Abandon it first, change it, and begin again.")));
        }

        bar.Children.Add(Action("Remove", () => Remove(adventure, confirm: adventure.IsBegun)));
        return bar;
    }

    // ---- the ask form ------------------------------------------------------------------------

    private Control BuildAsk()
    {
        var page = new StackPanel { Spacing = 10, Margin = new Thickness(14) };
        var reach = AdventureReach.NearHere;
        var length = AdventureLength.Evening;
        var thisShipOnly = false;
        var brief = string.Empty;

        var state = _surface.State();
        var hasChoice = state is not null && (state.Fleet.Ships.Count > 0 || state.Carrier.Owned);

        page.Children.Add(Title("Ask for an adventure", TypeScale.Heading));
        page.Children.Add(Muted(
            "The ship's AI writes a story for you to fly and waits for your yes. Three choices with "
            + "defaults, and a brief if you want one — pressing Go on an untouched form is a complete ask."));

        var reachButton = new Button { Padding = new Thickness(12, 4), MinHeight = TouchTarget };
        var lengthButton = new Button { Padding = new Thickness(12, 4), MinHeight = TouchTarget };
        var usingButton = new Button { Padding = new Thickness(12, 4), MinHeight = TouchTarget, IsVisible = hasChoice };
        var briefButton = new Button { Padding = new Thickness(12, 4), MinHeight = TouchTarget };
        var status = Muted(string.Empty);
        var go = new Button { Content = "Go", Padding = new Thickness(14, 4), MinHeight = TouchTarget };

        void Label()
        {
            reachButton.Content = "Reach: " + reach switch
            {
                AdventureReach.NearHere => "near here",
                AdventureReach.Session => "a session's flying",
                _ => "anywhere",
            };

            lengthButton.Content = "Length: " + length switch
            {
                AdventureLength.Short => "short",
                AdventureLength.Long => "long",
                _ => "an evening",
            };

            usingButton.Content = "Using: " + (thisShipOnly ? "this ship only" : "anything I own");
            briefButton.Content = string.IsNullOrWhiteSpace(brief) ? "Brief: none" : $"Brief: \"{brief}\"";
        }

        reachButton.Click += (_, _) => _prompts.Choose(
            new ChoiceRequest(
                "adventure.reach",
                "Reach",
                "How far may the story go?",
                "Measured from where you are, by what you can move.",
                [
                    new ChoiceOption("near", "Near here"),
                    new ChoiceOption("session", "A session's flying"),
                    new ChoiceOption("anywhere", "Anywhere"),
                ],
                reach switch { AdventureReach.NearHere => "near", AdventureReach.Session => "session", _ => "anywhere" },
                ChoiceSurface.Layer),
            option =>
            {
                reach = option.Key switch { "session" => AdventureReach.Session, "anywhere" => AdventureReach.Anywhere, _ => AdventureReach.NearHere };
                Label();
            });

        lengthButton.Click += (_, _) => _prompts.Choose(
            new ChoiceRequest(
                "adventure.length",
                "Length",
                "How long a story?",
                "Short is three beats; an evening is five; long spends the whole sheet.",
                [
                    new ChoiceOption("short", "Short"),
                    new ChoiceOption("evening", "An evening"),
                    new ChoiceOption("long", "Long"),
                ],
                length switch { AdventureLength.Short => "short", AdventureLength.Long => "long", _ => "evening" },
                ChoiceSurface.Layer),
            option =>
            {
                length = option.Key switch { "short" => AdventureLength.Short, "long" => AdventureLength.Long, _ => AdventureLength.Evening };
                Label();
            });

        usingButton.Click += (_, _) => _prompts.Choose(
            new ChoiceRequest(
                "adventure.using",
                "Using",
                "Which ships may the story use?",
                "This ship only is a story that stays aboard; anything you own lets it send you to fetch another.",
                [
                    new ChoiceOption("any", "Anything I own"),
                    new ChoiceOption("this", "This ship only"),
                ],
                thisShipOnly ? "this" : "any",
                ChoiceSurface.Layer),
            option =>
            {
                thisShipOnly = option.Key == "this";
                Label();
            });

        briefButton.Click += (_, _) => _prompts.Enter(
            new EntryRequest(
                "adventure.brief",
                "Brief",
                "What should it be about?",
                "A theme, a mood, a place it must include. Empty is fine.",
                brief,
                EntrySurface.Voice),
            value =>
            {
                brief = value.Trim();
                Label();
            });

        go.Click += (_, _) =>
        {
            if (!_surface.ModelAvailable() || !_surface.GalaxySearchOn())
            {
                status.Text = AskShutBecause();
                return;
            }

            go.IsEnabled = false;
            status.Text = "Writing…";

            var ask = new AdventureAsk(reach, length, thisShipOnly, string.IsNullOrWhiteSpace(brief) ? null : brief);

            _ = Task.Run(async () =>
            {
                var outcome = await _surface.Generator.GenerateAsync(ask, _surface.Now(), CancellationToken.None).ConfigureAwait(false);

                Dispatcher.UIThread.Post(() =>
                {
                    go.IsEnabled = true;
                    Offer(outcome, ask, status);
                });
            });
        };

        Label();

        page.Children.Add(reachButton);
        page.Children.Add(lengthButton);
        page.Children.Add(usingButton);
        page.Children.Add(briefButton);
        page.Children.Add(go);
        page.Children.Add(status);

        if (!_surface.ModelAvailable() || !_surface.GalaxySearchOn())
        {
            status.Text = AskShutBecause();
            go.IsEnabled = false;

            var settings = new Button { Content = "Open settings", Padding = new Thickness(12, 4), MinHeight = TouchTarget };
            settings.Click += (_, _) => _surface.OpenSettings();
            page.Children.Add(settings);
        }

        return new ScrollViewer { Content = page, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
    }

    /// <summary>A draft arrives: stored as a draft, the core's reply spoken, the reading level opened.</summary>
    private void Offer(AdventureOutcome outcome, AdventureAsk ask, TextBlock status)
    {
        if (outcome.Draft is not { } draft)
        {
            status.Text = outcome.Refusal ?? "Nothing came back.";
            _surface.Say(outcome.Refusal ?? "I could not write that one.");
            return;
        }

        var key = UniqueKey(draft.Key, draft.Previous?.Key);
        var stored = draft with { Key = key };
        var refusal = _surface.Book.Write(_surface.Commander(), stored);

        if (refusal is not null)
        {
            status.Text = refusal;
            return;
        }

        _asks[key] = ask;
        status.Text = string.Join(" ", outcome.Notes);

        _surface.Say(outcome.Reply ?? $"{stored.Name}. It is yours to accept or send back.");
        _nav.GoTo([new NavCrumb(RootKey, "Adventures"), new NavCrumb(ReadPrefix + key, stored.Name)]);
    }

    private string UniqueKey(string wanted, string? keep)
    {
        var existing = _surface.Book.Store.For(_surface.Commander());
        var key = string.IsNullOrWhiteSpace(wanted) ? "adventure" : wanted;
        var candidate = key;
        var suffix = 2;

        while (existing.Any(other =>
                   string.Equals(other.Key, candidate, StringComparison.OrdinalIgnoreCase)
                   && !string.Equals(other.Key, keep, StringComparison.OrdinalIgnoreCase)))
        {
            candidate = $"{key}-{suffix++}";
        }

        return candidate;
    }

    // ---- the acts ----------------------------------------------------------------------------

    private void Begin(string key)
    {
        var refusal = _surface.Book.Begin(_surface.Commander(), key, _surface.Now());

        if (refusal is not null)
        {
            _surface.Say(refusal);
        }

        // The opening itself is said by the callout on the next tick, in the core's voice.
    }

    private void Abandon(Adventure adventure) => _prompts.Choose(
        new ChoiceRequest(
            "adventure.abandon",
            "Abandon",
            $"Abandon {adventure.Name}?",
            "The story stops here. It stays on file under Set aside, and Begin again starts it from the opening.",
            [new ChoiceOption("keep", "Keep going"), new ChoiceOption("abandon", "Abandon it")],
            "keep",
            ChoiceSurface.Layer),
        option =>
        {
            if (option.Key != "abandon")
            {
                return;
            }

            var refusal = _surface.Book.Abandon(_surface.Commander(), adventure.Key, _surface.Now());

            if (refusal is not null)
            {
                _surface.Say(refusal);
            }
        });

    private void Remove(Adventure adventure, bool confirm)
    {
        if (!confirm)
        {
            _surface.Book.Remove(_surface.Commander(), adventure.Key);
            _nav.GoTo([new NavCrumb(RootKey, "Adventures")]);
            return;
        }

        _prompts.Choose(
            new ChoiceRequest(
                "adventure.remove",
                "Remove",
                $"Remove {adventure.Name}?",
                "It comes off the file, with everything it reached. There is no way back from this one.",
                [new ChoiceOption("keep", "Keep it"), new ChoiceOption("remove", "Remove it")],
                "keep",
                ChoiceSurface.Layer),
            option =>
            {
                if (option.Key != "remove")
                {
                    return;
                }

                _surface.Book.Remove(_surface.Commander(), adventure.Key);
                _nav.GoTo([new NavCrumb(RootKey, "Adventures")]);
            });
    }

    /// <summary>
    /// Reasoning with the AI about a draft (list.md Phase 47, "Revision before acceptance"). Said
    /// or typed; the revised draft replaces the pending one with the previous kept for Put it back.
    /// </summary>
    private void Revise(Adventure draft) => _prompts.Enter(
        new EntryRequest(
            "adventure.revise",
            "Change",
            "What should change?",
            "Say it as you would to the ship's AI — \"not Colonia, something closer\", \"the stakes are too low\".",
            string.Empty,
            EntrySurface.Voice,
            value => string.IsNullOrWhiteSpace(value) ? EntryVerdict.No("Say what should change.") : EntryVerdict.Ok),
        remark =>
        {
            var ask = _asks.GetValueOrDefault(draft.Key) ?? new AdventureAsk();
            var exchange = _exchanges.GetValueOrDefault(draft.Key) ?? [];

            _surface.Say("Let me think about that.");

            _ = Task.Run(async () =>
            {
                var outcome = await _surface.Generator
                    .ReviseAsync(draft, ask, exchange, remark, _surface.Now(), CancellationToken.None)
                    .ConfigureAwait(false);

                Dispatcher.UIThread.Post(() =>
                {
                    if (outcome.Draft is not { } revised)
                    {
                        _surface.Say(outcome.Refusal ?? "I could not change it that way.");
                        return;
                    }

                    // The same key, so the card stays where it was; the old draft rides along.
                    var stored = revised with { Key = draft.Key, Previous = draft with { Previous = null } };
                    var refusal = _surface.Book.Write(_surface.Commander(), stored);

                    if (refusal is not null)
                    {
                        _surface.Say(refusal);
                        return;
                    }

                    _exchanges[draft.Key] = [.. exchange, new AdventureRemark(remark, outcome.Reply)];
                    _asks[draft.Key] = ask;
                    _surface.Say(outcome.Reply ?? "Changed.");
                });
            });
        });

    // ---- drawing -----------------------------------------------------------------------------

    private static string By(Adventure adventure) => adventure.Source == AdventureSource.Commander
        ? "yours"
        : adventure.WrittenBy is { } id && D47.Core.Persona.PersonaCatalog.Knows(id)
            ? $"written by {D47.Core.Persona.PersonaCatalog.Resolve(id).Name}"
            : "written by d47";

    private static Border CardShell()
    {
        var border = new Border
        {
            Padding = new Thickness(12, 8),
            CornerRadius = new CornerRadius(4),
            Child = new StackPanel { Spacing = 4 },
            Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand),
        };

        Themed(border, Border.BackgroundProperty, ThemeManager.SurfaceAltKey);
        Themed(border, Border.BorderBrushProperty, ThemeManager.BorderKey);
        border.BorderThickness = new Thickness(1);
        return border;
    }

    private static Control Labelled(string label, string text)
    {
        var stack = new StackPanel { Spacing = 2, Margin = new Thickness(0, 4, 0, 0) };
        stack.Children.Add(Text(label, TypeScale.Small, ThemeManager.TextMutedKey));
        stack.Children.Add(Text(text, TypeScale.Body));
        return stack;
    }

    private static Button Action(string label, System.Action act)
    {
        var button = new Button { Content = label, Padding = new Thickness(12, 4), MinHeight = TouchTarget };
        button.Click += (_, _) => act();
        return button;
    }

    internal static TextBlock Title(string text, double size = TypeScale.Subheading)
    {
        var block = new TextBlock
        {
            Text = text,
            FontSize = size,
            FontWeight = FontWeight.SemiBold,
            TextWrapping = TextWrapping.Wrap,
        };

        Themed(block, TextBlock.ForegroundProperty, ThemeManager.TextKey);
        return block;
    }

    internal static TextBlock Text(string text, double size, string key = ThemeManager.TextKey)
    {
        var block = new TextBlock { Text = text, FontSize = size, TextWrapping = TextWrapping.Wrap };
        Themed(block, TextBlock.ForegroundProperty, key);
        return block;
    }

    internal static TextBlock Muted(string text) => Text(text, TypeScale.Body, ThemeManager.TextMutedKey);

    internal static void Themed(AvaloniaObject target, AvaloniaProperty property, string key) =>
        target.Bind(property, Application.Current!.Resources.GetResourceObservable(key));
}
