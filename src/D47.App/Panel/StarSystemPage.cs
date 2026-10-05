using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using D47.App.Controls;
using D47.App.Theming;
using D47.Core.Journal;
using D47.Core.Knowledge;

namespace D47.App.Panel;

/// <summary>What the Search tab's System page reads.</summary>
/// <param name="Systems">Where a system's record comes from.</param>
/// <param name="Here">Where the Commander is, from the journal.</param>
/// <param name="LookupsEnabled">Whether the galaxy setting is on.</param>
/// <param name="OpenSettings">A way to the row that turns it on, where the surface has one.</param>
public sealed record StarSystemSurface(
    IStarSystemService Systems,
    Func<JournalLocation?> Here,
    Func<bool> LookupsEnabled,
    Action? OpenSettings = null);

/// <summary>
/// One star system's record from Spansh: the Commander's own while following them, or any system typed or
/// opened.
/// </summary>
public sealed class StarSystemPage : UserControl, IPageSummary
{
    public const string RootKey = "search.system";

    public const string SummaryText = "A star system's record, as Spansh last had it.";

    public const string NoJournal =
        "D47 hasn't read a journal yet, so it doesn't know which system you're in. Type a system's name to look it up.";

    public const string Unrecorded =
        "Spansh has no record of this system yet. It has one once a Commander’s game reports the system to the galaxy databases.";

    public const string AsksAgain =
        "D47 asks again when you open a system, or when you arrive somewhere new while it follows you.";

    public const double FieldWidth = 460;

    public const string Unreadable = "Spansh's answer could not be read.";

    /// <summary>The most close names listed under the field.</summary>
    public const int CloseNames = 5;

    private const string Dash = "—";

    private readonly StarSystemSurface _surface;
    private readonly StackPanel _body = new() { Spacing = 18 };
    private readonly TextBox _name;

    private bool _following = true;
    private bool _redrawn;
    private bool _seen;
    private bool _seenEnabled;
    private long? _seenAddress;
    private string? _seenName;

    private Shown? _opened;
    private View _view;
    private StarSystemProfile? _profile;
    private string? _failure;
    private string _typed = string.Empty;
    private IReadOnlyList<SystemNameMatch> _matches = [];
    private CancellationTokenSource? _inFlight;

