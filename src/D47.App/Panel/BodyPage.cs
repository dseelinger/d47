using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;
using D47.App.Controls;
using D47.App.Theming;
using D47.Core.Journal;
using D47.Core.Knowledge;

namespace D47.App.Panel;

/// <summary>Navigation › On this body: the body's signals, the genus being sampled and how far from the last sample (#555).</summary>
public sealed class BodyPage : UserControl
{
    public const string RootKey = "routing.body";

    /// <summary>The footer's phrase; it carries a <c>get_body_biology</c> keyword.</summary>
    public const string Phrase = "what is the biology on this body";

    public const string Empty = "Map a body or take a sample on one, and this page shows what is on it.";

    public const string Measured = "Measured from the game's position as you walk.";

    public const string NoColonyDistance =
        "D47 has no colony distance for this genus, so it can't say when you're far enough.";

    public const string NoPosition = "The game is giving no position. It gives one on or near the surface.";

    public const string Idle = "Nothing is being sampled on this body.";

    private const double SurfaceGravity = 9.80665;

    private readonly Func<CommanderGameState?> _state;
    private readonly Func<GameStatus>? _status;
    private readonly Func<long, int, long?>? _worthIfMapped;
    private readonly JournalClock _clock;
    private readonly StackPanel _body = new();
    private readonly Border _distance = new();

    private Snapshot? _seen;
    private Distance? _distanceSeen;

