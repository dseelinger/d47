using System.Diagnostics;
using System.Globalization;
using D47.Core.Listening;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using D47.App.Controls;
using D47.App.Input;
using D47.App.Theming;
using D47.Core;
using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Coverage;

using D47.App.Windowing;

namespace D47.App.Settings;

/// <summary>The settings surface.</summary>
public partial class SettingsView : UserControl, D47.App.Panel.IFilterablePage, D47.App.Panel.IPageChrome, D47.App.Panel.IPageSummary
{
    public string Summary => "Changes apply as you make them.";

    event EventHandler? D47.App.Panel.IPageSummary.SummaryChanged
    {
        add { }
        remove { }
    }

    private readonly List<SectionView> _sections = [];
    private readonly List<RowView> _rows = [];

    private SettingsService? _settings;

    /// <summary>Each area's nav heading, and the run of sections beneath it.</summary>
    private readonly List<AreaView> _navAreas = [];

    /// <summary>The area holding the open place (#220), or −1 before <see cref="Build"/> runs.</summary>
    private int _activeArea = -1;

    /// <summary>The open place's head, and the parts of it that change with the place.</summary>
    private StackPanel? _pageHead;

    private TextBlock? _pageCrumb;

    private TextBlock? _pageTitle;

    /// <summary>The protected-row legend, shown under the page title when the place has one (#333).</summary>
    private TextBlock? _areaLegend;

    /// <summary>What the page says when a query leaves nothing on it.</summary>
    private StackPanel? _emptyBlock;

    private TextBlock? _emptyNote;

    private TextBlock? _otherPagesNote;

    /// <summary>The page-top toggles, drawn in the panel's page bar (<see cref="BarTool"/>).</summary>
    private Control? _barTool;

    private readonly List<Action> _barToolRefreshes = [];

    private readonly Dictionary<string, Control> _barToolControls = new(StringComparer.Ordinal);

    /// <summary>The place picker shown once the nav has collapsed (#220).</summary>
    private Stepper? _areaDropdown;

    /// <summary>True while the view is writing <see cref="_areaDropdown"/>, so its own selection change
    /// does not loop back into another page change.</summary>
    private bool _settingAreaDropdown;

    /// <summary>Marks an area's heading in the nav, for a test to tell it from a place.</summary>
    public const string NavAreaClass = "nav-area";

    /// <summary>Marks a place's item in the nav.</summary>
    public const string NavPlaceClass = "nav-place";

    /// <summary>How far an entry marked <see cref="SettingsEntry.Under"/> is drawn in.</summary>
    private const double UnderIndent = 24;

    /// <summary>
    /// The name on a row's reset glyph, so a lookup for the row's own control can exclude it (#61).
    /// </summary>
    public const string RowResetName = "RowReset";

    /// <summary>Whether a button is the row's chrome rather than the control the row is about.</summary>
    public static bool IsRowChrome(Button button) =>
        button?.Name is { } name && name == RowResetName;

    /// <summary>Which sections a jump or a "Show N more" press has unfolded for this session (#60, #221).
    /// Not saved: a fresh Build folds every place again.</summary>
    private readonly HashSet<int> _revealedSections = [];

    private ViewStateStore? _viewStateStore;
    private ViewState _viewState = new();

    /// <summary>Where a placement group's reset glyph reaches to clear the anchor it holds (#162).</summary>
    private D47.App.Headset.VrHost? _vrHost;

    private SettingControls _controls = null!;

    /// <summary>True while controls are being written from settings rather than read from.</summary>
    private bool _refreshing;

    /// <summary>The open place, or −1 before one is open.</summary>
    private int _activeSection = -1;

    /// <summary>
    /// Limits this instance to one <see cref="SettingsLayout"/> tab place — a tab's own strip rather
    /// than the settings page (#218).
    /// </summary>
    private string? _tabPlaceId;

    /// <summary>The tab strip's own disclosure content and header, so a search match can open it (#222).</summary>
    private StackPanel? _tabStripContent;

    private Action<bool>? _tabStripHeader;

    /// <summary>Every named group in every place, for a query that matches a group's title or help (#222).</summary>
    private readonly List<GroupView> _groups = [];

    /// <summary>Every row a <see cref="SettingsLayout.Tabs"/> place resolves to, for the "On other tabs" list a
    /// query builds on the settings page (#222).</summary>
    private readonly List<(SettingsTabPlace Tab, SettingRow Row)> _tabPlaceRows = [];

    /// <summary>The "On other tabs" section and its list of matches, built once on the settings page and empty
    /// off it (#222).</summary>
    private Control? _otherTabsSection;

    private StackPanel? _otherTabsList;

    /// <summary>Opens the tab and root a search match under "On other tabs" names (#222).</summary>
    private Action<string>? _openTabPlace;

    public SettingsView()
    {
        InitializeComponent();

    }

    /// <summary>The page-top toggles, for the panel to draw in its page bar; null off the settings page.</summary>
    public Control? BarTool => _barTool;

    /// <summary>The open place's guide, for the panel's HELP; null off the settings page.</summary>
    public string? HelpTopic =>
        _tabPlaceId is null && _activeSection >= 0 && _activeSection < _sections.Count
            ? _sections[_activeSection].DocsCapabilityId
            : null;

    /// <summary>Says the field filters, since a query removes rows here rather than marking them.</summary>
    public string FilterPlaceholder => "Filter settings";

    public double? FilterWidth => 340;

    /// <summary>Binds the view to a live settings service.</summary>
    public void Attach(
        SettingsService settings,
        ViewStateStore viewState,
        Func<CoverageReport>? coverage = null,
        D47.Core.Actions.MacroStore? macros = null,
        D47.Core.Checklists.ChecklistService? checklists = null,
        IReadOnlyList<string>? reservedPhrases = null,
        SwitchEditing? switches = null,
        Func<WhisperModel, IProgress<ModelProgress>, Task<ModelInstallResult>>? downloadModel = null,
        LoreEditing? lore = null,
        (D47.Core.Memory.MemoryBook Book, Func<DateTimeOffset> Now)? memories = null,
        D47.Core.Logbook.LogbookBook? logbook = null,

        // Appended rather than slotted in beside the macro store it most resembles: the callers pass these
        // positionally, so a parameter added in the middle silently rebinds every argument after it
        // (remediation.md 11, item 9).
        D47.Core.Persona.OwnPersonaStore? ownPersonas = null,

        // At the end, by the rule the comment above records the cost of (#164).
        (D47.Core.Diagnostics.Recording.RecordingLog Log, Func<DateTimeOffset> Now)? recording = null,

        // At the end, by the rule the comment above records the cost of (#162).
        (D47.Core.Debrief.DebriefBook Book, Func<DateTimeOffset> Now,
            Func<D47.Core.Persona.Persona> Core)? debrief = null,

        // At the end, by the same rule — the tab this view is limited to, or null for the settings
        // page (#218).
        string? tabPlaceId = null,

        // At the end, by the same rule — so a placement group's reset glyph can clear the anchor
        // VrHost holds rather than writing view-state itself (#162).
        D47.App.Headset.VrHost? vrHost = null,

        // At the end, by the same rule (#80).
        D47.Core.Input.BindingProfiles? bindingProfiles = null)
    {
        _settings = settings;
        _viewStateStore = viewState;
        _viewState = viewState.Load();
        _tabPlaceId = tabPlaceId;
        _vrHost = vrHost;

        _controls = new SettingControls(
            new SettingRowHost(settings, this, () => _refreshing, Apply, Refresh),
            new SettingServices(
                coverage,
                macros,
                ownPersonas,
                checklists,
                switches,
                lore,
                memories,
                debrief,
                logbook,
                recording,
                bindingProfiles,
                reservedPhrases ?? [],
                downloadModel));

        Build();

        if (_tabPlaceId is null)
        {
            RestoreSection();
        }

        settings.Changed += OnSettingsChanged;

        if (vrHost is not null)
        {
            vrHost.AnchorsChanged += OnAnchorsChanged;
        }

        // **Symmetric, which it was not** (#90).
        AttachedToVisualTree += (_, _) =>
        {
            settings.Changed -= OnSettingsChanged;
            settings.Changed += OnSettingsChanged;

            if (vrHost is not null)
            {
                vrHost.AnchorsChanged -= OnAnchorsChanged;
                vrHost.AnchorsChanged += OnAnchorsChanged;
            }
        };

        DetachedFromVisualTree += (_, _) =>
        {
            settings.Changed -= OnSettingsChanged;

            if (vrHost is not null)
            {
                vrHost.AnchorsChanged -= OnAnchorsChanged;
            }
        };
    }

    private void OnSettingsChanged(SettingsChanged change) => Dispatcher.UIThread.Post(Refresh);

    /// <summary>A placement group's reset lights while its surface has an anchor.</summary>
    private void OnAnchorsChanged() => Dispatcher.UIThread.Post(Refresh);

    /// <summary>A brush fetched at call time, so state changes pick up the current theme.</summary>
    private IBrush? Res(string key) => this.FindResource(key) as IBrush;

    /// <summary>
    /// The glyph button control theme (#376), shared by every bare-glyph button this view builds.
    /// Resolved from Application.Current rather than <c>this.FindResource</c>: these buttons are
    /// built before the view is attached to a window, and an unattached control's FindResource
    /// cannot walk up to Application yet.
    /// </summary>
    private static ControlTheme? GlyphButtonTheme =>
        Application.Current!.FindResource("D47.GlyphButton") as ControlTheme;

    /// <summary>
    /// Binds a brush property to a theme resource, so a theme switch repaints controls built in code
    /// the same way DynamicResource repaints the ones built in markup.
    /// </summary>
    private IDisposable Themed(AvaloniaObject target, AvaloniaProperty property, string key) =>
        target.Bind(property, this.GetResourceObservable(key));

