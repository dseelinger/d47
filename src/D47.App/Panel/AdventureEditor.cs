using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Threading;
using D47.App.Theming;
using D47.Core.Adventures;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Knowledge;

namespace D47.App.Panel;

/// <summary>
/// Writing an adventure by hand (list.md Phase 47, "Written, generated or imported").
/// <para>
/// <b>Every field is a closed vocabulary except the prose</b> — <see cref="Controls.MacroWindow"/>'s
/// guarantee, arrived at from the panel side: the kind of a beat is a chooser of exactly five, a
/// place is <em>Here</em> (read from the game state, with its ids) or a typed name resolved through
/// the galaxy search, and a rank is a career chooser and a number. So the form cannot compose what
/// the validator would refuse, and it writes the same <c>adventures.json</c> a text editor writes.
/// </para>
/// <para>
/// A level in the tab rather than a window, because choosers and entries take the panel since
/// Phase 25 — and every entry here is said or typed, so the whole form is driveable by voice
/// through the panel's own route and never through the model.
/// </para>
/// <para>
/// Begin is shut while any beat is unresolved or empty, <b>with the reason printed under it</b>,
/// never a silently grey button. A name that resolves to nothing stays on its row marked so — being
/// offline must not stop the Commander writing.
/// </para>
/// </summary>
public sealed class AdventureEditor : UserControl
{
    private const double TouchTarget = 30;

    private readonly AdventureSurface _surface;
    private readonly PanelNavigator _nav;
    private readonly PanelPrompts _prompts;
    private readonly bool _isNew;

    private Adventure _draft;

    private readonly StackPanel _page = new() { Spacing = 8, Margin = new Thickness(14) };

    public AdventureEditor(AdventureSurface surface, PanelNavigator nav, PanelPrompts prompts, Adventure? existing)
    {
        _surface = surface;
        _nav = nav;
        _prompts = prompts;
        _isNew = existing is null;

        _draft = existing ?? new Adventure
        {
            Key = string.Empty,
            Name = string.Empty,
            Source = AdventureSource.Commander,
            Written = surface.Now(),
        };

        Content = new ScrollViewer
        {
            Content = _page,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        };

        Rebuild();
    }

