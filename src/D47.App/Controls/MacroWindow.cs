using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using D47.App.Theming;
using D47.Core.Actions;
using D47.Core.Input;

namespace D47.App.Controls;

/// <summary>Authoring macros (Phase 10, "Macros": invocation is by voice; authoring is not).</summary>
public sealed class MacroWindow : Window
{
    private static readonly string[] States = ["toggle", "on", "off"];

    /// <summary>Everything except the fire groups, which macros may not reach.</summary>
    private static readonly string[] Allowed =
        [.. GameActions.All.Where(a => a.Group != GameActions.Weapons).Select(a => a.Id)];

    private readonly MacroStore _store;
    private readonly List<MutableMacro> _macros;
    private readonly StackPanel _list = new() { Spacing = 12 };
    private readonly TextBlock _problems = new()
    {
        TextWrapping = TextWrapping.Wrap,
        FontSize = TypeScale.Secondary,
        IsVisible = false,
    };

    public MacroWindow(MacroStore store)
    {
        _store = store;
        _macros = [.. store.Macros.Select(MutableMacro.From)];

        Title = "Macros";
        Width = 640;
        Height = 560;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var add = new Button { Content = "Add a macro" };
        add.Click += (_, _) =>
        {
            _macros.Add(new MutableMacro { Name = "new macro" });
            Rebuild();
        };

        var save = new Button { Content = "Save", MinWidth = 110 };
        save.Click += (_, _) => Save();

        var close = new Button { Content = "Close", MinWidth = 110 };
        close.Click += (_, _) => Close();

        Themed(_problems, TextBlock.ForegroundProperty, ThemeManager.RedKey);

        var header = new TextBlock
        {
            Text = $"Saved to {store.Path}. This is the same file you can edit by hand.",
            FontSize = TypeScale.Secondary,
            TextWrapping = TextWrapping.Wrap,
        };
        Themed(header, TextBlock.ForegroundProperty, ThemeManager.GreyKey);

        var root = new StackPanel { Spacing = 10, Children = { header, _problems, _list } };

        Modal.Apply(this, "Macros", Title, root, [add, save, close]);

        Rebuild();
        ShowProblems();
    }

    private void Save()
    {
        _store.Save([.. _macros.Select(macro => macro.ToMacro())]);

        // Re-read rather than trusting what was just written, so what the panel shows next is the validated
        // set.
        _store.Poll(ReservedPhrases);

        _macros.Clear();
        _macros.AddRange(_store.Macros.Select(MutableMacro.From));

        Rebuild();
        ShowProblems();
    }

    /// <summary>Supplied by whoever opened the window.</summary>
    public IReadOnlyList<string> ReservedPhrases { get; init; } = [];

    private void ShowProblems()
    {
        if (_store.Problems.Count == 0)
        {
            _problems.IsVisible = false;
            return;
        }

        _problems.IsVisible = true;
        _problems.Text = string.Join("\n", _store.Problems.Select(problem => problem.Reason));
    }

    private void Rebuild()
    {
        _list.Children.Clear();

        if (_macros.Count == 0)
        {
            var empty = new TextBlock
            {
                Text = "No macros yet. A macro is a name and a list of ship actions to run in order.",
                FontSize = TypeScale.Body,
                TextWrapping = TextWrapping.Wrap,
            };
            Themed(empty, TextBlock.ForegroundProperty, ThemeManager.GreyKey);
            _list.Children.Add(empty);
            return;
        }

        foreach (var macro in _macros)
        {
            _list.Children.Add(BuildMacro(macro));
        }
    }

