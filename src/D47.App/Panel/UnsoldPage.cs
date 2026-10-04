using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using D47.App.Controls;
using D47.App.Theming;
using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Journal;

namespace D47.App.Panel;

/// <summary>Navigation › Unsold data: the organic and exploration data carried, each with a protected reset (#556).</summary>
public sealed class UnsoldPage : UserControl
{
    public const string RootKey = "routing.unsold";

    /// <summary>The footer's phrase; it carries a <c>get_unsold_exploration</c> keyword.</summary>
    public const string Phrase = "how much exploration data am I carrying";

    public const string ResetHelp = "Zeroes this total from now on. It can't be undone.";

    public const string OrganicResetName = "ResetOrganic";

    public const string ExplorationResetName = "ResetExploration";

    private readonly Func<CommanderGameState?> _state;
    private readonly Func<ExobiologyLedger?> _exobiology;
    private readonly Func<CartographyLedger?> _cartography;
    private readonly CapabilityRegistry? _registry;
    private readonly JournalClock _clock;
    private readonly StackPanel _body = new();

    private (string? Commander, int? Organic, int? Exploration) _revisions;
    private Figures? _seen;

    public UnsoldPage(
        Func<CommanderGameState?> state,
        Func<ExobiologyLedger?> exobiology,
        Func<CartographyLedger?> cartography,
        CapabilityRegistry? registry)
    {
        _state = state;
        _exobiology = exobiology;
        _cartography = cartography;
        _registry = registry;
        _clock = new JournalClock(() => state()?.Session.LastEventAt);

        var root = new DockPanel { Margin = new Thickness(14) };
        var footer = new PageFooter(Phrase, _clock) { Margin = new Thickness(0, 12, 0, 0) };
        DockPanel.SetDock(footer, Dock.Bottom);
        root.Children.Add(footer);
        root.Children.Add(LoadoutPages.Scrolling(new Grid
        {
            ColumnDefinitions = [new ColumnDefinition(GridLength.Star) { MaxWidth = 900 }, new ColumnDefinition(GridLength.Auto)],
            Children = { _body },
        }));

        Content = root;
        Draw();
    }

    /// <summary>Redraws the page when either ledger has folded events or been reset and its figures moved.</summary>
    public bool Tick()
    {
        var changed = _clock.Tick();

        if (Revisions() == _revisions)
        {
            return changed;
        }

        if (Take() == _seen)
        {
            _revisions = Revisions();
            return changed;
        }

        Draw();
        return true;
    }

    /// <summary>A reset time as the help line prints it, in local time.</summary>
    public static string ResetLine(DateTimeOffset at) =>
        $"Reset at {at.ToLocalTime().ToString("HH:mm", CultureInfo.InvariantCulture)}. Counting from zero.";

    /// <summary>What one ledger shows.</summary>
    private sealed record Ledger(long Total, int Count, int Marked, int Unpriced, DateTimeOffset? ResetAt);

    private sealed record Figures(Ledger? Organic, Ledger? Exploration);

    private (string?, int?, int?) Revisions() =>
        (_state()?.Identity.FrontierId, _exobiology()?.Revision, _cartography()?.Revision);

    private Figures Take()
    {
        var fid = _state()?.Identity.FrontierId;

        Ledger? organic = null;
        Ledger? exploration = null;

        if (_exobiology() is { } exobiology)
        {
            var unsold = fid is null ? UnsoldExobiology.Empty : exobiology.Unsold(fid);
            organic = new Ledger(
                unsold.Total,
                unsold.Held.Count - unsold.Unpriced.Count,
                unsold.WithBonus,
                unsold.Unpriced.Count,
                fid is null ? null : exobiology.ResetAt(fid));
        }

        if (_cartography() is { } cartography)
        {
            var unsold = fid is null ? UnsoldCartography.Empty : cartography.Unsold(fid);
            exploration = new Ledger(
                unsold.Total,
                unsold.Held.Count - unsold.Unpriced.Count,
                unsold.Efficient,
                unsold.Unpriced.Count,
                fid is null ? null : cartography.ResetAt(fid));
        }

        return new Figures(organic, exploration);
    }