    private void Rebuild()
    {
        _page.Children.Clear();

        _page.Children.Add(AdventuresPage.Title(_isNew ? "Write an adventure" : $"Edit {_draft.Name}", TypeScale.Heading));

        _page.Children.Add(Field("Name", string.IsNullOrWhiteSpace(_draft.Name) ? "unnamed" : _draft.Name, () => Enter(
            "adventure.name", "Name", "What is it called?", null, _draft.Name, EntrySurface.Keyboard,
            value => _draft = _draft with { Name = value.Trim(), Key = _isNew ? AdventureValidation.Key(value) : _draft.Key },
            value => string.IsNullOrWhiteSpace(value) ? EntryVerdict.No("An adventure needs a name.") : EntryVerdict.Ok)));

        _page.Children.Add(Field("Opening", _draft.Opening ?? "none — said when it begins", () => Enter(
            "adventure.opening", "Opening", "What does the ship's AI say when it begins?",
            "The beat before the first beat. Show the place and what is in it; never tell yourself what to feel.",
            _draft.Opening ?? string.Empty, EntrySurface.Voice,
            value => _draft = _draft with { Opening = string.IsNullOrWhiteSpace(value) ? null : value.Trim() })));

        _page.Children.Add(AdventuresPage.Text("The spine — optional, in the craft's order", TypeScale.Small, ThemeManager.TextMutedKey));

        var spine = _draft.Spine ?? new AdventureSpine();

        _page.Children.Add(Field("What is this about", spine.Premise ?? "—", () => Enter(
            "adventure.premise", "Premise", "What is this about?", null, spine.Premise ?? string.Empty, EntrySurface.Voice,
            value => _draft = _draft with { Spine = (_draft.Spine ?? new AdventureSpine()) with { Premise = Blank(value) } })));

        _page.Children.Add(Field("What do you want in it", spine.Want ?? "—", () => Enter(
            "adventure.want", "Want", "What do you want in it?", null, spine.Want ?? string.Empty, EntrySurface.Voice,
            value => _draft = _draft with { Spine = (_draft.Spine ?? new AdventureSpine()) with { Want = Blank(value) } })));

        _page.Children.Add(Field("What is really at stake", spine.Stake ?? "—", () => Enter(
            "adventure.stake", "Stake", "What is really at stake?", null, spine.Stake ?? string.Empty, EntrySurface.Voice,
            value => _draft = _draft with { Spine = (_draft.Spine ?? new AdventureSpine()) with { Stake = Blank(value) } })));

        _page.Children.Add(Field("Where does it turn", spine.Turn ?? "—", () => Enter(
            "adventure.turn", "Turn", "Where does it turn?", null, spine.Turn ?? string.Empty, EntrySurface.Voice,
            value => _draft = _draft with { Spine = (_draft.Spine ?? new AdventureSpine()) with { Turn = Blank(value) } })));

        _page.Children.Add(Field("What does the end mean", spine.Ending ?? "—", () => Enter(
            "adventure.ending", "Ending", "What does the end mean?", null, spine.Ending ?? string.Empty, EntrySurface.Voice,
            value => _draft = _draft with { Spine = (_draft.Spine ?? new AdventureSpine()) with { Ending = Blank(value) } })));

        _page.Children.Add(AdventuresPage.Text("Beats", TypeScale.Small, ThemeManager.TextMutedKey));

        for (var index = 0; index < _draft.Beats.Count; index++)
        {
            _page.Children.Add(BeatRow(index));
        }

        var add = new Button { Content = "Add a beat", Padding = new Thickness(12, 4), MinHeight = TouchTarget };
        add.Click += (_, _) => AddBeat();
        _page.Children.Add(add);

        // The reasons, printed, never a silently grey button.
        var problems = AdventureValidation.Problems(_draft);
        var notReady = problems.Count == 0 ? AdventureValidation.NotReady(_draft) : [];

        if (problems.Count > 0 || notReady.Count > 0)
        {
            var reasons = AdventuresPage.Text(string.Join("\n", problems.Concat(notReady)), TypeScale.Secondary, ThemeManager.DangerKey);
            reasons.Margin = new Thickness(0, 8, 0, 0);
            _page.Children.Add(reasons);
        }

        var bar = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(0, 8, 0, 0) };

        var save = new Button { Content = "Save", Padding = new Thickness(12, 4), MinHeight = TouchTarget, IsEnabled = problems.Count == 0 };
        save.Click += (_, _) => Save(begin: false);

        var begin = new Button
        {
            Content = _draft.IsAbandoned ? "Save and begin again" : "Save and begin",
            Padding = new Thickness(12, 4),
            MinHeight = TouchTarget,
            IsEnabled = problems.Count == 0 && notReady.Count == 0,
        };

        begin.Click += (_, _) => Save(begin: true);

