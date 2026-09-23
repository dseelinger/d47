using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using D47.App.Theming;
using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Conversation;
using D47.Core.Journal;

namespace D47.App.Panel;

/// <summary>What the flying Commander has taught D47 stands for a declared phrase, and forgetting one (#171).</summary>
public sealed class LearnedPhrasesPage : UserControl
{
    /// <summary>The Settings tab's second root.</summary>
    public const string RootKey = "settings.learned-phrases";

    private readonly CapabilityRegistry _registry;
    private readonly LearnedPhrasesStore _store;
    private readonly Func<CommanderGameState?> _commander;

    private readonly StackPanel _body = new();

    public LearnedPhrasesPage(
        CapabilityRegistry registry,
        LearnedPhrasesStore store,
        Func<CommanderGameState?> commander)
    {
        _registry = registry;
        _store = store;
        _commander = commander;

        Content = new ScrollViewer
        {
            Padding = new Thickness(14),
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = _body,
        };

        _store.Changed += Refresh;

        Build();
    }

    /// <summary>Redraws after a forget, from the panel or from a phrase said out loud.</summary>
    public void Refresh() => Dispatcher.UIThread.Post(Build);

    private void Build()
    {
        _body.Children.Clear();
        _body.Children.Add(RoutingKit.Title("Learned phrases").Row);

        var fid = _commander()?.Identity.FrontierId;

        IReadOnlyList<LearnedPhrase> learned = fid is { Length: > 0 }
            ? [.. _store.For(fid).OrderByDescending(phrase => phrase.LearnedAt)]
            : [];

        if (learned.Count == 0)
        {
            _body.Children.Add(LoadoutPages.Section("Nothing learned yet"));
            _body.Children.Add(Text(
                "D47 asks once, after it runs a near miss on your say-so, whether to remember the "
                + "wording. A yes puts it here.",
                TypeScale.Body,
                ThemeManager.GreyKey,
                wrap: true));

            return;
        }

        var rows = new StackPanel { Spacing = 2 };

        foreach (var phrase in learned)
        {
            rows.Children.Add(Row(phrase));
        }

        _body.Children.Add(LoadoutPages.Section("What D47 has learned"));
        _body.Children.Add(rows);
    }

    private Control Row(LearnedPhrase phrase)
    {
        var forget = new Button
        {
            Content = "Forget",
            Classes = { "destructive" },
            VerticalAlignment = VerticalAlignment.Center,
        };

        forget.Click += (_, _) =>
        {
            _ = _registry.InvokeAsync(
                LearnedPhrasesCapability.ForgetTool,
                new ToolArguments(
                    new Dictionary<string, string>(StringComparer.Ordinal) { ["said"] = phrase.Said }),
                CancellationToken.None);
        };

        var words = new WrapPanel
        {
            ItemSpacing = 10,
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                ListRow.Name(Text($"\"{phrase.Said}\"", TypeScale.Body, null)),
                Text("→", TypeScale.Body, ThemeManager.GreyKey),
                ListRow.Secondary(Text($"\"{phrase.Phrase}\"", TypeScale.Body, null)),
            },
        };

        forget.Margin = new Thickness(12, 0, 0, 0);

        var row = new DockPanel();
        DockPanel.SetDock(forget, Dock.Right);
        row.Children.Add(forget);
        row.Children.Add(words);

        return ListRow.Dress(new Border { Padding = new Thickness(12, 6), Child = row });
    }

    private static TextBlock Text(string text, double size, string? colourKey, bool wrap = false)
    {
        var block = new TextBlock
        {
            Text = text,
            FontSize = size,
            TextWrapping = wrap ? TextWrapping.Wrap : TextWrapping.NoWrap,
            MaxWidth = wrap ? 520 : double.PositiveInfinity,
            HorizontalAlignment = HorizontalAlignment.Left,
        };

        if (colourKey is not null)
        {
            block.Bind(
                TextBlock.ForegroundProperty,
                Application.Current!.Resources.GetResourceObservable(colourKey));
        }

        return block;
    }
}
