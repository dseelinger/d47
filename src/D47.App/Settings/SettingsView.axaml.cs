using System.Diagnostics;
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
    private readonly List<(SettingsSection Section, Button Button)> _cardResets = [];

    /// <summary>
    /// The name on a row's reset glyph, so a lookup for the row's own control can exclude it (#61).
    /// </summary>
    public const string RowResetName = "RowReset";

    /// <summary>
    /// The prefix on a row's info glyph, which carries the row key so two rows' callouts are
    /// distinguishable (asked for 2026-09-01).
    /// </summary>
    public const string RowInfoPrefix = "Info_";

    /// <summary>
    /// Whether a button is the row's chrome rather than the control the row is about — the reset glyph
    /// and the info glyph.
    /// </summary>
    public static bool IsRowChrome(Button button) =>
        button?.Name is { } name
        && (name == RowResetName || name.StartsWith(RowInfoPrefix, StringComparison.Ordinal));

    /// <summary>Whether a jump has revealed the folded rows for this session (#60).</summary>
    private bool _revealedByJump;

    /// <summary>
    /// The strip above the cards holding the page's own controls, or null where there are none (#60).
    /// </summary>
    private StackPanel? _pageStrip;
    private ViewStateStore? _viewStateStore;
    private ViewState _viewState = new();
    private AppPaths? _paths;

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
            Spacing = 2,
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
            Func<D47.Core.Persona.Persona> Core)? debrief = null)
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

        Build();

        RestoreSection();

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
        _activeSection = -1;
        _pageStrip = null;

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
            var strip = new StackPanel { Spacing = 12, Margin = new Thickness(0, 0, 0, 6) };

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
        }
        else
        {
            // No page row to sit beside — the glyphs still have a page to open and shut, so they go on their
            // own line rather than disappearing.
            var alone = BulkControls();

            alone.Margin = new Thickness(18, 0, 18, 6);
            Cards.Children.Add(alone);
        }

        foreach (var section in settings.Sections)
        {
            var title = section.Capability.Display.PanelTitle ?? section.Capability.Name;
            var (card, content, heading, expand) = BuildCard(section, title, _sections.Count);

            Cards.Children.Add(card);

            var nav = BuildNavItem(_sections.Count, title);
            NavItems.Children.Add(nav.Item);

            _sections.Add(
                new SectionView(
                    section.Capability.Id, title, card, content, heading, nav.Item, nav.Bar, nav.Text)
                {
                    Expand = expand,
                });
        }

        SetActiveSection(_sections.Count > 0 ? 0 : -1);
        Refresh();
    }

    /// <summary>The capability a page-level row still belongs to.</summary>
    private static CapabilityDescriptor SectionOwning(SettingsService settings, SettingRow row) =>
        settings.Sections.First(section => section.Rows.Any(other => other.Key == row.Key)).Capability;

    private (Border Card, StackPanel Content, TextBlock Heading, Action<bool> Expand) BuildCard(
        SettingsSection section,
        string title,
        int index)
    {
        var content = new StackPanel
        {
            Spacing = 18,
            Margin = new Thickness(18, 4, 18, 18),
            // Applied while building, not after painting: a card that flashes open and then collapses is
            // worse than one that never remembered (Phase 4).
            IsVisible = _viewState.IsExpanded(section.Capability.Id, section.Capability.Display.StartCollapsed),
        };

        if (!content.IsVisible)
        {
            _collapsed.Add(index);
        }

        string? currentGroup = null;

        // Minus the page's own, which are drawn above every card rather than inside one (#60).
        foreach (var row in section.Rows.Where(row => !row.PageTop))
        {
            // A group heading, stated once, in place of the same sentence on every row.
            if (row.Group is { } group && group != currentGroup)
            {
                content.Children.Add(BuildGroupHeading(group, row.GroupHelp));
                currentGroup = group;
            }
            else if (row.Group is null)
            {
                currentGroup = null;
            }

            var view = BuildRow(section.Capability, row) with { Section = index };
            _rows.Add(view);
            content.Children.Add(view.Container);
        }

        var chevron = new TextBlock
        {
            Text = content.IsVisible ? "▾" : "▸",
            FontSize = TypeScale.Body,
            Width = 14,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Themed(chevron, TextBlock.ForegroundProperty, ThemeManager.TextMutedKey);

        var heading = new TextBlock
        {
            Text = title,
            FontSize = TypeScale.Subheading,
            FontWeight = FontWeight.Medium,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Themed(heading, TextBlock.ForegroundProperty, ThemeManager.TextKey);

        var headerRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        headerRow.Children.Add(chevron);
        headerRow.Children.Add(heading);

        // One per card, not one per row.
        var docs = new Button
        {
            Content = "?",
            FontSize = TypeScale.Secondary,
            Padding = new Thickness(5, 0),
            MinWidth = 0,
            VerticalAlignment = VerticalAlignment.Center,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
        };

        Themed(docs, Button.ForegroundProperty, ThemeManager.TextMutedKey);
        ToolTip.SetTip(docs, $"Open the setup guide for {title}");

        docs.Click += (_, _) => OpenDocs(section.Capability);

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
            Padding = new Thickness(6, 2),
            MinWidth = 0,
            VerticalAlignment = VerticalAlignment.Center,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            IsVisible = CardHasChanges(section),
        };

        // With the mark rather than against it (#208).
        Themed(reset, Button.ForegroundProperty, ThemeManager.AccentKey);

        // The word was this button's accessible name by being its content; a Path has no text, so without
        // this a screen reader finds an unnamed button where it used to find "Reset".
        AutomationProperties.SetName(reset, $"Reset {title}");
        ToolTip.SetTip(reset, $"Put every {title} setting you have changed back to its default. Keys are untouched.");

        reset.Click += (_, _) =>
        {
            _settings!.ResetCard(section.Capability.Id, SettingsCaller.Panel);

            // And forget what has been said about whether this card is open (#223).
            SaveViewState(state => state.Forgetting(section.Capability.Id));

            Refresh();
        };

        // Held so its visibility can follow the card's state, the same way each row's glyph follows its own.
        _cardResets.Add((section, reset));

        reset.PointerPressed += (_, e) => e.Handled = true;

        headerRow.Children.Add(reset);

        var header = new Border
        {
            Padding = new Thickness(14, 11),
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

            RememberCollapse(section.Capability.Id, expanded);
        }

        header.PointerPressed += (_, _) => Expand(!content.IsVisible);

        header.PointerEntered += (_, _) => header.Background = Res(ThemeManager.SurfaceAltKey);
        header.PointerExited += (_, _) => header.Background = Brushes.Transparent;

        var body = new StackPanel();
        body.Children.Add(header);
        body.Children.Add(content);

        var card = new Border
        {
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Child = body,
        };

        Themed(card, Border.BackgroundProperty, ThemeManager.SurfaceKey);
        Themed(card, Border.BorderBrushProperty, ThemeManager.BorderKey);

        return (card, content, heading, Expand);
    }

    private Control BuildGroupHeading(string group, string? help)
    {
        // Full text colour at the row-label size, not muted at help-text size.
        var heading = new TextBlock
        {
            Text = group,
            FontSize = TypeScale.Body,
            FontWeight = FontWeight.Medium,
        };
        Themed(heading, TextBlock.ForegroundProperty, ThemeManager.TextKey);

        var stack = new StackPanel { Spacing = 2, Margin = new Thickness(0, 18, 0, 4) };

        // The rule goes above the heading.
        var rule = new Border { Height = 1, Margin = new Thickness(0, 0, 0, 10) };
        Themed(rule, Border.BackgroundProperty, ThemeManager.BorderKey);

        stack.Children.Add(rule);
        stack.Children.Add(heading);

        if (!string.IsNullOrWhiteSpace(help))
        {
            var note = new TextBlock { Text = help, FontSize = TypeScale.Secondary, TextWrapping = TextWrapping.Wrap };
            Themed(note, TextBlock.ForegroundProperty, ThemeManager.TextMutedKey);
            stack.Children.Add(note);
        }

        return stack;
    }

    private (Border Item, Border Bar, TextBlock Text) BuildNavItem(int index, string title)
    {
        var bar = new Border
        {
            Width = 2.5,
            CornerRadius = new CornerRadius(1),
            Margin = new Thickness(0, 2),
            Opacity = 0,
        };
        Themed(bar, Border.BackgroundProperty, ThemeManager.AccentKey);

        var text = new TextBlock
        {
            Text = title,
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
            Padding = new Thickness(8, 7),
            CornerRadius = new CornerRadius(4),
            Background = Brushes.Transparent,
            Cursor = new Cursor(StandardCursorType.Hand),
            Child = layout,
        };

        item.PointerPressed += (_, _) => ScrollTo(index);
        item.PointerEntered += (_, _) =>
        {
            if (index != _activeSection)
            {
                item.Background = Res(ThemeManager.SurfaceKey);
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
                // A capability id this build no longer registers is a stale name, and a stale name is worth
                // the top of the page rather than a failure - the same reading Reveal takes of one it cannot
                // find.
                var index = _sections.FindIndex(
                    section => string.Equals(section.CapabilityId, remembered, StringComparison.Ordinal));

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
    /// The sections' capability ids, in page order, and which one the scroll-spy is naming (−1 for
    /// none).
    /// </summary>
    internal IReadOnlyList<string> SectionIds => [.. _sections.Select(section => section.CapabilityId)];

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

        var section = _sections[_activeSection].CapabilityId;

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
    /// The two colours that say which section is being read: the item's fill, and the ink its name is
    /// written in.
    /// </summary>
    private void PaintNav(SectionView section, bool active)
    {
        if (section.PaintedActive == active)
        {
            return;
        }

        section.PaintedActive = active;

        section.NavInk?.Dispose();
        section.NavInk = Themed(
            section.NavText,
            TextBlock.ForegroundProperty,
            active ? ThemeManager.TextKey : ThemeManager.TextMutedKey);

        section.NavFill?.Dispose();
        section.NavFill = null;

        if (active)
        {
            section.NavFill = Themed(section.NavItem, Border.BackgroundProperty, ThemeManager.SurfaceAltKey);
        }
        else
        {
            // No resource for "nothing", so the fill is dropped rather than bound.
            section.NavItem.Background = Brushes.Transparent;
        }
    }

    /// <summary>
    /// Shows one section, named by the capability that owns it — what a help card pressed on the
    /// Transcript page does (asked for 2026-08-23).
    /// </summary>
    public void Reveal(string capabilityId)
    {
        var index = _sections.FindIndex(
            section => string.Equals(section.CapabilityId, capabilityId, StringComparison.Ordinal));

        if (index < 0)
        {
            return;
        }

        // A jump unfolds (#60).
        if (!_revealedByJump)
        {
            _revealedByJump = true;
            Refresh();
        }

        _sections[index].Expand?.Invoke(true);

        // After the layout the expansion caused, not before it: CardTop reads the card's position in the
        // scroller's content, and the cards below one that just opened have not moved yet.
        Dispatcher.UIThread.Post(() => ScrollTo(index), DispatcherPriority.Loaded);
    }

    private void ScrollTo(int index)
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
        var show = e.NewSize.Width >= NavCollapsesBelow;

        if (_navShown == show)
        {
            return;
        }

        _navShown = show;

        Nav.IsVisible = show;
        Root.ColumnDefinitions[0].Width = new GridLength(show ? NavWidth : 0);
        Root.MinWidth = show ? WideFloor : NarrowFloor;
    }

    private void OnScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (_sections.Count == 0)
        {
            return;
        }

        // At the very bottom, the last card is the answer even when it is too short to ever become topmost —
        // the classic scroll-spy edge.
        if (Scroller.Offset.Y >= Scroller.Extent.Height - Scroller.Viewport.Height - 2)
        {
            SetActiveSection(_sections.Count - 1);
            return;
        }

        var offset = Scroller.Offset.Y;
        var topmost = 0;

        for (var i = 0; i < _sections.Count; i++)
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

    /// <summary>Open every card, or shut every card (#223).</summary>
    private void SetEveryCard(bool expanded)
    {
        foreach (var section in _sections)
        {
            section.Expand?.Invoke(expanded);
        }
    }

    // Kept as members rather than folded into BulkControls' handlers: SettingsIsATabTests drives the bulk
    // controls through them, and a lambda has no name for a test to reach.
    private void OnExpandAllClick(object? sender, RoutedEventArgs e) => SetEveryCard(true);

    private void OnCollapseAllClick(object? sender, RoutedEventArgs e) => SetEveryCard(false);

    private void RememberCollapse(string capabilityId, bool expanded) =>
        SaveViewState(state => state.With(capabilityId, expanded));

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

        // Which sections the query names.
        var named = new bool[_sections.Count];

        for (var i = 0; i < _sections.Count; i++)
        {
            named[i] = _query.Length > 0
                       && _sections[i].Title.Contains(_query, StringComparison.OrdinalIgnoreCase);
        }

        foreach (var (section, button) in _cardResets)
        {
            button.IsVisible = CardHasChanges(section);
        }

        var pageRowsShown = 0;

        _refreshing = true;
        try
        {
            foreach (var row in _rows)
            {
                // A row that does not apply is absent, not disabled: a greyed-out control still asserts that
                // the setting exists (Phase 4).
                var shown = row.Row.Applies(_settings.Current)
                            && !row.Row.DrawnElsewhere
                            && !SettingsFold.IsFolded(
                                row.Row,
                                _settings.Current,
                                row.Row.BoundKeys.Any(_settings.IsChanged),
                                ShowingEverything)
                            && (Matches(row.Row) || (row.Section >= 0 && named[row.Section]));

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

        // The section's own name, marked in both places it is written.
        for (var i = 0; i < _sections.Count; i++)
        {
            Paint(_sections[i].Heading, _sections[i].Title);
            Paint(_sections[i].NavText, _sections[i].Title);
        }

        ApplyFilterToCards(showing, named);
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
            var target = targets[0].Target!;
            var index = _sections.FindIndex(section => section.CapabilityId == target);

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
        || row.Warning?.Contains(_query, StringComparison.OrdinalIgnoreCase) == true
        || row.Key.Contains(_query, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// A card with nothing left in it goes, and so does its nav item — a sidebar still listing fourteen
    /// sections when three of them hold anything is a sidebar that has stopped telling the truth.
    /// </summary>
    private void ApplyFilterToCards(int[] showing, bool[] named)
    {
        var filtering = _query.Length > 0;

        for (var i = 0; i < _sections.Count; i++)
        {
            var section = _sections[i];

            // Named counts even with nothing under it.
            var holds = showing[i] > 0 || named[i];

            // A card the fold has emptied is absent rather than an empty box (#60), which does more for the
            // anxiety than folding rows does — it takes Diagnostics, and VR with no headset, off the page
            // entirely.
            var anyRows = showing[i] > 0;

            section.Card.IsVisible = (!filtering || holds) && anyRows;
            section.NavItem.IsVisible = (!filtering || holds) && anyRows;

            section.Content.IsVisible = filtering ? holds : !_collapsed.Contains(i);
        }
    }

    /// <summary>
    /// Which cards the Commander had shut when a filter opened them, so clearing it shuts them again.
    /// </summary>
    private readonly HashSet<int> _collapsed = [];

    /// <summary>The width a compact row's control is built to.</summary>
    private const double StandardControlWidth = 190;

    /// <summary>
    /// Marks a caption-and-control row, so a test can find the rows this view builds rather than every
    /// three-column grid that happens to be in the tree.
    /// </summary>
    public const string CompactRowClass = "compact-row";

    /// <summary>The height every control that opens a list stands at, and the padding inside it.</summary>
    private const double ChoiceHeight = 32;

    private static readonly Thickness ChoicePadding = new(11, 6);

    /// <summary>One look for the two controls that open a list.</summary>
    private bool ShowingEverything =>
        _revealedByJump || (_settings?.Current.Ui.ShowEverySetting ?? true);

    private bool CardHasChanges(SettingsSection section) =>
        _settings is { } settings
        && section.Rows.Any(row => row.Applies(settings.Current) && settings.IsChanged(row.Key));

    private void DressAsAChoice(TemplatedControl control)
    {
        // Fixed rather than a floor.
        control.Height = ChoiceHeight;
        control.Padding = ChoicePadding;
        control.BorderThickness = new Thickness(1);
        control.CornerRadius = new CornerRadius(3);
        control.FontSize = TypeScale.Body;

        Themed(control, TemplatedControl.BackgroundProperty, ThemeManager.SurfaceAltKey);
        Themed(control, TemplatedControl.BorderBrushProperty, ThemeManager.BorderKey);
    }

    /// <summary>Fetches a speech model, reporting progress.</summary>
    private Func<WhisperModel, IProgress<ModelProgress>, Task<ModelInstallResult>>? _downloadModel;

    /// <summary>One download at a time, and the row that is showing it.</summary>
    private bool _downloadingModel;

    private RowView BuildRow(CapabilityDescriptor capability, SettingRow row)
    {
        var header = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };

        var label = new TextBlock
        {
            Text = row.Label,
            FontSize = TypeScale.Body,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Themed(label, TextBlock.ForegroundProperty, ThemeManager.TextKey);
        header.Children.Add(label);

        if (row.Protected)
        {
            // Said on the row rather than only in the docs: a Commander who asks d47 to change this and gets
            // refused should already know why.
            var tag = new TextBlock { Text = "protected", FontSize = TypeScale.Small, VerticalAlignment = VerticalAlignment.Center };
            Themed(tag, TextBlock.ForegroundProperty, ThemeManager.AccentMutedKey);

            var pill = new Border
            {
                Padding = new Thickness(6, 1),
                CornerRadius = new CornerRadius(8),
                BorderThickness = new Thickness(1),
                VerticalAlignment = VerticalAlignment.Center,
                Child = tag,
            };
            Themed(pill, Border.BorderBrushProperty, ThemeManager.AccentMutedKey);

            header.Children.Add(pill);
        }

        if (row.Scope == SettingScope.Commander)
        {
            // The same pill for the other declaration a row can make (Phase 44): this value is the
            // Commander's who is flying, and a second Commander on this machine will see their own here
            // rather than this one.
            var tag = new TextBlock { Text = "per Commander", FontSize = TypeScale.Small, VerticalAlignment = VerticalAlignment.Center };
            Themed(tag, TextBlock.ForegroundProperty, ThemeManager.AccentMutedKey);

            var pill = new Border
            {
                Padding = new Thickness(6, 1),
                CornerRadius = new CornerRadius(8),
                BorderThickness = new Thickness(1),
                VerticalAlignment = VerticalAlignment.Center,
                Child = tag,
            };
            Themed(pill, Border.BorderBrushProperty, ThemeManager.AccentMutedKey);

            header.Children.Add(pill);
        }

        if (row.Warning is not null)
        {
            // The badge half of a row's Warning (#237): the same pill the declarations above use, in the
            // danger colour, so a hazard reads as one at a glance rather than as another property tag.
            var tag = new TextBlock { Text = "warning", FontSize = TypeScale.Small, VerticalAlignment = VerticalAlignment.Center };
            Themed(tag, TextBlock.ForegroundProperty, ThemeManager.DangerKey);

            var pill = new Border
            {
                Padding = new Thickness(6, 1),
                CornerRadius = new CornerRadius(8),
                BorderThickness = new Thickness(1),
                VerticalAlignment = VerticalAlignment.Center,
                Child = tag,
            };
            Themed(pill, Border.BorderBrushProperty, ThemeManager.DangerKey);

            header.Children.Add(pill);
        }

        // **The help is behind a glyph now** (asked for 2026-09-01 — *"That is WAY too much text"*).
        var help = new TextBlock
        {
            Text = row.Help,
            FontSize = TypeScale.Secondary,
            Margin = new Thickness(0, 2, 0, 0),
            TextWrapping = TextWrapping.Wrap,
            IsVisible = false,
        };
        Themed(help, TextBlock.ForegroundProperty, ThemeManager.TextMutedKey);

        // The sentence half (#237): under the help, in the danger colour, because the one thing this line
        // must not do is read as more background.
        var warning = new TextBlock
        {
            Text = row.Warning,
            FontSize = TypeScale.Secondary,
            Margin = new Thickness(0, 2, 0, 0),
            TextWrapping = TextWrapping.Wrap,
            IsVisible = !string.IsNullOrWhiteSpace(row.Warning),
        };
        Themed(warning, TextBlock.ForegroundProperty, ThemeManager.DangerKey);

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
            Margin = new Thickness(0, 2, 0, 0),
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
            header.Children.Add(Explains(capability, row, spoken));
        }

        if (row is { Kind: not SettingKind.Secret, Binding.Write: not null })
        {
            var back = new Button
            {
                // Named so anything looking for "the control this row is about" can tell this apart from it.
                Name = RowResetName,

                // A stroked Path rather than U+21BA (#69).
                Content = Glyphs.Draw(Glyphs.Reset, ThemeManager.AccentKey, TypeScale.Secondary),

                // The same room the card-level one above needs, and for the same reason.
                Padding = new Thickness(4, 2),
                MinWidth = 0,
                VerticalAlignment = VerticalAlignment.Center,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                IsVisible = false,
            };

            Themed(back, Button.ForegroundProperty, ThemeManager.AccentKey);
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

            header.Children.Add(back);

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
        caption.Children.Add(warning);
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
            // Label and help on the left, the control on the right — the layout every settings surface a
            // Commander already knows uses for one-glance rows.
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
                        new ColumnDefinition(3, GridUnitType.Star),
                        new ColumnDefinition(16, GridUnitType.Pixel),

                        // The floor is the width the controls are already built to; below it the caption
                        // yields instead, which is the lesser of the two bad narrow cases.
                        new ColumnDefinition(2, GridUnitType.Star) { MinWidth = StandardControlWidth },
                    ],

                HorizontalAlignment = row.PageTop ? HorizontalAlignment.Right : HorizontalAlignment.Stretch,
            };

            // Load-bearing rather than decorative: RowWidthTests asserts the caption keeps the larger share
            // of every compact row, and it needs a way to say which grids those are.
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
            control.HorizontalAlignment = HorizontalAlignment.Right;
            grid.Children.Add(caption);
            grid.Children.Add(control);
            body = grid;
        }
        else
        {
            var stack = new StackPanel { Spacing = 8 };
            stack.Children.Add(caption);
            stack.Children.Add(control);
            body = stack;
        }

        var container = new StackPanel();
        container.Children.Add(body);
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
    /// The row's help, behind a lower-case <c>i</c> in a circle (asked for 2026-09-01 — "use an info
    /// glyph … which goes away when clicked outside").
    /// </summary>
    private Control Explains(CapabilityDescriptor capability, SettingRow row, TextBlock spoken)
    {
        var inside = new StackPanel
        {
            Spacing = 10,
            Children = { spoken, ExplainsLink(capability, row) },
        };

        var button = new Button
        {
            Name = RowInfoPrefix + row.Key.Replace('.', '_'),
            // The muted accent the pills beside it carry, not the bright one (asked for 2026-09-01).
            Content = Glyphs.Draw(Glyphs.Info, ThemeManager.AccentMutedKey, TypeScale.Secondary),

            // Room above and below for the stroke.
            Padding = new Thickness(4, 2),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            VerticalAlignment = VerticalAlignment.Center,
            Cursor = new Cursor(StandardCursorType.Hand),
            Flyout = new Flyout
            {
                Content = new Border { Padding = new Thickness(4), Child = inside },
                Placement = PlacementMode.BottomEdgeAlignedLeft,
                ShowMode = FlyoutShowMode.Standard,
            },
        };

        // A Path has no text, so a screen reader would find an unnamed button — the same fault, and the same
        // fix, as the reset glyph and the run-composed captions.
        AutomationProperties.SetName(button, $"About {row.Label}");

        // The hover says the same words the click does (#341).
        ToolTip.SetTip(button, ExplainsTooltip(capability, row));

        return button;
    }

    /// <summary>
    /// A second copy of <see cref="Explains"/>'s flyout content, for the hover — the same words and the
    /// same way out to the web page, wrapped at the width the flyout already wraps at rather than
    /// running off the window on a two-sentence help string.
    /// </summary>
    private Control ExplainsTooltip(CapabilityDescriptor capability, SettingRow row)
    {
        var spoken = new TextBlock
        {
            Text = row.Help,
            FontSize = TypeScale.Secondary,
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 420,
        };
        Themed(spoken, TextBlock.ForegroundProperty, ThemeManager.TextKey);

        return new Border
        {
            Padding = new Thickness(4),
            Child = new StackPanel
            {
                Spacing = 10,
                Children = { spoken, ExplainsLink(capability, row) },
            },
        };
    }

    /// <summary>The "Help" link the flyout and its tooltip twin both carry.</summary>
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

            case SettingKind.Info:
                return BuildInfo(row);

            case SettingKind.Toggle:
                return BuildToggle(row, message);

            case SettingKind.Choice when row.AllowsFreeText || row.IsOpenVocabulary:
                // Long or open vocabulary: the searchable picker, which stays usable when the list is empty
                // because the value can be typed (Phase 4).
                return BuildPickerButton(row, message);

            case SettingKind.Choice:
                return BuildComboBox(row, message);

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

    /// <summary>The read-out an Info row shows.</summary>
    private (Control, Action, bool) BuildInfo(SettingRow row)
    {
        var text = new SelectableTextBlock { FontSize = TypeScale.Secondary, TextWrapping = TextWrapping.Wrap };
        Themed(text, SelectableTextBlock.ForegroundProperty, ThemeManager.TextMutedKey);

        var inset = new Border
        {
            Padding = new Thickness(10, 8),
            CornerRadius = new CornerRadius(4),
            Child = text,
        };
        Themed(inset, Border.BackgroundProperty, ThemeManager.SurfaceAltKey);

        return row.Binding?.Read is { } read
            ? (inset, () => text.Text = read(_settings!.Current), false)
            : (inset, () => { }, false);
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
            Padding = new Thickness(10, 4),
            HorizontalAlignment = HorizontalAlignment.Left,
        };

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
            Padding = new Thickness(10, 4),
            HorizontalAlignment = HorizontalAlignment.Left,
        };

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
            Padding = new Thickness(10, 4),
            HorizontalAlignment = HorizontalAlignment.Left,
        };

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
            Padding = new Thickness(10, 4),
            HorizontalAlignment = HorizontalAlignment.Left,
        };

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
            Padding = new Thickness(10, 4),
            HorizontalAlignment = HorizontalAlignment.Left,
        };

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
            Padding = new Thickness(10, 4),
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
        var (inset, refresh, _) = BuildInfo(row);

        var press = new Button
        {
            Name = $"Press_{row.Key.Replace('.', '_')}",
            Content = row.PressLabel,
            FontSize = TypeScale.Body,
            Padding = new Thickness(10, 4),
            HorizontalAlignment = HorizontalAlignment.Left,
        };

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

        if (row.PressAsync is { } running)
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
            Padding = new Thickness(10, 4),
            HorizontalAlignment = HorizontalAlignment.Left,
        };

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
            Padding = new Thickness(10, 4),
            HorizontalAlignment = HorizontalAlignment.Left,
        };

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
            Padding = new Thickness(10, 4),
            HorizontalAlignment = HorizontalAlignment.Left,
        };

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
            Padding = new Thickness(10, 4),
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
            Padding = new Thickness(10, 4),
            HorizontalAlignment = HorizontalAlignment.Left,
        };

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
            OnContent = null,
            OffContent = null,
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

    private (Control, Action, bool) BuildComboBox(SettingRow row, TextBlock message)
    {
        var combo = new ComboBox { MinWidth = StandardControlWidth, HorizontalAlignment = HorizontalAlignment.Right };
        DressAsAChoice(combo);

        // The closed box shows what fits in a fifth of the row, and some of these labels carry the part that
        // matters on the end of them: a speech model not on disk reads as "Small (English only) - more accu",
        // which is indistinguishable from one already installed.
        combo.SelectionChanged += (_, _) =>
            ToolTip.SetTip(combo, combo.SelectedItem as string);

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
        combo.ItemsSource = items;

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
            Margin = new Thickness(0, 6, 0, 0),
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

            // One handler with a branch rather than two handlers.
            if (downloads)
            {
                _ = FetchModelAsync(row, chosen, combo, bar, message);
                return;
            }

            Apply(row, chosen, message);
        };

        Control control = downloads
            ? new StackPanel { Children = { combo, bar } }
            : combo;

        return (control, () =>
        {
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
                combo,
                combo.SelectedIndex >= 0 && combo.SelectedIndex < items.Count
                    ? items[combo.SelectedIndex]
                    : null);
        }, true);
    }

    /// <summary>Applies a speech model choice, downloading it first if it is not on disk.</summary>
    private async Task FetchModelAsync(
        SettingRow row,
        string? chosen,
        ComboBox combo,
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
            await FetchChoiceAsync(row, chosen, combo, bar, message, fetch);
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
        combo.IsEnabled = false;

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
            combo.IsEnabled = true;
            bar.IsVisible = false;
        }
    }

    /// <summary>The same flow for a row that carries its own fetch (#139).</summary>
    private async Task FetchChoiceAsync(
        SettingRow row,
        string? chosen,
        ComboBox combo,
        ProgressBar bar,
        TextBlock message,
        Func<string?, IProgress<double>, CancellationToken, Task<string?>> fetch)
    {
        _downloadingModel = true;
        combo.IsEnabled = false;

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
            combo.IsEnabled = true;
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

        // Behind this is a window, which is a thing the headset's copy of this surface must not open: a
        // dialog on a desktop the Commander is not looking at is a dialog they cannot answer.
        button.Classes.Add(Panel.OffscreenSurface.DesktopOnly);

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
            MinWidth = 130,
            HorizontalAlignment = HorizontalAlignment.Right,

            // The row's own range where it declares one, so a stepper never offers a click that the store is
            // only going to clamp away — an arrow that appears to do nothing reads as a broken control rather
            // than as a value already at its limit.
            Minimum = row.Minimum is { } low ? (decimal)low : decimal.MinValue,
            Maximum = row.Maximum is { } high ? (decimal)high : decimal.MaxValue,
        };

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

    /// <summary>The one bind control (#217).</summary>
    private (Control, Action, bool) BuildBind(SettingRow row, TextBlock message)
    {
        var button = new Button { MinWidth = 150, HorizontalContentAlignment = HorizontalAlignment.Center };
        var clear = new Button { Content = "Unbind" };

        button.Click += async (_, _) => await CaptureBindAsync(row, button, message);

        clear.Click += (_, _) =>
        {
            foreach (var key in row.BoundKeys)
            {
                Apply(key, null, message);
            }
        };

        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        panel.Children.Add(button);
        panel.Children.Add(clear);

        return (panel, () =>
        {
            button.Content = BoundAs(row) ?? "Press to bind";

            // A row that can only be filled from a controller is dead without one, and saying so beats a
            // button that does nothing.
            button.IsEnabled = row.Kind != SettingKind.HotasButton || _switches is not null;

            if (row.Kind == SettingKind.HotasButton && _switches is null)
            {
                button.Content = "No controllers";
            }
        }, true);
    }

    /// <summary>
    /// What a bind row is bound to, in the Commander's words — both halves when it holds two, in the
    /// order they are pressed for rather than the order they are stored in.
    /// </summary>
    private string? BoundAs(SettingRow row)
    {
        var said = new List<string>();

        foreach (var key in row.BoundKeys)
        {
            var stored = _settings!.Read(key);

            if (string.IsNullOrWhiteSpace(stored))
            {
                continue;
            }

            said.Add(KindOf(key) == SettingKind.HotasButton
                ? D47.Core.Hotas.HotasButton.Parse(stored)?.Describe() ?? stored
                : Gestures.Describe(stored));
        }

        return said.Count == 0 ? null : string.Join(", ", said);
    }

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
                        Cost = audition.Cost(_settings.Current),
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

    /// <summary>A card's question mark.</summary>
    private void OpenDocs(CapabilityDescriptor capability)
    {
        if (_openHelp is { } open)
        {
            open(capability.Id);
            return;
        }

        Process.Start(new ProcessStartInfo(DocsSite.Capability(capability.Id))
        {
            UseShellExecute = true,
        });
    }

    private sealed record SectionView(
        string CapabilityId,
        string Title,
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

        /// <summary>
        /// How the nav item is currently painted, or null before it has been painted at all — which is
        /// what makes the first pass apply and the rest of them cost nothing.
        /// </summary>
        public bool? PaintedActive { get; set; }

        /// <summary>The nav item's live brush subscriptions, held so the next state can drop them.</summary>
        public IDisposable? NavInk { get; set; }

        public IDisposable? NavFill { get; set; }
    }

    private sealed record RowView(SettingRow Row, Control Container, Action Refresh)
    {
        /// <summary>Which card this row is in, so a filter can hide a card that has emptied.</summary>
        public int Section { get; init; } = -1;

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