    public BodyPage(Func<CommanderGameState?> state, Func<GameStatus>? status, Func<long, int, long?>? worthIfMapped)
    {
        _state = state;
        _status = status;
        _worthIfMapped = worthIfMapped;
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

    /// <summary>
    /// Redraws the page when the journal has changed what it shows, and the distance block alone when the
    /// game's position has moved it on.
    /// </summary>
    public bool Tick()
    {
        var changed = _clock.Tick();
        var now = Take();

        if (now != _seen)
        {
            Draw();
            return true;
        }

        if (Measure(now) != _distanceSeen)
        {
            DrawDistance(now);
            return true;
        }

        return changed;
    }

    /// <summary>A distance as the page prints it: <c>340 M</c>.</summary>
    public static string Metres(double metres) =>
        Math.Round(metres).ToString("N0", CultureInfo.InvariantCulture) + " M";

    /// <summary>What the page draws from; the page redraws in full when this changes.</summary>
    private sealed record Snapshot(
        Target? Target,
        BodyBiology? Signals,
        BodyScan? Scan,
        BodySampling? Sampling,
        long? Worth);

    private readonly record struct Target(long System, int Body, string? Name);

    /// <summary>What the distance block shows: the rounded metres, or null with no position.</summary>
    private readonly record struct Distance(long? Metres, int? Colony, bool Sampling);

    private Snapshot Take()
    {
        var state = _state();

        if (state is null || Find(state) is not { } target)
        {
            return new Snapshot(null, null, null, null, null);
        }

        var signals = state.Bodies.All.FirstOrDefault(body => body.SystemAddress == target.System && body.BodyId == target.Body);

        return new Snapshot(
            target,
            signals,
            state.Scans.For(target.System, target.Body),
            state.Sampling.On(target.System, target.Body),
            signals is { Source: BodySignalSource.SurfaceScan } ? null : _worthIfMapped?.Invoke(target.System, target.Body));
    }

    /// <summary>The body the Commander is at, else the one last sampled, else the last one mapped with biology.</summary>
    private static Target? Find(CommanderGameState state)
    {
        var location = state.Location;

        if (location is { SystemAddress: { } system, BodyId: { } body }
            && (state.Sampling.On(system, body) is not null
                || state.Scans.For(system, body) is not null
                || state.Bodies.All.Any(signals => signals.SystemAddress == system && signals.BodyId == body)))
        {
            return new Target(system, body, location.Body);
        }

        if (state.Sampling.MostRecent is { } sampled)
        {
            return new Target(sampled.SystemAddress, sampled.BodyId, null);
        }

        return state.Bodies.MostRecentWithBiology is { SystemAddress: { } mappedSystem, BodyId: { } mappedBody } mapped
            ? new Target(mappedSystem, mappedBody, mapped.BodyName)
            : null;
    }

    private static GenusProgress? Current(Snapshot snapshot) => snapshot.Sampling?.InProgress.FirstOrDefault();

    private Distance Measure(Snapshot snapshot)
    {
        if (Current(snapshot) is not { } genus)
        {
            return new Distance(null, null, false);
        }

        var colony = ExobiologyCatalogue.ColonyDistance(genus.Genus);
        var live = _status?.Invoke();
        var name = BodyName(snapshot);

        var here = live is { HasPosition: true, PlanetRadius: { } radius }
                   && (live.BodyName is null || name is null || string.Equals(live.BodyName, name, StringComparison.OrdinalIgnoreCase))
            ? new SurfaceFix(live.Latitude!.Value, live.Longitude!.Value, radius)
            : (SurfaceFix?)null;

        var metres = genus.LastAt is { } last && here is { } now ? (long?)Math.Round(last.MetresTo(now)) : null;

        return new Distance(metres, colony, true);
    }

    private static string? BodyName(Snapshot snapshot) =>
        snapshot.Signals?.BodyName ?? snapshot.Scan?.BodyName ?? snapshot.Target?.Name;

    private void Draw()
    {
        var snapshot = Take();
        _seen = snapshot;

        _body.Children.Clear();

        if (snapshot.Target is null)
        {
            _body.Children.Add(TitleText.Block(
                TitleText.Build("On this body", TypeScale.Title, TitleRank.Screen),
                TitleText.Context("On this body · your scan")));
            _body.Children.Add(Note(Empty));
            _distanceSeen = null;
            return;
        }

        var title = TitleText.Build(BodyName(snapshot) ?? "This body", TypeScale.Title, TitleRank.Screen);
        Themed(title, TextBlock.ForegroundProperty, ThemeManager.CyanKey);

        _body.Children.Add(TitleText.Block(
            title,
            TitleText.Context("On this body · your scan"),
            snapshot.Worth is { } worth ? WorthIfMapped(worth) : null));

        _body.Children.Add(Data(snapshot));

        _body.Children.Add(Group(
            "Sampling",
            Current(snapshot) is { } genus ? $"{genus.Species ?? genus.Genus} · the one you're sampling now." : Idle));

        if (Current(snapshot) is { } sampling)
        {
            var cells = SampleCells(sampling, ready: false);
            cells.Name = "samples";
            _body.Children.Add(cells);

            _distance.Margin = new Thickness(0, 10, 0, 0);
            _body.Children.Add(_distance);
            DrawDistance(snapshot);
        }
        else
        {
            _distanceSeen = null;
        }

        _body.Children.Add(Group("On this body", "Each genus, and what you've finished."));
        _body.Children.Add(Genera(snapshot));
    }

    private void DrawDistance(Snapshot snapshot)
    {
        var distance = Measure(snapshot);
        _distanceSeen = distance;

        if (Current(snapshot) is not { } genus)
        {
            return;
        }

        var far = distance is { Metres: { } metres, Colony: { } colony } && metres >= colony;

        // SAMPLE n · READY is the one cell that follows the position, so the row is redrawn with the block.
        if (_body.Children.OfType<Grid>().FirstOrDefault(grid => grid.Name == "samples") is { } row)
        {
            var index = _body.Children.IndexOf(row);
            var cells = SampleCells(genus, far);
            cells.Name = "samples";
            _body.Children[index] = cells;
        }

        var label = Caption("Distance since the last sample", ThemeManager.GreyKey);

        var (stateText, stateKey) = distance switch
        {
            { Colony: null } => ("Colony distance not known", ThemeManager.GreyKey),
            { Metres: null } => ("No position", ThemeManager.GreyKey),
            _ when far => ($"✓ Far enough · take the {Ordinal(genus.Taken + 1)} sample", ThemeManager.BlueKey),
            { Metres: { } walked, Colony: { } needed } => ($"Keep walking · {Metres(needed - walked)} to go", ThemeManager.AKey),
        };

        var state = Caption(stateText, stateKey);
        state.HorizontalAlignment = HorizontalAlignment.Right;
        state.FontWeight = FontWeight.SemiBold;
        Grid.SetColumn(state, 1);

        var block = new StackPanel
        {
            Spacing = 10,
            Children =
            {
                new Grid
                {
                    ColumnDefinitions = [new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)],
                    ColumnSpacing = 16,
                    Children = { label, state },
                },
            },
        };

        if (distance.Metres is { } shown)
        {
            var number = new TextBlock
            {
                Text = Metres(shown),
                FontFamily = new FontFamily(Fonts.MonoFamily),
                FontSize = TypeScale.Reading,
                VerticalAlignment = VerticalAlignment.Bottom,
            };
            Themed(number, TextBlock.ForegroundProperty, far ? ThemeManager.BlueKey : ThemeManager.WhiteKey);

            var figure = new WrapPanel { Children = { number } };

            if (distance.Colony is { } threshold)
            {
                var of = new TextBlock
                {
                    Text = $"OF {Metres(threshold)} FOR A NEW COLONY",
                    FontFamily = new FontFamily(Fonts.MonoFamily),
                    FontSize = TypeScale.Small,
                    Margin = new Thickness(12, 0, 0, 6),
                    VerticalAlignment = VerticalAlignment.Bottom,
                };
                Themed(of, TextBlock.ForegroundProperty, ThemeManager.GreyKey);
                figure.Children.Add(of);
            }

            block.Children.Add(figure);

            if (distance.Colony is { } needed)
            {
                block.Children.Add(Gauge.Track((double)shown / needed, far ? ThemeManager.BlueKey : ThemeManager.AKey));
            }
        }
        else
        {
            var none = new TextBlock { Text = NoPosition, FontSize = TypeScale.Secondary, TextWrapping = TextWrapping.Wrap };
            Themed(none, TextBlock.ForegroundProperty, ThemeManager.WhiteKey);
            block.Children.Add(none);
        }

        var hint = new TextBlock
        {
            Text = distance.Colony is null ? $"{Measured} {NoColonyDistance}" : Measured,
            FontSize = TypeScale.Small,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 10, 0, 0),
        };
        Themed(hint, TextBlock.ForegroundProperty, ThemeManager.GreyKey);

