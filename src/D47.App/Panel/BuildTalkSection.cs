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
using D47.Core.Checklists;
using D47.Core.Conversation;
using D47.Core.Knowledge;
using D47.Core.Ships;

namespace D47.App.Panel;

/// <summary>What a ship's page needs to draw the conversation about its plan.</summary>
public sealed record BuildTalkView(BuildTalk Talk, string BuildId, Func<string?> CommanderName);

/// <summary>
/// TALK THROUGH THIS BUILD on a ship's page: the exchange about its plan, each proposal with Accept and Reject per
/// slot, and the field a remark is typed into.
/// </summary>
public sealed class BuildTalkSection : UserControl
{
    public const string Hint = "D47 proposes changes here. Nothing reaches the plan until you accept it.";

    public const string Unchanged = "Your plan is unchanged.";

    private readonly BuildTalkView _view;
    private readonly StackPanel _exchange = new() { Spacing = 14 };
    private readonly Button _clear;
    private readonly TextBox _field;
    private readonly Button _send;

    public BuildTalkSection(BuildTalkView view)
    {
        _view = view;

        _clear = Tile("Clear", () => _view.Talk.Clear(_view.BuildId));
        _send = Tile("Send", Send);

        _field = new TextBox
        {
            PlaceholderText = "Ask about a slot, or say what this ship is for",
            MinHeight = TypeScale.MinimumTarget,
            VerticalContentAlignment = VerticalAlignment.Center,
        };

        AutomationProperties.SetName(_field, "Your remark");

        _field.KeyDown += (_, args) =>
        {
            if (args.Key == Key.Enter)
            {
                args.Handled = true;
                Send();
            }
        };

        var entry = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            ColumnSpacing = Gaps.Tile,
            Children = { _field, _send },
        };

        Grid.SetColumn(_send, 1);

        Content = new StackPanel
        {
            Spacing = 16,
            Margin = new Thickness(0, 6, 0, 16),
            Children =
            {
                Head(),
                _exchange,
                new StackPanel
                {
                    Spacing = 8,
                    Children =
                    {
                        Label("Your remark"),
                        entry,
                        LoadoutPages.Toned(Hint, ThemeManager.GreyKey, TypeScale.Small),
                    },
                },
            },
        };

        Draw();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _view.Talk.Changed += OnChanged;
        _view.Talk.Open = _view.BuildId;
        Draw();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _view.Talk.Changed -= OnChanged;