        bar.Children.Add(save);
        bar.Children.Add(begin);
        _page.Children.Add(bar);
    }

    private static string? Blank(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private Control BeatRow(int index)
    {
        var beat = _draft.Beats[index];
        var row = new StackPanel { Spacing = 4, Margin = new Thickness(0, 2, 0, 6) };

        var heading = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        heading.Children.Add(AdventuresPage.Title($"{index + 1}. {beat.Title}"));

        if (!beat.Trigger.IsResolved)
        {
            heading.Children.Add(AdventuresPage.Text("not yet a real place", TypeScale.Small, ThemeManager.DangerKey));
        }

        row.Children.Add(heading);
        row.Children.Add(AdventuresPage.Muted($"When you {beat.Trigger.Describe()}{(string.IsNullOrWhiteSpace(beat.Function) ? string.Empty : $" — {beat.Function}")}"));
        row.Children.Add(AdventuresPage.Text($"\"{beat.Line}\"", TypeScale.Body));

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };

        buttons.Children.Add(Small("Title", () => Enter(
            "adventure.beat.title", "Title", "The chapter's name", "A name, never a number.", beat.Title, EntrySurface.Keyboard,
            value => ReplaceBeat(index, beat with { Title = value.Trim() }),
            value => string.IsNullOrWhiteSpace(value) ? EntryVerdict.No("A beat needs a title.") : EntryVerdict.Ok)));

        buttons.Children.Add(Small("Line", () => Enter(
            "adventure.beat.line", "Line", "What the ship's AI says here",
            "Show the place and what is in it; never tell yourself what to feel.", beat.Line, EntrySurface.Voice,
            value => ReplaceBeat(index, beat with { Line = value.Trim() }),
            value => string.IsNullOrWhiteSpace(value) ? EntryVerdict.No("A beat needs a line.") : EntryVerdict.Ok)));

        buttons.Children.Add(Small("Where", () => ChooseWhere(index, beat)));

        if (index > 0)
        {
            buttons.Children.Add(Small("Up", () => Move(index, -1)));
        }

        if (index < _draft.Beats.Count - 1)
        {
            buttons.Children.Add(Small("Down", () => Move(index, 1)));
        }

        buttons.Children.Add(Small("Remove", () =>
        {
            _draft = _draft with { Beats = [.. _draft.Beats.Where((_, i) => i != index)] };
            Rebuild();
        }));

        row.Children.Add(buttons);
        return row;
    }

    private void ReplaceBeat(int index, AdventureBeat beat)
    {
        _draft = _draft with { Beats = [.. _draft.Beats.Select((other, i) => i == index ? beat : other)] };
        Rebuild();
    }

    private void Move(int index, int by)
    {
        var beats = _draft.Beats.ToList();
        (beats[index], beats[index + by]) = (beats[index + by], beats[index]);
        _draft = _draft with { Beats = beats };
        Rebuild();
    }

    /// <summary>Add a beat: what happens, where, and the line — three prompts in turn.</summary>
    private void AddBeat()
    {
        if (_draft.Beats.Count >= AdventureLimits.MaxBeats)
        {
            _surface.Say($"An adventure has at most {AdventureLimits.MaxBeats} beats.");
            return;
        }

        _prompts.Choose(
            new ChoiceRequest(
                "adventure.kind",
                "What happens",
                "What does this beat wait for?",
                "The five things a beat can wait for. Nothing else exists.",
                [
                    new ChoiceOption("arrive", "Arrive at a system"),
                    new ChoiceOption("dock", "Dock at a station"),
                    new ChoiceOption("land", "Land on a body"),
                    new ChoiceOption("scan", "Scan a body"),
                    new ChoiceOption("rank", "Reach a rank"),
                ],
                "arrive",
                ChoiceSurface.Layer),
            option =>
            {
                if (!AdventureValidation.TryKind(option.Key, out var kind))
                {
                    return;
                }

                var index = _draft.Beats.Count;
                var placeholder = new AdventureBeat
                {
                    Title = $"Beat {(index + 1).ToString(CultureInfo.InvariantCulture)}",
                    Trigger = new AdventureTrigger { Kind = kind },
                    Line = string.Empty,
                };

                _draft = _draft with { Beats = [.. _draft.Beats, placeholder] };
                Rebuild();
                ChooseWhere(index, placeholder);
            });
    }

    /// <summary>Where: Here, with its ids, or a typed name resolved through the galaxy search; or a rank.</summary>
    private void ChooseWhere(int index, AdventureBeat beat)
    {
        if (beat.Trigger.Kind == TriggerKind.Rank)
        {
            ChooseRank(index, beat);
            return;
        }

        var here = Here(beat.Trigger.Kind);

        var options = new List<ChoiceOption>();

        if (here is not null)
        {
            options.Add(new ChoiceOption("here", "Here", here.Describe()));
        }

        options.Add(new ChoiceOption("name", "Type a name", "Checked against the galaxy search when it is on."));

        _prompts.Choose(
            new ChoiceRequest(
                "adventure.where",
                "Where",
                "Where does it happen?",
                here is null ? "Nothing to read from the game right now; name the place." : null,
                options,
                here is null ? "name" : "here",
                ChoiceSurface.Layer),
            option =>
            {
                if (option.Key == "here" && here is not null)
                {
                    ReplaceBeat(index, beat with { Trigger = here });
                    AskForLine(index);
                    return;
                }

                TypeAName(index, beat);
            });
    }

    private void TypeAName(int index, AdventureBeat beat)
    {
        var kind = beat.Trigger.Kind;
        var what = kind switch
        {
            TriggerKind.Dock => "The system, then the station — \"Shinrarta Dezhra, Jameson Memorial\".",
            TriggerKind.Land or TriggerKind.Scan => "The system, then the body — \"Shinrarta Dezhra, A 1\".",
            _ => "The system's name.",
        };

        Enter("adventure.place", "Place", "Name the place", what, Current(beat.Trigger), EntrySurface.Voice, value =>
        {
            var parts = value.Split(',', 2, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            var system = parts.Length > 0 ? parts[0] : null;
            var second = parts.Length > 1 ? parts[1] : null;

            // Named now, resolved when the galaxy search answers; until then the row says so.
            var named = beat with
            {
                Trigger = new AdventureTrigger
                {
                    Kind = kind,
                    System = system,
                    Station = kind == TriggerKind.Dock ? second : null,
                    Body = kind is TriggerKind.Land or TriggerKind.Scan ? second : null,
                },
            };

            ReplaceBeat(index, named);

            if (_surface.GalaxySearchOn())
            {
                Resolve(index, named);
            }
            else
            {
                _surface.Say("Galaxy search is off, so I cannot check that place yet. It is saved by name.");
            }

            AskForLine(index);
        });
    }

    private void Resolve(int index, AdventureBeat named)
    {
        var resolver = _surface.Resolver();

        if (resolver is null)
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            var resolution = await resolver.ResolveAsync(
                named.Trigger.Kind,
                named.Trigger.System,
                named.Trigger.Station,
                named.Trigger.Body,
                $"Beat {(index + 1).ToString(CultureInfo.InvariantCulture)} ({named.Title})",
                needsLargePad: false,
                CancellationToken.None).ConfigureAwait(false);

            Dispatcher.UIThread.Post(() =>
            {
                if (index >= _draft.Beats.Count || !ReferenceEquals(_draft.Beats[index].Trigger, named.Trigger))
                {
                    return;
                }

                if (resolution.Trigger is { } trigger)
                {
                    ReplaceBeat(index, _draft.Beats[index] with { Trigger = trigger });
                }
                else
                {
                    _surface.Say(resolution.Refusal ?? "I could not find that place.");
                }
            });
        });
    }

    private void ChooseRank(int index, AdventureBeat beat)
    {
        var ranks = _surface.State()?.Ranks;

        _prompts.Choose(
            new ChoiceRequest(
                "adventure.career",
                "Career",
                "Which career?",
                "Counted, never named: the beat fires on the promotion.",
                [.. Careers.Keys.Select(key => new ChoiceOption(key, Careers.Word(key), ranks?.For(key) is { } held ? $"now {held.Describe()}" : null))],
                beat.Trigger.Career ?? Careers.Keys[0],
                ChoiceSurface.Layer),
            option =>
            {
                var held = ranks?.For(option.Key)?.Rank ?? 0;

                Enter("adventure.rank", "Rank", $"Which {Careers.Word(option.Key)} rank?", $"1 to 8. You hold {held}.",
                    (beat.Trigger.Rank ?? Math.Min(held + 1, RankStanding.Elite)).ToString(CultureInfo.InvariantCulture),
                    EntrySurface.Keyboard,
                    value =>
                    {
                        var rank = int.Parse(value.Trim(), CultureInfo.InvariantCulture);
                        ReplaceBeat(index, beat with { Trigger = new AdventureTrigger { Kind = TriggerKind.Rank, Career = option.Key, Rank = rank } });
                        AskForLine(index);
                    },
                    value => int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var rank) && rank is >= 1 and <= RankStanding.Elite
                        ? EntryVerdict.Ok
                        : EntryVerdict.No("A rank is a number from 1 to 8."));
            });
    }

    private void AskForLine(int index)
    {
        if (index >= _draft.Beats.Count || !string.IsNullOrWhiteSpace(_draft.Beats[index].Line))
        {
            return;
        }

        var beat = _draft.Beats[index];

        Enter("adventure.beat.line", "Line", "What does the ship's AI say here?",
            "Show the place and what is in it; never tell yourself what to feel.", string.Empty, EntrySurface.Voice,
            value => ReplaceBeat(index, beat with { Line = value.Trim() }),
            value => string.IsNullOrWhiteSpace(value) ? EntryVerdict.No("A beat needs a line.") : EntryVerdict.Ok);
    }

    /// <summary>The place the Commander is at right now, with its ids — zero egress, zero typos.</summary>
    private AdventureTrigger? Here(TriggerKind kind)
    {
        var state = _surface.State();
        var location = state?.Location;

        if (location?.SystemAddress is not { } address || location.StarSystem is null)
        {
            return null;
        }

        return kind switch
        {
            TriggerKind.Arrive => new AdventureTrigger { Kind = kind, SystemAddress = address, System = location.StarSystem },
            TriggerKind.Dock when location.Docked && location.MarketId is { } market =>
                new AdventureTrigger { Kind = kind, MarketId = market, SystemAddress = address, System = location.StarSystem, Station = location.StationName },
            TriggerKind.Land or TriggerKind.Scan when location.BodyId is { } body && location.Body is not null =>
                new AdventureTrigger { Kind = kind, SystemAddress = address, BodyId = body, System = location.StarSystem, Body = location.Body },
            _ => null,
        };
    }

    private static string Current(AdventureTrigger trigger) => trigger.Kind switch
    {
        TriggerKind.Dock when trigger.System is not null => $"{trigger.System}, {trigger.Station}",
        TriggerKind.Land or TriggerKind.Scan when trigger.System is not null => $"{trigger.System}, {trigger.Body}",
        _ => trigger.System ?? string.Empty,
    };

    private void Save(bool begin)
    {
        if (string.IsNullOrWhiteSpace(_draft.Key))
        {
            _draft = _draft with { Key = AdventureValidation.Key(_draft.Name) };
        }

        var refusal = _surface.Book.Write(_surface.Commander(), _draft);

        if (refusal is not null)
        {
            _surface.Say(refusal);
            return;
        }

        if (begin)
        {
            if (_surface.Book.Begin(_surface.Commander(), _draft.Key, _surface.Now()) is { } why)
            {
                _surface.Say(why);
                return;
            }
        }

        _nav.GoTo([new NavCrumb(AdventuresPage.RootKey, "Adventures"), new NavCrumb(AdventuresPage.ReadPrefix + _draft.Key, _draft.Name)]);
    }

    // ---- prompts and drawing -----------------------------------------------------------------

    private void Enter(
        string key,
        string word,
        string title,
        string? context,
        string initial,
        EntrySurface surface,
        Action<string> done,
        Func<string, EntryVerdict>? validate = null)
    {
        _prompts.Enter(new EntryRequest(key, word, title, context, initial, surface, validate), value =>
        {
            done(value);
            Rebuild();
        });
    }

    private static Control Field(string label, string value, Action edit)
    {
        var row = new DockPanel();
        var button = new Button { Content = label, Padding = new Thickness(10, 4), MinHeight = TouchTarget, Width = 200, HorizontalContentAlignment = HorizontalAlignment.Left };
        button.Click += (_, _) => edit();
        DockPanel.SetDock(button, Dock.Left);
        row.Children.Add(button);

        var text = AdventuresPage.Text(value, TypeScale.Body);
        text.Margin = new Thickness(10, 0, 0, 0);
        text.VerticalAlignment = VerticalAlignment.Center;
        row.Children.Add(text);
        return row;
    }

    private static Button Small(string label, Action act)
    {
        var button = new Button { Content = label, Padding = new Thickness(8, 2), MinHeight = TouchTarget, FontSize = TypeScale.Secondary };
        button.Click += (_, _) => act();
        return button;
    }
}