    private void Draw()
    {
        _revisions = Revisions();
        var figures = Take();
        _seen = figures;

        _body.Children.Clear();

        var together = (figures.Organic?.Total ?? 0) + (figures.Exploration?.Total ?? 0);

        _body.Children.Add(TitleText.Block(
            TitleText.Build("Unsold data", TypeScale.Title, TitleRank.Screen),
            TitleText.Context("Carried, not yet sold"),
            Together(together)));

        if (figures.Organic is { } organic)
        {
            _body.Children.Add(Group("Organic data", "Species you have sampled and not yet sold at Vista Genomics."));
            _body.Children.Add(Data(organic, "Species", "First footfall"));
            _body.Children.Add(ResetRow(
                organic,
                OrganicResetName,
                "Reset organic data",
                ExobiologyCapability.ResetTool,
                "reset unsold exobiology"));
        }

        if (figures.Exploration is { } exploration)
        {
            _body.Children.Add(Group("Exploration data", "Bodies you have mapped and not yet sold at Universal Cartographics."));
            _body.Children.Add(Data(exploration, "Bodies", "Mapped efficiently"));
            _body.Children.Add(ResetRow(
                exploration,
                ExplorationResetName,
                "Reset exploration data",
                JournalCapability.ResetExplorationTool,
                "reset unsold exploration"));
        }
    }

    private static Control Together(long total)
    {
        var name = Caption("Together", ThemeManager.GreyKey);
        name.HorizontalAlignment = HorizontalAlignment.Right;

        var figure = new TextBlock
        {
            FontFamily = new FontFamily(Fonts.MonoFamily),
            FontSize = TypeScale.Figure,
            HorizontalAlignment = HorizontalAlignment.Right,
            Text = SessionPage.Credits(total),
        };
        Themed(figure, TextBlock.ForegroundProperty, ThemeManager.AKey);

        return new StackPanel { Spacing = 2, Children = { name, figure } };
    }

    private static Control Data(Ledger ledger, string counted, string marked)
    {
        var value = Figure(SessionPage.Credits(ledger.Total), 22, ThemeManager.AKey);

        var tiles = new Control[]
        {
            Tile("Estimated value", value),
            Tile(counted, Figure(Number(ledger.Count), StatTile.ValueSize, ThemeManager.AKey)),
            Tile(marked, Figure(Number(ledger.Marked), StatTile.ValueSize, ThemeManager.AKey)),
            Tile("No price · not in total", Figure(Number(ledger.Unpriced), StatTile.ValueSize, ThemeManager.WhiteKey)),
        };

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("2*,*,*,*"),
            ColumnSpacing = StatTile.Gap,
        };

        for (var i = 0; i < tiles.Length; i++)
        {
            Grid.SetColumn(tiles[i], i);
            tiles[i].VerticalAlignment = VerticalAlignment.Stretch;
            grid.Children.Add(tiles[i]);
        }

