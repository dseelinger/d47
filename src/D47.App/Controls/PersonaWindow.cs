using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using D47.App.Theming;
using D47.Core.Persona;

namespace D47.App.Controls;

/// <summary>Writing a core of your own (remediation.md 11, item 9).</summary>
public sealed class PersonaWindow : Window
{
    private readonly OwnPersonaStore _store;
    private readonly List<Written> _cores;
    private readonly StackPanel _list = new() { Spacing = 12 };
    private readonly TextBlock _problems = new()
    {
        TextWrapping = TextWrapping.Wrap,
        FontSize = TypeScale.Secondary,
        IsVisible = false,
    };

    public PersonaWindow(OwnPersonaStore store)
    {
        _store = store;
        _cores = [.. store.Cores.Select(Written.From)];

        Title = "Your own cores";
        Width = 680;
        Height = 620;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var add = new Button { Content = "Write a core", Padding = new Thickness(10, 4) };

        add.Click += (_, _) =>
        {
            _cores.Add(new Written { Name = "New core" });
            Rebuild();
        };

        var save = new Button { Content = "Save", Padding = new Thickness(14, 4) };

        save.Click += (_, _) => Save();

        var close = new Button { Content = "Close", Padding = new Thickness(14, 4) };

        close.Click += (_, _) => Close();

        Themed(_problems, TextBlock.ForegroundProperty, ThemeManager.DangerKey);

        var header = new TextBlock
        {
            Text =
                $"Saved to {store.Path}, which is the same file you can edit by hand. D47 wraps what you "
                + "write in the same shared preamble and standing instructions every shipped core gets.",
            FontSize = TypeScale.Secondary,
            TextWrapping = TextWrapping.Wrap,
        };

        Themed(header, TextBlock.ForegroundProperty, ThemeManager.TextMutedKey);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 12, 0, 0),
            Children = { add, save, close },
        };

        var root = new DockPanel { Margin = new Thickness(16) };

        DockPanel.SetDock(header, Dock.Top);
        DockPanel.SetDock(_problems, Dock.Top);
        DockPanel.SetDock(buttons, Dock.Bottom);

        root.Children.Add(header);
        root.Children.Add(_problems);
        root.Children.Add(buttons);
        // The key to the marks on the cards below (#253).
        var legend = new StackPanel
        {
            Margin = new Thickness(0, 10, 0, 0),
            Children = { FormField.Legend(required: true, supplied: true) },
        };

        // Docked, like the two rows above it.
        DockPanel.SetDock(legend, Dock.Top);

        root.Children.Add(legend);

        root.Children.Add(new ScrollViewer { Content = _list, Margin = new Thickness(0, 8, 0, 0) });

        Content = root;

        Themed(this, BackgroundProperty, ThemeManager.SurfaceKey);

        Rebuild();
        ShowProblems();
    }

    private void Save()
    {
        _store.Save([.. _cores.Select(core => core.ToCore(_store.Cores))]);

        // Re-read rather than trusting what was just written, so what the panel shows next is the validated
        // set: a core the store refuses leaves the list and its reason appears above it.
        _cores.Clear();
        _cores.AddRange(_store.Cores.Select(Written.From));

        Rebuild();
        ShowProblems();
    }

    private void ShowProblems()
    {
        _problems.IsVisible = _store.Problems.Count > 0;
        _problems.Text = string.Join(
            "\n",
            _store.Problems.Select(problem => $"{problem.Which}: {problem.Reason}"));
    }

    private void Rebuild()
    {
        _list.Children.Clear();

        if (_cores.Count == 0)
        {
            var empty = new TextBlock
            {
                Text =
                    "Nothing yet. A core needs a name and a paragraph saying what it is like; "
                    + "everything else about it is supplied.",
                FontSize = TypeScale.Body,
                TextWrapping = TextWrapping.Wrap,
            };

            Themed(empty, TextBlock.ForegroundProperty, ThemeManager.TextMutedKey);
            _list.Children.Add(empty);

            return;
        }

        foreach (var core in _cores)
        {
            _list.Children.Add(Card(core));
        }
    }

    /// <summary>One core's three boxes (#253).</summary>
    private Control Card(Written core)
    {
        var name = new TextBox
        {
            Text = core.Name,
            PlaceholderText = "what to call it",
            FontSize = TypeScale.Body,
            MaxLength = D47.Core.Persona.OwnPersona.MaxNameLength,
        };

        name.TextChanged += (_, _) => core.Name = name.Text ?? string.Empty;

        var body = new TextBox
        {
            Text = core.Body,
            PlaceholderText = "Speak to it in the second person, as the shipped cores are written.",
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            MinHeight = 140,
            FontSize = TypeScale.Body,
            MaxLength = D47.Core.Persona.OwnPersona.MaxBodyLength,
        };

        body.TextChanged += (_, _) => core.Body = body.Text ?? string.Empty;

        var voice = new TextBox
        {
            Text = core.Voice,
            PlaceholderText = "how it should sound",
            FontSize = TypeScale.Body,
        };

        voice.TextChanged += (_, _) => core.Voice = voice.Text ?? string.Empty;

        FormField.Announce(name, "Name", FieldNeed.Required);
        FormField.Announce(body, "Character", FieldNeed.Required);
        FormField.Announce(voice, "Voice", FieldNeed.Supplied);

        var drop = new Button { Content = "Delete", Padding = new Thickness(10, 4) };

        drop.Click += (_, _) =>
        {
            _cores.Remove(core);
            Rebuild();
        };

        var head = new DockPanel();

        DockPanel.SetDock(drop, Dock.Right);
        drop.Margin = new Thickness(8, 0, 0, 0);

        head.Children.Add(drop);
        head.Children.Add(FormField.Label("Name", FieldNeed.Required));

        // Voice is the one field on this window that already described the ship-supplied state in prose —
        // "left empty, D47 pairs it on the name alone" — which is the same third state as the Neutron
        // Plotter's "this ship's".
        var card = new Border
        {
            Padding = new Thickness(12),
            CornerRadius = new CornerRadius(4),
            BorderThickness = new Thickness(1),
            Child = new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    new StackPanel { Spacing = 3, Children = { head, name } },
                    new StackPanel
                    {
                        Spacing = 3,
                        Children = { FormField.Label("Character", FieldNeed.Required), body },
                    },
                    new StackPanel
                    {
                        Spacing = 3,
                        Children = { FormField.Label("Voice", FieldNeed.Supplied), voice },
                    },
                },
            },
        };

        Themed(card, Border.BackgroundProperty, ThemeManager.SurfaceAltKey);
        Themed(card, Border.BorderBrushProperty, ThemeManager.BorderKey);

        return card;
    }

    private static void Themed(StyledElement element, AvaloniaProperty property, string key) =>
        element.Bind(property, Application.Current!.GetResourceObservable(key));

    /// <summary>One core while it is being edited.</summary>
    private sealed class Written
    {
        public string Id { get; init; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public string Body { get; set; } = string.Empty;

        public string Voice { get; set; } = string.Empty;

        public static Written From(OwnPersona core) => new()
        {
            Id = core.Id,
            Name = core.Name,
            Body = core.Body,
            Voice = core.Voice,
        };

        /// <summary>The stored core.</summary>
        public OwnPersona ToCore(IReadOnlyList<OwnPersona> existing) => new(
            Id is { Length: > 0 } known ? known : OwnPersonaStore.IdFor(Name, existing),
            Name.Trim(),
            Body.Trim(),
            Voice.Trim());
    }
}