    private Control BuildMacro(MutableMacro macro)
    {
        var name = new TextBox { Text = macro.Name, Width = 260 };
        name.TextChanged += (_, _) => macro.Name = name.Text ?? string.Empty;

        var remove = new Button { Content = "Remove", Classes = { "destructive" } };
        remove.Click += (_, _) =>
        {
            _macros.Remove(macro);
            Rebuild();
        };

        var steps = new StackPanel { Spacing = 6, Margin = new Thickness(0, 8, 0, 0) };

        foreach (var step in macro.Steps)
        {
            steps.Children.Add(BuildStep(macro, step));
        }

        var addStep = new Button
        {
            Content = "Add a step",
            Margin = new Thickness(0, 6, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Left,
            IsEnabled = macro.Steps.Count < Macro.MaxSteps,
        };

        addStep.Click += (_, _) =>
        {
            macro.Steps.Add(new MutableStep { Action = Allowed[0] });
            Rebuild();
        };

        var body = new StackPanel
        {
            Spacing = 4,
            Children =
            {
                new WrapPanel { ItemSpacing = 8, LineSpacing = 8, Children = { name, remove } },
                steps,
                addStep,
            },
        };

        // Ruled rather than a list row: the boxes and choices are Tile, and vanish on a Tile row.
        var card = new Border
        {
            Padding = new Thickness(0, 12),
            BorderThickness = new Thickness(0, 1, 0, 0),
            Child = body,
        };
        Themed(card, Border.BorderBrushProperty, ThemeManager.LineKey);

        return card;
    }

    private Control BuildStep(MutableMacro macro, MutableStep step)
    {
        var (actionView, action) = Choice.Build(Allowed, Array.IndexOf(Allowed, step.Action));
        actionView.Width = 200;
        action.SelectionChanged += (_, _) => step.Action = action.SelectedItem ?? step.Action;

        var (stateView, state) = Choice.Build(States, Array.IndexOf(States, step.State));
        stateView.Width = 100;
        state.SelectionChanged += (_, _) => step.State = state.SelectedItem ?? step.State;

        var pause = new NumericUpDown
        {
            Value = step.PauseMs,
            Minimum = 0,
            Maximum = MacroStep.MaxPauseMs,
            Increment = 50,
            Width = 170,
        };
        pause.ValueChanged += (_, _) => step.PauseMs = (int)(pause.Value ?? 0);

        var up = Glyph("↑", "Move this step up");
        up.Click += (_, _) => Move(macro, step, -1);

        var down = Glyph("↓", "Move this step down");
        down.Click += (_, _) => Move(macro, step, +1);

        var drop = Glyph("✕", "Remove this step");
        drop.Click += (_, _) =>
        {
            macro.Steps.Remove(step);
            Rebuild();
        };

        return new WrapPanel
        {
            ItemSpacing = 6,
            LineSpacing = 6,
            Children = { actionView, stateView, pause, up, down, drop },
        };
    }

    private static Button Glyph(string glyph, string says)
    {
        var button = new Button
        {
            Theme = Avalonia.Application.Current?.FindResource("D47.GlyphButton") as Avalonia.Styling.ControlTheme,
            Content = Glyphs.Text(glyph, TypeScale.Body),
        };

        ToolTip.SetTip(button, says);
        Avalonia.Automation.AutomationProperties.SetName(button, says);

        return button;
    }

    private void Move(MutableMacro macro, MutableStep step, int by)
    {
        var index = macro.Steps.IndexOf(step);
        var target = index + by;

        if (index < 0 || target < 0 || target >= macro.Steps.Count)
        {
            return;
        }

        macro.Steps.RemoveAt(index);
        macro.Steps.Insert(target, step);
        Rebuild();
    }

    private static void Themed(Avalonia.AvaloniaObject target, Avalonia.AvaloniaProperty property, string key) =>
        target.Bind(property, Avalonia.Application.Current!.Resources.GetResourceObservable(key));

    /// <summary>The editor's working copy.</summary>
    private sealed class MutableMacro
    {
        public string Name { get; set; } = string.Empty;

        public List<MutableStep> Steps { get; } = [];

        public static MutableMacro From(Macro macro)
        {
            var mutable = new MutableMacro { Name = macro.Name };

            foreach (var step in macro.Steps)
            {
                mutable.Steps.Add(new MutableStep
                {
                    Action = step.Action,
                    State = step.State.ToString().ToLowerInvariant(),
                    PauseMs = step.PauseMs,
                });
            }

            return mutable;
        }

        public Macro ToMacro() => new()
        {
            Name = Name.Trim(),
            Steps = [.. Steps.Select(step => new MacroStep(
                step.Action,
                step.State switch
                {
                    "on" => DesiredState.On,
                    "off" => DesiredState.Off,
                    _ => DesiredState.Toggle,
                },
                step.PauseMs))],
        };
    }

    private sealed class MutableStep
    {
        public string Action { get; set; } = string.Empty;

        public string State { get; set; } = "toggle";

        public int PauseMs { get; set; } = 250;
    }
}
