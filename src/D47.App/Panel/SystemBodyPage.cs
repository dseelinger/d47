using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using D47.App.Controls;
using D47.App.Theming;
using D47.Core.Capabilities.Builtin;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Knowledge;

namespace D47.App.Panel;

/// <summary>
/// One star or planet from a system's galaxy record, drilled from Search › System › Bodies or from another body's
/// page, and rebuilt from its crumb key alone.
/// </summary>
public sealed class SystemBodyPage : UserControl, IPageSummary
{
    public const string KeyPrefix = "search.system.body:";

    public const string SummaryText = "One body in a system, as last reported.";

    public const string ReadingText = "Reading the galaxy record.";

    public const string UnavailableLabel = "Galaxy record unavailable";

    public const string UnavailableText = "The galaxy record could not be read.";

    public const string MissingLabel = "Body not in the record";

    public const string MissingText = "The galaxy record no longer lists this body.";

    private const string Dash = "—";

    private readonly StarSystemSurface _surface;
    private readonly long _address;
    private readonly int _bodyId;
    private readonly string _word;
    private readonly BodyCodex _codex;
    private readonly Action<StarSystemProfile, BodyProfile>? _openBody;
    private readonly Action<StarSystemProfile, StationProfile>? _openStation;
    private readonly Action? _back;
    private readonly StackPanel _page = new() { Spacing = 18 };

    private StarSystemProfile? _profile;
    private string? _failure;
    private bool _loading;
    private bool _redrawn;
    private CancellationTokenSource? _inFlight;
    private Section _section;

    /// <param name="profile">The system's record when the page it was pressed on had read it; null fetches it.</param>
    /// <param name="openBody">Drills another body's page under this one.</param>
    /// <param name="back">Goes back to the crumb above.</param>
    public SystemBodyPage(
        StarSystemSurface surface,
        long address,
        int bodyId,
        string word,
        StarSystemProfile? profile = null,
        BodyCodex? codex = null,
        Action<StarSystemProfile, BodyProfile>? openBody = null,
        Action? back = null,
        Action<StarSystemProfile, StationProfile>? openStation = null)
    {
        _surface = surface;
        _address = address;
        _bodyId = bodyId;
        _word = word;
        _codex = codex ?? BodyCodex.None;
        _openBody = openBody;
        _back = back;
        _openStation = openStation;

        Content = new ScrollViewer
        {
            Padding = new Thickness(14, 18, 14, 24),
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            Content = _page,
        };

        if (profile is not null && profile.SystemAddress == address)
        {
            _profile = profile;
            Draw();
        }
        else
        {
            Fetch();
        }
    }

    /// <summary>The sections a body page can show, in the order the switch lists them.</summary>
    public enum Section
    {
        Overview,
        Composition,
        Orbit,
        Orbiting,
    }

    public string Summary => SummaryText;

    event EventHandler? IPageSummary.SummaryChanged
    {
        add { }
        remove { }
    }

    /// <summary>The body drawn, or null while the record is read or does not list it.</summary>
    public BodyProfile? Body => _profile?.Bodies.FirstOrDefault(body => body.BodyId == _bodyId);

    /// <summary>The sections the switch offers for the body drawn.</summary>
    public IReadOnlyList<Section> Sections => Body is { } body && _profile is { } profile ? SectionsOf(profile, body) : [];

    /// <summary>The section showing.</summary>
    public Section Showing => _section;

    public static string Key(long address, int bodyId) =>
        KeyPrefix + address.ToString(CultureInfo.InvariantCulture) + ":" + bodyId.ToString(CultureInfo.InvariantCulture);

    public static NavCrumb Crumb(long address, BodyProfile body) => Crumb(address, body.BodyId, body.Name);

    public static NavCrumb Crumb(long address, int bodyId, string name) =>
        new(Key(address, bodyId), name) { Help = GalaxyCapability.Id };

    public static bool IsKey(string key) => key.StartsWith(KeyPrefix, StringComparison.Ordinal);