        if (_view.Talk.Open == _view.BuildId)
        {
            _view.Talk.Open = null;
        }
    }

    private void OnChanged() => Dispatcher.UIThread.Post(Draw);

    private void Send()
    {
        if (_field.Text is not { } text || string.IsNullOrWhiteSpace(text) || _view.Talk.IsWorking(_view.BuildId))
        {
            return;
        }

        _field.Text = string.Empty;
        _ = _view.Talk.AskAsync(_view.BuildId, text, InputSource.Typed, CancellationToken.None);
    }

    /// <summary>Redraws the exchange and the state of the controls from the conversation as it stands; the field keeps its text.</summary>
    public void Draw()
    {
        var rounds = _view.Talk.Exchange(_view.BuildId);
        var working = rounds.Any(round => round.IsWorking);

        _clear.IsVisible = rounds.Count > 0;
        _clear.IsEnabled = !working;
        _field.IsEnabled = !working;
        _send.IsEnabled = !working;

        _exchange.Children.Clear();
        _exchange.IsVisible = rounds.Count > 0;

        foreach (var round in rounds)
        {
            _exchange.Children.Add(Commander(round));
            _exchange.Children.Add(Reply(round));

            if (round.Advice is { Succeeded: true, Changes.Count: > 0 } advice)
            {
                _exchange.Children.Add(Proposal(round, advice));
            }
        }
    }

    private Control Head()
    {
        var title = new TextBlock();
        TitleText.Style(title, TypeScale.Section, TitleRank.Group);
        TitleText.Show(title, "Talk through this build");
        title.VerticalAlignment = VerticalAlignment.Center;

        var row = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            MinHeight = 38,
            Children = { title, _clear },
        };

        Grid.SetColumn(_clear, 1);

        return Ruled(row, ThemeManager.AKey);
    }

    // ---- the exchange -----------------------------------------------------------------------

    private Control Commander(TalkRound round)
    {
        var name = _view.CommanderName() is { Length: > 0 } commander ? $"CMDR {commander}" : "CMDR";
        var how = round.Source == InputSource.Spoken ? "by voice" : "typed";

        var body = new StackPanel
        {
            Spacing = 5,
            Children =
            {
                Who(name, ThemeManager.CyanKey, $"{how} · {Clock(round.At)}"),
                Prose(round.Remark, ThemeManager.WhiteKey),
            },
        };

        var message = Message(body, ThemeManager.CyanKey, commander: true);
        LoadoutPages.Themed(message, Border.BackgroundProperty, ThemeManager.CyanGroundKey);

        return message;
    }

    private Control Reply(TalkRound round)
    {
        var body = new StackPanel { Spacing = 5 };

        body.Children.Add(Who("D47", ThemeManager.AKey, round.Answered is { } answered ? Clock(answered) : "working"));

        if (round.Advice is not { } advice)
        {
            body.Children.Add(Prose("Checking the proposal against the hull and the blueprints.", ThemeManager.GreyKey));
            return Message(body, ThemeManager.AKey, commander: false);
        }

        if (!advice.Succeeded)
        {
            var notice = new Notice(NoticeLevel.Error, inline: true)
            {
                Label = "No answer from the model",
                Text = $"{advice.Refusal} {Unchanged}",
                Detail = advice.Code ?? "unknown",
            };

            notice.Actions.Add(Tile("Retry", () => _ = _view.Talk.RetryAsync(_view.BuildId, round.Id, CancellationToken.None)));
            body.Children.Add(notice);

            var failed = Message(body, ThemeManager.RedKey, commander: false);
            failed.MaxWidth = double.PositiveInfinity;
            failed.HorizontalAlignment = HorizontalAlignment.Stretch;

            return failed;
        }

        body.Children.Add(Prose(advice.Reply ?? "I have nothing to add about that build.", ThemeManager.WhiteKey));

        foreach (var dropped in advice.Dropped)
        {
            body.Children.Add(Mono($"Dropped: {dropped}", ThemeManager.WarnKey, TypeScale.Meta));
        }

        return Message(body, ThemeManager.AKey, commander: false);
    }

    // ---- the proposal -----------------------------------------------------------------------

    private Control Proposal(TalkRound round, BuildAdvice advice)
    {
        var decisions = round.Decisions;
        var stale = advice.Changes.Select(change => _view.Talk.IsStale(_view.BuildId, change)).ToList();
        var accepted = decisions.Count(decision => decision == ChangeDecision.Accepted);
        var rejected = decisions.Count(decision => decision == ChangeDecision.Rejected);
        var undecided = decisions.Count - accepted - rejected;
        var acceptable = Enumerable.Range(0, decisions.Count)
            .Count(index => decisions[index] == ChangeDecision.Undecided && !stale[index]);

        var acceptAll = Tile("Accept all", () => _view.Talk.AcceptAll(_view.BuildId, round.Id));
        acceptAll.IsEnabled = acceptable > 0;

        var rejectAll = Tile("Reject all", () => _view.Talk.RejectAll(_view.BuildId, round.Id));
        rejectAll.IsEnabled = undecided > 0;

        var title = new TextBlock { Margin = new Thickness(0, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center };
        TitleText.Style(title, TypeScale.Section, TitleRank.Group);
        TitleText.Show(title, "Proposal");

        var counts = Mono(Counts(decisions.Count, accepted, rejected, undecided), ThemeManager.GreyKey, TypeScale.Meta);
        counts.VerticalAlignment = VerticalAlignment.Center;

        var head = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,Auto"),
            ColumnSpacing = Gaps.Tile,
            Children = { title, counts, acceptAll, rejectAll },
        };

        Grid.SetColumn(counts, 1);
        Grid.SetColumn(acceptAll, 2);
        Grid.SetColumn(rejectAll, 3);

        var stack = new StackPanel { Children = { Ruled(head, ThemeManager.AKey) } };

        if (advice.Cost is { } cost)
        {
            stack.Children.Add(Cost(cost, advice.Changes.Count));
        }

        var hull = _view.Talk.Hull(_view.BuildId);

        for (var index = 0; index < advice.Changes.Count; index++)
        {
            stack.Children.Add(Row(round, index, advice.Changes[index], decisions[index], stale[index], hull));
        }

        var bar = new Border { Padding = new Thickness(18, 16, 18, 18), Child = stack };
        LoadoutPages.Themed(bar, Border.BackgroundProperty, ThemeManager.BarKey);

        return bar;
    }

    /// <summary>"4 CHANGES · 1 ACCEPTED · 1 REJECTED · 2 TO DECIDE".</summary>
    public static string Counts(int changes, int accepted, int rejected, int undecided) =>
        $"{Number(changes)} {(changes == 1 ? "CHANGE" : "CHANGES")} · {Number(accepted)} ACCEPTED"
        + $" · {Number(rejected)} REJECTED · {Number(undecided)} TO DECIDE";

    /// <summary>A material's line: "3 / 12 · 9 SHORT", or "✓ 5 / 5" where it is in hand.</summary>
    public static string Held(PlanIngredient ingredient) =>
        ingredient.Short > 0
            ? $"{Number(ingredient.Held)} / {Number(ingredient.Needed)} · {Number(ingredient.Short)} SHORT"
            : $"✓ {Number(ingredient.Held)} / {Number(ingredient.Needed)}";

    private static Control Cost(PlanCosting cost, int changes)
    {
        var stack = new StackPanel { Spacing = 6, Margin = new Thickness(0, 14, 0, 6) };

        stack.Children.Add(Label(changes == 1 ? "What it costs, accepted" : "What it costs, with every change accepted"));

        var tiles = new StackPanel { Spacing = Gaps.Tile };

        foreach (var gate in cost.Gates)
        {
            var locked = new Border
            {
                Padding = new Thickness(11, 10, 14, 10),
                BorderThickness = new Thickness(3, 0, 0, 0),
                Child = new StackPanel
                {
                    Spacing = 4,
                    Children = { Label("Locked", ThemeManager.RedKey), Prose(gate, ThemeManager.WhiteKey, TypeScale.ControlLarge) },
                },
            };

            LoadoutPages.Themed(locked, Border.BackgroundProperty, ThemeManager.SlabKey);
            LoadoutPages.Themed(locked, Border.BorderBrushProperty, ThemeManager.RedKey);
            tiles.Children.Add(locked);
        }

        var materials = new Avalonia.Controls.Primitives.UniformGrid { Columns = 2 };

        foreach (var ingredient in cost.Ingredients.OrderByDescending(ingredient => ingredient.Short))
        {
            var name = Prose(ingredient.Material.Name, ThemeManager.WhiteKey, TypeScale.ControlLarge);
            var holding = Mono(Held(ingredient), ingredient.Short > 0 ? ThemeManager.WarnKey : ThemeManager.BlueKey, TypeScale.Control);
            holding.VerticalAlignment = VerticalAlignment.Center;

            var line = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                ColumnSpacing = 12,
                Children = { name, holding },
            };

            Grid.SetColumn(holding, 1);

            var tile = new Border { Padding = new Thickness(14, 10), Margin = new Thickness(0, 0, Gaps.Tile, Gaps.Tile), Child = line };
            LoadoutPages.Themed(tile, Border.BackgroundProperty, ThemeManager.SlabKey);
            materials.Children.Add(tile);
        }

        if (materials.Children.Count > 0)
        {
            tiles.Children.Add(materials);
        }

        if (tiles.Children.Count == 0)
        {
            tiles.Children.Add(Prose("No engineering is planned.", ThemeManager.GreyKey, TypeScale.ControlLarge));
        }

        stack.Children.Add(tiles);
        stack.Children.Add(Prose(
            "Accepted changes join the checklist and the gap like any plan edit.",
            ThemeManager.GreyKey,
            TypeScale.Small));

        return stack;
    }

    private Control Row(
        TalkRound round,
        int index,
        SlotChange change,
        ChangeDecision decision,
        bool stale,
        string? hull)
    {
        var rejected = decision == ChangeDecision.Rejected;
        var slot = hull is null ? null : EliteSpecifications.Slot(hull, change.Slot);

        var name = new StackPanel
        {
            Spacing = 2,
            Children = { Chrome(slot?.Describe() ?? change.Slot, rejected ? ThemeManager.GreyKey : ThemeManager.WhiteKey, TypeScale.ControlLarge) },
        };

        if (slot is not null)
        {
            name.Children.Add(Mono($"SIZE {Number(slot.Size)}", ThemeManager.GreyKey, TypeScale.Meta));
        }

        var then = Prose(change.After.Describe(), rejected ? ThemeManager.GreyKey : ThemeManager.AKey, TypeScale.ControlLarge);

        if (rejected)
        {
            then.TextDecorations = TextDecorations.Strikethrough;
        }

        var middle = new StackPanel
        {
            Spacing = 6,
            Children =
            {
                Labelled("Now", Prose(change.Before is { IsEmpty: false } before ? before.Describe() : "Nothing planned", ThemeManager.GreyKey, TypeScale.ControlLarge)),
                Labelled("Then", then),
                Prose(change.Reason, ThemeManager.WhiteKey, TypeScale.ControlLarge),
            },
        };

        if (stale && decision == ChangeDecision.Undecided)
        {
            middle.Children.Add(new Notice(NoticeLevel.Warning, inline: true)
            {
                Label = "Plan changed since",
                Text = "This slot's plan changed after D47 proposed the change. Ask again for a proposal against the plan as it is now.",
                Margin = new Thickness(0, 4, 0, 0),
            });
        }

        Control end = decision switch
        {
            ChangeDecision.Accepted => Chrome("✓ In the plan", ThemeManager.BlueKey, TypeScale.Control),
            ChangeDecision.Rejected => Chrome("Rejected", ThemeManager.GreyKey, TypeScale.Control),
            _ => Decide(round, index, stale),
        };

        end.VerticalAlignment = VerticalAlignment.Top;
        end.HorizontalAlignment = HorizontalAlignment.Right;

        if (end is TextBlock said)
        {
            said.MinHeight = TypeScale.MinimumTarget;
        }

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("200,*,Auto"),
            ColumnSpacing = 16,
            Children = { name, middle, end },
        };

        Grid.SetColumn(middle, 1);
        Grid.SetColumn(end, 2);

        var row = new Border
        {
            Padding = new Thickness(0, 14),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Child = grid,
        };

        LoadoutPages.Themed(row, Border.BorderBrushProperty, ThemeManager.Line2Key);

        return row;
    }

    private Control Decide(TalkRound round, int index, bool stale)
    {
        var accept = Tile("Accept", () => _view.Talk.Accept(_view.BuildId, round.Id, index));
        accept.IsEnabled = !stale;

        var reject = Tile("Reject", () => _view.Talk.Reject(_view.BuildId, round.Id, index));

        return new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = Gaps.Tile,
            Children = { accept, reject },
        };
    }

    // ---- pieces -----------------------------------------------------------------------------

    private static Border Message(Control body, string bar, bool commander)
    {
        var message = new TurnBorder
        {
            Child = body,
            BorderThickness = commander ? new Thickness(0, 0, 3, 0) : new Thickness(3, 0, 0, 0),
            Padding = new Thickness(12, 10, 14, 12),
            HorizontalAlignment = commander ? HorizontalAlignment.Right : HorizontalAlignment.Left,
            MaxWidth = 700,
        };

        LoadoutPages.Themed(message, Border.BorderBrushProperty, bar);

        return message;
    }

    private static Control Who(string name, string key, string meta)
    {
        var who = Chrome(name, key, TypeScale.Small);
        who.FontWeight = FontWeight.Bold;

        var when = Mono(meta, ThemeManager.GreyKey, TypeScale.Meta);

        return new WrapPanel
        {
            ItemSpacing = 10,
            Children = { who, when },
        };
    }

    private static Control Labelled(string label, Control value)
    {
        var name = Label(label);
        name.VerticalAlignment = VerticalAlignment.Top;

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("64,*"),
            ColumnSpacing = 10,
            Children = { name, value },
        };

        Grid.SetColumn(value, 1);

        return grid;
    }

    private static Border Ruled(Control content, string key)
    {
        var border = new Border
        {
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(0, 0, 0, 6),
            Child = content,
        };

        LoadoutPages.Themed(border, Border.BorderBrushProperty, key);

        return border;
    }

    private static Button Tile(string label, Action pressed)
    {
        var button = new Button { Content = label, VerticalAlignment = VerticalAlignment.Center };
        button.Click += (_, _) => pressed();
        return button;
    }

    private static TextBlock Label(string text, string key = ThemeManager.GreyKey) => Chrome(text, key, TypeScale.Meta);

    private static TextBlock Chrome(string text, string key, double size)
    {
        var block = new TextBlock
        {
            Text = text.ToUpperInvariant(),
            FontFamily = new FontFamily(Fonts.ChromeFamily),
            FontSize = size,
            FontWeight = FontWeight.Medium,
            LetterSpacing = size * Fonts.ChromeTracking,
            TextWrapping = TextWrapping.Wrap,
        };

        LoadoutPages.Themed(block, TextBlock.ForegroundProperty, key);
        return block;
    }

    private static TextBlock Prose(string text, string key, double size = TypeScale.Body)
    {
        var block = LoadoutPages.Toned(text, key, size);
        block.LineHeight = size * 1.45;
        return block;
    }

    private static TextBlock Mono(string text, string key, double size)
    {
        var block = LoadoutPages.Toned(text, key, size);
        block.FontFamily = new FontFamily(Fonts.MonoFamily);
        return block;
    }

    private static string Clock(DateTimeOffset at) =>
        at.ToLocalTime().ToString("HH:mm", CultureInfo.InvariantCulture);

    private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);
}