    public StarSystemPage(StarSystemSurface surface)
    {
        _surface = surface;

        _name = new TextBox
        {
            PlaceholderText = "Type any system name",
            Width = FieldWidth,
            HorizontalAlignment = HorizontalAlignment.Left,
            InnerLeftContent = Prefix("System"),
        };
        Avalonia.Automation.AutomationProperties.SetName(_name, "System name");
        _name.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                Look(_name.Text ?? string.Empty);
            }
        };

        Content = new ScrollViewer
        {
            Padding = new Thickness(14, 18, 14, 24),
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            Content = _body,
        };

        Tick();
    }

    private enum View
    {
        Off,
        NoJournal,
        Loading,
        Ready,
        Unavailable,
        Unrecorded,
        Suggest,
        NoMatch,
    }

    private sealed record Shown(long Address, string Name);

    public string Summary => SummaryText;

    event EventHandler? IPageSummary.SummaryChanged
    {
        add { }
        remove { }
    }

    /// <summary>Whether the page follows the Commander's own system.</summary>
    public bool Following => _following;

    /// <summary>
    /// Fetches the Commander's new system when they have moved and the page follows them. Returns at once;
    /// true when the page redrew since the last call, an answer arriving included. Call on the UI thread.
    /// </summary>
    public bool Tick()
    {
        Check();

        var redrawn = _redrawn;
        _redrawn = false;

        return redrawn;
    }

    private void Check()
    {
        var enabled = _surface.LookupsEnabled();
        var here = _surface.Here();
        var address = here?.SystemAddress;
        var name = here?.StarSystem;

        if (_seen && enabled == _seenEnabled && address == _seenAddress && name == _seenName)
        {
            return;
        }

        var switchedOn = !_seen || enabled != _seenEnabled;
        var moved = !_seen || address != _seenAddress;

        _seen = true;
        _seenEnabled = enabled;
        _seenAddress = address;
        _seenName = name;

        if (!enabled)
        {
            Cancel();
            _view = View.Off;
        }
        else if (switchedOn && !_following && _opened is { } opened)
        {
            Open(opened.Address, opened.Name, following: false);
            return;
        }
        else if (switchedOn || (_following && moved))
        {
            _following = true;
            Follow();
            return;
        }

        Draw();
    }

    private void BackToMySystem()
    {
        _name.Text = string.Empty;
        _following = true;
        Follow();
    }

    /// <summary>Resolves a typed name: an exact match opens; otherwise the close names, or none.</summary>
    private void Look(string typed)
    {
        typed = typed.Trim();

        if (typed.Length == 0 || !_surface.LookupsEnabled())
        {
            return;
        }

        var request = Restart();

        _following = false;
        _ = Resolve(typed, request);
    }

    private void Follow()
    {
        if (_seenAddress is { } address)
        {
            Open(address, _seenName ?? address.ToString(CultureInfo.InvariantCulture), following: true);
            return;
        }

        Cancel();
        _opened = null;
        _view = View.NoJournal;
        Draw();
    }

    private void Open(long address, string name, bool following)
    {
        _following = following;
        _opened = new Shown(address, name);
        _profile = null;
        _failure = null;
        _matches = [];
        _view = View.Loading;

        var request = Restart();

        Draw();

        _ = Fetch(address, request);
    }

    private async Task Fetch(long address, CancellationTokenSource request)
    {
        var token = request.Token;
        StarSystemProfile? profile = null;
        string? failure = null;

        try
        {
            profile = await Task.Run(() => _surface.Systems.ProfileAsync(address, token), token)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (GalaxyUnavailableException ex)
        {
            failure = ex.Message;
        }
        catch (Exception)
        {
            failure = Unreadable;
        }

        Dispatcher.UIThread.Post(() =>
        {
            if (!Finish(request))
            {
                return;
            }

            _profile = profile;
            _failure = failure;
            _view = failure is not null ? View.Unavailable : profile is null ? View.Unrecorded : View.Ready;

            if (profile is not null && _opened is { } opened && opened.Address == address)
            {
                _opened = opened with { Name = profile.Name };
            }

            Draw();
        });
    }

    private async Task Resolve(string typed, CancellationTokenSource request)
    {
        var token = request.Token;
        IReadOnlyList<SystemNameMatch> matches = [];
        string? failure = null;

        try
        {
            matches = await Task.Run(() => _surface.Systems.MatchNamesAsync(typed, token), token)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (GalaxyUnavailableException ex)
        {
            failure = ex.Message;
        }
        catch (Exception)
        {
            failure = Unreadable;
        }

        Dispatcher.UIThread.Post(() =>
        {
            if (!Finish(request))
            {
                return;
            }

            _typed = typed;

            var exact = matches.FirstOrDefault(match => string.Equals(match.Name, typed, StringComparison.OrdinalIgnoreCase));

            if (failure is null && exact is not null)
            {
                Choose(exact);
                return;
            }

            _following = false;
            _opened = null;
            _profile = null;
            _failure = failure;
            _matches = [.. matches.Take(CloseNames)];
            _view = failure is not null ? View.Unavailable : _matches.Count > 0 ? View.Suggest : View.NoMatch;

            Draw();
        });
    }

    private void Choose(SystemNameMatch match)
    {
        if (match.SystemAddress == _seenAddress)
        {
            BackToMySystem();
            return;
        }

        _name.Text = match.Name;
        Open(match.SystemAddress, match.Name, following: false);
    }

    private CancellationTokenSource Restart()
    {
        Cancel();

        return _inFlight = new CancellationTokenSource();
    }

    private void Cancel()
    {
        _inFlight?.Cancel();
        _inFlight?.Dispose();
        _inFlight = null;
    }

    /// <summary>True when <paramref name="request"/> is still the latest; it is then retired.</summary>
    private bool Finish(CancellationTokenSource request)
    {
        if (!ReferenceEquals(request, _inFlight))
        {
            return false;
        }

        _inFlight = null;
        request.Dispose();

        return true;
    }

    private void Draw()
    {
        _redrawn = true;
        _body.Children.Clear();

        if (_view == View.Off)
        {
            _body.Children.Add(RoutingKit.SwitchedOff(
                "System lookups are off",
                "Looking up a star system is switched off. It shares the galaxy search setting, so turning on "
                + "“Look things up in the galaxy” switches it on.",
                _surface.OpenSettings));
            return;
        }

        _body.Children.Add(FieldRow());

        switch (_view)
        {
            case View.NoJournal:
                _body.Children.Add(Sentence(NoJournal, ThemeManager.GreyKey));
                break;

            case View.NoMatch:
                _body.Children.Add(Sentence($"Spansh has no system called “{_typed}”, and none with a close name.", ThemeManager.WhiteKey));
                break;

            case View.Loading:
                _body.Children.Add(Heading(null));
                _body.Children.Add(Sentence(
                    $"Asking Spansh for {_opened?.Name}. A populated system takes about two seconds.", ThemeManager.WhiteKey));
                break;

            case View.Unavailable:
                if (_opened is not null)
                {
                    _body.Children.Add(Heading(null));
                }

                _body.Children.Add(new Notice
                {
                    Label = "Spansh didn't answer",
                    Text = $"{_failure} {AsksAgain}",
                    Detail = D47.Knowledge.SpanshStarSystemService.Host,
                    MaxWidth = 760,
                    HorizontalAlignment = HorizontalAlignment.Left,
                });
                break;

            case View.Unrecorded:
                _body.Children.Add(Heading(null));
                _body.Children.Add(Sentence(Unrecorded, ThemeManager.WhiteKey));
                break;

            case View.Ready when _profile is { } profile:
                _body.Children.Add(Heading(profile.ReportedAt));
                _body.Children.Add(Overview(profile));
                break;
        }
    }

    private Control FieldRow()
    {
        if (_name.Parent is Avalonia.Controls.Panel holder)
        {
            holder.Children.Remove(_name);
        }

        var column = new StackPanel { Spacing = Gaps.Tile, Width = FieldWidth, HorizontalAlignment = HorizontalAlignment.Left };
        column.Children.Add(_name);

        if (_view == View.Suggest)
        {
            column.Children.Add(Suggestions());
        }

        var row = new DockPanel { LastChildFill = false };

        if (!_following && _seenName is { } home)
        {
            var back = new Button
            {
                Content = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 8,
                    Children =
                    {
                        new TextBlock { Text = "BACK TO MY SYSTEM" },
                        Inked(new TextBlock { Text = home.ToUpperInvariant() }, ThemeManager.CyanKey),
                    },
                },
                VerticalAlignment = VerticalAlignment.Top,
            };
            Avalonia.Automation.AutomationProperties.SetName(back, $"Back to my system, {home}");
            back.Click += (_, _) => BackToMySystem();

            DockPanel.SetDock(back, Dock.Right);
            row.Children.Add(back);
        }

        DockPanel.SetDock(column, Dock.Left);
        row.Children.Add(column);

        return row;
    }

    private Border Suggestions()
    {
        var list = new StackPanel { Spacing = Gaps.Tile };

        var hint = RoutingKit.Ink(
            $"No system is called “{_typed}”. Spansh has these close names.", TypeScale.Small, ThemeManager.GreyKey, wrap: true);
        hint.Margin = new Thickness(10, 8, 10, 6);
        list.Children.Add(hint);

        foreach (var match in _matches)
        {
            var name = new TextBlock
            {
                Text = match.Name,
                FontFamily = new FontFamily(Fonts.ChromeFamily),
                FontSize = TypeScale.ControlLarge,
                FontWeight = FontWeight.Medium,
            };
            RoutingKit.Themed(name, TextBlock.ForegroundProperty, ThemeManager.AKey);

            var option = new Button
            {
                Content = name,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Left,
            };
            Avalonia.Automation.AutomationProperties.SetName(option, match.Name);
            option.Click += (_, _) => Choose(match);
            list.Children.Add(option);
        }

        var box = new Border { Padding = new Thickness(2), BorderThickness = new Thickness(1), Child = list };
        RoutingKit.Themed(box, Border.BackgroundProperty, ThemeManager.BarKey);
        RoutingKit.Themed(box, Border.BorderBrushProperty, ThemeManager.AKey);

        return box;
    }

    /// <summary>The context line, the system's name and, once read, when Spansh last heard of it.</summary>
    private Control Heading(DateTimeOffset? reportedAt)
    {
        var ink = _following ? ThemeManager.CyanKey : ThemeManager.AKey;

        var context = TitleText.Context(_following ? "Your system · following you" : "Star system");
        RoutingKit.Themed(context, TextBlock.ForegroundProperty, ink);

        var title = TitleText.Style(
            new SelectableTextBlock { TextWrapping = TextWrapping.Wrap },
            TypeScale.Heading,
            TitleRank.Screen);
        TitleText.Show(title, _opened?.Name ?? string.Empty);
        RoutingKit.Themed(title, TextBlock.ForegroundProperty, ink);

        return TitleText.Block(title, context, reportedAt is { } at ? Reported(at) : null);
    }

    private static StackPanel Reported(DateTimeOffset at)
    {
        var label = Caption("Last report to Spansh", ThemeManager.GreyKey);
        label.HorizontalAlignment = HorizontalAlignment.Right;

        var value = new TextBlock
        {
            Text = at.UtcDateTime.ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture),
            FontFamily = new FontFamily(Fonts.MonoFamily),
            FontSize = TypeScale.Body,
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        RoutingKit.Themed(value, TextBlock.ForegroundProperty, ThemeManager.WhiteKey);

        return new StackPanel { Spacing = 2, Children = { label, value } };
    }

    private static StackPanel Overview(StarSystemProfile profile)
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,*,*,*"),
            RowDefinitions = new RowDefinitions("Auto,Auto,Auto"),
            ColumnSpacing = Gaps.Tile,
            RowSpacing = Gaps.Tile,
        };

        Place(grid, 0, 0, 1, StatTile.Build("Economy", Economy(profile)));
        Place(grid, 0, 1, 1, StatTile.Build("Government", profile.Government ?? Dash));
        Place(grid, 0, 2, 1, StatTile.Build("Allegiance", profile.Allegiance ?? Dash));
        Place(grid, 0, 3, 1, StatTile.Build("Security", profile.Security ?? Dash));

        Place(grid, 1, 0, 1, StatTile.Build(
            "Population",
            profile.Population is { } people ? people.ToString("N0", CultureInfo.InvariantCulture) : Dash,
            StatInk.Number));
        Place(grid, 1, 1, 2, ControllingFaction(profile));
        Place(grid, 1, 3, 1, StatTile.Build("Permit", profile.NeedsPermit ? "Needed" : "Not needed"));

        Place(grid, 2, 0, 1, StatTile.Build("Main star", MainStar(profile)));
        Place(grid, 2, 1, 2, StatTile.Build("Coordinates", Coordinates(profile.Position), StatInk.Number));
        Place(grid, 2, 3, 1, StatTile.Build(
            "Bodies", profile.Bodies.Count.ToString(CultureInfo.InvariantCulture), StatInk.Number));

        return new StackPanel { Spacing = 24, Children = { grid, Factions(profile) } };
    }

    private static void Place(Grid grid, int row, int column, int span, Control cell)
    {
        cell.VerticalAlignment = VerticalAlignment.Stretch;
        Grid.SetRow(cell, row);
        Grid.SetColumn(cell, column);
        Grid.SetColumnSpan(cell, span);
        grid.Children.Add(cell);
    }

    /// <summary>"Industrial / Refinery", or the primary alone.</summary>
    public static string Economy(StarSystemProfile profile) =>
        (profile.PrimaryEconomy, profile.SecondaryEconomy) switch
        {
            (null, _) => Dash,
            ({ } primary, null or "" or "None") => primary,
            ({ } primary, { } secondary) => $"{primary} / {secondary}",
        };

    /// <summary>"K7 V · Scoopable": the main star's class, luminosity and whether a scoop works on it.</summary>
    public static string MainStar(StarSystemProfile profile)
    {
        var star = profile.Bodies.FirstOrDefault(body => body.IsMainStar)
            ?? profile.Bodies.Where(body => body.Type == "Star").OrderBy(body => body.BodyId).FirstOrDefault();

        if (star is null)
        {
            return Dash;
        }

        var kind = star.SpectralClass is { } spectral
            ? $"{spectral} {star.Luminosity}".TrimEnd()
            : star.SubType ?? Dash;

        return star.Scoopable switch
        {
            true => $"{kind} · Scoopable",
            false => $"{kind} · Not scoopable",
            null => kind,
        };
    }

    /// <summary>"7.97 / −41.91 / 77.88", with true minus signs.</summary>
    public static string Coordinates(StarPosition? position) =>
        position is { } at ? $"{Axis(at.X)} / {Axis(at.Y)} / {Axis(at.Z)}" : Dash;

    private static string Axis(double value) =>
        value.ToString("0.00", CultureInfo.InvariantCulture).Replace('-', '−');

    private static Border ControllingFaction(StarSystemProfile profile)
    {
        var line = new WrapPanel { ItemSpacing = 8, Orientation = Orientation.Horizontal };

        var name = new TextBlock
        {
            Text = profile.ControllingFaction ?? Dash,
            FontFamily = new FontFamily(Fonts.ChromeFamily),
            FontSize = StatTile.ValueSize,
            FontWeight = FontWeight.Medium,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
        };
        RoutingKit.Themed(name, TextBlock.ForegroundProperty, ThemeManager.WhiteKey);
        line.Children.Add(name);

        var controlling = profile.Factions.FirstOrDefault(faction =>
            string.Equals(faction.Name, profile.ControllingFaction, StringComparison.OrdinalIgnoreCase));

        if (controlling?.ActiveStates is { Count: > 0 } states)
        {
            line.Children.Add(RoutingKit.Tag($"· In {string.Join(", ", states)}", ThemeManager.AKey));
        }

        var tile = new Border
        {
            Padding = new Thickness(14, 10),
            Child = new StackPanel { Spacing = 2, Children = { Caption("Controlling faction", ThemeManager.GreyKey), line } },
        };
        RoutingKit.Themed(tile, Border.BackgroundProperty, ThemeManager.SlabKey);

        return tile;
    }

    private const string FactionColumns = "*,130,120,200,150,150";

    private static StackPanel Factions(StarSystemProfile profile)
    {
        var rows = new StackPanel { Spacing = Gaps.Tile };

        var header = FactionGrid();
        header.Height = 28;
        header.Margin = new Thickness(14, 0);
        string[] titles = ["Faction", "Government", "Allegiance", "Influence", "Active", "Pending"];

        for (var i = 0; i < titles.Length; i++)
        {
            var title = Caption(titles[i], ThemeManager.GreyKey);
            title.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(title, i);
            header.Children.Add(title);
        }

        rows.Children.Add(header);

        foreach (var faction in profile.Factions)
        {
            rows.Children.Add(FactionRow(
                faction,
                string.Equals(faction.Name, profile.ControllingFaction, StringComparison.OrdinalIgnoreCase)));
        }

        var count = profile.Factions.Count;

        return new StackPanel
        {
            Spacing = 10,
            Children =
            {
                GroupHead("Minor factions", "Most influence first.", $"{count} {(count == 1 ? "faction" : "factions")}"),
                rows,
            },
        };
    }

    private static Grid FactionGrid() => new()
    {
        ColumnDefinitions = new ColumnDefinitions(FactionColumns),
        ColumnSpacing = 16,
    };

    private static Border FactionRow(FactionStanding faction, bool controls)
    {
        var grid = FactionGrid();
        grid.VerticalAlignment = VerticalAlignment.Center;

        var name = new WrapPanel { ItemSpacing = 10, VerticalAlignment = VerticalAlignment.Center };
        var named = new TextBlock
        {
            Text = faction.Name,
            FontFamily = new FontFamily(Fonts.ChromeFamily),
            FontSize = TypeScale.ControlLarge,
            FontWeight = FontWeight.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
        };
        RoutingKit.Themed(named, TextBlock.ForegroundProperty, ThemeManager.WhiteKey);
        name.Children.Add(named);

        if (controls)
        {
            var tag = RoutingKit.Tag("Controls", ThemeManager.AKey);
            tag.FontSize = TypeScale.MetaSmall;
            name.Children.Add(tag);
        }

        Cell(grid, 0, name);
        Cell(grid, 1, Word(faction.Government ?? Dash));
        Cell(grid, 2, Word(faction.Allegiance ?? Dash));
        Cell(grid, 3, Influence(faction.Influence));
        Cell(grid, 4, States(faction.ActiveStates, ThemeManager.AKey));
        Cell(grid, 5, States(faction.PendingStates, ThemeManager.GreyKey));

        var row = new Border
        {
            MinHeight = TypeScale.MinimumTarget,
            Padding = new Thickness(11, 0, 14, 0),
            BorderThickness = new Thickness(3, 0, 0, 0),
            Child = grid,
        };
        RoutingKit.Themed(row, Border.BackgroundProperty, ThemeManager.SlabKey);

        if (controls)
        {
            RoutingKit.Themed(row, Border.BorderBrushProperty, ThemeManager.AKey);
        }
        else
        {
            row.BorderBrush = Brushes.Transparent;
        }

        return row;
    }

    private static void Cell(Grid grid, int column, Control cell)
    {
        cell.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(cell, column);
        grid.Children.Add(cell);
    }

    private static TextBlock Word(string text)
    {
        var block = new TextBlock
        {
            Text = text,
            FontFamily = new FontFamily(Fonts.ChromeFamily),
            FontSize = TypeScale.ControlLarge,
            FontWeight = FontWeight.Medium,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        RoutingKit.Themed(block, TextBlock.ForegroundProperty, ThemeManager.AKey);

        return block;
    }

    /// <summary>"27.3%", one decimal, with a gauge of it.</summary>
    public static string InfluenceText(double? influence) =>
        influence is { } share ? (share * 100).ToString("0.0", CultureInfo.InvariantCulture) + "%" : Dash;

    private static DockPanel Influence(double? influence)
    {
        var figure = new TextBlock
        {
            Text = InfluenceText(influence),
            FontFamily = new FontFamily(Fonts.MonoFamily),
            FontSize = TypeScale.Small,
            Width = 52,
            TextAlignment = TextAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 10, 0),
        };
        RoutingKit.Themed(figure, TextBlock.ForegroundProperty, ThemeManager.WhiteKey);

        var track = Gauge.Track(influence ?? 0, ThemeManager.AKey);
        track.VerticalAlignment = VerticalAlignment.Center;

        var cell = new DockPanel();
        DockPanel.SetDock(figure, Dock.Left);
        cell.Children.Add(figure);
        cell.Children.Add(track);

        return cell;
    }

    private static TextBlock States(IReadOnlyList<string> states, string key)
    {
        var tag = RoutingKit.Tag(states.Count > 0 ? string.Join(", ", states) : Dash, states.Count > 0 ? key : ThemeManager.GreyKey);
        tag.TextWrapping = TextWrapping.Wrap;

        return tag;
    }

    private static StackPanel GroupHead(string name, string description, string count)
    {
        var heading = new TextBlock { VerticalAlignment = VerticalAlignment.Center };
        TitleText.Style(heading, TypeScale.Section, TitleRank.Group);
        TitleText.Show(heading, name);

        var note = RoutingKit.Ink(description, TypeScale.ControlLarge, ThemeManager.GreyKey);
        note.VerticalAlignment = VerticalAlignment.Center;
        note.Margin = new Thickness(12, 0, 0, 0);

        var counted = new TextBlock
        {
            Text = count.ToUpperInvariant(),
            FontFamily = new FontFamily(Fonts.MonoFamily),
            FontSize = TypeScale.Meta,
            VerticalAlignment = VerticalAlignment.Center,
        };
        RoutingKit.Themed(counted, TextBlock.ForegroundProperty, ThemeManager.GreyKey);

        var line = new DockPanel { MinHeight = 38 };
        DockPanel.SetDock(counted, Dock.Right);
        line.Children.Add(counted);
        line.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Children = { heading, note } });

        var rule = new Border { Height = 1, Margin = new Thickness(0, 6, 0, 0) };
        RoutingKit.Themed(rule, Border.BackgroundProperty, ThemeManager.AKey);

        return new StackPanel { Children = { line, rule } };
    }

    private static TextBlock Caption(string text, string key)
    {
        var block = new TextBlock
        {
            Text = text.ToUpperInvariant(),
            FontFamily = new FontFamily(Fonts.ChromeFamily),
            FontSize = TypeScale.Meta,
            FontWeight = FontWeight.Medium,
            LetterSpacing = TypeScale.Meta * Fonts.ChromeTracking,
        };
        RoutingKit.Themed(block, TextBlock.ForegroundProperty, key);

        return block;
    }

    private static TextBlock Prefix(string text)
    {
        var block = new TextBlock
        {
            Text = text.ToUpperInvariant(),
            FontFamily = new FontFamily(Fonts.ChromeFamily),
            FontSize = TypeScale.Control,
            FontWeight = FontWeight.SemiBold,
            LetterSpacing = TypeScale.Control * Fonts.ChromeTracking,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(12, 0, 0, 0),
        };
        RoutingKit.Themed(block, TextBlock.ForegroundProperty, ThemeManager.AKey);

        return block;
    }

    private static TextBlock Sentence(string text, string key)
    {
        var block = RoutingKit.Ink(text, TypeScale.ControlLarge, key, wrap: true);
        block.MaxWidth = 640;

        return block;
    }

    private static TextBlock Inked(TextBlock block, string key)
    {
        RoutingKit.Themed(block, TextBlock.ForegroundProperty, key);

        return block;
    }
}