    private void Build()
    {
        var settings = _settings ?? throw new InvalidOperationException("Attach() has not been called.");

        Cards.Children.Clear();
        NavItems.Children.Clear();
        _sections.Clear();
        _rows.Clear();
        _revealedSections.Clear();
        _navAreas.Clear();
        _activeSection = -1;
        _activeArea = -1;
        _pageHead = null;
        _pageCrumb = null;
        _pageTitle = null;
        _areaLegend = null;
        _emptyBlock = null;
        _emptyNote = null;
        _otherPagesNote = null;
        _barTool = null;
        _barToolRefreshes.Clear();
        _barToolControls.Clear();
        _areaDropdown = null;
        _groups.Clear();
        _tileGrids.Clear();
        _tabPlaceRows.Clear();
        _otherTabsSection = null;
        _otherTabsList = null;
        _tabStripContent = null;
        _tabStripHeader = null;

        if (_tabPlaceId is { } placeId)
        {
            BuildTabPlace(settings, placeId);
            return;
        }

        foreach (var tab in SettingsLayout.Tabs)
        {
            foreach (var row in settings.RowsForPlace(tab.Id))
            {
                _tabPlaceRows.Add((tab, row));
            }
        }

        _otherTabsSection = BuildOtherTabsSection(out _otherTabsList);
        _areaDropdown = BuildPlaceDropdown();
        _barTool = BuildBarTool(settings);
        _pageHead = BuildPageHead();
        _emptyBlock = BuildEmptyBlock();

        var owners = new Dictionary<string, CapabilityDescriptor>(StringComparer.Ordinal);

        foreach (var section in settings.Sections)
        {
            foreach (var row in section.Rows)
            {
                owners.TryAdd(row.Key, section.Capability);
            }
        }

        foreach (var area in SettingsLayout.Areas)
        {
            var first = _sections.Count;
            var areaIndex = _navAreas.Count;
            var (areaHeading, areaHeadingText, areaHeadingBar) = BuildNavArea(area.Title, areaIndex);

            NavItems.Children.Add(areaHeading);

            foreach (var place in area.Places)
            {
                var (content, rows, foldButton) = BuildPlace(settings, owners, place, _sections.Count);

                var nav = BuildNavItem(_sections.Count, place.Title);
                NavItems.Children.Add(nav.Item);

                _sections.Add(
                    new SectionView(
                        place.Id, place.Title, place.Terms, place.DocsCapabilityId, content, rows,
                        nav.Item, nav.Bar, nav.Text, nav.Count)
                    {
                        FoldButton = foldButton,
                    });
            }

            _navAreas.Add(
                new AreaView(
                    area.Id, area.Title, areaHeading, areaHeadingText, areaHeadingBar, first, _sections.Count - first));
        }

        // Once to learn which places have a page, then to open the first of them.
        Refresh();

        if (FirstExisting() is var opening and >= 0)
        {
            ShowPlace(opening);
        }
    }

    /// <summary>
    /// Draws one <see cref="SettingsLayout"/> tab place's rows, with no nav, page-top strip, card header, width floor
    /// or fold. <see cref="BarTool"/> is the tile that opens them (#218, #954).
    /// </summary>
    private void BuildTabPlace(SettingsService settings, string placeId)
    {
        Nav.IsVisible = false;
        Root.ColumnDefinitions[0].Width = new GridLength(0);
        Root.MinWidth = 0;

        // Laid out to the column's width, so the rows fit it instead of scrolling sideways.
        Scroller.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
        Cards.HorizontalAlignment = HorizontalAlignment.Stretch;

        var rows = settings.RowsForPlace(placeId);

        var content = new StackPanel { Spacing = 2, Margin = new Thickness(0, 8, 0, 0) };

        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];

            if (SettingsLayout.IsSubsystemLevelFamily(row.Key))
            {
                // Drawn as one track at the first of the family, and skipped after (#283).
                if (i > 0 && SettingsLayout.IsSubsystemLevelFamily(rows[i - 1].Key))
                {
                    continue;
                }

                var family = rows.Skip(i).TakeWhile(r => SettingsLayout.IsSubsystemLevelFamily(r.Key)).ToList();
                var defaultRow = rows.FirstOrDefault(r => r.Key == DiagnosticsCapability.DefaultLevelKey) ?? row;
                var track = new SubsystemLevelTrack(settings, defaultRow, family);
                var trackView = new RowView(row with { DrawnElsewhere = false }, track, track.Refresh);

                _rows.Add(trackView);
                content.Children.Add(track);
                continue;
            }