    /// <summary>The system address and body id in a body key; null for any other key.</summary>
    public static (long Address, int BodyId)? Parse(string key)
    {
        if (!IsKey(key))
        {
            return null;
        }

        var parts = key[KeyPrefix.Length..].Split(':');

        return parts.Length == 2
            && long.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var address)
            && int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var bodyId)
                ? (address, bodyId)
                : null;
    }

    /// <summary>True when the page redrew since the last call, an answer arriving included. Call on the UI thread.</summary>
    public bool Tick()
    {
        var redrawn = _redrawn;
        _redrawn = false;

        return redrawn;
    }

    /// <summary>Shows one section; a section the body does not offer is ignored.</summary>
    public void Show(Section section)
    {
        if (!Sections.Contains(section))
        {
            return;
        }

        _section = section;
        Draw();
    }

    /// <summary>OVERVIEW always; COMPOSITION for a planet with any; ORBIT with any orbit or rotation; ORBITING with any station or body.</summary>
    public static IReadOnlyList<Section> SectionsOf(StarSystemProfile profile, BodyProfile body)
    {
        var sections = new List<Section> { Section.Overview };

        if (body.Type != "Star"
            && (body.AtmosphereComposition.Count > 0 || body.SolidComposition.Count > 0 || body.Materials.Count > 0))
        {
            sections.Add(Section.Composition);
        }

        if (OrbitFacts(body).Count > 0 || RotationFacts(body).Count > 0)
        {
            sections.Add(Section.Orbit);
        }

        if (Orbiting(profile, body) > 0)
        {
            sections.Add(Section.Orbiting);
        }

        return sections;
    }

    /// <summary>The stations on the body, nearest first.</summary>
    public static IReadOnlyList<StationProfile> StationsOn(StarSystemProfile profile, BodyProfile body) =>
        [.. StationFilter.Nearest(profile.Stations.Where(station => string.Equals(station.Body, body.Name, StringComparison.Ordinal)))];

    /// <summary>The bodies whose parent is this body, in <c>BodyId</c> order.</summary>
    public static IReadOnlyList<BodyProfile> Children(StarSystemProfile profile, BodyProfile body) =>
        [.. profile.Bodies.Where(child => child.ParentId == body.BodyId && child.BodyId != body.BodyId).OrderBy(child => child.BodyId)];

    private static int Orbiting(StarSystemProfile profile, BodyProfile body) =>
        StationsOn(profile, body).Count + Children(profile, body).Count;

    /// <summary>"Planet · High metal content world", or "Star · Main star" for the system's main star.</summary>
    public static string ContextLine(StarSystemProfile profile, BodyProfile body) =>
        body.Type == "Star" && profile.MainStar?.BodyId == body.BodyId
            ? "Star · Main star"
            : body.SubType is { Length: > 0 } subType ? $"{body.Type} · {subType}" : body.Type;

    private void Fetch()
    {
        _inFlight?.Cancel();
        _inFlight?.Dispose();

        var request = _inFlight = new CancellationTokenSource();

        _profile = null;
        _failure = null;
        _loading = true;
        Draw();

        if (!_surface.LookupsEnabled())
        {
            _loading = false;
            _inFlight = null;
            request.Dispose();
            Draw();
            return;
        }

        _ = Read(request);
    }

    private async Task Read(CancellationTokenSource request)
    {
        var token = request.Token;
        StarSystemProfile? profile = null;
        string? failure = null;

        try
        {
            profile = await Task.Run(() => _surface.Systems.ProfileAsync(_address, token), token).ConfigureAwait(false);
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
            failure = StarSystemPage.Unreadable;
        }

        Dispatcher.UIThread.Post(() =>
        {
            if (!ReferenceEquals(request, _inFlight))
            {
                return;
            }

            _inFlight = null;
            request.Dispose();

            _loading = false;
            _profile = profile;
            _failure = failure;
            Draw();
        });
    }

    private void Draw()
    {
        _redrawn = true;
        _page.Children.Clear();

        if (_loading)
        {
            _page.Children.Add(Heading(null, null));
            _page.Children.Add(Sentence(ReadingText));
            return;
        }

        if (_failure is { } failure)
        {
            _page.Children.Add(Heading(null, null));
            _page.Children.Add(Unavailable(failure));
            return;
        }

        if (_profile is null && !_surface.LookupsEnabled())
        {
            _page.Children.Add(RoutingKit.SwitchedOff(
                "System lookups are off",
                "Looking up a star system is switched off. It shares the galaxy search setting, so turning on "
                + "“Look things up in the galaxy” switches it on.",
                _surface.OpenSettings));
            return;
        }

        if (_profile is not { } profile || Body is not { } body)
        {
            _page.Children.Add(Heading(null, null));
            _page.Children.Add(Missing());
            return;
        }

        var sections = SectionsOf(profile, body);

        if (!sections.Contains(_section))
        {
            _section = Section.Overview;
        }

        _page.Children.Add(Heading(profile, body));
        _page.Children.Add(Switch(profile, body, sections));
        _page.Children.Add(_section switch
        {
            Section.Composition => Composition(body),
            Section.Orbit => Orbit(profile, body),
            Section.Orbiting => OrbitingList(profile, body),
            _ => body.Type == "Star" ? StarOverview(body) : PlanetOverview(profile, body),
        });
    }

    private Control Heading(StarSystemProfile? profile, BodyProfile? body)
    {
        var context = TitleText.Context(profile is not null && body is not null ? ContextLine(profile, body) : "Body");

        var title = TitleText.Style(
            new SelectableTextBlock { TextWrapping = TextWrapping.Wrap },
            TypeScale.Heading,
            TitleRank.Screen);
        TitleText.Show(title, body?.Name ?? _word);
        ThemeInk.Bind(title, TextBlock.ForegroundProperty, ThemeManager.WhiteKey);

        var block = TitleText.Block(
            title,
            context,
            (body?.ReportedAt ?? profile?.ReportedAt) is { } at ? StarSystemPage.Reported(at) : null);

        if (body is null)
        {
            return block;
        }

        var tile = _codex.Tile(body, framed: false);
        tile.Margin = new Thickness(0, 0, 16, 0);

        var row = new DockPanel();
        DockPanel.SetDock(tile, Dock.Left);
        row.Children.Add(tile);
        row.Children.Add(block);

        return row;
    }

    private Grid Switch(StarSystemProfile profile, BodyProfile body, IReadOnlyList<Section> sections)
    {
        var orbiting = Orbiting(profile, body).ToString(CultureInfo.InvariantCulture);

        var segment = new Segment
        {
            ItemsSource = sections.Select(section => section switch
            {
                Section.Composition => "Composition",
                Section.Orbit => "Orbit",
                Section.Orbiting => $"Orbiting {orbiting}",
                _ => "Overview",
            }).ToList(),
            SelectedIndex = sections.ToList().IndexOf(_section),
        };
        Avalonia.Automation.AutomationProperties.SetName(segment, "Sections");
        segment.SelectionChanged += (_, _) =>
        {
            if (segment.SelectedIndex >= 0 && segment.SelectedIndex < sections.Count && sections[segment.SelectedIndex] != _section)
            {
                _section = sections[segment.SelectedIndex];
                Draw();
            }
        };

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*,*,*") };
        Grid.SetColumnSpan(segment, 4);
        grid.Children.Add(segment);

        return grid;
    }

    private StackPanel PlanetOverview(StarSystemProfile profile, BodyProfile body)
    {
        var column = new StackPanel { Spacing = 24 };

        column.Children.Add(StatTile.Grid(
        [
            StatTile.Build("Orbits", ParentName(profile, body) ?? Dash, StatInk.Name),
            StatTile.Build("Distance", StarSystemPage.BodyDistanceText(body.DistanceToArrival), StatInk.Number),
            StatTile.Build("Landing", body.IsLandable ? "Landable" : "Not landable"),
            StatTile.Build("Gravity", Figure(body.Gravity, "N2", " g"), StatInk.Number),
            StatTile.Build("Surface temperature", Figure(body.SurfaceTemperature, "N0", " K"), StatInk.Number),
            StatTile.Build("Pressure", Figure(body.SurfacePressure, "N2", " atm"), StatInk.Number),
            StatTile.Build("Radius", Figure(body.Radius, "N0", " km"), StatInk.Number),
            StatTile.Build("Mass", Figure(body.EarthMasses, "N2", " Earth masses"), StatInk.Number),
            StatTile.Build("Terraforming", body.TerraformingState ?? "Not terraformable"),
            StatTile.Build("Atmosphere", body.Atmosphere ?? "No atmosphere"),
            StatTile.Build("Volcanism", body.Volcanism ?? "No volcanism"),
        ]));

        if (body.Signals.Count > 0)
        {
            column.Children.Add(Group(
                "Signals",
                null,
                StatTile.Grid([.. body.Signals.Select(signal => (Control)StatTile.Build(
                    StarSystemPage.SignalName(signal.Key), signal.Value.ToString(CultureInfo.InvariantCulture), StatInk.Number))])));
        }

        if (body.Genera.Count > 0)
        {
            column.Children.Add(Group("Genera", Count(body.Genera.Count), Genera(body)));
        }

        if (body.Rings.Count > 0)
        {
            column.Children.Add(Group("Rings", Count(body.Rings.Count), Rings(body)));
        }

        if (Mined(body) is { } mined)
        {
            column.Children.Add(mined);
        }

        return column;
    }

    private StackPanel StarOverview(BodyProfile body)
    {
        var column = new StackPanel { Spacing = 24 };

        if (StarClassText.For(body.SpectralClass, body.Luminosity, body.SubType) is { } line)
        {
            column.Children.Add(ClassTile(body, line));
        }

        column.Children.Add(StatTile.Grid(
        [
            StatTile.Build("Distance", StarSystemPage.BodyDistanceText(body.DistanceToArrival), StatInk.Number),
            StatTile.Build("Fuel scoop", body.Scoopable switch { true => "Scoopable", false => "Not scoopable", null => Dash }),
            StatTile.Build("Mass", Figure(body.SolarMasses, "N2", " solar masses"), StatInk.Number),
            StatTile.Build("Radius", Figure(body.SolarRadius, "N2", " solar radius"), StatInk.Number),
            StatTile.Build("Surface temperature", Figure(body.SurfaceTemperature, "N0", " K"), StatInk.Number),
            StatTile.Build("Age", Figure(body.Age, "N0", " million years"), StatInk.Number),
            StatTile.Build("Absolute magnitude", Figure(body.AbsoluteMagnitude, "N2", string.Empty), StatInk.Number),
            StatTile.Build("Rotational period", Figure(body.RotationalPeriod, "N2", " days"), StatInk.Number),
        ]));

        if (body.Rings.Count > 0)
        {
            column.Children.Add(Group("Rings", Count(body.Rings.Count), Rings(body)));
        }

        return column;
    }

    /// <summary>The full-width CLASS tile: the class, its short name, what each part means, and CODEX › where the class has an entry.</summary>
    private Border ClassTile(BodyProfile body, StarClassLine line)
    {
        var shown = Typeset.Mono(line.Class, TypeScale.Heading, ThemeManager.AKey, weight: FontWeight.Medium);
        shown.Name = "StarClass";

        var name = Typeset.Chrome(line.Name, TypeScale.ControlLarge, ThemeManager.WhiteKey, weight: FontWeight.SemiBold);
        name.Name = "StarClassName";

        var meaning = Typeset.Prose(line.Meaning, TypeScale.Small, ThemeManager.GreyKey, wrap: true);
        meaning.Name = "StarClassMeaning";

        var text = new StackPanel
        {
            Spacing = 4,
            Children = { StatTile.Caption("Class", ThemeManager.GreyKey), shown, name, meaning },
        };

        var row = new DockPanel();

        if (_codex.EntryOf(body) is not null)
        {
            var codex = _codex.Tile(body, framed: true);
            codex.Margin = new Thickness(16, 0, 0, 0);
            DockPanel.SetDock(codex, Dock.Right);
            row.Children.Add(codex);
        }

        row.Children.Add(text);

        var tile = new Border { Padding = new Thickness(14, 12), Child = row };
        ThemeInk.Bind(tile, Border.BackgroundProperty, ThemeManager.SlabKey);

        return tile;
    }

    private StackPanel Genera(BodyProfile body)
    {
        var list = new StackPanel { Spacing = Gaps.Tile };

        foreach (var genus in body.Genera)
        {
            var name = Typeset.Chrome(genus, TypeScale.ControlLarge, ThemeManager.WhiteKey, weight: FontWeight.SemiBold, vertical: VerticalAlignment.Center);

            var slab = new Border { MinHeight = TypeScale.MinimumTarget, Padding = new Thickness(14, 0), Child = name };
            ThemeInk.Bind(slab, Border.BackgroundProperty, ThemeManager.SlabKey);

            list.Children.Add(Beside(_codex.GenusTile(genus), slab));
        }

        return list;
    }

    private static StackPanel Rings(BodyProfile body)
    {
        var list = new StackPanel { Spacing = Gaps.Tile };

        foreach (var ring in body.Rings)
        {
            var title = Typeset.Chrome(StarSystemPage.RingName(body, ring), TypeScale.Control, ThemeManager.AKey, weight: FontWeight.SemiBold);

            var facts = new WrapPanel { ItemSpacing = 16, LineSpacing = 2 };

            foreach (var fact in StarSystemPage.RingFacts(body, ring))
            {
                facts.Children.Add(Typeset.Mono(fact, TypeScale.Meta, ThemeManager.WhiteKey));
            }

            var slab = new Border
            {
                Padding = new Thickness(14, 9, 14, 10),
                Child = new StackPanel { Spacing = 5, Children = { title, facts } },
            };
            ThemeInk.Bind(slab, Border.BackgroundProperty, ThemeManager.SlabKey);
            list.Children.Add(slab);
        }

        return list;
    }

    /// <summary>YOUR SURFACE MINING in Cyan, or null where the Commander has refined nothing here.</summary>
    private StackPanel? Mined(BodyProfile body)
    {
        var site = _surface.SurfaceMining?.Invoke(_address, body.BodyId);

        if (StarSystemPage.MinedText(site) is not { } most || site is null)
        {
            return null;
        }

        var tiles = StatTile.Grid(
        [
            StatTile.Build("Runs", site.Runs.Count.ToString(CultureInfo.InvariantCulture), StatInk.Here),
            StatTile.Build("Tonnes refined", site.Runs.Sum(run => run.TonnesRefined).ToString("N0", CultureInfo.InvariantCulture), StatInk.Here),
            StatTile.Build("Most refined", most, StatInk.Here),
        ], maxColumns: 3);

        return Group("Your surface mining", null, tiles);
    }

    private static Control Composition(BodyProfile body)
    {
        var column = new StackPanel { Spacing = 24 };

        var parts = new List<Control>();

        if (body.AtmosphereComposition.Count > 0)
        {
            parts.Add(Group("Atmosphere", null, Shares(body.AtmosphereComposition)));
        }

        if (body.SolidComposition.Count > 0)
        {
            parts.Add(Group("Solid composition", null, Shares(body.SolidComposition)));
        }

        if (parts.Count > 0)
        {
            var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*"), ColumnSpacing = 24 };

            for (var i = 0; i < parts.Count; i++)
            {
                Grid.SetColumn(parts[i], i);
                grid.Children.Add(parts[i]);
            }

            column.Children.Add(grid);
        }

        if (body.Materials.Count > 0)
        {
            column.Children.Add(Group("Materials", Count(body.Materials.Count), Shares(body.Materials)));
        }

        return column;
    }

    /// <summary>Each part, largest first, with a bar scaled against the largest and its real percentage.</summary>
    private static StackPanel Shares(IReadOnlyDictionary<string, double> shares)
    {
        var list = new StackPanel { Spacing = Gaps.Tile };
        var largest = shares.Values.DefaultIfEmpty(0).Max();

        foreach (var (name, share) in shares.OrderByDescending(part => part.Value).ThenBy(part => part.Key, StringComparer.Ordinal))
        {
            var label = Typeset.Chrome(name, TypeScale.Control, ThemeManager.WhiteKey, weight: FontWeight.Medium, trimming: TextTrimming.CharacterEllipsis, vertical: VerticalAlignment.Center);
            label.Width = 150;

            var figure = Typeset.Mono(ShareText(share), TypeScale.Control, ThemeManager.AKey, vertical: VerticalAlignment.Center, align: TextAlignment.Right);
            figure.Width = 64;

            var bar = Gauge.Track(largest > 0 ? share / largest : 0, ThemeManager.AKey);
            bar.VerticalAlignment = VerticalAlignment.Center;
            bar.Margin = new Thickness(12, 0);

            var row = new DockPanel { MinHeight = 28 };
            DockPanel.SetDock(label, Dock.Left);
            DockPanel.SetDock(figure, Dock.Right);
            row.Children.Add(label);
            row.Children.Add(figure);
            row.Children.Add(bar);
            Avalonia.Automation.AutomationProperties.SetName(row, $"{name}, {ShareText(share)}");

            list.Children.Add(row);
        }

        return list;
    }

    /// <summary>"21.3%": one decimal of a share already given as a percentage.</summary>
    public static string ShareText(double percent) => percent.ToString("0.0", CultureInfo.InvariantCulture) + "%";

    private static StackPanel Orbit(StarSystemProfile profile, BodyProfile body)
    {
        var column = new StackPanel { Spacing = 24 };

        if (OrbitFacts(body) is { Count: > 0 } orbit)
        {
            column.Children.Add(Group("Orbit", null, StatTile.Grid(orbit), ParentName(profile, body)));
        }

        if (RotationFacts(body) is { Count: > 0 } rotation)
        {
            column.Children.Add(Group("Rotation", null, StatTile.Grid(rotation)));
        }

        return column;
    }

    private static List<Control> OrbitFacts(BodyProfile body)
    {
        var tiles = new List<Control>();

        Add(tiles, "Orbital period", body.OrbitalPeriod, "N2", " days");
        Add(tiles, "Semi-major axis", body.SemiMajorAxis, "N4", " AU");
        Add(tiles, "Eccentricity", body.OrbitalEccentricity, "N4", string.Empty);
        Add(tiles, "Inclination", body.OrbitalInclination, "N2", "°");
        Add(tiles, "Argument of periapsis", body.ArgOfPeriapsis, "N2", "°");

        return tiles;
    }

    private static List<Control> RotationFacts(BodyProfile body)
    {
        var tiles = new List<Control>();

        Add(tiles, "Rotational period", body.RotationalPeriod, "N2", " days");

        if (body.TidallyLocked is { } locked)
        {
            tiles.Add(StatTile.Build("Tidally locked", locked ? "Yes" : "No"));
        }

        Add(tiles, "Axial tilt", body.AxialTilt, "N2", "°");

        return tiles;
    }

    private static void Add(List<Control> tiles, string label, double? value, string format, string unit)
    {
        if (value is not null)
        {
            tiles.Add(StatTile.Build(label, Figure(value, format, unit), StatInk.Number));
        }
    }

    private StackPanel OrbitingList(StarSystemProfile profile, BodyProfile body)
    {
        var column = new StackPanel { Spacing = 24 };

        if (StationsOn(profile, body) is { Count: > 0 } stations)
        {
            var list = new StackPanel { Spacing = Gaps.Tile };

            foreach (var station in stations)
            {
                var sub = string.Join(" · ", new[]
                {
                    StarSystemPage.StationType(station),
                    station.LargestPad is { } pad ? $"{StarSystemPage.PadText(pad)} pad" : null,
                }.Where(part => part is not null));

                Action? open = _openStation is { } drill && station.MarketId is not null ? () => drill(profile, station) : null;

                list.Children.Add(Row(station.Name, sub, StarSystemPage.ArrivalText(station.DistanceToArrival), open));
            }

            column.Children.Add(Group("Stations", Count(stations.Count), list));
        }

        if (Children(profile, body) is { Count: > 0 } children)
        {
            var list = new StackPanel { Spacing = Gaps.Tile };

            foreach (var child in children)
            {
                var sub = string.Join(" · ", new[]
                {
                    child.SubType ?? child.Type,
                    child.IsLandable ? "Landable" : null,
                    child.Signals.Count > 0 ? Signals(child.Signals.Values.Sum()) : null,
                }.Where(part => part is not null));

                Action? open = _openBody is { } drill ? () => drill(profile, child) : null;
                var row = Row(child.Name, sub, StarSystemPage.BodyDistanceText(child.DistanceToArrival), open);

                list.Children.Add(Beside(_codex.Tile(child, framed: true), row));
            }

            column.Children.Add(Group(body.Type == "Star" ? "Orbiting this star" : "Moons", Count(children.Count), list));
        }

        return column;
    }

    /// <summary>A 44px list row: name, sub-line, the distance in mono at the right, and a › where it opens a page.</summary>
    internal static Border Row(string name, string sub, string distance, Action? open)
    {
        var right = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, VerticalAlignment = VerticalAlignment.Center };
        right.Children.Add(Typeset.Mono(distance, TypeScale.Control, ThemeManager.AKey, vertical: VerticalAlignment.Center));

        if (open is not null)
        {
            right.Children.Add(Typeset.Chrome("›", TypeScale.ControlLarge, ThemeManager.AKey, vertical: VerticalAlignment.Center));
        }

        var text = new StackPanel
        {
            Spacing = 1,
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                ListRow.NameInk(new TextBlock
                {
                    Text = name,
                    FontFamily = Fonts.ChromeFamily,
                    FontSize = ListRow.NameSize,
                    FontWeight = FontWeight.SemiBold,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                }),
                ListRow.Sub(new TextBlock { Text = sub, TextTrimming = TextTrimming.CharacterEllipsis }),
            },
        };

        var line = new DockPanel();
        DockPanel.SetDock(right, Dock.Right);
        line.Children.Add(right);
        line.Children.Add(text);

        var row = new Border
        {
            MinHeight = TypeScale.MinimumTarget,
            Padding = new Thickness(14, 4),
            Child = line,
        };
        ListRow.Dress(row);
        Avalonia.Automation.AutomationProperties.SetName(row, name);

        if (open is not null)
        {
            ListRow.Opens(row, open);
        }

        return row;
    }

    /// <summary>A Codex tile at the left of <paramref name="content"/>, a press target of its own.</summary>
    internal static DockPanel Beside(Control tile, Control content)
    {
        tile.Margin = new Thickness(0, 0, Gaps.Tile, 0);

        var panel = new DockPanel();
        DockPanel.SetDock(tile, Dock.Left);
        panel.Children.Add(tile);
        panel.Children.Add(content);

        return panel;
    }

    private Control Unavailable(string failure)
    {
        var notice = new Notice(NoticeLevel.Error)
        {
            Label = UnavailableLabel,
            Text = UnavailableText,
            Detail = failure,
            MaxWidth = 760,
            HorizontalAlignment = HorizontalAlignment.Left,
        };

        var retry = new Button { Name = "Retry", Content = "Retry" };
        retry.Click += (_, _) => Fetch();
        notice.Actions.Add(retry);

        return notice;
    }

    private Control Missing()
    {
        var notice = new Notice(NoticeLevel.Warning)
        {
            Label = MissingLabel,
            Text = MissingText,
            MaxWidth = 760,
            HorizontalAlignment = HorizontalAlignment.Left,
        };

        if (_back is { } back)
        {
            var tile = new Button { Name = "Back", Content = "Back" };
            tile.Click += (_, _) => back();
            notice.Actions.Add(tile);
        }

        return notice;
    }

    private static StackPanel Group(string name, string? count, Control content, string? note = null)
    {
        var head = StarSystemPage.GroupHead(name, note ?? string.Empty, count);

        return new StackPanel { Spacing = 10, Children = { head, content } };
    }

    private static string? ParentName(StarSystemProfile profile, BodyProfile body) =>
        body.ParentId is { } parent ? profile.Bodies.FirstOrDefault(other => other.BodyId == parent)?.Name : null;

    private static string Figure(double? value, string format, string unit) =>
        value is { } figure ? figure.ToString(format, CultureInfo.InvariantCulture) + unit : Dash;

    private static string Count(int count) => count.ToString(CultureInfo.InvariantCulture);

    private static string Signals(int count) =>
        $"{count.ToString(CultureInfo.InvariantCulture)} {(count == 1 ? "signal" : "signals")}";

    private static TextBlock Sentence(string text)
    {
        var block = RoutingKit.Ink(text, TypeScale.ControlLarge, ThemeManager.WhiteKey, wrap: true);
        block.MaxWidth = 640;

        return block;
    }
}
