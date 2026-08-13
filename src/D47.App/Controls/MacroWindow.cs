using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using D47.App.Theming;
using D47.Core.Actions;
using D47.Core.Input;

namespace D47.App.Controls;

/// <summary>
/// Authoring macros (list.md Phase 10, "Macros": <i>invocation is by voice; authoring is not</i>).
/// <para>
/// <b>Every field here is a closed vocabulary except the name.</b> The action is a drop-down of
/// the catalogue, the state is one of three, and the pause is a bounded number — so a macro
/// composed here cannot express anything the validator would refuse. That is the same guarantee
/// the file has, arrived at from the other direction: the file is checked after the fact, and
/// this cannot produce something to check.
/// </para>
/// <para>
/// It writes the same <c>macros.json</c> a text editor writes. There is no second store and no
/// import step — the panel is a convenience over the file, not an alternative to it.
/// </para>
/// </summary>
public sealed class MacroWindow : Window
{
    private static readonly string[] States = ["toggle", "on", "off"];

    /// <summary>
    /// Everything except the fire groups, which macros may not reach. Built once: this is the
    /// allowlist, and offering something the validator will refuse is a way to teach somebody
    /// that d47 is unreliable.
    /// </summary>
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

        var add = new Button { Content = "Add a macro", Padding = new Thickness(10, 4) };
        add.Click += (_, _) =>
        {
            _macros.Add(new MutableMacro { Name = "new macro" });
            Rebuild();
        };

        var save = new Button { Content = "Save", Padding = new Thickness(14, 4) };
        save.Click += (_, _) => Save();

        var close = new Button { Content = "Close", Padding = new Thickness(14, 4) };
        close.Click += (_, _) => Close();

        Themed(_problems, TextBlock.ForegroundProperty, ThemeManager.DangerKey);

        var header = new TextBlock
        {
            Text = $"Saved to {store.Path}. This is the same file you can edit by hand.",
            FontSize = TypeScale.Secondary,
            TextWrapping = TextWrapping.Wrap,
        };
        Themed(header, TextBlock.ForegroundProperty, ThemeManager.TextMutedKey);

        var root = new DockPanel { Margin = new Thickness(16) };

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 12, 0, 0),
            Children = { add, save, close },
        };

        DockPanel.SetDock(header, Dock.Top);
        DockPanel.SetDock(_problems, Dock.Top);
        DockPanel.SetDock(buttons, Dock.Bottom);

        root.Children.Add(header);
        root.Children.Add(_problems);
        root.Children.Add(buttons);
        root.Children.Add(new ScrollViewer { Content = _list, Margin = new Thickness(0, 12, 0, 0) });

        Content = root;
        Themed(this, BackgroundProperty, ThemeManager.SurfaceKey);

        Rebuild();
        ShowProblems();
    }

    private void Save()
    {
        _store.Save([.. _macros.Select(macro => macro.ToMacro())]);

        // Re-read rather than trusting what was just written, so what the panel shows next is
        // the validated set. A macro the validator refuses disappears from the list and its
        // reason appears above it, which is the same answer the voice path would give.
        _store.Poll(ReservedPhrases);

        _macros.Clear();
        _macros.AddRange(_store.Macros.Select(MutableMacro.From));

        Rebuild();
        ShowProblems();
    }

    /// <summary>
    /// Supplied by whoever opened the window. Empty is safe — it only means a name collision
    /// with a built-in phrase is caught on the next reload rather than on this save.
    /// </summary>
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
            Themed(empty, TextBlock.ForegroundProperty, ThemeManager.TextMutedKey);
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

        var remove = new Button { Content = "Remove", Padding = new Thickness(10, 2), FontSize = TypeScale.Body };
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
            Padding = new Thickness(10, 2),
            FontSize = TypeScale.Body,
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
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 8,
                    Children = { name, remove },
                },
                steps,
                addStep,
            },
        };

        var card = new Border
        {
            Padding = new Thickness(12),
            CornerRadius = new CornerRadius(4),
            Child = body,
        };
        Themed(card, Border.BackgroundProperty, ThemeManager.SurfaceAltKey);

        return card;
    }

    private Control BuildStep(MutableMacro macro, MutableStep step)
    {
        var action = new ComboBox { ItemsSource = Allowed, SelectedItem = step.Action, Width = 200 };
        action.SelectionChanged += (_, _) => step.Action = action.SelectedItem as string ?? step.Action;

        var state = new ComboBox { ItemsSource = States, SelectedItem = step.State, Width = 100 };
        state.SelectionChanged += (_, _) => step.State = state.SelectedItem as string ?? step.State;

        var pause = new NumericUpDown
        {
            Value = step.PauseMs,
            Minimum = 0,
            Maximum = MacroStep.MaxPauseMs,
            Increment = 50,
            Width = 110,
        };
        pause.ValueChanged += (_, _) => step.PauseMs = (int)(pause.Value ?? 0);

        var up = new Button { Content = "↑", Padding = new Thickness(8, 2), FontSize = TypeScale.Body };
        up.Click += (_, _) => Move(macro, step, -1);

        var down = new Button { Content = "↓", Padding = new Thickness(8, 2), FontSize = TypeScale.Body };
        down.Click += (_, _) => Move(macro, step, +1);

        var drop = new Button { Content = "✕", Padding = new Thickness(8, 2), FontSize = TypeScale.Body };
        drop.Click += (_, _) =>
        {
            macro.Steps.Remove(step);
            Rebuild();
        };

        return new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            Children = { action, state, pause, up, down, drop },
        };
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

    /// <summary>
    /// The editor's working copy. <see cref="Macro"/> is immutable, which is right for something
    /// being validated and run, and wrong for something being typed into.
    /// </summary>
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