            var view = BuildRow(SectionOwning(settings, row), row);
            _rows.Add(view);
            content.Children.Add(view.Container);
        }

        var expanded = _viewState.IsExpanded(placeId, startCollapsed: true);
        content.IsVisible = expanded;
        _tabStripContent = content;

        var count = rows.Count.ToString(CultureInfo.InvariantCulture);

        string Said(bool open) => $"{(open ? "▾" : "▸")} SETTINGS {count}";

        var tile = new Button
        {
            Name = TabTileName,
            Content = Said(expanded),
            HorizontalAlignment = HorizontalAlignment.Left,
        };

        AutomationProperties.SetName(tile, "Page settings");

        void Show(bool open)
        {
            tile.Content = Said(open);

            if (open)
            {
                D47.App.Panel.LoadoutPages.Themed(tile, BackgroundProperty, ThemeManager.AKey);
                D47.App.Panel.LoadoutPages.Themed(tile, ForegroundProperty, ThemeManager.KnockKey);
            }
            else
            {
                tile.ClearValue(BackgroundProperty);
                tile.ClearValue(ForegroundProperty);
            }

            // Collapsed, the view is empty and takes no height.
            Cards.Margin = new Thickness(0, 0, 0, open ? 10 : 0);
        }

        Show(expanded);
        _tabStripHeader = Show;
        _barTool = tile;

        tile.Click += (_, _) =>
        {
            var open = !content.IsVisible;
            content.IsVisible = open;
            Show(open);
            SaveViewState(state => state.With(placeId, open));
        };

        var strip = new StackPanel { Name = TabStripName, Spacing = 8 };
        strip.Children.Add(content);

        Cards.Children.Add(strip);

        Refresh();
    }

    /// <summary>Marks the strip a tab place draws, for a test to find it by name (#218).</summary>
    public const string TabStripName = "SettingsForThisPage";

    /// <summary>Marks the tile on the title line that opens the strip, for a test to find it.</summary>
    public const string TabTileName = "PageSettingsTile";

    /// <summary>The capability a page-level row still belongs to.</summary>
    private static CapabilityDescriptor SectionOwning(SettingsService settings, SettingRow row) =>
        settings.Sections.First(section => section.Rows.Any(other => other.Key == row.Key)).Capability;

    /// <summary>One <see cref="SettingsLayout"/> place as a page: its groups, in order, and every row they resolve to.</summary>
    private (StackPanel Content, IReadOnlyList<SettingRow> Rows, Button FoldButton) BuildPlace(
        SettingsService settings,
        IReadOnlyDictionary<string, CapabilityDescriptor> owners,
        SettingsPlace place,
        int index)
    {
        var content = new StackPanel { Spacing = 2 };

        var rows = new List<SettingRow>();

        for (var inPlace = 0; inPlace < place.Groups.Count; inPlace++)
        {
            // A group heading, stated once, in place of the same sentence on every row — and something a
            // query can match to reveal every row under it (#222).
            var group = place.Groups[inPlace];
            var groupIndex = _groups.Count;
            var groupView = BuildGroupHeading(index, place.Id, inPlace, group);

            content.Children.Add(groupView.Container);
            _groups.Add(groupView);

            if (group.Layout == SettingsGroupLayout.Mixer)
            {
                var mixed = group.Entries.SelectMany(settings.RowsForEntry).ToList();

                content.Children.Add(BuildMixer(mixed, index, groupIndex));
                rows.AddRange(mixed);
                continue;
            }

            // The group's toggles, as one grid of tiles where its first toggle falls.
            TileGrid? tiles = null;

            foreach (var entry in group.Entries)
            {
                foreach (var row in settings.RowsForEntry(entry))
                {
                    if (row is { Kind: SettingKind.Toggle, PageTop: false } && group.ToggleColumns > 0)
                    {
                        if (tiles is null)
                        {
                            tiles = new TileGrid { Name = ToggleTilesName, Columns = group.ToggleColumns };
                            _tileGrids.Add(tiles);
                            content.Children.Add(tiles);
                        }

                        var tile = BuildToggleTile(row) with { Section = index, GroupIndex = groupIndex };

                        _rows.Add(tile);
                        rows.Add(row);
                        tiles.Children.Add(tile.Container);
                        continue;
                    }

                    var view = BuildRow(owners[row.Key], row, entry.Under) with { Section = index, GroupIndex = groupIndex };

                    _rows.Add(view);
                    rows.Add(row);
                    content.Children.Add(view.Container);
                }
            }
        }

        // "Show N more" for this place's own folded rows, at the foot of its page (#221). Its visibility and
        // label are set by Refresh, which is the only place that knows how many rows a fold is hiding.
        var foldButton = new Button
        {
            FontSize = TypeScale.Secondary,
            Padding = new Thickness(0),
            MinWidth = 0,
            Margin = new Thickness(0, 8, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Left,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Cursor = new Cursor(StandardCursorType.Hand),
            IsVisible = false,
        };

        Themed(foldButton, Button.ForegroundProperty, ThemeManager.AKey);

        foldButton.Click += (_, _) =>
        {
            if (!_revealedSections.Remove(index))
            {
                _revealedSections.Add(index);
            }

            Refresh();
        };

        content.Children.Add(foldButton);

        return (content, rows, foldButton);
    }

    /// <summary>A group's head, with a reset that puts the whole group back.</summary>
    private GroupView BuildGroupHeading(int section, string placeId, int inPlace, SettingsPlaceGroup group)
    {
        var slot = VrCapability.SlotForPlacementGroup(group.Title);

        var reset = ResetGlyph(GroupResetName, $"Reset {group.Title}");

        // Absent on a group with nothing it could put back, such as one that only reports.
        reset.IsVisible = slot is not null
                          || (_settings is { } settings && group.Entries
                              .SelectMany(settings.RowsForEntry)
                              .Any(row => row is { Kind: not SettingKind.Secret, Binding.Write: not null }));

        // A placement group also clears its surface's anchor, through VrHost rather than by writing
        // view-state directly, since VrHost is the anchors' only owner (#162).
        reset.Click += (_, _) =>
        {
            if (group.Entries.Any(entry => entry.Key == SpeechCapability.GuardianPresetKey))
            {
                ResetGuardianVoice();
            }
            else
            {
                _settings!.ResetGroup(placeId, inPlace, SettingsCaller.Panel);
            }

            if (slot is not null)
            {
                _vrHost?.ResetPlacement(slot);
            }

            Refresh();
        };

        reset.PointerPressed += (_, e) => e.Handled = true;

        var (container, heading, note) = GroupHead(group.Title, group.Help, reset);

        return new GroupView(section, group.Title, group.Help, container, heading, note, reset, slot);
    }

    /// <summary>Marks a group's grid of toggle tiles, for a test to find it.</summary>
    public const string ToggleTilesName = "ToggleTiles";

    /// <summary>Marks one toggle tile, for a test to find it.</summary>
    public const string ToggleTileName = "ToggleTile";

    /// <summary>Every group's grid of toggle tiles, hidden when none of its tiles is shown.</summary>
    private readonly List<TileGrid> _tileGrids = [];

    /// <summary>
    /// A toggle row as a checkbox tile: the Elite box and the row's label, toggled from anywhere on it, with
    /// the row's help on the label. No reset of its own; the group reset covers it.
    /// </summary>
    private RowView BuildToggleTile(SettingRow row)
    {
        var (box, label) = LabeledCheckBox.Build(row.Label);
        label.TextWrapping = TextWrapping.Wrap;
        box.HorizontalAlignment = HorizontalAlignment.Stretch;
        box.VerticalAlignment = VerticalAlignment.Stretch;
        AutomationProperties.SetName(box, row.Label);

        var spoken = new TextBlock
        {
            Text = row.Help,
            FontSize = TypeScale.Secondary,
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 420,
        };
        Themed(spoken, TextBlock.ForegroundProperty, ThemeManager.WhiteKey);

        if (!string.IsNullOrWhiteSpace(row.Help))
        {
            ToolTip.SetTip(label, spoken);

            // Keyboard focus lands on the tile, not the label, so the tile opens the label's help.
            box.GotFocus += (_, e) => ToolTip.SetIsOpen(label, e.NavigationMethod == NavigationMethod.Tab);
            box.LostFocus += (_, _) => ToolTip.SetIsOpen(label, false);
        }

        var message = new StatusLine { Margin = new Thickness(0, 4, 0, 0) };

        box.IsCheckedChanged += (_, _) =>
        {
            if (!_refreshing)
            {
                Apply(row, box.IsChecked == true ? "true" : "false", message);
            }
        };

        var bar = new Border
        {
            BorderThickness = new Thickness(RowBarWidth, 0, 0, 0),
            BorderBrush = Brushes.Transparent,
            Child = box,
        };

        if (row.Protected)
        {
            Themed(bar, Border.BorderBrushProperty, ThemeManager.AKey);
        }

        var container = new DockPanel { Name = ToggleTileName };
        DockPanel.SetDock(message, Dock.Bottom);
        container.Children.Add(message);
        container.Children.Add(bar);

        return new RowView(row, container, () => box.IsChecked = _settings!.Read(row.Key) is "true")
        {
            Control = box,
            Label = label,
            Spoken = spoken,
        };
    }

    /// <summary>Marks a group's head, for a test to find it.</summary>
    public const string GroupHeadName = "GroupHead";

    /// <summary>Marks a group's reset glyph, for a test to find it.</summary>
    public const string GroupResetName = "GroupReset";

    /// <summary>Whether a placement group's surface has an anchor its reset would clear.</summary>
    private bool HasAnchor(string slot) =>
        _vrHost is { } host
        && (slot == VrCapability.CurrentSlot
            ? host.AnchorFor(VrCapability.PanelSlot) is not null || host.AnchorFor(VrCapability.MiniSlot) is not null
            : host.AnchorFor(slot) is not null);

    /// <summary>
    /// An area's title in the nav; pressing it selects the area. Upper case and tracked, the top-level
    /// node's own mark (#279).
    /// </summary>
    private (Border Item, TextBlock Text, Border Bar) BuildNavArea(string title, int areaIndex)
    {
        var text = new TextBlock
        {
            Text = title.ToUpperInvariant(),
            FontSize = TypeScale.Secondary,
            FontWeight = FontWeight.Medium,
            LetterSpacing = TypeScale.Secondary * Fonts.ChromeTracking,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        Themed(text, TextBlock.ForegroundProperty, ThemeManager.GreyKey);

        // The selected tree node's own mark (#279), shared in shape with a place's below.
        var bar = new Border
        {
            Width = 3,
            Margin = new Thickness(0, 4, 8, 4),
            Opacity = 0,
        };
        Themed(bar, Border.BackgroundProperty, ThemeManager.AKey);

        var layout = new DockPanel();
        DockPanel.SetDock(bar, Dock.Left);
        layout.Children.Add(bar);
        layout.Children.Add(text);

        var item = new Border
        {
            Padding = new Thickness(8, 12, 8, 4),
            Background = Brushes.Transparent,
            Cursor = new Cursor(StandardCursorType.Hand),
            Child = layout,
        };

        item.Classes.Add(NavAreaClass);
        item.PointerPressed += (_, _) => SelectArea(areaIndex);

        return (item, text, bar);
    }

    /// <summary>
    /// Marks the "On other tabs" section, for a test to find it (#222).
    /// </summary>
    public const string OtherTabsName = "OtherTabs";

    /// <summary>
    /// A query's matches on tab places this page has no card for — every row shown by name, and a
    /// button that opens the tab and root it lives on. Built once; <see cref="UpdateOtherTabs"/> fills
    /// it in on every <see cref="Refresh"/> (#222).
    /// </summary>
    private Control BuildOtherTabsSection(out StackPanel list)
    {
        var heading = TitleText.GroupRow(TitleText.Build("On other tabs", TypeScale.Section, TitleRank.Group));
        heading.Margin = new Thickness(0, 16, 0, 8);

        list = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };

        return new StackPanel
        {
            Name = OtherTabsName,
            IsVisible = false,
            Children = { heading, list },
        };
    }

    /// <summary>One matched row on another tab, and the button that opens it there (#222).</summary>
    private Control BuildOtherTabRow(SettingsTabPlace tab, SettingRow row)
    {
        var text = new TextBlock
        {
            Text = $"{row.Label} — on {tab.Title}",
            FontSize = TypeScale.Body,
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
        };
        Themed(text, TextBlock.ForegroundProperty, ThemeManager.WhiteKey);

        var button = new Button
        {
            Content = tab.Strip ? $"Open {tab.Title}" : $"Open the {tab.Title} tab",
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(12, 0, 0, 0),
        };

        button.Click += (_, _) => _openTabPlace?.Invoke(tab.RootKey);

        var line = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(button, Dock.Right);
        line.Children.Add(button);
        line.Children.Add(text);

        return RuledRow(line, TypeScale.MinimumTarget, new Thickness(RowHorizontalPadding, 4));
    }

    /// <summary>What a protected row's bar means, said once per page rather than on every row (#333).</summary>
    internal const string ProtectedLegend =
        "Rows marked ▌ are protected — D47 will not change them on your say-so alone.";

    /// <summary>Marks the page head, for a test to find it.</summary>
    public const string PageHeadName = "PageHead";

    /// <summary>The open place's head: a title block with its area's breadcrumb as the context line, and the
    /// protected-row legend (#333).</summary>
    private StackPanel BuildPageHead()
    {
        var crumb = TitleText.Context();

        var title = new TextBlock { TextWrapping = TextWrapping.Wrap };
        TitleText.Style(title, TypeScale.Heading, TitleRank.Screen);

        var legend = new TextBlock
        {
            Text = ProtectedLegend,
            FontSize = TypeScale.Secondary,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 4, 0, 0),
            IsVisible = false,
        };
        Themed(legend, TextBlock.ForegroundProperty, ThemeManager.GreyKey);

        _pageCrumb = crumb;
        _pageTitle = title;
        _areaLegend = legend;

        return new StackPanel
        {
            Name = PageHeadName,
            Spacing = 4,
            Margin = new Thickness(0, 0, 0, 8),
            Children = { TitleText.Block(title, crumb), legend },
        };
    }

    /// <summary>Marks what the page says when nothing on it matches the query, for a test to find it.</summary>
    public const string NothingMatchesName = "NothingMatches";

    /// <summary>What the page says when nothing on it matches the query, and where the matches are.</summary>
    private StackPanel BuildEmptyBlock()
    {
        var empty = new TextBlock { FontSize = TypeScale.Body, TextWrapping = TextWrapping.Wrap };
        Themed(empty, TextBlock.ForegroundProperty, ThemeManager.WhiteKey);

        var others = new TextBlock { FontSize = TypeScale.Secondary, TextWrapping = TextWrapping.Wrap };
        Themed(others, TextBlock.ForegroundProperty, ThemeManager.GreyKey);

        _emptyNote = empty;
        _otherPagesNote = others;

        return new StackPanel
        {
            Name = NothingMatchesName,
            Spacing = 4,
            Margin = new Thickness(0, 8, 0, 0),
            Children = { empty, others },
        };
    }

    /// <summary>Marks the page bar's tool, for a test to find it.</summary>
    public const string BarToolName = "SettingsBarTool";

    /// <summary>
    /// The page-top toggles as Elite checkbox tiles, for the panel's page bar — null where there are
    /// none (#60).
    /// </summary>
    private Control? BuildBarTool(SettingsService settings)
    {
        var rows = settings.Sections
            .SelectMany(section => section.Rows)
            .Where(row => row.PageTop && row.Kind == SettingKind.Toggle)
            .ToList();

        if (rows.Count == 0)
        {
            return null;
        }

        var strip = new StackPanel { Name = BarToolName, Orientation = Orientation.Horizontal, Spacing = Gaps.Tile };

        foreach (var row in rows)
        {
            var box = new CheckBox { Content = row.Label, VerticalAlignment = VerticalAlignment.Center };

            AutomationProperties.SetName(box, row.Label);
            ToolTip.SetTip(box, D47.Core.Interface.HelpLinks.Plain(row.Help));

            // Not drawn: a toggle's write does not fail in a way worth a line under the page bar.
            var message = new StatusLine();

            box.IsCheckedChanged += (_, _) =>
            {
                if (!_refreshing)
                {
                    Apply(row, box.IsChecked == true ? "true" : "false", message);
                }
            };

            _barToolControls[row.Key] = box;
            _barToolRefreshes.Add(() => box.IsChecked = _settings!.Read(row.Key) is "true");
            strip.Children.Add(box);
        }

        return strip;
    }

    /// <summary>The place picker shown once the nav has collapsed (#220).</summary>
    private Stepper BuildPlaceDropdown()
    {
        var combo = new Stepper
        {
            Name = "AreaDropdown",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = new Thickness(0, 0, 0, 8),
            IsVisible = false,
        };
        DressAsAChoice(combo);
        AutomationProperties.SetName(combo, "Page");

        combo.SelectionChanged += (_, _) =>
        {
            if (_settingAreaDropdown || combo.SelectedIndex < 0)
            {
                return;
            }

            ShowPlace(_dropdownPlaceIndexes[combo.SelectedIndex]);
        };

        return combo;
    }

    /// <summary>Which section each entry in <see cref="_areaDropdown"/> names, since a place with no page is
    /// left out.</summary>
    private readonly List<int> _dropdownPlaceIndexes = [];

    /// <summary>Refills the place picker from the places that currently have a page.</summary>
    private void SyncPlaceDropdown()
    {
        if (_areaDropdown is not { } combo)
        {
            return;
        }

        _dropdownPlaceIndexes.Clear();
        var titles = new List<string>();

        for (var i = 0; i < _sections.Count; i++)
        {
            if (!_sift.Places[i].Exists)
            {
                continue;
            }

            _dropdownPlaceIndexes.Add(i);
            titles.Add($"{_navAreas[AreaOf(i)].Title} › {_sections[i].Title}");
        }

        _settingAreaDropdown = true;
        try
        {
            if (combo.ItemsSource is not IEnumerable<string> shown || !shown.SequenceEqual(titles))
            {
                combo.ItemsSource = titles;
            }

            combo.SelectedIndex = _dropdownPlaceIndexes.IndexOf(_activeSection);
        }
        finally
        {
            _settingAreaDropdown = false;
        }
    }

    /// <summary>
    /// The area's first place with a match while a query is typed, else its first place with a page,
    /// else its first place.
    /// </summary>
    private int FirstShownPlace(AreaView area)
    {
        var places = Enumerable.Range(area.First, area.Count).ToList();

        if (_query.Length > 0 && places.FirstOrDefault(i => MatchesIn(i) > 0, -1) is var matched and >= 0)
        {
            return matched;
        }

        return places.FirstOrDefault(i => _sift.Places[i].Exists, area.First);
    }

    /// <summary>Opens an area's first place (#220).</summary>
    public void SelectArea(int areaIndex)
    {
        if (areaIndex < 0 || areaIndex >= _navAreas.Count)
        {
            return;
        }

        ShowPlace(FirstShownPlace(_navAreas[areaIndex]));
    }

    /// <summary>Replaces the page with one place, filtered by the current query.</summary>
    internal void ShowPlace(int index)
    {
        if (index < 0 || index >= _sections.Count)
        {
            return;
        }

        var changed = _activeSection != index;

        _activeSection = index;
        _activeArea = AreaOf(index);

        ShowActiveInNav();
        RememberSection();
        Refresh();

        if (changed)
        {
            Scroller.Offset = default;
        }
    }

    /// <summary>Which area index a section belongs to.</summary>
    private int AreaOf(int sectionIndex) =>
        _navAreas.FindIndex(area => sectionIndex >= area.First && sectionIndex < area.First + area.Count);

    /// <summary>The area currently selected, by its id, for a test to read.</summary>
    internal string? ActiveAreaId => _activeArea >= 0 && _activeArea < _navAreas.Count ? _navAreas[_activeArea].Id : null;

    /// <summary>The open place's area heading is marked as its parent (#220).</summary>
    private void PaintAreaHeading(AreaView area, NavPaint paint)
    {
        if (area.Painted == paint)
        {
            return;
        }

        area.Painted = paint;

        area.Ink?.Dispose();
        area.Ink = Themed(
            area.HeadingText,
            TextBlock.ForegroundProperty,
            paint switch
            {
                NavPaint.Active => ThemeManager.WhiteKey,
                NavPaint.Dim => ThemeManager.Grey2Key,
                _ => ThemeManager.GreyKey,
            });

        area.HeadingBar.Opacity = paint == NavPaint.Active ? 1 : 0;

        area.Fill?.Dispose();
        area.Fill = null;

        if (paint == NavPaint.Active)
        {
            area.Fill = Themed(area.Heading, Border.BackgroundProperty, ThemeManager.Tile2Key);
        }
        else
        {
            area.Heading.Background = Brushes.Transparent;
        }
    }

    private (Border Item, Border Bar, TextBlock Text, TextBlock Count) BuildNavItem(int index, string title)
    {
        // The selected tree node's own mark: a 3px Accent bar (#279).
        var bar = new Border
        {
            Width = 3,
            Margin = new Thickness(0, 4),
            Opacity = 0,
        };
        Themed(bar, Border.BackgroundProperty, ThemeManager.AKey);

        var text = new TextBlock
        {
            Text = title,
            FontFamily = Fonts.ChromeFamily,
            FontSize = TypeScale.Body,
            Margin = new Thickness(8, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };

        // In the name's own ink, so it follows the same states.
        var count = new TextBlock
        {
            FontFamily = Fonts.ChromeFamily,
            FontSize = TypeScale.Secondary,
            Margin = new Thickness(8, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            IsVisible = false,
        };
        count.Bind(TextBlock.ForegroundProperty, text.GetObservable(TextBlock.ForegroundProperty));

        var words = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        Grid.SetColumn(count, 1);
        words.Children.Add(text);
        words.Children.Add(count);

        var layout = new DockPanel();
        DockPanel.SetDock(bar, Dock.Left);
        layout.Children.Add(bar);
        layout.Children.Add(words);

        var item = new Border
        {
            Padding = new Thickness(8, 8),

            // Indented under its area (#279).
            Margin = new Thickness(16, 0, 0, 0),
            Background = Brushes.Transparent,
            Cursor = new Cursor(StandardCursorType.Hand),
            Child = layout,
        };

        item.Classes.Add(NavPlaceClass);
        item.PointerPressed += (_, _) => ShowPlace(index);
        item.PointerEntered += (_, _) =>
        {
            if (index != _activeSection)
            {
                item.Background = Res(ThemeManager.TileKey);
            }
        };
        item.PointerExited += (_, _) =>
        {
            if (index != _activeSection)
            {
                item.Background = Brushes.Transparent;
            }
        };

        return (item, bar, text, count);
    }

    /// <summary>Opens the place the page was left on (#268).</summary>
    private void RestoreSection()
    {
        var remembered = _viewState.SettingsSection;

        // An id that names no place is stale, and leaves the first page open rather than failing, as Reveal
        // does with an id it cannot find.
        var index = _sections.FindIndex(
            section => string.Equals(section.PlaceId, remembered, StringComparison.Ordinal));

        if (index >= 0 && _sift.Places[index].Exists)
        {
            ShowPlace(index);
        }

        _rememberingSection = true;
    }

    /// <summary>Whether a change of page is worth writing down yet.</summary>
    private bool _rememberingSection;

    /// <summary>The sections' place ids, in nav order, and which one is open (−1 for none).</summary>
    internal IReadOnlyList<string> SectionIds => [.. _sections.Select(section => section.PlaceId)];

    /// <inheritdoc cref="SectionIds"/>
    internal int ActiveSection => _activeSection;

    private void RememberSection()
    {
        if (_rememberingSection)
        {
            SettleSection();
        }
    }

    /// <summary>Writes down the place the page is on (#268).</summary>
    internal void SettleSection()
    {
        if (_activeSection < 0 || _activeSection >= _sections.Count)
        {
            return;
        }

        var section = _sections[_activeSection].PlaceId;

        if (!string.Equals(_viewState.SettingsSection, section, StringComparison.Ordinal))
        {
            SaveViewState(state => state with { SettingsSection = section });
        }
    }

    /// <summary>
    /// Brings the highlighted nav entry into view, and only when it is not already there
    /// (remediation.md 17, item 3).
    /// </summary>
    private void ShowActiveInNav()
    {
        if (_activeSection < 0 || _activeSection >= _sections.Count)
        {
            return;
        }

        var item = _sections[_activeSection].NavItem;

        if (item.Bounds.Height <= 0)
        {
            // Not laid out yet, which is the case during Build.
            return;
        }

        var top = item.Bounds.Y;
        var bottom = top + item.Bounds.Height;
        var seen = NavScroller.Offset.Y;
        var floor = seen + NavScroller.Viewport.Height;

        if (top >= seen && bottom <= floor)
        {
            return;
        }

        // Scrolled to whichever edge it went past, so the list moves the least it can.
        NavScroller.Offset = NavScroller.Offset.WithY(
            top < seen ? top : bottom - NavScroller.Viewport.Height);
    }

    /// <summary>
    /// The open place's node: an A fill with Knock text. Another is Grey, and Grey2 while a query finds
    /// nothing in it (#279, #357).
    /// </summary>
    private void PaintNav(SectionView section, NavPaint paint)
    {
        if (section.Painted == paint)
        {
            return;
        }

        section.Painted = paint;

        var active = paint == NavPaint.Active;

        section.NavBar.Opacity = active ? 1 : 0;
        section.NavText.FontWeight = active ? FontWeight.Medium : FontWeight.Normal;

        section.NavFill?.Dispose();
        section.NavFill = null;

        section.NavInk?.Dispose();

        if (active)
        {
            section.NavFill = Themed(section.NavItem, Border.BackgroundProperty, ThemeManager.AKey);
            section.NavInk = Themed(section.NavText, TextBlock.ForegroundProperty, ThemeManager.KnockKey);
        }
        else
        {
            // No resource for "nothing", so the fill is dropped rather than bound.
            section.NavItem.Background = Brushes.Transparent;
            section.NavInk = Themed(
                section.NavText,
                TextBlock.ForegroundProperty,
                paint == NavPaint.Dim ? ThemeManager.Grey2Key : ThemeManager.GreyKey);
        }
    }

    /// <summary>
    /// Opens the page holding a capability's first row, with that page's folded rows drawn — what a help
    /// card pressed on the Transcript page does. A capability with no row here changes nothing.
    /// </summary>
    public void Reveal(string capabilityId)
    {
        var index = SectionHolding(capabilityId);

        if (index < 0)
        {
            return;
        }

        // A jump unfolds the place it lands on, and only that place (#60, #221).
        _revealedSections.Add(index);

        ShowPlace(index);
    }

    /// <summary>Opens the page holding a setting's row, and says whether there is one.</summary>
    internal bool ShowPlaceOf(string key)
    {
        var index = SectionOf(key);

        ShowPlace(index);

        return index >= 0;
    }

    /// <summary>
    /// Opens the page holding a setting's row with that page's folded rows drawn, for a message naming the
    /// row. A row not on this page changes nothing.
    /// </summary>
    public void RevealRow(string key)
    {
        var index = SectionOf(key);

        if (index < 0)
        {
            return;
        }

        _revealedSections.Add(index);

        ShowPlace(index);
    }

    private int SectionOf(string key) =>
        _rows.FirstOrDefault(
            view => view.Section >= 0 && string.Equals(view.Row.Key, key, StringComparison.Ordinal))?.Section ?? -1;

    /// <summary>The section holding a capability's first row on this page, or −1 where it has none here.</summary>
    private int SectionHolding(string capabilityId)
    {
        var owned = _settings?.Sections.FirstOrDefault(
            section => string.Equals(section.Capability.Id, capabilityId, StringComparison.Ordinal));

        if (owned is null)
        {
            return -1;
        }

        foreach (var row in owned.Rows)
        {
            if (_rows.FirstOrDefault(view => view.Section >= 0 && view.Row.Key == row.Key) is { } drawn)
            {
                return drawn.Section;
            }
        }

        return -1;
    }

    /// <summary>Keeps the page between a floor and a ceiling of width.</summary>
    private void OnScrollerSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        // A tab's own strip has no width floor to hold (#218).
        if (_tabPlaceId is not null)
        {
            return;
        }

        const double Floor = 420;

        // The margin the page is laid out with, both sides, and the vertical scroll bar's width.
        var available = e.NewSize.Width - 56 - ScrollBarWidth;

        Cards.Width = Math.Clamp(available, Floor, RowsMaxWidth);
    }

    /// <summary>The width the scroller's vertical bar takes from its viewport.</summary>
    private const double ScrollBarWidth = 16;

    /// <summary>The nav column's width, and the point below which it is not worth its space.</summary>
    private const double NavWidth = 224;

    private const double NavCollapsesBelow = 900;

    /// <summary>The floor with the nav and without it.</summary>
    private const double WideFloor = 700;

    private const double NarrowFloor = 476;

    /// <summary>Null until the first arrange, so the first pass always applies.</summary>
    private bool? _navShown;

    /// <summary>Collapses the nav column on a narrow page, and brings it back on a wide one.</summary>
    private void OnRootSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        // A tab's own strip has no nav to collapse or floor to hold (#218).
        if (_tabPlaceId is not null)
        {
            return;
        }

        var show = e.NewSize.Width >= NavCollapsesBelow;

        if (_navShown == show)
        {
            return;
        }

        _navShown = show;

        Nav.IsVisible = show;
        Root.ColumnDefinitions[0].Width = new GridLength(show ? NavWidth : 0);
        Root.MinWidth = show ? WideFloor : NarrowFloor;

        if (_areaDropdown is { } dropdown)
        {
            dropdown.IsVisible = !show;
        }
    }

    /// <summary>Changes one thing about the view state and writes it down, re-reading first (#268).</summary>
    private void SaveViewState(Func<ViewState, ViewState> change)
    {
        _viewState = change(_viewStateStore?.Load() ?? _viewState);
        _viewStateStore?.Save(_viewState);
    }

    /// <summary>Re-reads every row from settings.</summary>
    public void Refresh()
    {
        if (_settings is null)
        {
            return;
        }

        _sift = SettingsSieve.Sift(
            _query,
            [.. _rows.Select(row => new SieveRow(row.Row, row.Section, row.GroupIndex, row.Heading))],
            [.. _sections.Select((section, i) => new SievePlace(section.Title, section.Terms, AreaOf(i)))],
            [.. _navAreas.Select(area => area.Title)],
            [.. _groups.Select(group => new SieveGroup(group.Title, group.Help))],
            _settings.Current,
            _settings.IsChanged,
            ShowingEverything,
            _revealedSections);

        _refreshing = true;
        try
        {
            foreach (var refresh in _barToolRefreshes)
            {
                refresh();
            }

            for (var r = 0; r < _rows.Count; r++)
            {
                var row = _rows[r];
                var shown = _sift.Rows[r].Shown;

                row.Container.IsVisible = shown;
                row.Refresh();

                // Greyed out rather than absent: the setting still exists and still holds a value, it
                // just does nothing right now (#378). Left alone on a row with no DisabledWhen, so this
                // never overrides ShowBusy's own IsEnabled on a row that has one.
                if (row.Row.DisabledWhen is not null && row.Control is not null)
                {
                    row.Control.IsEnabled = !row.Row.DisabledFor(_settings.Current);
                }

                // Only the survivors.
                if (shown)
                {
                    Illuminate(row);
                }
            }
        }
        finally
        {
            _refreshing = false;
        }

        var filtering = _sift.Filtering;

        // The section's name, marked in the nav, and its "Show N more" at the foot of its page.
        for (var i = 0; i < _sections.Count; i++)
        {
            Paint(_sections[i].NavText, _sections[i].Title);

            if (_sections[i].FoldButton is not { } button)
            {
                continue;
            }

            var revealed = _revealedSections.Contains(i);

            button.IsVisible = !ShowingEverything && !filtering && (_sift.Places[i].Folded > 0 || revealed);
            button.Content = revealed ? "Show fewer" : $"Show {_sift.Places[i].Folded} more";
        }

        // An area's title, marked in the nav (#222).
        foreach (var area in _navAreas)
        {
            Paint(area.HeadingText, area.Title);
        }

        // A group's own title and help, the other two things a query can match (#222). A group with no row
        // drawn is not drawn either, heading and all.
        for (var g = 0; g < _groups.Count; g++)
        {
            var group = _groups[g];

            group.Container.IsVisible = _sift.Groups[g].Showing;
            group.Reset.IsEnabled = _sift.Groups[g].Changed || (group.Slot is { } slot && HasAnchor(slot));

            Paint(group.HeadingText, group.Title.ToUpperInvariant());
            Paint(group.HelpText, group.Help);
        }

        foreach (var tiles in _tileGrids)
        {
            tiles.IsVisible = tiles.Children.Any(tile => tile.IsVisible);
        }

        RefreshMixers();

        UpdateOtherTabs();
        ApplyFilterToNav();
        LayoutPage();
    }

    /// <summary>
    /// Fills in the "On other tabs" section with this query's matches on the tab places — empty and
    /// hidden with no query, and always empty off the settings page (#222).
    /// </summary>
    private void UpdateOtherTabs()
    {
        if (_otherTabsSection is not { } section || _otherTabsList is not { } list || _settings is null)
        {
            return;
        }

        list.Children.Clear();

        if (_query.Length == 0)
        {
            section.IsVisible = false;
            return;
        }

        var matches = _tabPlaceRows
            .Where(entry => entry.Row.Applies(_settings.Current) && !entry.Row.DrawnElsewhere)
            .Where(entry => SettingsSieve.Matches(entry.Row, _query))
            .ToList();

        foreach (var (tab, row) in matches)
        {
            list.Children.Add(BuildOtherTabRow(tab, row));
        }

        section.IsVisible = matches.Count > 0;
    }

    /// <summary>What the surface is being filtered by, or empty when it is not.</summary>
    private string _query = string.Empty;

    /// <summary>
    /// Shows only the rows that match, and marks what the query found in each of them (Phase 12,
    /// "Search whichever tab you are looking at").
    /// </summary>
    public bool Filters => true;

    public void Filter(string? query)
    {
        var wanted = query?.Trim() ?? string.Empty;

        if (string.Equals(_query, wanted, StringComparison.Ordinal))
        {
            return;
        }

        _query = wanted;
        Refresh();
    }

    /// <summary>Label, help or key.</summary>
    private void Illuminate(RowView row)
    {
        if (row.Label is { } label)
        {
            Paint(label, row.Row.Label);
        }

        if (row.Spoken is { } spoken)
        {
            Paint(spoken, row.Row.Help);
        }

        // **The help is behind a glyph, so a query that only it answers has to bring it out.** Matches() has
        // always tested the help text, and since the callout it is no longer on screen — so a row could stay
        // behind a filter with every visible word on it disagreeing with the query, which reads as the filter
        // being broken rather than as a match the Commander cannot see.
        if (row.Help is { } help)
        {
            var inTheHelp = _query.Length > 0
                && row.Row.Help.Contains(_query, StringComparison.OrdinalIgnoreCase)
                && !row.Row.Label.Contains(_query, StringComparison.OrdinalIgnoreCase);

            help.IsVisible = inTheHelp;

            if (inTheHelp)
            {
                Paint(help, row.Row.Help);
            }
        }

        if (row.KeyLine is not { } keyLine)
        {
            return;
        }

        var onlyTheKey = _query.Length > 0
            && row.Row.Key.Contains(_query, StringComparison.OrdinalIgnoreCase)
            && !row.Row.Label.Contains(_query, StringComparison.OrdinalIgnoreCase)
            && !row.Row.Help.Contains(_query, StringComparison.OrdinalIgnoreCase);

        keyLine.IsVisible = onlyTheKey;

        if (onlyTheKey)
        {
            Paint(keyLine, row.Row.Key);
        }
    }

    /// <summary>
    /// One block of caption text with the hits in it marked, or the plain string when there is no
    /// query.
    /// </summary>
    private void Paint(TextBlock block, string markup)
    {
        // The sentence without its markup: what is read out, what is searched, and what is drawn where there
        // is no link to draw.
        var segments = D47.Core.Interface.HelpLinks.Parse(markup);
        var text = D47.Core.Interface.HelpLinks.Plain(markup);

        // A block composed of runs reports no Text of its own, and Text is what an automation peer reads — so
        // the name is set outright rather than left to be inferred.
        AutomationProperties.SetName(block, text);

        // Qualified: Avalonia.Controls has a TextSearch of its own, about typing to select an item in a list,
        // and it is the one that wins in this file's usings.
        var matches = D47.Core.Interface.TextSearch.Find(text, _query);

        var links = segments.Any(segment => segment.Target is not null);

        if (matches.Count == 0 && !links)
        {
            block.Inlines?.Clear();
            block.Text = text;
            return;
        }

        if (links)
        {
            PaintWithLinks(block, segments, matches);
            return;
        }

        // Text and Inlines both draw, one after the other.
        block.Text = null;
        block.Inlines!.Clear();

        var cursor = 0;

        foreach (var match in matches)
        {
            if (match.Start > cursor)
            {
                block.Inlines!.Add(new Run(text[cursor..match.Start]));
            }

            var hit = new Run(text[match.Start..match.End]);
            hit.Bind(TextElement.BackgroundProperty, this.GetResourceObservable(ThemeManager.LineKey));

            block.Inlines!.Add(hit);
            cursor = match.End;
        }

        if (cursor < text.Length)
        {
            block.Inlines!.Add(new Run(text[cursor..]));
        }
    }

    /// <summary>The same caption when some of it is a cross-reference (#65).</summary>
    private void PaintWithLinks(
        TextBlock block,
        IReadOnlyList<D47.Core.Interface.HelpSegment> segments,
        IReadOnlyList<D47.Core.Interface.SearchMatch> matches)
    {
        block.Text = null;
        block.Inlines!.Clear();

        var at = 0;

        foreach (var segment in segments)
        {
            var start = at;
            var end = at + segment.Text.Length;
            at = end;

            // Every boundary inside this stretch: where it starts, where it ends, and every edge of every hit
            // that falls in it.
            var cuts = new SortedSet<int> { start, end };

            foreach (var match in matches)
            {
                if (match.Start > start && match.Start < end) { cuts.Add(match.Start); }
                if (match.End > start && match.End < end) { cuts.Add(match.End); }
            }

            var edges = cuts.ToArray();

            for (var i = 0; i + 1 < edges.Length; i++)
            {
                var from = edges[i];
                var to = edges[i + 1];

                var run = new Run(segment.Text[(from - start)..(to - start)]);
                var marked = matches.Any(match => match.Start <= from && match.End >= to);

                if (marked)
                {
                    run.Bind(
                        TextElement.BackgroundProperty,
                        this.GetResourceObservable(ThemeManager.LineKey));
                }

                if (segment.Target is not null)
                {
                    run.Bind(
                        TextElement.ForegroundProperty,
                        this.GetResourceObservable(ThemeManager.AKey));

                    run.TextDecorations = TextDecorations.Underline;
                }

                block.Inlines!.Add(run);
            }
        }

        // The click is on the block rather than per-run: a Run is not an input element in Avalonia, so it has
        // no pointer events of its own.
        var targets = segments.Where(segment => segment.Target is not null).ToList();

        if (targets.Count == 0)
        {
            return;
        }

        block.Cursor = new Cursor(StandardCursorType.Hand);

        // One handler per painted block, and Paint runs again on every filter keystroke - so the old one is
        // dropped rather than stacked, or a caption painted twenty times would jump twenty times on one
        // click.
        if (_linkHandlers.TryGetValue(block, out var previous))
        {
            block.PointerPressed -= previous;
        }

        EventHandler<PointerPressedEventArgs> handler = (_, e) =>
        {
            // Whichever section the first link on this caption names.
            var index = SectionHolding(targets[0].Target!);

            if (index >= 0)
            {
                ShowPlace(index);
                e.Handled = true;
            }
        };

        block.PointerPressed += handler;
        _linkHandlers[block] = handler;
    }

    /// <summary>
    /// The click handler each linked caption currently carries, so repainting replaces it instead of
    /// adding a second one.
    /// </summary>
    private readonly Dictionary<TextBlock, EventHandler<PointerPressedEventArgs>> _linkHandlers = [];

    /// <summary>
    /// Marks the nav: every place with a page, and while a query is typed, the count of its matching
    /// rows beside it and every place and area without one drawn in Grey2.
    /// </summary>
    private void ApplyFilterToNav()
    {
        var filtering = _query.Length > 0;

        for (var i = 0; i < _sections.Count; i++)
        {
            var section = _sections[i];

            var matches = MatchesIn(i);

            section.NavItem.IsVisible = _sift.Places[i].Exists;
            section.NavCount.Text = matches.ToString(CultureInfo.InvariantCulture);
            section.NavCount.IsVisible = matches > 0;
        }

        // A place none of whose rows apply is not left open.
        if (_activeSection >= 0 && !_sift.Places[_activeSection].Exists && FirstExisting() is var next and >= 0)
        {
            _activeSection = next;
            _activeArea = AreaOf(next);
        }

        for (var i = 0; i < _sections.Count; i++)
        {
            var section = _sections[i];

            PaintNav(
                section,
                i == _activeSection ? NavPaint.Active
                : filtering && MatchesIn(i) == 0 ? NavPaint.Dim
                : NavPaint.Normal);
        }

        for (var a = 0; a < _navAreas.Count; a++)
        {
            var area = _navAreas[a];
            var places = Enumerable.Range(area.First, area.Count);

            area.Heading.IsVisible = places.Any(i => _sift.Places[i].Exists);

            PaintAreaHeading(
                area,
                a == _activeArea ? NavPaint.Active
                : filtering && !places.Any(i => MatchesIn(i) > 0) ? NavPaint.Dim
                : NavPaint.Normal);
        }

        SyncPlaceDropdown();
    }

    /// <summary>The first place with a page, or −1 where none has one.</summary>
    private int FirstExisting() => _sift.Places.ToList().FindIndex(place => place.Exists);

    /// <summary>The rows a typed query leaves on a place, or 0 with no query.</summary>
    private int MatchesIn(int place) => _sift.Filtering ? _sift.Places[place].Showing : 0;

    /// <summary>What the last <see cref="Refresh"/> decided.</summary>
    private SettingsSift _sift = new(string.Empty, [], [], []);

    /// <summary>
    /// Assembles the page: the collapsed-nav picker, the open place's head and rows, the line saying
    /// nothing on it matches, and "On other tabs".
    /// </summary>
    /// <remarks>
    /// A no-op when the order already matches, so an ordinary row edit never resets the scroll offset.
    /// </remarks>
    private void LayoutPage()
    {
        if (_tabPlaceId is not null)
        {
            return;
        }

        var order = new List<Control>();

        if (_areaDropdown is { } dropdown)
        {
            order.Add(dropdown);
        }

        if (_activeSection >= 0 && _activeSection < _sections.Count
            && _pageHead is { } head && _pageCrumb is { } crumb && _pageTitle is { } title
            && _areaLegend is { } legend && _emptyBlock is { } empty)
        {
            var section = _sections[_activeSection];
            var filtering = _query.Length > 0;
            var nothing = filtering && MatchesIn(_activeSection) == 0;

            crumb.Text = $"{_navAreas[_activeArea].Title.ToUpperInvariant()} ›";
            Paint(title, section.Title.ToUpperInvariant());

            legend.IsVisible = _rows.Any(row => row.Row.Protected && row.Section == _activeSection);

            order.Add(head);

            section.Content.IsVisible = !nothing;
            order.Add(section.Content);

            if (nothing)
            {
                _emptyNote!.Text = $"Nothing on this page matches \"{_query}\".";

                var others = Enumerable.Range(0, _sections.Count).Count(other => other != _activeSection && MatchesIn(other) > 0);

                _otherPagesNote!.IsVisible = others > 0;
                _otherPagesNote.Text = others == 1
                    ? "Matches on 1 other page are marked in the list."
                    : $"Matches on {others} other pages are marked in the list.";

                order.Add(empty);
            }
        }

        if (_otherTabsSection is { } otherTabs)
        {
            order.Add(otherTabs);
        }

        if (_layingOut || Cards.Children.SequenceEqual(order))
        {
            return;
        }

        // Clearing detaches a focused text box, whose LostFocus saves and refreshes back into here.
        _layingOut = true;

        try
        {
            Cards.Children.Clear();
            Cards.Children.AddRange(order);
        }
        finally
        {
            _layingOut = false;
        }
    }

    /// <summary>Whether <see cref="LayoutPage"/> is replacing the page's children.</summary>
    private bool _layingOut;

    /// <summary>The width a compact row's control is built to.</summary>
    internal const double StandardControlWidth = 190;

    /// <summary>The label column's width, the same on every row so every control starts at one x.</summary>
    internal const double LabelColumnWidth = 240;

    /// <summary>The gap either side of the control column.</summary>
    internal const double RowColumnGap = 16;

    /// <summary>The reset gutter's width, reserved on every row whether or not it draws one (#332).</summary>
    private const double ResetGutterWidth = 44;

    /// <summary>A row's minimum height.</summary>
    private const double RowMinHeight = 56;

    /// <summary>The widest the page's rows run, bar to reset gutter, so each reset stays near its control.</summary>
    public const double RowsMaxWidth = 900;

    /// <summary>A row's own top and bottom padding (#332).</summary>
    private const double RowVerticalPadding = 8;

    /// <summary>A row's own left and right padding.</summary>
    internal const double RowHorizontalPadding = 12;

    /// <summary>The width of every row's left bar: <c>A</c> on a protected row, transparent on the rest (#333).</summary>
    internal const double RowBarWidth = 3;

    /// <summary>
    /// Marks a settings row's grid, so a test can find the rows this view builds rather than every grid
    /// that happens to be in the tree.
    /// </summary>
    public const string CompactRowClass = "compact-row";

    /// <summary>The height every control that opens a list stands at, and the padding inside it.</summary>
    internal const double ChoiceHeight = 32;

    internal static readonly Thickness ChoicePadding = new(12, 4);

    /// <summary>One look for the two controls that open a list.</summary>
    private bool ShowingEverything =>
        _tabPlaceId is not null || (_settings?.Current.Ui.ShowEverySetting ?? true);

    internal static void DressAsAChoice(TemplatedControl control)
    {
        // A stepper sizes itself: a 44px frame with its position line under it, which 32px would cut off.
        if (control is Stepper)
        {
            return;
        }

        // A segment row sizes itself too: fixing its height clips wrapped labels (#408).
        if (control is Segment)
        {
            control.Padding = ChoicePadding;
            control.BorderThickness = new Thickness(1);
            control.FontSize = TypeScale.Body;
            return;
        }

        // Fixed rather than a floor.
        control.Height = ChoiceHeight;
        control.Padding = ChoicePadding;
        control.BorderThickness = new Thickness(1);
        control.FontSize = TypeScale.Body;
    }

    /// <summary>A row on the page ground under a 1px Line rule.</summary>
    private Border RuledRow(Control child, double minHeight, Thickness padding)
    {
        var row = new Border
        {
            MinHeight = minHeight,
            Padding = padding,
            BorderThickness = new Thickness(0, 1, 0, 0),
            Child = child,
        };
        Themed(row, Border.BorderBrushProperty, ThemeManager.LineKey);

        return row;
    }

    /// <summary>A row's inline tag: the word in uppercase chrome type, Grey.</summary>
    private static TextBlock RowTag(string said) => D47.App.Panel.RoutingKit.Tag(said, ThemeManager.GreyKey);

    /// <summary>One row; <paramref name="under"/> indents its caption inside the label column.</summary>
    private RowView BuildRow(CapabilityDescriptor capability, SettingRow row, bool under = false)
    {
        var header = new DockPanel { HorizontalAlignment = HorizontalAlignment.Left };

        var label = RowLabel(row.Label);

        // This value is the flying Commander's; a second Commander on this machine sees their own (Phase 44).
        if (row.Scope == SettingScope.Commander)
        {
            var tag = RowTag("per Commander");
            tag.Margin = new Thickness(0, 4, 0, 0);
            tag.HorizontalAlignment = HorizontalAlignment.Left;
            DockPanel.SetDock(tag, Dock.Bottom);
            header.Children.Add(tag);
        }

        header.Children.Add(label);

        // Hidden inline copy, shown only when a search matches text no other visible control carries (#333).
        var help = new TextBlock
        {
            Text = row.Help,
            FontSize = TypeScale.Secondary,
            Margin = new Thickness(0, 4, 0, 0),
            TextWrapping = TextWrapping.Wrap,
            IsVisible = false,
        };
        Themed(help, TextBlock.ForegroundProperty, ThemeManager.GreyKey);

        var message = new StatusLine { Margin = new Thickness(0, 4, 0, 0) };

        var (control, refresh, underRow) = _controls.Build(row, message);

        // The settings key, shown only when it is the reason this row survived a filter.
        var keyLine = new TextBlock
        {
            FontSize = TypeScale.Small,
            Margin = new Thickness(0, 4, 0, 0),
            TextWrapping = TextWrapping.Wrap,
            IsVisible = false,
        };
        Themed(keyLine, TextBlock.ForegroundProperty, ThemeManager.Grey2Key);

        // A way back from this one row .com/dseelinger/d47/issues/61), on the rows the Commander has actually
        // changed and nowhere else.
        var spoken = new TextBlock
        {
            Text = row.Help,
            FontSize = TypeScale.Secondary,
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 420,
        };
        Themed(spoken, TextBlock.ForegroundProperty, ThemeManager.WhiteKey);

        if (!string.IsNullOrWhiteSpace(row.Help))
        {
            AttachHelp(label, spoken);
        }

        // A square glyph button at the end of the row rather than beside the label, so it stays put
        // however wide the caption or the control run (#279).
        Button? resetButton = null;

        // A row drawing its whole group leaves resetting to the group head.
        if (row is { Kind: not SettingKind.Secret, Binding.Write: not null } && underRow is null)
        {
            // Named so anything looking for "the control this row is about" can tell this apart from it.
            var back = ResetGlyph(RowResetName, $"Reset {row.Label}");
            back.Margin = new Thickness(8, 0, 0, 0);
            back.IsVisible = false;

            back.Click += (_, _) =>
            {
                // Every key the control holds, so resetting a merged row puts both halves back rather than
                // the one whose key happens to name the row (#217).
                foreach (var key in row.BoundKeys)
                {
                    _settings!.Reset(key, SettingsCaller.Panel);
                }

                Refresh();
            };

            resetButton = back;

            // Folded into the row's refresh rather than set once, because whether this row has been changed
            // is exactly what a reset — or any other write — moves.
            var shownBefore = refresh;

            refresh = () =>
            {
                shownBefore();
                back.IsVisible = _settings is { } settings && row.BoundKeys.Any(settings.IsChanged);
            };
        }

        var caption = new StackPanel { Spacing = 0 };

        if (under && !row.PageTop)
        {
            caption.Margin = new Thickness(UnderIndent, 0, 0, 0);
        }

        caption.Children.Add(header);
        caption.Children.Add(help);
        caption.Children.Add(keyLine);

        if (row.Note is { } noteSource)
        {
            var note = new TextBlock
            {
                FontSize = TypeScale.Secondary,
                Margin = new Thickness(0, 4, 0, 0),
                TextWrapping = TextWrapping.Wrap,
                IsVisible = false,
            };
            Themed(note, TextBlock.ForegroundProperty, ThemeManager.GreyKey);
            caption.Children.Add(note);

            var shownBefore = refresh;

            refresh = () =>
            {
                shownBefore();
                note.Text = noteSource(_settings!.Current);
                note.IsVisible = note.Text is not null;
            };
        }

        if (row is { ValueAsHint: true, Binding: { } hinted })
        {
            // On the caption rather than on the label alone, so the help line under it answers the hover too
            // — the request was "the label or the description", and the two read as one block.
            var describe = refresh;

            refresh = () =>
            {
                describe();
                ToolTip.SetTip(caption, hinted.Read(_settings!.Current));
            };

            // The pointer has to have something to be over.
            caption.Background = Brushes.Transparent;
        }

        caption.VerticalAlignment = VerticalAlignment.Center;
        control.VerticalAlignment = VerticalAlignment.Center;

        // A group's control grows downward as it opens; its label stays level with the control's first line.
        if (underRow is not null)
        {
            caption.VerticalAlignment = VerticalAlignment.Top;
            control.VerticalAlignment = VerticalAlignment.Top;
            caption.Margin = new Thickness(0, 10, 0, 0);
        }

        control.HorizontalAlignment = row.PageTop
            ? HorizontalAlignment.Right
            : underRow is null ? HorizontalAlignment.Left : HorizontalAlignment.Stretch;

        Grid grid;
        Control line;

        if (row.PageTop)
        {
            grid = new Grid
            {
                ColumnDefinitions =
                [
                    new ColumnDefinition(GridLength.Auto),
                    new ColumnDefinition(12, GridUnitType.Pixel),
                    new ColumnDefinition(GridLength.Auto),
                ],
                HorizontalAlignment = HorizontalAlignment.Right,
            };

            Grid.SetColumn(caption, 0);
            Grid.SetColumn(control, 2);
            grid.Children.Add(caption);
            grid.Children.Add(control);

            line = grid;

            // The page-top strip has no gutter column, so its reset docks outside the grid (#279).
            if (resetButton is not null)
            {
                var dock = new DockPanel { LastChildFill = true };
                DockPanel.SetDock(resetButton, Dock.Right);
                dock.Children.Add(resetButton);
                dock.Children.Add(grid);
                line = dock;
            }
        }
        else
        {
            // Fixed columns on every row, so every label, every control and every reset gutter sits at one x
            // down the page whichever rows the filter leaves showing.
            grid = RowColumns(caption, control, resetButton);
            line = RowFrame(grid, row.Protected);
        }

        Control body = grid;

        var container = new StackPanel();
        container.Children.Add(line);

        if (underRow is { Block: { } block })
        {
            container.Children.Add(block);
        }

        container.Children.Add(message);

        return new RowView(row, container, refresh)
        {
            Heading = underRow?.Words,
            Control = control,
            Body = body,
            Label = label,
            Help = help,
            Spoken = spoken,
            KeyLine = keyLine,
        };
    }

    /// <summary>
    /// The row's help, reached by hovering or focusing its label rather than a separate glyph — the
    /// label is where a Commander already looks to know what the row is (#333). The card heading's own
    /// HELP reaches the capability's page; this tooltip carries text only (#383).
    /// </summary>
    private static void AttachHelp(TextBlock label, TextBlock spoken)
    {
        label.Focusable = true;
        ToolTip.SetTip(label, spoken);

        // A TextBlock shows its tooltip on hover already; keyboard focus needs to open and close it by hand.
        label.GotFocus += (_, _) => ToolTip.SetIsOpen(label, true);
        label.LostFocus += (_, _) => ToolTip.SetIsOpen(label, false);
    }

    /// <summary>
    /// Says that a row is waiting on something that is not happening in this class — the gap reaction
    /// spends a model round trip between the Commander picking a core and that core saying its first
    /// word, and the affordance they touched is this row.
    /// </summary>
    internal Control? ControlFor(string key) =>
        _rows.FirstOrDefault(row => string.Equals(row.Row.Key, key, StringComparison.Ordinal))?.Control
        ?? _controls.DrawnByGroup.GetValueOrDefault(key)
        ?? _barToolControls.GetValueOrDefault(key);

    /// <summary>The row's own label, which carries its help as a tooltip on hover and focus (#333).</summary>
    internal TextBlock? LabelFor(string key) =>
        _rows.FirstOrDefault(row => string.Equals(row.Row.Key, key, StringComparison.Ordinal))?.Label;

    public void ShowBusy(string key, bool busy)
    {
        if (_rows.FirstOrDefault(row => string.Equals(row.Row.Key, key, StringComparison.Ordinal))
            is not { } view)
        {
            return;
        }

        if (busy && view.Busy is null && view.Body is Grid grid)
        {
            // In the spacer column between the caption and the control, which is 16 wide and holds nothing —
            // so the glyph lands beside the control that was touched without taking a pixel from either side
            // of it.
            var glyph = new BusyGlyph
            {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
            };
            Themed(glyph, BusyGlyph.StrokeProperty, ThemeManager.AKey);

            Grid.SetColumn(glyph, 1);
            grid.Children.Add(glyph);

            view.Busy = glyph;
        }

        if (view.Busy is not null)
        {
            view.Busy.IsVisible = busy;
        }

        // Shut while it runs, like every other affordance that is working: picking a second core before the
        // first one has spoken queues a second round trip behind the first.
        if (view.Control is not null)
        {
            view.Control.IsEnabled = !busy;
        }
    }

    /// <summary>Marks the data block an Info row's value is drawn on.</summary>
    public const string DataBlockClass = "data-block";

    /// <summary>A Report: its value in White on a Slab data block.</summary>
    internal static (Border Inset, SelectableTextBlock Text) Report()
    {
        var text = new SelectableTextBlock { FontSize = TypeScale.Body, TextWrapping = TextWrapping.Wrap };
        text[!SelectableTextBlock.ForegroundProperty] = new DynamicResourceExtension(ThemeManager.WhiteKey);

        var inset = new Border
        {
            Padding = new Thickness(14, 10),
            Child = text,
            Classes = { DataBlockClass },
        };
        inset[!Border.BackgroundProperty] = new DynamicResourceExtension(ThemeManager.SlabKey);

        return (inset, text);
    }

    /// <summary>The button class the kit draws red, for a press that destroys something.</summary>
    public const string DestructiveClass = "destructive";

    /// <summary>Marks a binding chip, so a test can read what a bind row shows.</summary>
    public const string BindingChipClass = "binding-chip";

    /// <summary>A binding chip: the bound key in mono capitals, White on Slab. Shows; does not start a capture.</summary>
    internal static (Border Chip, TextBlock Text) BindingChip(string? text = null)
    {
        var said = new TextBlock
        {
            Text = text?.ToUpperInvariant(),
            FontFamily = new FontFamily(Fonts.MonoFamily),
            FontSize = TypeScale.Secondary,
            FontWeight = FontWeight.Normal,
            LetterSpacing = 0,
            VerticalAlignment = VerticalAlignment.Center,
        };
        said[!TextBlock.ForegroundProperty] = new DynamicResourceExtension(ThemeManager.WhiteKey);

        var chip = new Border
        {
            Padding = new Thickness(12, 8),
            MinHeight = TypeScale.MinimumTarget - 12,
            Child = said,
            Classes = { BindingChipClass },
        };
        chip[!Border.BackgroundProperty] = new DynamicResourceExtension(ThemeManager.SlabKey);

        return (chip, said);
    }

    /// <summary>How long a first press stays armed before a second press has to ask again (#85).</summary>
    internal static readonly TimeSpan ConfirmPressWindow = TimeSpan.FromSeconds(4);

    /// <summary>More options than this and a Choice row opens the picker page rather than stepping.</summary>
    public const int LongListThreshold = 7;

    /// <summary>A 0–1 row, recognised by its declared range and step: drawn as a <see cref="Level"/> bar.</summary>
    internal static bool IsLevel(SettingRow row) =>
        row.Kind == SettingKind.Number && row.Maximum == 1 && row.Step == 0.05;

    private bool Apply(SettingRow row, string? value, StatusLine message) =>
        Apply(row.Key, value, message);

    /// <summary>
    /// By key rather than by row, because one control can hold two of them (#217) and the message line,
    /// the redraw and the refusal are the same for both halves.
    /// </summary>
    private bool Apply(string key, string? value, StatusLine message)
    {
        if (_settings is null)
        {
            return false;
        }

        var result = _settings.Apply(key, value, SettingsCaller.Panel);

        // Only failures are worth *saying*.
        if (result.Ok)
        {
            message.Clear();
        }
        else
        {
            message.Fail(result.Message);
        }

        // **But every outcome is worth redrawing** (#90).
        Refresh();

        return result.Ok;
    }

    /// <summary>
    /// Gives an "On other tabs" match somewhere to go — this view has no way to change tab itself
    /// (#222). <paramref name="open"/> takes the matched <see cref="SettingsTabPlace.RootKey"/>.
    /// </summary>
    public void EnableTabJump(Action<string> open) => _openTabPlace = open;

    /// <summary>
    /// Opens this instance's own tab strip — a no-op off a tab place, or where it is already open. What
    /// an "On other tabs" match asks for once the tab and root it names are on screen (#222).
    /// </summary>
    public void ExpandTabStrip()
    {
        if (_tabPlaceId is not { } placeId || _tabStripContent is not { } content
            || _tabStripHeader is not { } header || content.IsVisible)
        {
            return;
        }

        content.IsVisible = true;
        header(true);
        SaveViewState(state => state.With(placeId, true));
    }

    /// <summary>How a place's nav entry is painted.</summary>
    private enum NavPaint
    {
        Normal,
        Active,

        /// <summary>No match for the query.</summary>
        Dim,
    }

    private sealed record SectionView(
        string PlaceId,
        string Title,

        /// <summary>The place's own search abbreviations — "ptt" for Microphone, and so on (#222).</summary>
        IReadOnlyList<string> Terms,

        /// <summary>The guide the panel's HELP opens while this place is showing.</summary>
        string DocsCapabilityId,

        /// <summary>The place's groups and rows, drawn as the page while it is open.</summary>
        StackPanel Content,
        IReadOnlyList<SettingRow> Rows,
        Border NavItem,
        Border NavBar,
        TextBlock NavText,

        /// <summary>How many rows match the query, beside the place's name in the nav.</summary>
        TextBlock NavCount)
    {
        /// <summary>This place's own "Show N more" for the rows its fold is hiding (#221).</summary>
        public Button? FoldButton { get; init; }

        /// <summary>How the nav item is currently painted, or null before it has been painted at all.</summary>
        public NavPaint? Painted { get; set; }

        /// <summary>The nav item's live fill subscription, held so the next state can drop it.</summary>
        public IDisposable? NavFill { get; set; }

        /// <summary>The nav item's live text-ink subscription, held so the next state can drop it.</summary>
        public IDisposable? NavInk { get; set; }
    }

    /// <summary>One area's nav heading and the run of sections beneath it (#220).</summary>
    private sealed record AreaView(
        string Id,
        string Title,
        Border Heading,
        TextBlock HeadingText,

        /// <summary>The 3px bar that marks the selected tree node, shared with a place's own (#279).</summary>
        Border HeadingBar,
        int First,
        int Count)
    {
        /// <summary>How the heading is currently painted, or null before it has been painted at all.</summary>
        public NavPaint? Painted { get; set; }

        public IDisposable? Ink { get; set; }

        public IDisposable? Fill { get; set; }
    }

    /// <summary>A named group heading within a place, so a query matching it reveals every row under it
    /// (#222).</summary>
    /// <param name="Slot">The headset surface a placement group's reset also clears the anchor of.</param>
    private sealed record GroupView(
        int Section,
        string Title,
        string Help,
        Control Container,
        TextBlock HeadingText,
        TextBlock HelpText,
        Button Reset,
        string? Slot);

    private sealed record RowView(SettingRow Row, Control Container, Action Refresh)
    {
        /// <summary>Which card this row is in, so a filter can hide a card that has emptied.</summary>
        public int Section { get; init; } = -1;

        /// <summary>
        /// Which of <see cref="_groups"/> this row is under, or −1 on a tab place, which has no groups.
        /// </summary>
        public int GroupIndex { get; init; } = -1;

        /// <summary>The control the Commander touches, so a slow answer can shut it.</summary>
        public Control? Control { get; init; }

        /// <summary>The row's layout, which is a three-column grid where the row is compact.</summary>
        public Control? Body { get; init; }

        /// <summary>Made the first time this row has something slow to say.</summary>
        public BusyGlyph? Busy { get; set; }

        /// <summary>The row's words, held so a query can be painted into them and taken out again.</summary>
        public TextBlock? Label { get; init; }

        /// <summary>The inline copy, drawn only when a search matched words only it holds.</summary>
        public TextBlock? Help { get; init; }

        /// <summary>The callout's copy — what a Commander reads when they press the glyph.</summary>
        public TextBlock? Spoken { get; init; }

        /// <summary>The settings key, drawn only when it is why this row survived.</summary>
        public TextBlock? KeyLine { get; init; }

        /// <summary>A name the row is drawn under, which a query also matches: a mixer's channel.</summary>
        public string? Heading { get; init; }
    }
}
