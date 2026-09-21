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
using Avalonia.Media;
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
public partial class SettingsView : UserControl, D47.App.Panel.IFilterablePage
{

    private readonly List<SectionView> _sections = [];
    private readonly List<RowView> _rows = [];

    private SettingsService? _settings;

    /// <summary>
    /// The per-card reset controls, so each can be hidden again once its card is back at its defaults
    /// (#61).
    /// </summary>
    private readonly List<(IReadOnlyList<SettingRow> Rows, Button Button)> _cardResets = [];

    /// <summary>Each area's nav heading, and the run of sections beneath it.</summary>
    private readonly List<AreaView> _navAreas = [];

    /// <summary>Which area's places the scroller is showing (#220), or −1 before <see cref="Build"/> runs.</summary>
    private int _activeArea = -1;

    /// <summary>The selected area's title and sentence, drawn above its cards.</summary>
    private TextBlock? _areaHeaderTitle;

    private TextBlock? _areaHeaderSentence;

    /// <summary>The protected-row legend, shown once under the screen title when the area has one (#333).</summary>
    private TextBlock? _areaLegend;

    private StackPanel? _areaHeader;

    /// <summary>The area picker shown once the nav has collapsed (#220).</summary>
    private Stepper? _areaDropdown;

    /// <summary>True while <see cref="SelectArea"/> is writing <see cref="_areaDropdown"/>, so its own
    /// selection change does not loop back into another select.</summary>
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

    /// <summary>
    /// The strip above the cards holding the page's own controls, or null where there are none (#60).
    /// </summary>
    private StackPanel? _pageStrip;

    /// <summary>Whichever control sits above the area header — <see cref="_pageStrip"/> or the bulk row alone.</summary>
    private Control? _topStrip;
    private ViewStateStore? _viewStateStore;
    private ViewState _viewState = new();
    private AppPaths? _paths;

    /// <summary>Where a placement group's reset glyph reaches to clear the anchor it holds (#162).</summary>
    private D47.App.Headset.VrHost? _vrHost;

    /// <summary>Reopens the guided key setup from About (Phase 16).</summary>
    private Func<Task>? _setUpKeys;

    /// <summary>Where the hand-testing coverage record stands, when this process was asked to keep one.</summary>
    private Func<CoverageReport>? _coverage;

    private D47.Core.Actions.MacroStore? _macros;
    private D47.Core.Persona.OwnPersonaStore? _ownPersonas;

    /// <summary>The Commander's checklist, for the row that offers the panel.</summary>
    private D47.Core.Checklists.ChecklistService? _checklists;

    /// <summary>
    /// Everything the switch editor needs: the file, the hardware to walk a switch against, the
    /// reconciler whose health line the cards show, and where a declined capture is written.
    /// </summary>
    private SwitchEditing? _switches;

    private LoreEditing? _lore;

    /// <summary>
    /// What d47 remembers about the Commander, and the clock a hand-typed fact is stamped with (Phase
    /// 31).
    /// </summary>
    private (D47.Core.Memory.MemoryBook Book, Func<DateTimeOffset> Now)? _memories;

    /// <summary>What the audio recorder has kept, and the clock a kept test case is stamped with (#164).</summary>
    private (D47.Core.Diagnostics.Recording.RecordingLog Log, Func<DateTimeOffset> Now)? _recording;

    /// <summary>
    /// What the debrief drafted, the clock an adoption is stamped with, and which core is aboard
    /// (#162).
    /// </summary>
    private (D47.Core.Debrief.DebriefBook Book, Func<DateTimeOffset> Now,
        Func<D47.Core.Persona.Persona> Core)? _debrief;

    /// <summary>The Commander's log (Phase 33).</summary>
    private D47.Core.Logbook.LogbookBook? _logbook;

    /// <summary>Phrases d47 already answers to, so the editor can refuse a macro that would shadow one.</summary>
    private IReadOnlyList<string> _reserved = [];

    /// <summary>True while controls are being written from settings rather than read from.</summary>
    private bool _refreshing;

    private int _activeSection = -1;

    /// <summary>
    /// Limits this instance to one <see cref="SettingsLayout"/> tab place — a tab's own strip rather
    /// than the settings page (#218).
    /// </summary>
    private string? _tabPlaceId;

    /// <summary>The tab strip's own disclosure content and chevron, so a search match can open it (#222).</summary>
    private StackPanel? _tabStripContent;

    private TextBlock? _tabStripChevron;

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

    /// <summary>The container holding the two bulk glyphs, so a test can tell them from a reset.</summary>
    public const string BulkName = "BulkExpand";