        var slab = new Border { Padding = new Thickness(14, 12), Child = block };
        Themed(slab, Border.BackgroundProperty, ThemeManager.SlabKey);

        _distance.Child = new StackPanel { Children = { slab, hint } };
    }

    private static string Ordinal(int n) => n switch
    {
        1 => "first",
        2 => "second",
        _ => "third",
    };

    private static Control WorthIfMapped(long worth)
    {
        var name = Caption("Unmapped · worth if mapped", ThemeManager.GreyKey);
        name.HorizontalAlignment = HorizontalAlignment.Right;

        var figure = new TextBlock
        {
            FontFamily = new FontFamily(Fonts.MonoFamily),
            FontSize = TypeScale.Figure,
            HorizontalAlignment = HorizontalAlignment.Right,
            Text = "~" + SessionPage.Credits(worth),
        };
        Themed(figure, TextBlock.ForegroundProperty, ThemeManager.AKey);

        return new StackPanel { Spacing = 2, Children = { name, figure } };
    }

    private static Control Data(Snapshot snapshot)
    {
        var signals = snapshot.Signals;

        var genera = Names(snapshot);

        var others = signals?.Signals
            .Where(signal => !signal.Type.Contains("Biological", StringComparison.OrdinalIgnoreCase))
            .ToList() ?? [];

        var otherLine = new TextBlock
        {
            FontFamily = Fonts.ChromeFamily,
            FontSize = StatTile.ValueSize,
            FontWeight = FontWeight.Medium,
            TextWrapping = TextWrapping.Wrap,
        };

        if (others.Count == 0)
        {
            otherLine.Text = signals is null ? "—" : "None";
            Themed(otherLine, TextBlock.ForegroundProperty, ThemeManager.WhiteKey);
        }
        else
        {
            var inlines = new InlineCollection();

            for (var i = 0; i < others.Count; i++)
            {
                var name = new Run((i > 0 ? " · " : string.Empty) + others[i].Type.ToUpperInvariant() + " ");
                Themed(name, TextElement.ForegroundProperty, ThemeManager.WhiteKey);
                var count = new Run(others[i].Count.ToString(CultureInfo.InvariantCulture));
                Themed(count, TextElement.ForegroundProperty, ThemeManager.AKey);
                inlines.Add(name);
                inlines.Add(count);
            }

            otherLine.Inlines = inlines;
        }

        var gravity = snapshot.Scan?.SurfaceGravity is { } metresPerSecond
            ? (metresPerSecond / SurfaceGravity).ToString("0.00", CultureInfo.InvariantCulture) + " G"
            : "—";

        var tiles = new (Control Tile, int Row, int Column, int Span)[]
        {
            (StatTile.Build(
                "Biological signals",
                signals is null ? "—" : signals.BiologicalCount.ToString(CultureInfo.InvariantCulture),
                StatInk.Number), 0, 0, 1),
            (StatTile.Build(
                "Genera",
                genera.Count == 0 ? "Not yet known" : string.Join(" · ", genera.Select(genus => genus.ToUpperInvariant())),
                StatInk.Name), 0, 1, 2),
            (Tile("Other signals", otherLine), 1, 0, 2),
            (StatTile.Build("Gravity", gravity, StatInk.Number), 1, 2, 1),
        };

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,*,*"),
            RowDefinitions = new RowDefinitions("Auto,Auto"),
            ColumnSpacing = StatTile.Gap,
            RowSpacing = StatTile.Gap,
            Margin = new Thickness(0, 28, 0, 0),
        };

        foreach (var (tile, row, column, span) in tiles)
        {
            Grid.SetRow(tile, row);
            Grid.SetColumn(tile, column);
            Grid.SetColumnSpan(tile, span);
            tile.VerticalAlignment = VerticalAlignment.Stretch;
            grid.Children.Add(tile);
        }

        return grid;
    }

    /// <summary>Every genus on the body: the ones the signals name, then any sampled that they do not.</summary>
    private static List<string> Names(Snapshot snapshot)
    {
        var names = new List<string>(snapshot.Signals?.Genera ?? []);

        foreach (var genus in snapshot.Sampling?.Genera.Keys ?? [])
        {
            if (!names.Contains(genus, StringComparer.OrdinalIgnoreCase))
            {
                names.Add(genus);
            }
        }

        return names;
    }

    private static Grid SampleCells(GenusProgress genus, bool ready)
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,*,*"),
            ColumnSpacing = Gaps.Tile,
        };

        for (var n = 1; n <= GenusProgress.Required; n++)
        {
            var taken = n <= genus.Taken;
            var next = n == genus.Taken + 1;

            var (text, key) = taken ? ($"Sample {n} ✓", ThemeManager.BlueKey)
                : next && ready ? ($"Sample {n} · ready", ThemeManager.CyanKey)
                : ($"Sample {n}", ThemeManager.GreyKey);

            var label = Caption(text, key);
            label.FontWeight = FontWeight.SemiBold;
            label.HorizontalAlignment = HorizontalAlignment.Center;
            label.VerticalAlignment = VerticalAlignment.Center;

            var cell = new Border { Height = TypeScale.MinimumTarget, Child = label };
            Themed(cell, Border.BackgroundProperty, ThemeManager.SlabKey);
            Grid.SetColumn(cell, n - 1);
            grid.Children.Add(cell);
        }

        return grid;
    }

    private static Control Genera(Snapshot snapshot)
    {
        var sampling = snapshot.Sampling;
        var rows = new StackPanel { Spacing = 2 };

        foreach (var genus in sampling?.Completed ?? [])
        {
            rows.Children.Add(GenusRow(genus.Genus, genus.Species, ThemeManager.WhiteKey, $"✓ Done · {genus.Taken} / {GenusProgress.Required}", ThemeManager.BlueKey, genus.Species));
        }

        foreach (var genus in sampling?.InProgress ?? [])
        {
            rows.Children.Add(GenusRow(genus.Genus, genus.Species, ThemeManager.CyanKey, $"Sampling · {genus.Taken} / {GenusProgress.Required}", ThemeManager.CyanKey, genus.Species));
        }

        foreach (var genus in Names(snapshot).Where(name => sampling?.Genera.ContainsKey(name) != true))
        {
            rows.Children.Add(GenusRow(genus, null, ThemeManager.GreyKey, "Not started", ThemeManager.GreyKey, null));
        }

        return rows.Children.Count > 0 ? rows : Note("No genus is known on this body yet. Map it with the surface scanner, or take a sample.");
    }

    private static Control GenusRow(string genus, string? species, string speciesKey, string state, string stateKey, string? priced)
    {
        var name = Caption(genus, ThemeManager.WhiteKey);
        name.FontWeight = FontWeight.Bold;
        name.VerticalAlignment = VerticalAlignment.Center;
        name.TextTrimming = TextTrimming.CharacterEllipsis;

        var kind = new TextBlock
        {
            Text = species ?? "Not yet sampled",
            FontFamily = Fonts.ProseFamily,
            FontSize = TypeScale.Body,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        Themed(kind, TextBlock.ForegroundProperty, speciesKey);
        Grid.SetColumn(kind, 1);

        var progress = Caption(state, stateKey);
        progress.FontWeight = FontWeight.SemiBold;
        progress.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(progress, 2);

        var row = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("150,*,150,150"),
            Children = { name, kind, progress },
        };

        if (priced is not null && Value(priced) is { } value)
        {
            var figure = new TextBlock
            {
                Text = SessionPage.Credits(value),
                FontFamily = new FontFamily(Fonts.MonoFamily),
                FontSize = TypeScale.Small,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
            };
            Themed(figure, TextBlock.ForegroundProperty, ThemeManager.AKey);
            Grid.SetColumn(figure, 3);
            row.Children.Add(figure);
        }

        var slab = new Border { Height = TypeScale.MinimumTarget, Padding = new Thickness(14, 0), Child = row };
        Themed(slab, Border.BackgroundProperty, ThemeManager.SlabKey);

        return slab;
    }

    private static long? Value(string species) =>
        ExobiologyCatalogue.All
            .FirstOrDefault(entry => string.Equals(entry.Species, species, StringComparison.OrdinalIgnoreCase))
            ?.Value;

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

    private static TextBlock Note(string text)
    {
        var note = new TextBlock
        {
            Text = text,
            FontSize = TypeScale.Small,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 18, 0, 0),
        };
        Themed(note, TextBlock.ForegroundProperty, ThemeManager.GreyKey);
        return note;
    }

    private static void Themed(StyledElement target, AvaloniaProperty property, string key) =>
        target.Bind(property, target.GetResourceObservable(key));
}