        return grid;
    }

    private Control ResetRow(Ledger ledger, string name, string title, string tool, string phrase)
    {
        var label = new TextBlock
        {
            Text = "Reset the total",
            FontFamily = Fonts.ProseFamily,
            FontSize = TypeScale.Secondary,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Themed(label, TextBlock.ForegroundProperty, ThemeManager.WhiteKey);

        var reset = new Button
        {
            Name = name,
            Content = "RESET",
            Height = TypeScale.MinimumTarget,
            MinWidth = 76,
            FontSize = TypeScale.Control,
            VerticalAlignment = VerticalAlignment.Center,
            IsEnabled = _registry is not null,
            Classes = { Settings.SettingsView.DestructiveClass },
        };
        reset.Click += async (_, _) => await ResetAsync(ledger, title, tool, phrase).ConfigureAwait(true);
        Grid.SetColumn(reset, 1);

        var help = new TextBlock
        {
            Text = ledger.ResetAt is { } at ? ResetLine(at) : ResetHelp,
            FontSize = TypeScale.Small,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(16, 0, 0, 0),
        };
        Themed(help, TextBlock.ForegroundProperty, ThemeManager.GreyKey);
        Grid.SetColumn(help, 2);

        var row = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("256,Auto,*"),
            Children = { label, reset, help },
        };

        var bar = new Border
        {
            BorderThickness = new Thickness(3, 0, 0, 0),
            Padding = new Thickness(11, 6, 14, 6),
            Margin = new Thickness(0, 10, 0, 0),
            Child = row,
        };
        Themed(bar, Border.BorderBrushProperty, ThemeManager.AKey);

        return bar;
    }

    private async Task ResetAsync(Ledger ledger, string title, string tool, string phrase)
    {
        if (_registry is not { } registry || TopLevel.GetTopLevel(this) is not Window owner)
        {
            return;
        }

        var confirmed = await new UnsoldResetDialog(title, SessionPage.Credits(ledger.Total), phrase)
            .AskAsync(owner)
            .ConfigureAwait(true);

        if (!confirmed)
        {
            return;
        }

        await registry.InvokeAsync(tool, ToolArguments.Empty, caller: ToolCaller.Commander).ConfigureAwait(true);
        Draw();
    }

    private static string Number(int n) => n.ToString("N0", CultureInfo.InvariantCulture);

    private static TextBlock Figure(string text, double size, string key)
    {
        var block = new TextBlock
        {
            Text = text,
            FontFamily = new FontFamily(Fonts.MonoFamily),
            FontSize = size,
            FontWeight = FontWeight.Medium,
        };
        Themed(block, TextBlock.ForegroundProperty, key);
        return block;
    }

    private static Border Tile(string label, Control value)
    {
        var caption = Caption(label, ThemeManager.GreyKey);
        caption.FontWeight = FontWeight.Medium;

        var tile = new Border
        {
            Padding = new Thickness(14, 10),
            Child = new StackPanel { Spacing = 2, Children = { caption, value } },
        };
        Themed(tile, Border.BackgroundProperty, ThemeManager.SlabKey);

        return tile;
    }

    /// <summary>Upper-case tracked chrome at meta size.</summary>
    private static TextBlock Caption(string text, string key)
    {
        var block = new TextBlock
        {
            Text = text.ToUpperInvariant(),
            FontFamily = Fonts.ChromeFamily,
            FontSize = TypeScale.Meta,
            LetterSpacing = TypeScale.Meta * Fonts.ChromeTracking,
            TextWrapping = TextWrapping.Wrap,
        };
        Themed(block, TextBlock.ForegroundProperty, key);
        return block;
    }

    private static Control Group(string title, string help)
    {
        var heading = TitleText.Build(title, TypeScale.Secondary, TitleRank.Group);
        heading.Margin = new Thickness(0, 0, 12, 0);
        heading.VerticalAlignment = VerticalAlignment.Center;

        var note = new TextBlock
        {
            Text = help,
            FontSize = TypeScale.Secondary,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Themed(note, TextBlock.ForegroundProperty, ThemeManager.GreyKey);

        var words = new WrapPanel { Children = { heading, note } };

        var rule = new Border { Height = 1, Margin = new Thickness(0, 10, 0, 0) };
        Themed(rule, Border.BackgroundProperty, ThemeManager.AKey);

        return new StackPanel { Margin = new Thickness(0, 28, 0, 10), Children = { words, rule } };
    }

    private static void Themed(StyledElement target, AvaloniaProperty property, string key) =>
        target.Bind(property, target.GetResourceObservable(key));
}