    /// <summary>
    /// A plus and a minus, beside "Show every setting" (#223, moved onto that row on the Commander's
    /// instruction 2026-09-01).
    /// </summary>
    private Control BulkControls()
    {
        var open = new Button
        {
            Name = "ExpandAll",
            Height = 28,
            Padding = new Thickness(8, 0),
            VerticalContentAlignment = VerticalAlignment.Center,
            HorizontalContentAlignment = HorizontalAlignment.Center,
        };

        var shut = new Button
        {
            Name = "CollapseAll",
            Height = 28,
            Padding = new Thickness(8, 0),
            VerticalContentAlignment = VerticalAlignment.Center,
            HorizontalContentAlignment = HorizontalAlignment.Center,
        };

        open.Click += (_, _) => SetEveryCard(true);
        shut.Click += (_, _) => SetEveryCard(false);

        Controls.Glyphs.Mark(open, Controls.Glyphs.ExpandAll, ThemeManager.AccentKey, "Expand all");

        // Filled, alone among these: a minus has no height, and a stretched-to-fit geometry with no height
        // collapses to nothing.
        Controls.Glyphs.Mark(
            shut, Controls.Glyphs.CollapseAll, ThemeManager.AccentKey, "Collapse all", filled: true);

        return new StackPanel
        {
            Name = BulkName,
            Orientation = Orientation.Horizontal,
            Spacing = 4,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 0, 12, 0),
            Children = { open, shut },
        };
    }

    /// <summary>Binds the view to a live settings service.</summary>
    public void Attach(
        SettingsService settings,
        ViewStateStore viewState,
        AppPaths paths,
        Func<CoverageReport>? coverage = null,
        D47.Core.Actions.MacroStore? macros = null,
        D47.Core.Checklists.ChecklistService? checklists = null,
        IReadOnlyList<string>? reservedPhrases = null,
        SwitchEditing? switches = null,
        Func<WhisperModel, IProgress<ModelProgress>, Task<ModelInstallResult>>? downloadModel = null,
        Func<Task>? setUpKeys = null,
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
        D47.App.Headset.VrHost? vrHost = null)
    {
        _setUpKeys = setUpKeys;
        _downloadModel = downloadModel;
        _settings = settings;
        _viewStateStore = viewState;
        _viewState = viewState.Load();
        _paths = paths;
        _coverage = coverage;
        _macros = macros;
        _ownPersonas = ownPersonas;
        _checklists = checklists;
        _switches = switches;
        _lore = lore;
        _memories = memories;
        _recording = recording;
        _debrief = debrief;
        _logbook = logbook;
        _reserved = reservedPhrases ?? [];
        _tabPlaceId = tabPlaceId;
        _vrHost = vrHost;

        Build();

        if (_tabPlaceId is null)
        {
            RestoreSection();
        }

        settings.Changed += OnSettingsChanged;

        // **Symmetric, which it was not** (#90).
        AttachedToVisualTree += (_, _) =>
        {
            settings.Changed -= OnSettingsChanged;
            settings.Changed += OnSettingsChanged;
        };

        DetachedFromVisualTree += (_, _) => settings.Changed -= OnSettingsChanged;
    }

    private void OnSettingsChanged(SettingsChanged change) => Dispatcher.UIThread.Post(Refresh);

    /// <summary>A brush fetched at call time, so state changes pick up the current theme.</summary>
    private IBrush? Res(string key) => this.FindResource(key) as IBrush;

    /// <summary>
    /// Binds a brush property to a theme resource, so a theme switch repaints controls built in code
    /// the same way DynamicResource repaints the ones built in markup.
    /// </summary>
    private IDisposable Themed(AvaloniaObject target, AvaloniaProperty property, string key) =>
        target.Bind(property, this.GetResourceObservable(key));

    /// <summary>Hangs the row's default on a control as a tooltip, in full.</summary>
    private void ShowDefaultOnHover(Control control, SettingRow row)
    {
        var shown = row.DefaultDisplayFor(_settings!.Current);

        ToolTip.SetTip(control, string.IsNullOrWhiteSpace(shown) ? null : $"Default: {shown}");
    }

    private void Build()
    {
        var settings = _settings ?? throw new InvalidOperationException("Attach() has not been called.");

        Cards.Children.Clear();
        NavItems.Children.Clear();
        _sections.Clear();
        _rows.Clear();
        _collapsed.Clear();
        _revealedSections.Clear();
        _navAreas.Clear();
        _activeSection = -1;
        _activeArea = -1;
        _pageStrip = null;
        _topStrip = null;
        _areaHeader = null;
        _areaHeaderTitle = null;
        _areaHeaderSentence = null;
        _areaLegend = null;
        _areaDropdown = null;
        _groups.Clear();
        _tabPlaceRows.Clear();
        _otherTabsSection = null;
        _otherTabsList = null;
        _tabStripContent = null;
        _tabStripChevron = null;

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

        _areaDropdown = BuildAreaDropdown();
        Cards.Children.Add(_areaDropdown);

        // The rows that govern the page rather than a card, drawn once above everything
        // .com/dseelinger/d47/issues/60). "Show every setting" decides what the whole page draws, and a
        // Commander who cannot see the rest of the settings will not go looking for the reason four rows into
        // Interface.
        var pageRows = settings.Sections
            .SelectMany(section => section.Rows)
            .Where(row => row.PageTop)
            .ToList();

        if (pageRows.Count > 0)
        {
            // Flush with the cards rather than inset from them.
            var strip = new StackPanel { Spacing = 12, Margin = new Thickness(0, 0, 0, 4) };

            var first = true;

            foreach (var row in pageRows)
            {
                var view = BuildRow(SectionOwning(settings, row), row);

                _rows.Add(view);

                // **Beside the first page row rather than docked above the scroller** (the Commander's
                // instruction, 2026-09-01).
                if (first)
                {
                    // **A DockPanel rather than a grid with a star column.** A star cannot be resolved
                    // against an unbounded width, and the cards sit in a ScrollViewer that scrolls
                    // horizontally — so measure hands its contents infinity and the row's own three-star
                    // caption and two-star control were laid out against it.
                    var line = new DockPanel();
                    var bulk = BulkControls();

                    DockPanel.SetDock(bulk, Dock.Left);

                    line.Children.Add(bulk);
                    line.Children.Add(view.Container);

                    strip.Children.Add(line);
                    first = false;
                    continue;
                }

                strip.Children.Add(view.Container);
            }

            Cards.Children.Add(strip);
            _pageStrip = strip;
            _topStrip = strip;
        }
        else
        {
            // No page row to sit beside — the glyphs still have a page to open and shut, so they go on their
            // own line rather than disappearing.
            var alone = BulkControls();

            alone.Margin = new Thickness(16, 0, 16, 4);
            Cards.Children.Add(alone);
            _topStrip = alone;
        }

        _areaHeader = BuildAreaHeader(out _areaHeaderTitle, out _areaHeaderSentence, out _areaLegend);
        Cards.Children.Add(_areaHeader);

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

            var filterHeading = BuildFilterAreaHeading(area.Title);

            foreach (var place in area.Places)
            {
                var (card, content, heading, expand, foldButton) = BuildCard(settings, owners, place, _sections.Count);

                var nav = BuildNavItem(_sections.Count, place.Title);
                NavItems.Children.Add(nav.Item);

                _sections.Add(
                    new SectionView(
                        place.Id, place.Title, place.Terms, card, content, heading, nav.Item, nav.Bar, nav.Text)
                    {
                        Expand = expand,
                        FoldButton = foldButton,
                    });
            }

            _navAreas.Add(
                new AreaView(
                    area.Id, area.Title, area.Sentence, areaHeading, areaHeadingText, areaHeadingBar,
                    filterHeading, first, _sections.Count - first));
        }

        if (_sections.Count > 0)
        {
            SelectArea(0, resetScroll: false);
        }
        else
        {
            Refresh();
        }
    }

    /// <summary>
    /// Draws one <see cref="SettingsLayout"/> tab place's rows behind a "Settings for this page"
    /// disclosure — no nav, no page-top strip, no card header, no width floor, and no fold (#218).
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

        var content = new StackPanel { Spacing = 16, Margin = new Thickness(0, 8, 0, 0) };

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

            var view = BuildRow(SectionOwning(settings, row), row, shaded: i % 2 == 1);
            _rows.Add(view);
            content.Children.Add(view.Container);
        }

        var expanded = _viewState.IsExpanded(placeId, startCollapsed: true);
        content.IsVisible = expanded;
        _tabStripContent = content;

        var chevron = new TextBlock
        {
            Text = expanded ? "▾" : "▸",
            FontSize = TypeScale.Body,
            Width = 14,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Themed(chevron, TextBlock.ForegroundProperty, ThemeManager.TextMutedKey);
        _tabStripChevron = chevron;

        var heading = new TextBlock
        {
            Text = "Settings for this page",
            FontSize = TypeScale.Subheading,
            FontWeight = FontWeight.Medium,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Themed(heading, TextBlock.ForegroundProperty, ThemeManager.TextKey);

        var headerRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4,
            Margin = new Thickness(8, 4),
        };
        headerRow.Children.Add(chevron);
        headerRow.Children.Add(heading);
        headerRow.Children.Add(Chip(rows.Count.ToString(CultureInfo.InvariantCulture)));

        // Chamfered rather than rounded, in the HUD redesign's own shape (#273, #278).
        var header = new ChamferedBorder
        {
            Chamfer = new CornerRadius(0, 10, 0, 10),
            BorderThickness = new Thickness(1),
            Cursor = new Cursor(StandardCursorType.Hand),
            Child = headerRow,
        };

        Themed(header, ChamferedBorder.BackgroundProperty, ThemeManager.FillLowKey);
        Themed(header, ChamferedBorder.BorderBrushProperty, ThemeManager.RuleKey);

        header.PointerPressed += (_, _) =>
        {
            var open = !content.IsVisible;
            content.IsVisible = open;
            chevron.Text = open ? "▾" : "▸";
            SaveViewState(state => state.With(placeId, open));
        };

        var strip = new StackPanel { Name = TabStripName, Spacing = 8 };
        strip.Children.Add(header);
        strip.Children.Add(content);

        Cards.Children.Add(strip);

        Refresh();
    }

    /// <summary>A small bordered count, the shape the build badge and the issue chips already use (#278).</summary>
    private Border Chip(string said)
    {
        var text = new TextBlock
        {
            Text = said,
            FontSize = TypeScale.Small,
            FontWeight = FontWeight.Bold,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var chip = new Border
        {
            Padding = new Thickness(4, 4),
            BorderThickness = new Thickness(1),
            VerticalAlignment = VerticalAlignment.Center,
            Child = text,
        };

        Themed(text, TextBlock.ForegroundProperty, ThemeManager.AccentKey);
        Themed(chip, Border.BorderBrushProperty, ThemeManager.AccentKey);
        Themed(chip, Border.BackgroundProperty, ThemeManager.FillLowKey);

        return chip;
    }

    /// <summary>Marks the strip a tab place draws, for a test to find it by name (#218).</summary>
    public const string TabStripName = "SettingsForThisPage";

    /// <summary>The capability a page-level row still belongs to.</summary>
    private static CapabilityDescriptor SectionOwning(SettingsService settings, SettingRow row) =>
        settings.Sections.First(section => section.Rows.Any(other => other.Key == row.Key)).Capability;

    /// <summary>One <see cref="SettingsLayout"/> place as a card: its groups, in order, and every row they resolve to.</summary>
    private (Border Card, StackPanel Content, TextBlock Heading, Action<bool> Expand, Button FoldButton) BuildCard(
        SettingsService settings,
        IReadOnlyDictionary<string, CapabilityDescriptor> owners,
        SettingsPlace place,
        int index)
    {
        var title = place.Title;

        var content = new StackPanel
        {
            Spacing = 16,
            Margin = new Thickness(16, 4, 16, 16),
            // Applied while building, not after painting: a card that flashes open and then collapses is
            // worse than one that never remembered (Phase 4).
            IsVisible = _viewState.IsExpanded(place.Id, place.StartCollapsed),
        };

        if (!content.IsVisible)
        {
            _collapsed.Add(index);
        }

        var rows = new List<SettingRow>();

        foreach (var group in place.Groups)
        {
            // A group heading, stated once, in place of the same sentence on every row — and, once it has
            // one, something a query can match to reveal every row under it (#222).
            var groupIndex = -1;

            if (group.Title is { } groupTitle)
            {
                var resetSlot = VrCapability.SlotForPlacementGroup(groupTitle);
                var (groupHeading, headingText, helpText) = BuildGroupHeading(groupTitle, group.Help, resetSlot);

                content.Children.Add(groupHeading);
                groupIndex = _groups.Count;
                _groups.Add(new GroupView(index, groupTitle, group.Help, headingText, helpText));
            }

            foreach (var entry in group.Entries)
            {
                foreach (var row in settings.RowsForEntry(entry))
                {
                    // Alternates on every row this card draws, so the stripe holds regardless of how
                    // groups and folds arrange the rows (#279).
                    var view = BuildRow(owners[row.Key], row, shaded: rows.Count % 2 == 1)
                        with
                    { Section = index, GroupIndex = groupIndex };

                    if (entry.Under)
                    {
                        view.Container.Margin = new Thickness(UnderIndent, 0, 0, 0);
                    }

                    _rows.Add(view);
                    rows.Add(row);
                    content.Children.Add(view.Container);
                }
            }
        }

        // "Show N more" for this place's own folded rows, with no effect on any other place (#221). Its
        // own visibility and label are set by Refresh, which is the only place that knows how many rows a
        // fold is currently hiding.
        var foldButton = new Button
        {
            FontSize = TypeScale.Secondary,
            Padding = new Thickness(0),
            MinWidth = 0,
            Margin = new Thickness(0, -4, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Left,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Cursor = new Cursor(StandardCursorType.Hand),
            IsVisible = false,
        };

        Themed(foldButton, Button.ForegroundProperty, ThemeManager.AccentKey);

        foldButton.Click += (_, _) =>
        {
            if (!_revealedSections.Remove(index))
            {
                _revealedSections.Add(index);
            }

            Refresh();
        };

        content.Children.Add(foldButton);

        var chevron = new TextBlock
        {
            Text = content.IsVisible ? "▾" : "▸",
            FontSize = TypeScale.Body,
            Width = 14,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Themed(chevron, TextBlock.ForegroundProperty, ThemeManager.TextMutedKey);

        var heading = new TextBlock { FontWeight = FontWeight.Medium, VerticalAlignment = VerticalAlignment.Center };
        TitleText.Style(heading, TypeScale.Caption, TitleRank.Subgroup);
        TitleText.Show(heading, title);

        var headerRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        headerRow.Children.Add(chevron);
        headerRow.Children.Add(heading);

        // One per card, not one per row.
        var docs = new Button
        {
            Content = "?",
            FontSize = TypeScale.Secondary,
            Padding = new Thickness(4, 0),
            MinWidth = 0,
            VerticalAlignment = VerticalAlignment.Center,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
        };

        Themed(docs, Button.ForegroundProperty, ThemeManager.TextMutedKey);
        ToolTip.SetTip(docs, $"Open the setup guide for {title}");

        docs.Click += (_, _) => OpenDocs(place.DocsCapabilityId);

        // Stops the click reaching the header underneath, which would collapse the card the Commander just
        // asked to read about.
        docs.PointerPressed += (_, e) => e.Handled = true;

        headerRow.Children.Add(docs);

        // The gesture that matters when things have gone wrong .com/dseelinger/d47/issues/61).
        var reset = new Button
        {
            // Accent, like every other bare glyph whose only affordance is that it can be pressed (#208).
            Content = Glyphs.Draw(Glyphs.Reset, ThemeManager.AccentKey, TypeScale.Small),

            // Room for the stroke, which Made puts half of outside the box — see the note on Glyphs.Reset.
            Padding = new Thickness(4, 4),
            MinWidth = 0,
            VerticalAlignment = VerticalAlignment.Center,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            IsVisible = CardHasChanges(rows),
        };

        // With the mark rather than against it (#208).
        Themed(reset, Button.ForegroundProperty, ThemeManager.AccentKey);

        // The word was this button's accessible name by being its content; a Path has no text, so without
        // this a screen reader finds an unnamed button where it used to find "Reset".
        AutomationProperties.SetName(reset, $"Reset {title}");
        ToolTip.SetTip(reset, $"Put every {title} setting you have changed back to its default. Keys are untouched.");

        reset.Click += (_, _) =>
        {
            _settings!.ResetPlace(place.Id, SettingsCaller.Panel);

            // And forget what has been said about whether this card is open (#223).
            SaveViewState(state => state.Forgetting(place.Id));

            Refresh();
        };

        // Held so its visibility can follow the card's state, the same way each row's glyph follows its own.
        _cardResets.Add((rows, reset));

        reset.PointerPressed += (_, e) => e.Handled = true;

        headerRow.Children.Add(reset);

        var header = new Border
        {
            Padding = new Thickness(12, 12),
            Background = Brushes.Transparent,
            Cursor = new Cursor(StandardCursorType.Hand),
            Child = headerRow,
        };

        // One place either route changes it.
        void Expand(bool expanded)
        {
            content.IsVisible = expanded;
            chevron.Text = expanded ? "▾" : "▸";

            // Recorded here as well as on disk, because a filter opens a card without being asked and has to
            // put it back the way the Commander left it.
            if (expanded)
            {
                _collapsed.Remove(index);
            }
            else
            {
                _collapsed.Add(index);
            }

            RememberCollapse(place.Id, expanded);
        }

        header.PointerPressed += (_, _) => Expand(!content.IsVisible);

        header.PointerEntered += (_, _) => header.Background = Res(ThemeManager.FillLowKey);
        header.PointerExited += (_, _) => header.Background = Brushes.Transparent;

        var body = new StackPanel();
        body.Children.Add(header);
        body.Children.Add(content);

        var card = new Border { Child = body };

        CardChrome.Card(card);

        return (card, content, heading, Expand, foldButton);
    }

    private (Control Container, TextBlock Heading, TextBlock? Help) BuildGroupHeading(
        string group, string? help, string? resetSlot = null)
    {
        // Full text colour at the row-label size, not muted at help-text size.
        var heading = new TextBlock
        {
            Text = group,
            FontSize = TypeScale.Body,
            FontWeight = FontWeight.Medium,
        };
        Themed(heading, TextBlock.ForegroundProperty, ThemeManager.TextKey);

        var stack = new StackPanel { Spacing = 4, Margin = new Thickness(0, 16, 0, 4) };

        // The rule goes above the heading.
        var rule = new Border { Height = 1, Margin = new Thickness(0, 0, 0, 8) };
        Themed(rule, Border.BackgroundProperty, ThemeManager.BorderKey);

        stack.Children.Add(rule);

        if (resetSlot is { } slot)
        {
            var headingRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
            headingRow.Children.Add(heading);
            headingRow.Children.Add(GroupResetButton(group, slot));
            stack.Children.Add(headingRow);
        }
        else
        {
            stack.Children.Add(heading);
        }

        TextBlock? note = null;

        if (!string.IsNullOrWhiteSpace(help))
        {
            note = new TextBlock { Text = help, FontSize = TypeScale.Secondary, TextWrapping = TextWrapping.Wrap };
            Themed(note, TextBlock.ForegroundProperty, ThemeManager.TextMutedKey);
            stack.Children.Add(note);
        }

        return (stack, heading, note);
    }

    /// <summary>
    /// The reset glyph on a placement group heading — puts that surface back where a fresh install
    /// puts it, through <see cref="_vrHost"/> rather than by writing view-state directly, since VrHost
    /// is the anchors' only owner and rewrites the file on every change of its own (#162).
    /// </summary>
    private Button GroupResetButton(string group, string slot)
    {
        var reset = new Button
        {
            Content = Glyphs.Draw(Glyphs.Reset, ThemeManager.AccentKey, TypeScale.Small),
            Padding = new Thickness(4, 4),
            MinWidth = 0,
            VerticalAlignment = VerticalAlignment.Center,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
        };

        Themed(reset, Button.ForegroundProperty, ThemeManager.AccentKey);

        AutomationProperties.SetName(reset, $"Reset {group}");
        ToolTip.SetTip(
            reset,
            "Put this panel back where a fresh install puts it: world-locked, at its default "
            + "distance, resting in front of you.");

        reset.Click += (_, _) =>
        {
            _vrHost?.ResetPlacement(slot);
            Refresh();
        };

        reset.PointerPressed += (_, e) => e.Handled = true;

        return reset;
    }

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
            LetterSpacing = 1,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        Themed(text, TextBlock.ForegroundProperty, ThemeManager.TextMutedKey);

        // The selected tree node's own mark (#279), shared in shape with a place's below.
        var bar = new Border
        {
            Width = 3,
            Margin = new Thickness(0, 4, 8, 4),
            Opacity = 0,
        };
        Themed(bar, Border.BackgroundProperty, ThemeManager.AccentKey);

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

    /// <summary>The area's title, drawn once above its group of cards while a query is active (#220).</summary>
    private TextBlock BuildFilterAreaHeading(string title)
    {
        var heading = new TextBlock
        {
            Text = title,
            FontSize = TypeScale.Subheading,
            FontWeight = FontWeight.Medium,
            Margin = new Thickness(16, 16, 16, 4),
        };
        Themed(heading, TextBlock.ForegroundProperty, ThemeManager.TextMutedKey);

        return heading;
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
        var heading = new TextBlock
        {
            Text = "On other tabs",
            FontSize = TypeScale.Subheading,
            FontWeight = FontWeight.Medium,
            Margin = new Thickness(16, 16, 16, 4),
        };
        Themed(heading, TextBlock.ForegroundProperty, ThemeManager.TextMutedKey);

        list = new StackPanel { Spacing = 4, Margin = new Thickness(16, 0, 16, 12) };

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
        Themed(text, TextBlock.ForegroundProperty, ThemeManager.TextKey);

        var button = new Button
        {
            Content = tab.Strip ? $"Open {tab.Title}" : $"Open the {tab.Title} tab",
            FontSize = TypeScale.Secondary,
            Padding = new Thickness(8, 4),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(12, 0, 0, 0),
        };

        button.Click += (_, _) => _openTabPlace?.Invoke(tab.RootKey);

        var line = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(button, Dock.Right);
        line.Children.Add(button);
        line.Children.Add(text);

        return line;
    }

    /// <summary>The selected area's own title and sentence, drawn above its cards (#220).</summary>
    /// <summary>What a protected row's bar means, said once per screen rather than on every row (#333).</summary>
    internal const string ProtectedLegend =
        "Rows marked ▌ are protected — D47 will not change them on your say-so alone.";

    private StackPanel BuildAreaHeader(out TextBlock title, out TextBlock sentence, out TextBlock legend)
    {
        title = new TextBlock { FontWeight = FontWeight.Medium };
        TitleText.Style(title, TypeScale.Heading, TitleRank.Group);

        sentence = new TextBlock { FontSize = TypeScale.Secondary, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0) };
        Themed(sentence, TextBlock.ForegroundProperty, ThemeManager.TextMutedKey);

        legend = new TextBlock
        {
            Text = ProtectedLegend,
            FontSize = TypeScale.Secondary,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 4, 0, 0),
            IsVisible = false,
        };
        Themed(legend, TextBlock.ForegroundProperty, ThemeManager.WarnKey);

        return new StackPanel
        {
            Margin = new Thickness(16, 4, 16, 8),
            Children = { TitleText.GroupRow(title), sentence, legend },
        };
    }

    /// <summary>The area picker shown once the nav has collapsed (#220).</summary>
    private Stepper BuildAreaDropdown()
    {
        var combo = new Stepper
        {
            Name = "AreaDropdown",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = new Thickness(16, 0, 16, 8),
            IsVisible = false,
        };
        DressAsAChoice(combo);
        AutomationProperties.SetName(combo, "Area");

        combo.SelectionChanged += (_, _) =>
        {
            if (_settingAreaDropdown || combo.SelectedIndex < 0)
            {
                return;
            }

            SelectArea(_dropdownAreaIndexes[combo.SelectedIndex]);
        };

        return combo;
    }

    /// <summary>Which area index each entry in <see cref="_areaDropdown"/> names, since a structurally empty
    /// area is left out (#220).</summary>
    private readonly List<int> _dropdownAreaIndexes = [];

    /// <summary>Refills the area dropdown from the areas that currently have anything to show.</summary>
    private void SyncAreaDropdown()
    {
        if (_areaDropdown is not { } combo)
        {
            return;
        }

        _dropdownAreaIndexes.Clear();
        var titles = new List<string>();

        for (var i = 0; i < _navAreas.Count; i++)
        {
            if (!_navAreas[i].Heading.IsVisible)
            {
                continue;
            }

            _dropdownAreaIndexes.Add(i);
            titles.Add(_navAreas[i].Title);
        }

        _settingAreaDropdown = true;
        try
        {
            combo.ItemsSource = titles;
            combo.SelectedIndex = _dropdownAreaIndexes.IndexOf(_activeArea);
        }
        finally
        {
            _settingAreaDropdown = false;
        }
    }

    /// <summary>The first of an area's places still showing a card, or its first place where none are.</summary>
    private int FirstShownPlace(Border areaHeading)
    {
        var area = _navAreas.First(a => a.Heading == areaHeading);

        for (var i = area.First; i < area.First + area.Count; i++)
        {
            if (_sections[i].Card.IsVisible)
            {
                return i;
            }
        }

        return area.First;
    }

    /// <summary>Selects an area, drawing its places under it and marking one of them active (#220).</summary>
    public void SelectArea(int areaIndex) => SelectArea(areaIndex, resetScroll: true);

    private void SelectArea(int areaIndex, bool resetScroll)
    {
        if (areaIndex < 0 || areaIndex >= _navAreas.Count)
        {
            return;
        }

        var changed = _activeArea != areaIndex;
        _activeArea = areaIndex;

        UpdateAreaVisuals();
        Refresh();

        if (changed && resetScroll && _query.Length == 0)
        {
            Scroller.Offset = new Vector(0, 0);
        }

        if (_query.Length == 0)
        {
            SetActiveSection(FirstShownPlace(_navAreas[areaIndex].Heading));
        }
    }

    /// <summary>Which area index a section belongs to.</summary>
    private int AreaOf(int sectionIndex) =>
        _navAreas.FindIndex(area => sectionIndex >= area.First && sectionIndex < area.First + area.Count);

    /// <summary>The area currently selected, by its id, for a test to read.</summary>
    internal string? ActiveAreaId => _activeArea >= 0 && _activeArea < _navAreas.Count ? _navAreas[_activeArea].Id : null;

    private void UpdateAreaVisuals()
    {
        for (var i = 0; i < _navAreas.Count; i++)
        {
            PaintAreaHeading(_navAreas[i], i == _activeArea);
        }
    }

    /// <summary>The selected area's heading is marked the same way the selected place is (#220).</summary>
    private void PaintAreaHeading(AreaView area, bool active)
    {
        if (area.PaintedActive == active)
        {
            return;
        }

        area.PaintedActive = active;

        area.Ink?.Dispose();
        area.Ink = Themed(
            area.HeadingText,
            TextBlock.ForegroundProperty,
            active ? ThemeManager.TextKey : ThemeManager.TextMutedKey);

        area.HeadingBar.Opacity = active ? 1 : 0;

        area.Fill?.Dispose();
        area.Fill = null;

        if (active)
        {
            area.Fill = Themed(area.Heading, Border.BackgroundProperty, ThemeManager.FillHighKey);
        }
        else
        {
            area.Heading.Background = Brushes.Transparent;
        }
    }

    private (Border Item, Border Bar, TextBlock Text) BuildNavItem(int index, string title)
    {
        // The selected tree node's own mark: a 3px Accent bar (#279).
        var bar = new Border
        {
            Width = 3,
            Margin = new Thickness(0, 4),
            Opacity = 0,
        };
        Themed(bar, Border.BackgroundProperty, ThemeManager.AccentKey);

        var text = new TextBlock
        {
            Text = title,
            FontFamily = Fonts.ChromeFamily,
            FontSize = TypeScale.Body,
            Margin = new Thickness(8, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };

        var layout = new DockPanel();
        DockPanel.SetDock(bar, Dock.Left);
        layout.Children.Add(bar);
        layout.Children.Add(text);

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
        item.PointerPressed += (_, _) => ScrollTo(index);
        item.PointerEntered += (_, _) =>
        {
            if (index != _activeSection)
            {
                item.Background = Res(ThemeManager.FillLowKey);
            }
        };
        item.PointerExited += (_, _) =>
        {
            if (index != _activeSection)
            {
                item.Background = Brushes.Transparent;
            }
        };

        return (item, bar, text);
    }

    private void SetActiveSection(int index)
    {
        if (_activeSection == index)
        {
            return;
        }

        _activeSection = index;
        UpdateNavVisuals();
        ShowActiveInNav();
        RememberSection();
    }

    /// <summary>Puts the page back where it was left (#268).</summary>
    private void RestoreSection()
    {
        var remembered = _viewState.SettingsSection;

        Dispatcher.UIThread.Post(
            () =>
            {
                // An id that names no place is a stale name, and a stale name is worth the top of the page
                // rather than a failure - the same reading Reveal takes of one it cannot find.
                var index = _sections.FindIndex(
                    section => string.Equals(section.PlaceId, remembered, StringComparison.Ordinal));

                if (index >= 0)
                {
                    ScrollTo(index);
                }

                _rememberingSection = true;
            },
            DispatcherPriority.Loaded);
    }

    /// <summary>Whether the scroll-spy's answer is worth writing down yet.</summary>
    private bool _rememberingSection;

    /// <summary>
    /// The sections' place ids, in page order, and which one the scroll-spy is naming (−1 for none).
    /// </summary>
    internal IReadOnlyList<string> SectionIds => [.. _sections.Select(section => section.PlaceId)];

    /// <inheritdoc cref="SectionIds"/>
    internal int ActiveSection => _activeSection;

    /// <summary>Whether a section's card is open.</summary>
    internal bool IsSectionExpanded(int index) => _sections[index].Content.IsVisible;

    /// <summary>Writes down which section the page is on, once the scrolling has settled (#268).</summary>
    private void RememberSection()
    {
        if (!_rememberingSection)
        {
            return;
        }

        _sectionSettle ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _sectionSettle.Tick -= OnSectionSettled;
        _sectionSettle.Tick += OnSectionSettled;

        // Restarted rather than left running, so the half second is measured from the last change rather than
        // from the first.
        _sectionSettle.Stop();
        _sectionSettle.Start();
    }

    private DispatcherTimer? _sectionSettle;

    private void OnSectionSettled(object? sender, EventArgs e) => SettleSection();

    /// <summary>Writes down the section the page is on, now (#268).</summary>
    internal void SettleSection()
    {
        _sectionSettle?.Stop();

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

    private void UpdateNavVisuals()
    {
        for (var i = 0; i < _sections.Count; i++)
        {
            var section = _sections[i];
            var active = i == _activeSection;

            section.NavBar.Opacity = active ? 1 : 0;
            section.NavText.FontWeight = active ? FontWeight.Medium : FontWeight.Normal;

            PaintNav(section, active);
        }
    }

    /// <summary>
    /// The selected node's own mark — an Accent fill with Knock text; an unselected node's text is
    /// TextMuted (#279, #357).
    /// </summary>
    private void PaintNav(SectionView section, bool active)
    {
        if (section.PaintedActive == active)
        {
            return;
        }

        section.PaintedActive = active;

        section.NavFill?.Dispose();
        section.NavFill = null;

        section.NavInk?.Dispose();

        if (active)
        {
            section.NavFill = Themed(section.NavItem, Border.BackgroundProperty, ThemeManager.AccentKey);
            section.NavInk = Themed(section.NavText, TextBlock.ForegroundProperty, ThemeManager.KnockKey);
        }
        else
        {
            // No resource for "nothing", so the fill is dropped rather than bound.
            section.NavItem.Background = Brushes.Transparent;
            section.NavInk = Themed(section.NavText, TextBlock.ForegroundProperty, ThemeManager.TextMutedKey);
        }
    }

    /// <summary>
    /// Shows the section holding a capability's first row on this page — what a help card pressed on
    /// the Transcript page does. A capability with no row here changes nothing.
    /// </summary>
    public void Reveal(string capabilityId)
    {
        var index = SectionHolding(capabilityId);

        if (index < 0)
        {
            return;
        }

        // A jump unfolds the place it lands on, and only that place (#60, #221).
        if (_revealedSections.Add(index))
        {
            Refresh();
        }

        _sections[index].Expand?.Invoke(true);

        // After the layout the expansion caused, not before it: CardTop reads the card's position in the
        // scroller's content, and the cards below one that just opened have not moved yet.
        Dispatcher.UIThread.Post(() => ScrollTo(index), DispatcherPriority.Loaded);
    }

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

    /// <summary>
    /// Goes to a section, selecting its area first when the section is not the one currently drawn (#220).
    /// </summary>
    private void ScrollTo(int index)
    {
        if (index < 0 || index >= _sections.Count)
        {
            return;
        }

        // A query draws every area at once, so there is no area to switch to.
        if (_query.Length == 0 && AreaOf(index) != _activeArea)
        {
            SelectArea(AreaOf(index), resetScroll: false);

            // After the layout the area switch caused, not before it — see the note on Reveal.
            Dispatcher.UIThread.Post(() => JumpTo(index), DispatcherPriority.Loaded);
            return;
        }

        JumpTo(index);
    }

    private void JumpTo(int index)
    {
        if (index < 0 || index >= _sections.Count)
        {
            return;
        }

        SetActiveSection(index);
        Scroller.Offset = new Vector(0, CardTop(_sections[index].Card));
    }

    /// <summary>The card's position in the scroller's content, which is not where it is on screen.</summary>
    private double CardTop(Border card) => card.Bounds.Y + Cards.Margin.Top;

    /// <summary>
    /// Highlights the section the panel is actually showing — the topmost card still in view — rather
    /// than the last one clicked (Phase 4, "Settings Nav Menu").
    /// </summary>
    private void OnScrollerSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        // A tab's own strip has no width floor to hold (#218).
        if (_tabPlaceId is not null)
        {
            return;
        }

        const double Floor = 420;
        const double Ceiling = 700;

        // The margin the cards are laid out with, both sides.
        var available = e.NewSize.Width - 56;

        Cards.Width = Math.Clamp(available, Floor, Ceiling);
    }

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

    /// <summary>The sections actually drawn right now, in page order — the selected area's, or every area's
    /// matches while a query narrows all of them (#220).</summary>
    private IEnumerable<int> DrawnSections() =>
        Enumerable.Range(0, _sections.Count).Where(i => _sections[i].Card.IsVisible);

    private void OnScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        var drawn = DrawnSections().ToList();

        if (drawn.Count == 0)
        {
            return;
        }

        // At the very bottom, the last card is the answer even when it is too short to ever become topmost —
        // the classic scroll-spy edge.
        if (Scroller.Offset.Y >= Scroller.Extent.Height - Scroller.Viewport.Height - 2)
        {
            SetActiveSection(drawn[^1]);
            return;
        }

        var offset = Scroller.Offset.Y;
        var topmost = drawn[0];

        foreach (var i in drawn)
        {
            // Topmost once its head has passed the top edge, with a little tolerance so a card sitting
            // exactly at the edge does not flicker between two answers.
            if (CardTop(_sections[i].Card) <= offset + 16)
            {
                topmost = i;
            }
            else
            {
                break;
            }
        }

        SetActiveSection(topmost);
    }

    /// <summary>Open every drawn card, or shut every drawn card (#223, restricted to what is on screen by
    /// #220).</summary>
    private void SetEveryCard(bool expanded)
    {
        foreach (var i in DrawnSections())
        {
            _sections[i].Expand?.Invoke(expanded);
        }
    }

    // Kept as members rather than folded into BulkControls' handlers: SettingsIsATabTests drives the bulk
    // controls through them, and a lambda has no name for a test to reach.
    private void OnExpandAllClick(object? sender, RoutedEventArgs e) => SetEveryCard(true);

    private void OnCollapseAllClick(object? sender, RoutedEventArgs e) => SetEveryCard(false);

    private void RememberCollapse(string placeId, bool expanded) =>
        SaveViewState(state => state.With(placeId, expanded));

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

        var showing = new int[_sections.Count];

        // Rows a fold would hide in this section, regardless of whether a reveal is currently drawing
        // them — what "Show N more" counts (#221).
        var folded = new int[_sections.Count];

        // Which areas the query names, by title (#222).
        var areaNamed = new bool[_navAreas.Count];

        for (var a = 0; a < _navAreas.Count; a++)
        {
            areaNamed[a] = _query.Length > 0
                           && _navAreas[a].Title.Contains(_query, StringComparison.OrdinalIgnoreCase);
        }

        // Which sections the query names — by the section's own title, one of its search terms, or the
        // area holding it (#222).
        var named = new bool[_sections.Count];

        for (var i = 0; i < _sections.Count; i++)
        {
            var section = _sections[i];

            named[i] = _query.Length > 0
                       && (section.Title.Contains(_query, StringComparison.OrdinalIgnoreCase)
                           || section.Terms.Any(term => term.Contains(_query, StringComparison.OrdinalIgnoreCase))
                           || areaNamed[AreaOf(i)]);
        }

        // Which named groups the query matches, by title or help (#222).
        var groupNamed = new bool[_groups.Count];

        for (var g = 0; g < _groups.Count; g++)
        {
            var group = _groups[g];

            groupNamed[g] = _query.Length > 0
                            && (group.Title.Contains(_query, StringComparison.OrdinalIgnoreCase)
                                || (group.Help is { } help
                                    && D47.Core.Interface.HelpLinks.Plain(help)
                                        .Contains(_query, StringComparison.OrdinalIgnoreCase)));
        }

        foreach (var (rows, button) in _cardResets)
        {
            button.IsVisible = CardHasChanges(rows);
        }

        var pageRowsShown = 0;

        _refreshing = true;
        try
        {
            foreach (var row in _rows)
            {
                // A row that does not apply is absent, not disabled: a greyed-out control still asserts that
                // the setting exists (Phase 4).
                var applies = row.Row.Applies(_settings.Current) && !row.Row.DrawnElsewhere;

                var isFolded = applies
                               && SettingsFold.IsFolded(
                                   row.Row,
                                   _settings.Current,
                                   row.Row.BoundKeys.Any(_settings.IsChanged),
                                   ShowingEverything);

                // A place's own "Show N more" draws its folded rows without unfolding any other place
                // (#221).
                var revealed = row.Section >= 0 && _revealedSections.Contains(row.Section);

                var shown = applies
                            && (!isFolded || revealed)
                            && (Matches(row.Row)
                                || (row.Section >= 0 && named[row.Section])
                                || (row.GroupIndex >= 0 && groupNamed[row.GroupIndex]));

                row.Container.IsVisible = shown;
                row.Refresh();

                // Only the survivors.
                if (shown)
                {
                    Illuminate(row);
                }

                if (shown && row.Section >= 0)
                {
                    showing[row.Section]++;
                }
                else if (shown)
                {
                    pageRowsShown++;
                }

                if (isFolded && row.Section >= 0)
                {
                    folded[row.Section]++;
                }
            }
        }
        finally
        {
            _refreshing = false;
        }

        if (_pageStrip is { } strip)
        {
            strip.IsVisible = pageRowsShown > 0;
        }

        var filtering = _query.Length > 0;

        // The section's own name, marked in both places it is written, and its "Show N more" beside it.
        for (var i = 0; i < _sections.Count; i++)
        {
            Paint(_sections[i].Heading, _sections[i].Title);
            Paint(_sections[i].NavText, _sections[i].Title);

            if (_sections[i].FoldButton is not { } button)
            {
                continue;
            }

            var revealed = _revealedSections.Contains(i);

            button.IsVisible = !ShowingEverything && !filtering && (folded[i] > 0 || revealed);
            button.Content = revealed ? "Show fewer" : $"Show {folded[i]} more";
        }

        // An area's title, marked in both places it is written (#222).
        foreach (var area in _navAreas)
        {
            Paint(area.HeadingText, area.Title);
            Paint(area.FilterHeading, area.Title);
        }

        // A group's own title and help, the other two things a query can match (#222).
        foreach (var group in _groups)
        {
            Paint(group.HeadingText, group.Title);

            if (group.HelpText is { } helpText && group.Help is { } help)
            {
                Paint(helpText, help);
            }
        }

        UpdateOtherTabs();
        ApplyFilterToCards(showing, folded, named);
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
            .Where(entry => Matches(entry.Row))
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
            hit.Bind(TextElement.BackgroundProperty, this.GetResourceObservable(ThemeManager.AccentMutedKey));

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
                        this.GetResourceObservable(ThemeManager.AccentMutedKey));
                }

                if (segment.Target is not null)
                {
                    run.Bind(
                        TextElement.ForegroundProperty,
                        this.GetResourceObservable(ThemeManager.AccentKey));

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
                ScrollTo(index);
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

    // Against the plain sentence rather than the written one (#65): searching the markup would let a query
    // match a capability id inside (privacy) and show a row whose visible text does not contain the query
    // anywhere.
    private bool Matches(SettingRow row) =>
        _query.Length == 0
        || row.Label.Contains(_query, StringComparison.OrdinalIgnoreCase)
        || D47.Core.Interface.HelpLinks.Plain(row.Help).Contains(_query, StringComparison.OrdinalIgnoreCase)
        || row.Key.Contains(_query, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// A card with nothing left in it goes, and so does its nav item — a sidebar still listing fourteen
    /// sections when three of them hold anything is a sidebar that has stopped telling the truth.
    /// </summary>
    private void ApplyFilterToCards(int[] showing, int[] folded, bool[] named)
    {
        var filtering = _query.Length > 0;

        for (var i = 0; i < _sections.Count; i++)
        {
            var section = _sections[i];

            // Named counts even with nothing under it.
            var holds = showing[i] > 0 || named[i];

            // A card the fold has emptied is absent rather than an empty box (#60), which does more for the
            // anxiety than folding rows does — it takes Diagnostics, and VR with no headset, off the page
            // entirely. A place whose rows are all folded is the exception: its card stays, holding its
            // own "Show N more" button (#221).
            var anyRows = showing[i] > 0 || folded[i] > 0;

            // Every area's matches draw while a query is narrowing the page; otherwise only the selected
            // area's places do (#220).
            var inSelectedArea = filtering || AreaOf(i) == _activeArea;

            section.Card.IsVisible = (!filtering || holds) && anyRows && inSelectedArea;
            section.NavItem.IsVisible = (!filtering || holds) && anyRows && inSelectedArea;

            section.Content.IsVisible = filtering ? holds : !_collapsed.Contains(i);
        }

        // An area heading is absent once every one of its places has nothing to show, independent of the
        // query — the same reading the old per-place rule took, moved up a level (#220).
        for (var a = 0; a < _navAreas.Count; a++)
        {
            var area = _navAreas[a];
            area.Heading.IsVisible = Enumerable.Range(area.First, area.Count).Any(i => showing[i] > 0);
        }

        SyncAreaDropdown();
        LayoutCards();
    }

    /// <summary>
    /// Assembles the scroller's content: the selected area's cards behind its own title and sentence, or
    /// while a query is active, every area's matches behind that area's own title (#220).
    /// </summary>
    /// <remarks>
    /// A no-op when the desired order already matches, so an ordinary row edit — which calls
    /// <see cref="Refresh"/> without changing which cards are drawn — never resets the scroll offset.
    /// </remarks>
    private void LayoutCards()
    {
        if (_tabPlaceId is not null)
        {
            return;
        }

        var desired = DesiredCardsOrder();

        if (Cards.Children.SequenceEqual(desired))
        {
            return;
        }

        Cards.Children.Clear();
        Cards.Children.AddRange(desired);
    }

    private List<Control> DesiredCardsOrder()
    {
        var order = new List<Control>();

        if (_areaDropdown is { } dropdown)
        {
            order.Add(dropdown);
        }

        if (_topStrip is { } strip)
        {
            order.Add(strip);
        }

        if (_query.Length > 0)
        {
            foreach (var area in _navAreas)
            {
                var cards = Enumerable.Range(area.First, area.Count)
                    .Where(i => _sections[i].Card.IsVisible)
                    .ToList();

                if (cards.Count == 0)
                {
                    continue;
                }

                order.Add(area.FilterHeading);
                order.AddRange(cards.Select(i => (Control)_sections[i].Card));
            }

            if (_otherTabsSection is { } otherTabs)
            {
                order.Add(otherTabs);
            }

            return order;
        }

        if (_activeArea < 0 || _activeArea >= _navAreas.Count)
        {
            return order;
        }

        var selected = _navAreas[_activeArea];

        TitleText.Show(_areaHeaderTitle!, selected.Title);
        _areaHeaderSentence!.Text = selected.Sentence;

        _areaLegend!.IsVisible = _rows.Any(row =>
            row.Row.Protected && row.Section >= selected.First && row.Section < selected.First + selected.Count);

        if (_areaHeader is { } header)
        {
            order.Add(header);
        }

        order.AddRange(
            Enumerable.Range(selected.First, selected.Count)
                .Where(i => _sections[i].Card.IsVisible)
                .Select(i => (Control)_sections[i].Card));

        return order;
    }

    /// <summary>
    /// Which cards the Commander had shut when a filter opened them, so clearing it shuts them again.
    /// </summary>
    private readonly HashSet<int> _collapsed = [];

    /// <summary>The width a compact row's control is built to.</summary>
    private const double StandardControlWidth = 190;

    /// <summary>The label column's maximum width — past it the control takes the rest of the row (#332).</summary>
    private const double LabelColumnMaxWidth = 300;

    /// <summary>The reset gutter's width, reserved on every compact row whether or not it draws one (#332).</summary>
    private const double ResetGutterWidth = 44;

    /// <summary>A compact row's minimum height — enough for its label at the current type scale (#332).</summary>
    private const double RowMinHeight = 52;

    /// <summary>A compact row's own top and bottom padding (#332).</summary>
    private const double RowVerticalPadding = 8;

    /// <summary>The width of a protected row's left bar (#333).</summary>
    private const double ProtectedBarWidth = 3;

    /// <summary>The extra left padding a protected row needs, so its bar does not crowd the label (#333).</summary>
    private const double ProtectedBarPadding = 12;

    /// <summary>
    /// Marks a caption-and-control row, so a test can find the rows this view builds rather than every
    /// three-column grid that happens to be in the tree.
    /// </summary>
    public const string CompactRowClass = "compact-row";

    /// <summary>The height every control that opens a list stands at, and the padding inside it.</summary>
    private const double ChoiceHeight = 32;

    private static readonly Thickness ChoicePadding = new(12, 4);

    /// <summary>One look for the two controls that open a list.</summary>
    private bool ShowingEverything =>
        _tabPlaceId is not null || (_settings?.Current.Ui.ShowEverySetting ?? true);

    private bool CardHasChanges(IReadOnlyList<SettingRow> rows) =>
        _settings is { } settings
        && rows.Any(row => row.Applies(settings.Current) && settings.IsChanged(row.Key));

    private void DressAsAChoice(TemplatedControl control)
    {
        // A stepper sizes itself: a 44px frame with its position line under it, which 32px would cut off.
        if (control is Stepper)
        {
            return;
        }

        // Fixed rather than a floor.
        control.Height = ChoiceHeight;
        control.Padding = ChoicePadding;
        control.BorderThickness = new Thickness(1);
        control.FontSize = TypeScale.Body;
    }

    /// <summary>Fetches a speech model, reporting progress.</summary>
    private Func<WhisperModel, IProgress<ModelProgress>, Task<ModelInstallResult>>? _downloadModel;

    /// <summary>One download at a time, and the row that is showing it.</summary>
    private bool _downloadingModel;

    /// <summary>A row's inline tag — a 1px Accent border at 60%, Accent ink, no fill, no rounded corners (#279).</summary>
    private Control RowTag(string said)
    {
        var text = new TextBlock { Text = said, FontSize = TypeScale.Small, VerticalAlignment = VerticalAlignment.Center };
        Themed(text, TextBlock.ForegroundProperty, ThemeManager.AccentKey);

        var tag = new Border
        {
            Padding = new Thickness(4, 4),
            BorderThickness = new Thickness(1),
            VerticalAlignment = VerticalAlignment.Center,
            Child = text,
        };
        Themed(tag, Border.BorderBrushProperty, ThemeManager.TagBorderKey);

        return tag;
    }

    private RowView BuildRow(CapabilityDescriptor capability, SettingRow row, bool shaded = false)
    {
        var header = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };

        var label = new TextBlock
        {
            Text = row.Label,
            FontFamily = Fonts.ProseFamily,
            FontSize = TypeScale.Body,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Themed(label, TextBlock.ForegroundProperty, ThemeManager.TextKey);
        header.Children.Add(label);

        if (row.Scope == SettingScope.Commander)
        {
            // The same tag for the other declaration a row can make (Phase 44): this value is the
            // Commander's who is flying, and a second Commander on this machine will see their own here
            // rather than this one.
            header.Children.Add(RowTag("per Commander"));
        }

        // Hidden inline copy, shown only when a search matches text no other visible control carries (#333).
        var help = new TextBlock
        {
            Text = row.Help,
            FontSize = TypeScale.Secondary,
            Margin = new Thickness(0, 4, 0, 0),
            TextWrapping = TextWrapping.Wrap,
            IsVisible = false,
        };
        Themed(help, TextBlock.ForegroundProperty, ThemeManager.TextMutedKey);

        var message = new TextBlock
        {
            FontSize = TypeScale.Secondary,
            IsVisible = false,
            Margin = new Thickness(0, 4, 0, 0),
            TextWrapping = TextWrapping.Wrap,
        };
        Themed(message, TextBlock.ForegroundProperty, ThemeManager.DangerKey);

        var (control, refresh, compact) = BuildControl(row, message);

        // The settings key, shown only when it is the reason this row survived a filter.
        var keyLine = new TextBlock
        {
            FontSize = TypeScale.Small,
            Margin = new Thickness(0, 4, 0, 0),
            TextWrapping = TextWrapping.Wrap,
            IsVisible = false,
        };
        Themed(keyLine, TextBlock.ForegroundProperty, ThemeManager.TextMutedKey);

        // A way back from this one row .com/dseelinger/d47/issues/61), on the rows the Commander has actually
        // changed and nowhere else.
        var spoken = new TextBlock
        {
            Text = row.Help,
            FontSize = TypeScale.Secondary,
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 420,
        };
        Themed(spoken, TextBlock.ForegroundProperty, ThemeManager.TextKey);

        if (!string.IsNullOrWhiteSpace(row.Help))
        {
            AttachHelp(label, capability, row, spoken);
        }

        // A square glyph button at the end of the row rather than beside the label, so it stays put
        // however wide the caption or the control run (#279).
        Button? resetButton = null;

        if (row is { Kind: not SettingKind.Secret, Binding.Write: not null })
        {
            var back = new Button
            {
                // Named so anything looking for "the control this row is about" can tell this apart from it.
                Name = RowResetName,

                // A stroked Path rather than U+21BA (#69).
                Content = Glyphs.Draw(Glyphs.Reset, ThemeManager.AccentKey, TypeScale.Secondary),

                Width = TypeScale.MinimumTarget,
                Height = TypeScale.MinimumTarget,
                Padding = new Thickness(0),
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 0, 0),
                IsVisible = false,
            };

            ToolTip.SetTip(back, $"Put {row.Label} back to its default");

            // The character used to be this button's accessible name by being its content; a Path has no
            // text, so a screen reader would have found an unnamed button where it used to find one.
            AutomationProperties.SetName(back, $"Reset {row.Label}");

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
        caption.Children.Add(header);
        caption.Children.Add(help);
        caption.Children.Add(keyLine);

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

            ToolTip.SetShowDelay(caption, 250);

            // The pointer has to have something to be over.
            caption.Background = Brushes.Transparent;
        }

        Control body;
        if (compact)
        {
            // The label sits at its own content width, capped rather than proportional, so the control
            // starts immediately after it instead of at the far edge of a share it does not fill (#332).
            // The reset gutter is its own column, held at the same width whether or not this row draws
            // one, so the control column does not go ragged down a card that mixes both kinds of row.
            var grid = new Grid
            {
                ColumnDefinitions = row.PageTop
                    ?
                    [
                        new ColumnDefinition(GridLength.Auto),
                        new ColumnDefinition(12, GridUnitType.Pixel),
                        new ColumnDefinition(GridLength.Auto),
                    ]
                    :
                    [
                        new ColumnDefinition(GridLength.Auto) { MaxWidth = LabelColumnMaxWidth },
                        new ColumnDefinition(16, GridUnitType.Pixel),
                        new ColumnDefinition(1, GridUnitType.Star),
                        new ColumnDefinition(16, GridUnitType.Pixel),
                        new ColumnDefinition(ResetGutterWidth, GridUnitType.Pixel),
                    ],

                HorizontalAlignment = row.PageTop ? HorizontalAlignment.Right : HorizontalAlignment.Stretch,
            };

            // Not a styling hook: tests find the rows this view builds by the class rather than by
            // shape, since a three-column grid is also what Avalonia builds a TextBox out of.
            if (!row.PageTop)
            {
                grid.Classes.Add(CompactRowClass);
            }

            Grid.SetColumn(caption, 0);
            Grid.SetColumn(control, 2);

            // **Both halves centred, not just the control** (asked 2026-09-01: *"now align them
            // vertically"*).
            caption.VerticalAlignment = VerticalAlignment.Center;
            control.VerticalAlignment = VerticalAlignment.Center;
            control.HorizontalAlignment = row.PageTop ? HorizontalAlignment.Right : HorizontalAlignment.Left;
            grid.Children.Add(caption);
            grid.Children.Add(control);

            // Placed in the row's own gutter column rather than docked outside it, so the column stays
            // 44 wide whether or not this particular row can be reset.
            if (!row.PageTop && resetButton is not null)
            {
                Grid.SetColumn(resetButton, 4);
                resetButton.HorizontalAlignment = HorizontalAlignment.Center;
                resetButton.Margin = new Thickness(0);
                grid.Children.Add(resetButton);
                resetButton = null;
            }

            body = grid;
        }
        else
        {
            var stack = new StackPanel { Spacing = 8 };
            stack.Children.Add(caption);
            stack.Children.Add(control);
            body = stack;
        }

        // The reset glyph sits at the end of the row, right of the caption and the control alike (#279)
        // — the page-top row and the stacked (non-compact) rows are not part of the reserved gutter above,
        // so they still dock it outside the body rather than into a grid column.
        Control line = body;

        if (resetButton is not null)
        {
            var dock = new DockPanel { LastChildFill = true };
            DockPanel.SetDock(resetButton, Dock.Right);
            dock.Children.Add(resetButton);
            dock.Children.Add(body);
            line = dock;
        }

        // The page-top strip is a separate case and keeps its own rules (#279).
        if (!row.PageTop)
        {
            var rowShape = new Border
            {
                MinHeight = RowMinHeight,
                Padding = new Thickness(0, RowVerticalPadding),
                BorderThickness = new Thickness(0, 1, 0, 0),
                Child = line,
            };
            Themed(rowShape, Border.BorderBrushProperty, ThemeManager.BorderKey);

            if (shaded)
            {
                Themed(rowShape, Border.BackgroundProperty, ThemeManager.RowFillKey);
            }

            line = rowShape;
        }

        if (row.Protected)
        {
            // A bar rather than a chip: the row itself says it is protected, with no extra control to read
            // (#333). The legend that explains the bar is drawn once, above the cards.
            var bar = new Border
            {
                BorderThickness = new Thickness(ProtectedBarWidth, 0, 0, 0),
                Padding = new Thickness(ProtectedBarPadding, 0, 0, 0),
                Child = line,
            };
            Themed(bar, Border.BorderBrushProperty, ThemeManager.WarnKey);

            line = bar;
        }

        var container = new StackPanel();
        container.Children.Add(line);
        container.Children.Add(message);

        return new RowView(row, container, refresh)
        {
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
    /// label is where a Commander already looks to know what the row is (#333).
    /// </summary>
    private void AttachHelp(TextBlock label, CapabilityDescriptor capability, SettingRow row, TextBlock spoken)
    {
        var inside = new StackPanel
        {
            Spacing = 8,
            Children = { spoken, ExplainsLink(capability, row) },
        };

        label.Focusable = true;
        ToolTip.SetTip(label, new Border { Padding = new Thickness(4), Child = inside });
        ToolTip.SetShowDelay(label, 250);

        // A TextBlock shows its tooltip on hover already; keyboard focus needs to open and close it by hand.
        label.GotFocus += (_, _) => ToolTip.SetIsOpen(label, true);
        label.LostFocus += (_, _) => ToolTip.SetIsOpen(label, false);
    }

    /// <summary>The "Help" link the row's tooltip carries.</summary>
    private Button ExplainsLink(CapabilityDescriptor capability, SettingRow row)
    {
        var page = new Button
        {
            Content = "Help",
            FontSize = TypeScale.Secondary,
            Padding = new Thickness(0),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            HorizontalAlignment = HorizontalAlignment.Left,
            Cursor = new Cursor(StandardCursorType.Hand),
        };

        Themed(page, ForegroundProperty, ThemeManager.AccentKey);

        // The row's own anchor where it has one, the capability's page where it does not — a row with no
        // anchor still has somewhere to send the Commander, and it is better than a link that is missing on
        // the rows that most need explaining.
        page.Click += (_, _) => Process.Start(new ProcessStartInfo(
            DocsSite.Capability(capability.Id, row.DocsAnchor)) { UseShellExecute = true });

        return page;
    }

    /// <summary>
    /// Says that a row is waiting on something that is not happening in this class — the gap reaction
    /// spends a model round trip between the Commander picking a core and that core saying its first
    /// word, and the affordance they touched is this row.
    /// </summary>
    internal Control? ControlFor(string key) =>
        _rows.FirstOrDefault(row => string.Equals(row.Row.Key, key, StringComparison.Ordinal))?.Control;

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
            Themed(glyph, BusyGlyph.StrokeProperty, ThemeManager.AccentKey);

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

    /// <summary>
    /// The control for a row, its refresh action, and whether it is compact enough to sit to the right
    /// of its own caption.
    /// </summary>
    private (Control Control, Action Refresh, bool Compact) BuildControl(SettingRow row, TextBlock message)
    {
        switch (row.Kind)
        {
            // The one row that offers a window instead of a value.
            case SettingKind.Info when row.Key == DiagnosticsCapability.CoverageKey && _coverage is not null:
                return BuildCoverage(row);

            // The other row that offers a window instead of a value.
            case SettingKind.Info when row.Key == MacroCapability.ListKey && _macros is not null:
                return BuildMacros(row);

            // And the row that opens the persona editor.
            case SettingKind.Info when row.Key == PersonaCapability.OwnKey && _ownPersonas is not null:
                return BuildOwnPersonas(row);

            // The third row that offers a window.
            case SettingKind.Info when row.Key == ChecklistCapability.SummaryKey && _checklists is not null:
                return BuildChecklists(row);

            // The fourth row that offers a window.
            case SettingKind.Info when row.Key == SwitchCapability.ListKey && _switches is not null:
                return BuildSwitches(row);

            // The fifth row that offers a window.
            case SettingKind.Info when row.Key == LoreCapability.BookKey && _lore is not null:
                return BuildLore(row);

            // The sixth row that offers a window.
            case SettingKind.Info when row.Key == MemoryCapability.StoreKey && _memories is not null:
                return BuildMemories(row);

            // The seventh, and the only one whose button starts work rather than opening something.

            // The eighth, and the only one behind which a button spends money.
            case SettingKind.Info when row.Key == LogbookCapability.StoreKey && _logbook is not null:
                return BuildLogbook(row);

            // The ninth row that offers a window, and the only one that also clears what the window shows.
            case SettingKind.Info when row.Key == PrivacyCapability.AudioRecordingKey && _recording is not null:
                return BuildAudioRecording(row);

            // The tenth, and the only one behind which nothing is written down by D47 at all until the
            // Commander presses something.
            case SettingKind.Info when row.Key == DebriefCapability.DirectionsKey && _debrief is not null:
                return BuildDebrief(row);

            // An Info row that also clears the state it describes.
            case SettingKind.Info when row.Press is not null || row.PressAsync is not null:
                return BuildPressable(row, message);

            // A disclosure that is consulted rather than read.
            case SettingKind.Info when row.ValueAsHint:
                return (new Avalonia.Controls.Panel(), () => { }, true);

            // A summary with its full detail behind a one-press disclosure (#339).
            case SettingKind.Info when row.DetailBinding is not null:
                return BuildEgressDisclosure(row);

            case SettingKind.Info:
                return BuildInfo(row);

            case SettingKind.Toggle:
                return BuildToggle(row, message);

            case SettingKind.Choice when row.AllowsFreeText:
                // Free text: the searchable picker, which stays usable when the list is empty because the
                // value can be typed (Phase 4).
                return BuildPickerButton(row, message);

            case SettingKind.Choice:
                // Segment or stepper, by option count — always a stepper when the list comes from
                // ChoiceSource, since a run-time list can grow past a segment row's four (#274).
                return BuildChoice(row, message);

            case SettingKind.Number:
                return BuildNumber(row, message);

            case SettingKind.Secret:
                return BuildSecret(row, message);

            // One control for both, which is the whole of #217: what a bind row asks is "press the thing you
            // want", and which mechanisms are armed to hear it follows from the row rather than from a second
            // builder.
            case SettingKind.Hotkey:
            case SettingKind.HotasButton:
                return BuildBind(row, message);

            default:
                return BuildText(row, message);
        }
    }

    /// <summary>The read-out an Info row shows: no box, a 2px rule on the left edge, so it cannot be
    /// mistaken for a field (#335).</summary>
    private (Control, Action, bool) BuildInfo(SettingRow row)
    {
        var text = new SelectableTextBlock { FontSize = TypeScale.Body, TextWrapping = TextWrapping.Wrap };
        Themed(text, SelectableTextBlock.ForegroundProperty, ThemeManager.TextKey);

        var inset = new Border
        {
            BorderThickness = new Thickness(2, 0, 0, 0),
            Padding = new Thickness(14),
            Child = text,
        };
        Themed(inset, Border.BorderBrushProperty, ThemeManager.BorderKey);

        return row.Binding?.Read is { } read
            ? (inset, () => text.Text = read(_settings!.Current), false)
            : (inset, () => { }, false);
    }

    /// <summary>
    /// The two-line summary <see cref="BuildInfo"/> already draws, plus the full text behind a "Show
    /// more" press (#339).
    /// </summary>
    private (Control, Action, bool) BuildEgressDisclosure(SettingRow row)
    {
        var (summary, refreshSummary, _) = BuildInfo(row);

        var detail = new SelectableTextBlock
        {
            FontSize = TypeScale.Secondary,
            TextWrapping = TextWrapping.Wrap,
            IsVisible = false,
            Margin = new Thickness(14, 0, 14, 8),
        };
        Themed(detail, SelectableTextBlock.ForegroundProperty, ThemeManager.TextMutedKey);

        var toggle = new Button
        {
            Content = "Show more",
            FontSize = TypeScale.Secondary,
            Padding = new Thickness(0),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            HorizontalAlignment = HorizontalAlignment.Left,
            Cursor = new Cursor(StandardCursorType.Hand),
            Margin = new Thickness(14, 4, 0, 8),
        };
        Themed(toggle, ForegroundProperty, ThemeManager.AccentKey);

        toggle.Click += (_, _) =>
        {
            detail.IsVisible = !detail.IsVisible;
            toggle.Content = detail.IsVisible ? "Show less" : "Show more";
        };

        var stack = new StackPanel { Children = { summary, toggle, detail } };

        void Refresh()
        {
            refreshSummary();

            if (row.DetailBinding is { } read)
            {
                detail.Text = read(_settings!.Current);
            }
        }

        return (stack, Refresh, false);
    }

    /// <summary>A disclosure with the button that clears it.</summary>
    private (Control, Action, bool) BuildMemories(SettingRow row)
    {
        var (inset, refresh, _) = BuildInfo(row);

        var open = new Button
        {
            Name = "OpenMemories",
            Content = "Open what D47 remembers",
            FontSize = TypeScale.Body,
            Padding = new Thickness(8, 4),
            HorizontalAlignment = HorizontalAlignment.Left,
        };

        Panel.OffscreenSurface.OpensAWindow(open);

        open.Click += async (_, _) =>
        {
            if (_memories is not { } memories || TopLevel.GetTopLevel(this) is not Window owner)
            {
                return;
            }

            await new Controls.MemoryWindow(memories.Book, memories.Now).Over(owner);

            // The window writes the file; this is what puts the new count on the row without waiting for
            // something else to notice.
            refresh();
        };

        var stack = new StackPanel { Spacing = 8, Children = { inset, open } };

        return (stack, refresh, false);
    }

    /// <summary>The debrief summary, plus the way into the proposals.</summary>
    private (Control, Action, bool) BuildDebrief(SettingRow row)
    {
        var (inset, refresh, _) = BuildInfo(row);

        var open = new Button
        {
            Name = "OpenDebrief",
            Content = "Open what D47 has drafted",
            FontSize = TypeScale.Body,
            Padding = new Thickness(8, 4),
            HorizontalAlignment = HorizontalAlignment.Left,
        };

        Panel.OffscreenSurface.OpensAWindow(open);

        open.Click += async (_, _) =>
        {
            if (_debrief is not { } debrief || TopLevel.GetTopLevel(this) is not Window owner)
            {
                return;
            }

            await new Controls.DebriefWindow(debrief.Book, debrief.Now, debrief.Core).Over(owner);

            // The window writes the file; this is what puts the new count on the row without waiting for
            // something else to notice.
            refresh();
        };

        var stack = new StackPanel { Spacing = 8, Children = { inset, open } };

        return (stack, refresh, false);
    }

    private (Control, Action, bool) BuildLore(SettingRow row)
    {
        var (inset, refresh, _) = BuildInfo(row);

        var open = new Button
        {
            Name = "OpenLore",
            Content = "Open your notes",
            FontSize = TypeScale.Body,
            Padding = new Thickness(8, 4),
            HorizontalAlignment = HorizontalAlignment.Left,
        };

        Panel.OffscreenSurface.OpensAWindow(open);

        open.Click += async (_, _) =>
        {
            if (_lore is not { } editing || TopLevel.GetTopLevel(this) is not Window owner)
            {
                return;
            }

            await new Controls.LoreWindow(editing).Over(owner);

            // The window writes the file; this is what puts the new count on the row without waiting for
            // something else to notice.
            refresh();
        };

        var stack = new StackPanel { Spacing = 8, Children = { inset, open } };

        return (stack, refresh, false);
    }

    /// <summary>The Commander's log, behind a button (Phase 33).</summary>
    private (Control, Action, bool) BuildLogbook(SettingRow row)
    {
        var (inset, refresh, _) = BuildInfo(row);

        var open = new Button
        {
            Name = "OpenLogbook",
            Content = "Write up a session",
            FontSize = TypeScale.Body,
            Padding = new Thickness(8, 4),
            HorizontalAlignment = HorizontalAlignment.Left,
        };

        Panel.OffscreenSurface.OpensAWindow(open);

        open.Click += async (_, _) =>
        {
            if (_logbook is not { } logbook || TopLevel.GetTopLevel(this) is not Window owner)
            {
                return;
            }

            await new Controls.LogbookWindow(logbook).Over(owner);
            refresh();
        };

        var stack = new StackPanel { Spacing = 8, Children = { inset, open } };

        if (_logbook is { } book)
        {
            void OnChanged() => Avalonia.Threading.Dispatcher.UIThread.Post(refresh);

            stack.AttachedToVisualTree += (_, _) => book.Changed += OnChanged;
            stack.DetachedFromVisualTree += (_, _) => book.Changed -= OnChanged;
        }

        return (stack, refresh, false);
    }

    /// <summary>What the audio recorder holds, the way into reviewing it, and the wipe (#164).</summary>
    private (Control, Action, bool) BuildAudioRecording(SettingRow row)
    {
        var (inset, refresh, _) = BuildInfo(row);

        var open = new Button
        {
            Name = "OpenAudioRecorder",
            Content = "Review the recording",
            FontSize = TypeScale.Body,
            Padding = new Thickness(8, 4),
            HorizontalAlignment = HorizontalAlignment.Left,
        };

        Panel.OffscreenSurface.OpensAWindow(open);

        open.Click += async (_, _) =>
        {
            if (_recording is not { } recording || TopLevel.GetTopLevel(this) is not Window owner)
            {
                return;
            }

            await new Controls.AudioRecorderWindow(recording.Log, recording.Now).Over(owner);

            // Keeping a row changes what the summary says, and the window is where keeping happens — so the
            // row is re-read on the way out rather than left stating what was true when it opened.
            Refresh();
        };

        var wipe = new Button
        {
            Name = $"Press_{row.Key.Replace('.', '_')}",
            Content = row.PressLabel,
            FontSize = TypeScale.Body,
            Padding = new Thickness(8, 4),
            HorizontalAlignment = HorizontalAlignment.Left,
        };

        wipe.Click += (_, _) =>
        {
            row.Press!();
            Refresh();
        };

        var stack = new StackPanel { Spacing = 8, Children = { inset, open, wipe } };

        return (stack, refresh, false);
    }

    private (Control, Action, bool) BuildPressable(SettingRow row, TextBlock message)
    {
        var (inset, baseRefresh, _) = BuildInfo(row);

        var press = new Button
        {
            Name = $"Press_{row.Key.Replace('.', '_')}",
            Content = row.PressLabelFor?.Invoke() ?? row.PressLabel,
            FontSize = TypeScale.Body,
            Padding = new Thickness(8, 4),
            HorizontalAlignment = HorizontalAlignment.Left,
        };

        // A label computed from state discovered after the row was built — a pending update's version —
        // has to be re-read on every refresh, not only when the button is first drawn (#193).
        var refresh = row.PressLabelFor is { } label
            ? () =>
            {
                baseRefresh();
                press.Content = label();
            }
            : baseRefresh;

        // Along the bottom of the button rather than across the row, because it is the button's work it is
        // reporting.
        var bar = new ProgressBar
        {
            Name = $"Progress_{row.Key.Replace('.', '_')}",
            Height = 3,
            Minimum = 0,
            Maximum = 1,
            IsVisible = false,
        };

        if (row.ConfirmPress)
        {
            WireConfirmPress(row, press, bar, message);
        }
        else if (row.PressAsync is { } running)
        {
            press.Click += async (_, _) => await RunPressAsync(row, running, press, bar, message);
        }
        else
        {
            press.Click += (_, _) =>
            {
                row.Press!();

                // The whole surface rather than this row, because a press is not always about the row it is
                // on: binding a core to a ship changes what the row above says *and* what the list below it
                // says, and refreshing only the one pressed left the other one stating the state before the
                // press (Phase 35).
                Refresh();
            };
        }

        var pressed = new StackPanel
        {
            Spacing = 4,
            HorizontalAlignment = HorizontalAlignment.Left,
            Children = { press, bar },
        };

        // Nothing to read means nothing to show above the button, so the inset stays out (#78).
        var stack = row.Binding?.Read is null
            ? new StackPanel { Spacing = 8, Children = { pressed } }
            : new StackPanel { Spacing = 8, Children = { inset, pressed } };

        return (stack, refresh, false);
    }

    /// <summary>How long a first press stays armed before a second press has to ask again (#85).</summary>
    internal static readonly TimeSpan ConfirmPressWindow = TimeSpan.FromSeconds(4);

    /// <summary>
    /// A press this row acts on only asks the first time: it arms the button, and the actual work
    /// waits for a second press inside the window. Nothing else on the panel is touched by the first
    /// press, and the ask lapses back to the row's own label on its own if the second press does not
    /// come — pressable exactly like any other row, so the headset ray reaches it too (#85).
    /// </summary>
    private void WireConfirmPress(SettingRow row, Button press, ProgressBar bar, TextBlock message)
    {
        var armed = false;
        DispatcherTimer? lapse = null;

        void Disarm()
        {
            armed = false;
            press.Content = row.PressLabel;
            lapse?.Stop();
        }

        void Arm()
        {
            armed = true;
            press.Content = "Press again to confirm";

            lapse?.Stop();
            lapse = new DispatcherTimer { Interval = ConfirmPressWindow };
            lapse.Tick += (_, _) => Disarm();
            lapse.Start();
        }

        press.Click += async (_, _) =>
        {
            if (!armed)
            {
                Arm();
                return;
            }

            Disarm();

            if (row.PressAsync is { } running)
            {
                await RunPressAsync(row, running, press, bar, message);
            }
            else
            {
                row.Press!();
                Refresh();
            }
        };
    }

    /// <summary>Whether a long press is already running.</summary>
    private bool _pressing;

    /// <summary>A press that takes long enough to watch (#101).</summary>
    private async Task RunPressAsync(
        SettingRow row,
        LongPress running,
        Button press,
        ProgressBar bar,
        TextBlock message)
    {
        if (_pressing)
        {
            return;
        }

        _pressing = true;
        press.IsEnabled = false;
        bar.Value = 0;
        bar.IsVisible = true;
        message.IsVisible = false;

        try
        {
            var progress = new Progress<double>(fraction => bar.Value = fraction);
            var said = await running(progress, CancellationToken.None);

            if (said is { Length: > 0 })
            {
                Note(message, said);
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or TaskCanceledException)
        {
            Note(message, $"{row.Label} could not be done: {ex.Message}");
        }
        finally
        {
            _pressing = false;
            press.IsEnabled = true;
            bar.IsVisible = false;

            // The whole surface, for the reason the plain press refreshes it: what a press changes is not
            // always the row it was on.
            Refresh();
        }
    }

    /// <summary>The coverage summary, plus the way into the whole list.</summary>
    private (Control, Action, bool) BuildCoverage(SettingRow row)
    {
        var (inset, refresh, _) = BuildInfo(row);

        var open = new Button
        {
            Name = "OpenCoverage",
            Content = "Show the list",
            FontSize = TypeScale.Body,
            Padding = new Thickness(8, 4),
            HorizontalAlignment = HorizontalAlignment.Left,
        };

        Panel.OffscreenSurface.OpensAWindow(open);

        open.Click += async (_, _) =>
        {
            if (_coverage is not null && TopLevel.GetTopLevel(this) is Window owner)
            {
                await new Controls.CoverageWindow(_coverage()).Over(owner);
            }
        };

        var stack = new StackPanel { Spacing = 8, Children = { inset, open } };

        return (stack, refresh, false);
    }

    /// <summary>The Commander's own cores, plus the way into the editor (remediation.md 11, item 9).</summary>
    private (Control, Action, bool) BuildOwnPersonas(SettingRow row)
    {
        var (inset, refresh, _) = BuildInfo(row);

        var open = new Button
        {
            Name = "OpenOwnPersonas",
            Content = "Write a core",
            FontSize = TypeScale.Body,
            Padding = new Thickness(8, 4),
            HorizontalAlignment = HorizontalAlignment.Left,
        };

        Panel.OffscreenSurface.OpensAWindow(open);

        open.Click += async (_, _) =>
        {
            if (_ownPersonas is null || TopLevel.GetTopLevel(this) is not Window owner)
            {
                return;
            }

            await new Controls.PersonaWindow(_ownPersonas).Over(owner);

            // The editor writes the file; this is what puts the new summary on the row without waiting for
            // something else to notice.
            refresh();
        };

        return (new StackPanel { Spacing = 8, Children = { inset, open } }, refresh, false);
    }

    /// <summary>The macro summary, plus the way into the editor.</summary>
    private (Control, Action, bool) BuildMacros(SettingRow row)
    {
        var (inset, refresh, _) = BuildInfo(row);

        var open = new Button
        {
            Name = "OpenMacros",
            Content = "Edit macros",
            FontSize = TypeScale.Body,
            Padding = new Thickness(8, 4),
            HorizontalAlignment = HorizontalAlignment.Left,
        };

        Panel.OffscreenSurface.OpensAWindow(open);

        open.Click += async (_, _) =>
        {
            if (_macros is null || TopLevel.GetTopLevel(this) is not Window owner)
            {
                return;
            }

            await new Controls.MacroWindow(_macros) { ReservedPhrases = _reserved }.Over(owner);

            // The editor writes the file; this is what puts the new summary on the row without waiting for
            // something else to notice.
            refresh();
        };

        var stack = new StackPanel { Spacing = 8, Children = { inset, open } };

        return (stack, refresh, false);
    }

    /// <summary>The checklist summary, plus the way into the panel.</summary>
    private (Control, Action, bool) BuildChecklists(SettingRow row)
    {
        var (inset, refresh, _) = BuildInfo(row);

        // A tab of this panel since Phase 25, rather than a dialog over it: a Window cannot appear in the
        // headset at all, so the checklist was unreachable there for a Commander wearing one.
        var open = new Button
        {
            Name = "OpenChecklist",
            Content = "Open the checklist",
            FontSize = TypeScale.Body,
            Padding = new Thickness(8, 4),
            HorizontalAlignment = HorizontalAlignment.Left,
        };

        open.Click += (_, _) =>
        {
            // Its own panel, found up the tree rather than handed in.
            if (this.GetSelfAndVisualAncestors().OfType<Panel.PanelView>().FirstOrDefault() is { } panel)
            {
                panel.Tab = D47.Core.Interface.PanelTab.Checklist;
            }

            // The tab writes the file; this is what puts the new summary on the row without waiting for
            // something else to notice.
            refresh();
        };

        var stack = new StackPanel { Spacing = 8, Children = { inset, open } };

        return (stack, refresh, false);
    }

    /// <summary>The switch summary, plus the way into the walk.</summary>
    private (Control, Action, bool) BuildSwitches(SettingRow row)
    {
        var (inset, refresh, _) = BuildInfo(row);

        var open = new Button
        {
            Name = "OpenSwitches",
            Content = "Assign switches",
            FontSize = TypeScale.Body,
            Padding = new Thickness(8, 4),
            HorizontalAlignment = HorizontalAlignment.Left,
        };

        Panel.OffscreenSurface.OpensAWindow(open);

        open.Click += async (_, _) =>
        {
            if (_switches is not { } editing || TopLevel.GetTopLevel(this) is not Window owner)
            {
                return;
            }

            await new Controls.SwitchWindow(
                editing.Store,
                editing.Reader,
                editing.Reconciler,
                editing.Now,
                editing.ExportPath,
                editing.Destinations())
                .Over(owner);

            // The editor writes the file; this is what puts the new summary on the row without waiting for
            // something else to notice.
            refresh();
        };

        var stack = new StackPanel { Spacing = 8, Children = { inset, open } };

        return (stack, refresh, false);
    }

    private (Control, Action, bool) BuildToggle(SettingRow row, TextBlock message)
    {
        var toggle = new ToggleSwitch
        {
            Margin = new Thickness(0),
            Padding = new Thickness(0),
        };

        toggle.IsCheckedChanged += (_, _) =>
        {
            if (!_refreshing)
            {
                Apply(row, toggle.IsChecked == true ? "true" : "false", message);
            }
        };

        return (toggle, () => toggle.IsChecked = _settings!.Read(row.Key) is "true", true);
    }

    private (Control, Action, bool) BuildChoice(SettingRow row, TextBlock message)
    {
        // Through ChoicesFor, not the bare list.
        var choices = row.ChoicesFor(_settings!.Current);

        // A clear item only where clearing means something.
        var clearable = row.IsClearable;

        var items = new List<string>();
        if (clearable)
        {
            items.Add(row.BareDefaultFor(_settings!.Current) is { } bare ? $"(default: {bare})" : "(default)");
        }

        // One describer for the whole list rather than one call per item: a row may label a choice against
        // the others beside it — the model rows mark the cheapest of what is offered — and that is a property
        // of the list, not of the line (#152).
        items.AddRange(choices.Select(row.DescriberFor(_settings!.Current)));

        // A row whose list comes from ChoiceSource can grow past a segment row's four between one Refresh
        // and the next, so it is always a stepper (#274).
        var (view, combo) = Choice.Build(items, selectedIndex: -1, alwaysStepper: row.ChoiceSource is not null);

        // The position and what stepping onto a value costs, both shown only on a stepper (#336).
        if (view is Stepper stepper)
        {
            var consequences = new List<string?>();
            if (clearable)
            {
                consequences.Add(null);
            }

            consequences.AddRange(choices.Select(row.ConsequenceFor));
            stepper.Consequences = consequences;
        }

        view.HorizontalAlignment = HorizontalAlignment.Right;
        view.MinWidth = StandardControlWidth;
        DressAsAChoice(view);
        AutomationProperties.SetName(view, row.Label);

        // What fits in a fifth of the row, and some of these labels carry the part that matters on the
        // end of them: a speech model not on disk reads as "Small (English only) - more accu", which is
        // indistinguishable from one already installed.
        combo.SelectionChanged += (_, _) => ToolTip.SetTip(view, combo.SelectedItem);

        var offset = clearable ? 1 : 0;

        // The rows that download something carry a progress bar, and only those.
        var downloads = string.Equals(row.Key, ListeningCapability.ModelKey, StringComparison.Ordinal)
                        || row.FetchChoiceAsync is not null;

        var bar = new ProgressBar
        {
            Height = 3,
            Minimum = 0,
            Maximum = 1,
            IsVisible = false,
            Margin = new Thickness(0, 4, 0, 0),
        };

        // A row whose change costs something stages the pressed choice, and only this button applies it,
        // so stepping past a value never fetches it (#274).
        var confirm = new Button
        {
            IsVisible = false,
            HorizontalAlignment = HorizontalAlignment.Right,
            MinHeight = 36,
            VerticalContentAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 4, 0, 0),
        };

        confirm.Classes.Add("primary");

        var stagedNote = new TextBlock
        {
            Text = "Staged. What is in use keeps running until you press this.",
            FontSize = TypeScale.Small,
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Right,
            IsVisible = false,
            Margin = new Thickness(0, 4, 0, 0),
        };

        Themed(stagedNote, TextBlock.ForegroundProperty, ThemeManager.TextMutedKey);

        string? staged = null;

        void Unstage()
        {
            confirm.IsVisible = false;
            stagedNote.IsVisible = false;
        }

        void Take(string? chosen)
        {
            // One handler with a branch rather than two handlers.
            if (downloads)
            {
                _ = FetchModelAsync(row, chosen, view, combo, bar, message);
                return;
            }

            Apply(row, chosen, message);
        }

        confirm.Click += (_, _) =>
        {
            Unstage();
            Take(staged);
        };

        combo.SelectionChanged += (_, _) =>
        {
            if (_refreshing || combo.SelectedIndex < 0)
            {
                return;
            }

            var chosen = clearable && combo.SelectedIndex == 0
                ? null
                : choices[combo.SelectedIndex - offset];

            if (row.ConfirmLabel is not { } confirmLabel)
            {
                Take(chosen);
                return;
            }

            // Stepping back to what is in use has nothing left to apply.
            if (string.Equals(chosen, _settings!.Read(row.Key), StringComparison.OrdinalIgnoreCase))
            {
                Unstage();
                return;
            }

            staged = chosen;
            confirm.Content = chosen is null ? "Use the default" : confirmLabel(chosen);
            confirm.IsVisible = true;
            stagedNote.IsVisible = true;
        };

        Control control = downloads || row.ConfirmLabel is not null
            ? new StackPanel { Children = { view, stagedNote, confirm, bar } }
            : view;

        return (control, () =>
        {
            Unstage();

            var value = _settings!.Read(row.Key);
            var found = value is null
                ? -1
                : choices.Select((choice, i) => (choice, i))
                    .Where(pair => string.Equals(pair.choice, value, StringComparison.OrdinalIgnoreCase))
                    .Select(pair => (int?)pair.i)
                    .FirstOrDefault() ?? -1;

            combo.SelectedIndex = found < 0 ? (clearable ? 0 : -1) : found + offset;

            // The column is bounded, so a label written as a sentence - the speech models state their size
            // and speed - is clipped at the closed control.
            ToolTip.SetTip(
                view,
                combo.SelectedIndex >= 0 && combo.SelectedIndex < items.Count
                    ? items[combo.SelectedIndex]
                    : null);
        }, true);
    }

    /// <summary>Applies a speech model choice, downloading it first if it is not on disk.</summary>
    private async Task FetchModelAsync(
        SettingRow row,
        string? chosen,
        TemplatedControl view,
        IChoiceControl combo,
        ProgressBar bar,
        TextBlock message)
    {
        if (_downloadingModel)
        {
            return;
        }

        // A row that carries its own fetch (#139).
        if (row.FetchChoiceAsync is { } fetch)
        {
            await FetchChoiceAsync(row, chosen, view, combo, bar, message, fetch);
            return;
        }

        var model = WhisperModels.Find(chosen);

        // None, or no downloader behind this view: an ordinary setting with nothing to fetch.
        if (model is null || _downloadModel is null)
        {
            Apply(row, chosen, message);
            return;
        }

        _downloadingModel = true;

        // Shut while it runs.
        view.IsEnabled = false;

        bar.Value = 0;
        bar.IsVisible = true;

        Note(message, $"Fetching {model.Label} - about {model.ApproximateMegabytes} MB.");

        try
        {
            // A model already on disk comes straight back as AlreadyPresent, so there is no need to ask the
            // store separately whether this is a download at all.
            var progress = new Progress<ModelProgress>(report => bar.Value = report.Fraction);
            var result = await _downloadModel(model, progress);

            if (result.Outcome is ModelInstall.Installed or ModelInstall.AlreadyPresent)
            {
                // Written only now that the file is there, so the row can never name a model d47 cannot load.
                Apply(row, chosen, message);

                // Nothing left to say.
                message.IsVisible = false;
                message.Text = null;
                return;
            }

            Refresh();
            Note(message, result.Detail ?? $"{model.Id} was not downloaded.");
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or TaskCanceledException)
        {
            Refresh();
            Note(message, $"{model.Id} could not be downloaded: {ex.Message}");
        }
        finally
        {
            _downloadingModel = false;
            view.IsEnabled = true;
            bar.IsVisible = false;
        }
    }

    /// <summary>The same flow for a row that carries its own fetch (#139).</summary>
    private async Task FetchChoiceAsync(
        SettingRow row,
        string? chosen,
        TemplatedControl view,
        IChoiceControl combo,
        ProgressBar bar,
        TextBlock message,
        Func<string?, IProgress<double>, CancellationToken, Task<string?>> fetch)
    {
        _downloadingModel = true;
        view.IsEnabled = false;

        bar.Value = 0;
        bar.IsVisible = true;

        Note(message, $"Fetching {row.LabelForChoice(chosen ?? string.Empty, _settings!.Current)}.");

        try
        {
            var progress = new Progress<double>(fraction => bar.Value = fraction);
            var failure = await fetch(chosen, progress, CancellationToken.None);

            if (failure is null)
            {
                Apply(row, chosen, message);

                // Nothing left to say, for the reason the speech model row records: a change that worked is
                // visible in the control that made it.
                message.IsVisible = false;
                message.Text = null;
                return;
            }

            Refresh();
            Note(message, failure);
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or TaskCanceledException)
        {
            Refresh();
            Note(message, $"That could not be downloaded: {ex.Message}");
        }
        finally
        {
            _downloadingModel = false;
            view.IsEnabled = true;
            bar.IsVisible = false;
        }
    }

    /// <summary>Says something on the row itself.</summary>
    private static void Note(TextBlock message, string text)
    {
        message.Text = text;
        message.IsVisible = true;
    }

    private (Control, Action, bool) BuildPickerButton(SettingRow row, TextBlock message)
    {
        // Trimmed, because the column is a fifth of a row and a voice name is whatever the provider's account
        // calls it — "Bill - Wise, Mature, Balanced — male, american" is one of 473 real ones.
        var value = new TextBlock
        {
            FontSize = TypeScale.Body,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };

        var chevron = new TextBlock { Text = "⌄", FontSize = TypeScale.Body, VerticalAlignment = VerticalAlignment.Center };
        Themed(chevron, TextBlock.ForegroundProperty, ThemeManager.TextMutedKey);

        var layout = new DockPanel();
        DockPanel.SetDock(chevron, Dock.Right);
        layout.Children.Add(chevron);
        layout.Children.Add(value);

        var button = new Button
        {
            Content = layout,

            // On the button, not on the panel inside it: the floor is what the control stands at beside a
            // combo box, and a floor set inside the padding made the narrowest picker button 212 wide against
            // the combo's 190.
            MinWidth = StandardControlWidth,
            HorizontalAlignment = HorizontalAlignment.Right,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Center,
        };

        DressAsAChoice(button);

        Panel.OffscreenSurface.OpensAWindow(button);

        // Said rather than left to be guessed at.
        var busy = new BusyGlyph
        {
            IsVisible = false,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0),
        };
        Themed(busy, BusyGlyph.StrokeProperty, ThemeManager.AccentKey);

        button.Click += async (_, _) => await ChooseAsync(row, button, busy, message);

        // A DockPanel rather than a horizontal StackPanel, which is the other half of the same bug: a
        // StackPanel measures along its own direction with no limit at all, so the button was told it could
        // be as wide as it liked and believed it.
        var withBusy = new DockPanel { HorizontalAlignment = HorizontalAlignment.Right };
        DockPanel.SetDock(busy, Dock.Left);
        withBusy.Children.Add(busy);
        withBusy.Children.Add(button);

        return (withBusy, () =>
        {
            var current = _settings!.Read(row.Key);
            value.Text = current is null
                ? $"({row.BareDefaultFor(_settings.Current) ?? "not set"})"
                : row.LabelForChoice(current, _settings.Current);

            // The button is one line in a column; a model id or a resolved device name is routinely longer
            // than it.
            ToolTip.SetTip(button, current is null ? null : row.LabelForChoice(current, _settings.Current));

            if (current is null)
            {
                ShowDefaultOnHover(button, row);
            }
            Themed(value, TextBlock.ForegroundProperty, current is null ? ThemeManager.TextMutedKey : ThemeManager.TextKey);
        }, true);
    }

    private (Control, Action, bool) BuildNumber(SettingRow row, TextBlock message)
    {
        // Both from the row, so the control cannot offer a precision the store will not keep.
        var number = new NumericUpDown
        {
            Increment = (decimal)row.Step,
            FormatString = row.NumberFormat,
            Width = 216,
            HorizontalAlignment = HorizontalAlignment.Right,

            // The row's own range where it declares one, so a stepper never offers a click that the store is
            // only going to clamp away — an arrow that appears to do nothing reads as a broken control rather
            // than as a value already at its limit.
            Minimum = row.Minimum is { } low ? (decimal)low : decimal.MinValue,
            Maximum = row.Maximum is { } high ? (decimal)high : decimal.MaxValue,
        };

        if (row.Unit is { } unit)
        {
            var text = new TextBlock
            {
                Text = unit,
                FontFamily = new FontFamily(Fonts.MonoFamily),
                FontSize = TypeScale.Meta,
                VerticalAlignment = VerticalAlignment.Center,
            };
            Themed(text, TextBlock.ForegroundProperty, ThemeManager.TextFaintKey);

            var chip = new Border
            {
                BorderThickness = new Thickness(1, 0, 0, 0),
                Padding = new Thickness(11, 0),
                Child = text,
            };
            Themed(chip, Border.BorderBrushProperty, ThemeManager.BorderKey);

            number.InnerRightContent = chip;
        }

        number.ValueChanged += (_, e) =>
        {
            if (!_refreshing)
            {
                Apply(
                    row,
                    e.NewValue?.ToString(row.NumberFormat, System.Globalization.CultureInfo.InvariantCulture),
                    message);
            }
        };

        return (number, () =>
        {
            number.Value = decimal.TryParse(_settings!.Read(row.Key), out var parsed) ? parsed : null;
            number.PlaceholderText = row.DefaultDisplayFor(_settings.Current);
            ShowDefaultOnHover(number, row);
        }, true);
    }

    private (Control, Action, bool) BuildText(SettingRow row, TextBlock message)
    {
        var box = new TextBox
        {
            AcceptsReturn = row.Multiline,
            TextWrapping = row.Multiline ? TextWrapping.Wrap : TextWrapping.NoWrap,
            HorizontalAlignment = HorizontalAlignment.Left,
            Width = 460,
        };

        if (row.Multiline)
        {
            box.MinHeight = 90;
        }

        // Applied on leaving the box rather than on every keystroke: a setting that persists per character
        // would write a file per character, and would reject half-typed URLs as it went.
        box.LostFocus += (_, _) =>
        {
            if (!_refreshing)
            {
                Apply(row, box.Text, message);
            }
        };

        box.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter && !row.Multiline)
            {
                e.Handled = true;
                Apply(row, box.Text, message);
            }
        };

        return (box, () =>
        {
            box.Text = _settings!.Read(row.Key) ?? string.Empty;

            // The default is a placeholder, never a value, so "I have not chosen" stays distinguishable from
            // "I chose the default" (Phase 4).
            box.PlaceholderText = row.DefaultDisplayFor(_settings.Current) ?? string.Empty;
            ShowDefaultOnHover(box, row);
        }, false);
    }

    /// <summary>
    /// The key row, which is <see cref="SecretEditor"/> — the same control the first-run guide shows
    /// (Phase 16).
    /// </summary>
    private (Control, Action, bool) BuildSecret(SettingRow row, TextBlock message)
    {
        // The editor reports its own failures inline, next to the box that caused them, so the row's shared
        // message line stays for everything else.
        message.IsVisible = false;

        var editor = new SecretEditor(row, _settings!);

        // A stored key changes what other rows can offer — the voice picker is the obvious one — so the
        // surface re-reads itself rather than waiting for the next open.
        editor.Changed += Refresh;

        return (editor, editor.Refresh, false);
    }

    /// <summary>
    /// The one bind control (#217): a chip per bound key and a quiet CLEAR, in a row that wraps
    /// (#354). One persistent slot per <see cref="SettingRow.BoundKeys"/> entry plus the empty-state
    /// button, shown or hidden rather than rebuilt, so a capture in progress keeps its own control.
    /// </summary>
    private (Control, Action, bool) BuildBind(SettingRow row, TextBlock message)
    {
        var empty = new Button { MinWidth = 150, HorizontalContentAlignment = HorizontalAlignment.Center };
        var proseFont = empty.FontFamily;

        empty.Click += async (_, _) => await CaptureBindAsync(row, empty, message);

        var chips = row.BoundKeys.Select(_ =>
        {
            var chip = new Button
            {
                FontFamily = new FontFamily(Fonts.MonoFamily),
                FontSize = TypeScale.Secondary,
                FontWeight = FontWeight.Normal,
                LetterSpacing = 0,
                Padding = new Thickness(14, 10)
            };
            Themed(chip, Button.BackgroundProperty, ThemeManager.FillHigherKey);
            Themed(chip, Button.ForegroundProperty, ThemeManager.AccentInkKey);
            chip.Click += async (_, _) => await CaptureBindAsync(row, chip, message);
            return chip;
        }).ToList();

        var clear = new Button { Content = "CLEAR" };
        clear.Classes.Add("quiet");

        clear.Click += (_, _) =>
        {
            foreach (var key in row.BoundKeys)
            {
                Apply(key, null, message);
            }
        };

        var wrap = new WrapPanel { ItemSpacing = 8, LineSpacing = 8, HorizontalAlignment = HorizontalAlignment.Right };
        wrap.Children.Add(empty);
        foreach (var chip in chips)
        {
            wrap.Children.Add(chip);
        }
        wrap.Children.Add(clear);

        return (wrap, () =>
        {
            var said = BoundAs(row);
            var bound = said.Any(text => text is not null);

            // "Press to bind" and "No controllers" are prose, not data, so the empty-state control keeps
            // the chrome font rather than the chips' monospace (#279).
            empty.FontFamily = proseFont;
            empty.IsVisible = !bound;

            // A row that can only be filled from a controller is dead without one, and saying so beats a
            // button that does nothing.
            empty.IsEnabled = row.Kind != SettingKind.HotasButton || _switches is not null;
            empty.Content = row.Kind == SettingKind.HotasButton && _switches is null
                ? "No controllers"
                : "Press to bind";

            for (var i = 0; i < chips.Count; i++)
            {
                chips[i].IsVisible = said[i] is not null;
                chips[i].Content = said[i];
            }

            clear.IsVisible = bound;
        }, true);
    }

    /// <summary>
    /// What each of a bind row's <see cref="SettingRow.BoundKeys"/> is bound to, in the Commander's
    /// words, aligned by index — null where that slot holds nothing.
    /// </summary>
    private IReadOnlyList<string?> BoundAs(SettingRow row) => row.BoundKeys.Select(key =>
    {
        var stored = _settings!.Read(key);

        if (string.IsNullOrWhiteSpace(stored))
        {
            return null;
        }

        return KindOf(key) == SettingKind.HotasButton
            ? D47.Core.Hotas.HotasButton.Parse(stored)?.Describe() ?? stored
            : Gestures.Describe(stored);
    }).ToList();

    private SettingKind KindOf(string key) => _settings?.Find(key)?.Kind ?? SettingKind.Hotkey;

    private async Task ChooseAsync(SettingRow row, Button button, BusyGlyph busy, TextBlock message)
    {
        if (_settings is null || TopLevel.GetTopLevel(this) is not Window owner)
        {
            return;
        }

        // The work is "until the list is on screen", not "until the Commander has chosen": what can take a
        // moment is asking the machine for its capture devices or a provider for its voices, and once the
        // picker is up it is modal and speaks for itself.
        var listed = new TaskCompletionSource();

        var picking = PickerWindow.ShowAsync(
            owner,
            new PickerRequest
            {
                Prompt = row.Label,
                Help = row.Help,
                Choices = row.ChoicesFor(_settings.Current),

                // Read at open like the choices themselves: a label can depend on the provider serving the
                // list right now, and on the rest of the list beside it (#152).
                Describe = row.DescriberFor(_settings.Current),
                Current = _settings.Read(row.Key),
                DefaultDisplay = row.IsClearable ? row.BareDefaultFor(_settings.Current) : null,
                AllowsFreeText = row.AllowsFreeText,
                WhyEmpty = row.WhyNoChoicesFor(_settings.Current),

                // Read at open like the choices themselves, and for the same reason: which properties the
                // list has depends on the provider serving it right now (#146).
                Facet = row.Facet?.Invoke(_settings.Current),

                // Read at open rather than captured once, because both the price and the reason it might be
                // unavailable follow the selected provider.
                Audition = row.Audition is { } audition
                    ? new PickerAudition
                    {
                        Play = audition.Play,
                        Preview = audition.Preview,
                        HasPreview = audition.HasPreview,
                        Cost = audition.Cost(_settings.Current),
                        LineCost = audition.LineCost?.Invoke(_settings.Current),
                        Unavailable = audition.Unavailable?.Invoke(_settings.Current),
                    }
                    : null,
            },
            onListed: () => listed.TrySetResult());

        // A picker that throws on its way open never lists, and a glyph waiting for a list that is not coming
        // spins forever on a row nobody can use.
        _ = picking.ContinueWith(_ => listed.TrySetResult(), TaskScheduler.Default);

        // Hand-rolled here until Phase 12: shut, spinning, and two numbers nobody else shared.
        await Busy.While(button, busy, () => listed.Task);

        if (await picking is { } result)
        {
            Apply(row, result.Value, message);
        }

        button.Focus();
    }

    /// <summary>Binds by listening for a gesture, rather than by offering a list of key names.</summary>
    private async Task CaptureBindAsync(SettingRow row, Button button, TextBlock message)
    {
        if (TopLevel.GetTopLevel(this) is not { } top || _settings is null)
        {
            return;
        }

        var keys = row.Kind != SettingKind.HotasButton;

        // Whether a modifier pressed on its own is a binding here.
        var bare = keys && !row.SystemWide;

        // The stick is armed for a row that is one, or for a row naming one as its other half.
        var buttonKey = row.Kind == SettingKind.HotasButton
            ? row.Key
            : row.BoundKeys.FirstOrDefault(key => KindOf(key) == SettingKind.HotasButton);

        var stick = buttonKey is not null && _switches is not null;

        var previous = button.Content;

        button.Content = (keys, stick) switch
        {
            (true, true) => "Press a key or button…",
            (true, false) => "Press a key…",
            _ => "Press a button…",
        };

        var captured = new TaskCompletionSource<(string Key, string? Value)?>();

        // A modifier held on the way down, on a row where one is a binding in its own right.
        Key? held = null;

        void OnKey(object? sender, KeyEventArgs e)
        {
            var modifier = e.Key is Key.LeftCtrl or Key.RightCtrl or Key.LeftShift or Key.RightShift
                or Key.LeftAlt or Key.RightAlt or Key.LWin or Key.RWin;

            if (modifier)
            {
                // On a polled row, told apart by which edge it arrives on rather than refused: pressed, it is
                // still someone assembling a chord; released with nothing else pressed, it was the binding.
                held = bare ? e.Key : null;
                return;
            }

            held = null;
            e.Handled = true;

            captured.TrySetResult(e.Key == Key.Escape
                ? null
                : (row.Key, new KeyGesture(e.Key, e.KeyModifiers).ToString()));
        }

        void OnKeyUp(object? sender, KeyEventArgs e)
        {
            if (held != e.Key)
            {
                return;
            }

            e.Handled = true;
            captured.TrySetResult((row.Key, new KeyGesture(e.Key, KeyModifiers.None).ToString()));
        }

        if (keys)
        {
            // Tunnelling: the gesture belongs to the binding, not to whatever control the click left focused,
            // so it has to be seen on the way down.
            top.AddHandler(KeyDownEvent, OnKey, RoutingStrategies.Tunnel, handledEventsToo: true);

            if (bare)
            {
                top.AddHandler(KeyUpEvent, OnKeyUp, RoutingStrategies.Tunnel, handledEventsToo: true);
            }
        }

        var walking = stick ? Walk(_switches!, buttonKey!, message, captured) : null;

        try
        {
            if (await captured.Task is { } caught)
            {
                Apply(caught.Key, caught.Value, message);
            }
        }
        finally
        {
            if (keys)
            {
                top.RemoveHandler(KeyDownEvent, OnKey);
                top.RemoveHandler(KeyUpEvent, OnKeyUp);
            }

            walking?.Stop();
            button.Content = previous;
            Refresh();
        }
    }

    /// <summary>
    /// The controller half of a capture: the same 10 Hz walk the modal bind window ran, on a timer this
    /// control owns.
    /// </summary>
    private DispatcherTimer Walk(
        SwitchEditing editing,
        string key,
        TextBlock message,
        TaskCompletionSource<(string Key, string? Value)?> captured)
    {
        var capture = new D47.Core.Hotas.ButtonCapture();
        var opened = editing.Now();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };

        void Say(string text)
        {
            message.Text = text;
            message.IsVisible = true;
        }

        timer.Tick += (_, _) =>
        {
            if (editing.Reader.Unavailable is { Length: > 0 } why)
            {
                Say(why);
                timer.Stop();
                return;
            }

            // Nothing is read until the device list stops changing: a single enumeration at startup reported
            // three of six devices on the bench (Phase 21, finding 1).
            if (!editing.Reader.IsSettled)
            {
                Say("Looking for your controllers…");
                return;
            }

            var result = capture.Poll(editing.Reader.Poll(), editing.Now() - opened);

            Say(result.Says);

            if (result.Stage == D47.Core.Hotas.ButtonCaptureStage.Captured)
            {
                timer.Stop();
                captured.TrySetResult((key, result.Binding!.Value.ToString()));
            }
            else if (result.Stage == D47.Core.Hotas.ButtonCaptureStage.Declined)
            {
                // The decline is the answer, and it stays on the line.
                timer.Stop();
            }
        };

        timer.Start();

        return timer;
    }

    private bool Apply(SettingRow row, string? value, TextBlock message) =>
        Apply(row.Key, value, message);

    /// <summary>
    /// By key rather than by row, because one control can hold two of them (#217) and the message line,
    /// the redraw and the refusal are the same for both halves.
    /// </summary>
    private bool Apply(string key, string? value, TextBlock message)
    {
        if (_settings is null)
        {
            return false;
        }

        var result = _settings.Apply(key, value, SettingsCaller.Panel);

        // Only failures are worth *saying*.
        message.IsVisible = !result.Ok;
        message.Text = result.Message;

        // **But every outcome is worth redrawing** (#90).
        Refresh();

        return result.Ok;
    }

    /// <summary>How this surface shows a capability's help, or null where nothing wired one.</summary>
    private Action<string>? _openHelp;

    /// <summary>Gives the card marks somewhere to go that is not a browser (asked for 2026-08-23).</summary>
    public void EnableHelp(Action<string> open) => _openHelp = open;

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
            || _tabStripChevron is not { } chevron || content.IsVisible)
        {
            return;
        }

        content.IsVisible = true;
        chevron.Text = "▾";
        SaveViewState(state => state.With(placeId, true));
    }

    /// <summary>A card's question mark.</summary>
    private void OpenDocs(string capabilityId)
    {
        if (_openHelp is { } open)
        {
            open(capabilityId);
            return;
        }

        Process.Start(new ProcessStartInfo(DocsSite.Capability(capabilityId))
        {
            UseShellExecute = true,
        });
    }

    private sealed record SectionView(
        string PlaceId,
        string Title,

        /// <summary>The place's own search abbreviations — "ptt" for Microphone, and so on (#222).</summary>
        IReadOnlyList<string> Terms,
        Border Card,
        StackPanel Content,

        /// <summary>The card's own title, so a query that matched the section can be marked in it.</summary>
        TextBlock Heading,
        Border NavItem,
        Border NavBar,
        TextBlock NavText)
    {
        /// <summary>
        /// Opens or closes this card, chevron and remembered state together — the header press and <see
        /// cref="SettingsView.Reveal"/> both go through it, so neither can leave the two disagreeing.
        /// </summary>
        public Action<bool>? Expand { get; init; }

        /// <summary>This place's own "Show N more" for the rows its fold is hiding (#221).</summary>
        public Button? FoldButton { get; init; }

        /// <summary>
        /// How the nav item is currently painted, or null before it has been painted at all — which is
        /// what makes the first pass apply and the rest of them cost nothing.
        /// </summary>
        public bool? PaintedActive { get; set; }

        /// <summary>The nav item's live fill subscription, held so the next state can drop it.</summary>
        public IDisposable? NavFill { get; set; }

        /// <summary>The nav item's live text-ink subscription, held so the next state can drop it.</summary>
        public IDisposable? NavInk { get; set; }
    }

    /// <summary>One area's nav heading, its title and sentence, and the run of sections beneath it (#220).</summary>
    private sealed record AreaView(
        string Id,
        string Title,
        string Sentence,
        Border Heading,
        TextBlock HeadingText,

        /// <summary>The 3px bar that marks the selected tree node, shared with a place's own (#279).</summary>
        Border HeadingBar,

        /// <summary>Drawn above this area's cards while a query is narrowing every area at once.</summary>
        TextBlock FilterHeading,
        int First,
        int Count)
    {
        /// <summary>How the heading is currently painted, or null before it has been painted at all.</summary>
        public bool? PaintedActive { get; set; }

        public IDisposable? Ink { get; set; }

        public IDisposable? Fill { get; set; }
    }

    /// <summary>A named group heading within a place, so a query matching it reveals every row under it
    /// (#222).</summary>
    private sealed record GroupView(int Section, string Title, string? Help, TextBlock HeadingText, TextBlock? HelpText);

    private sealed record RowView(SettingRow Row, Control Container, Action Refresh)
    {
        /// <summary>Which card this row is in, so a filter can hide a card that has emptied.</summary>
        public int Section { get; init; } = -1;

        /// <summary>
        /// Which of <see cref="_groups"/> this row is under, or −1 where its group has no title to
        /// search (#222).
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
    }
}
