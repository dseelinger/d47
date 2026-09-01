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

/// <summary>
/// The settings surface. Every row on it is generated from a capability descriptor — there is
/// no list of controls here to keep in step with the registry, which is the whole point of
/// descriptors declaring their own rows (architecture.md §5 D5).
/// <para>
/// There is no save button. A control that changes calls <see cref="SettingsService.Apply"/>
/// as <see cref="SettingsCaller.Panel"/>, which validates, persists and announces; every open
/// surface then refreshes from the announcement. That is also why a rejected value snaps back
/// rather than sitting on screen looking accepted.
/// </para>
/// <para>
/// Control choice per row kind: a short closed vocabulary is a ComboBox, because a dialog for
/// seven fixed values is ceremony; the searchable picker is reserved for the long and open
/// lists it was built for — models now, voices and devices in later phases (Phase 4).
/// Free text is only used where the value genuinely is free text.
/// </para>
/// </summary>
public partial class SettingsView : UserControl, D47.App.Panel.IFilterablePage
{

    private readonly List<SectionView> _sections = [];
    private readonly List<RowView> _rows = [];

    private SettingsService? _settings;

    /// <summary>
    /// The per-card reset controls, so each can be hidden again once its card is back at its
    /// defaults (<a href="https://github.com/dseelinger/d47/issues/61">#61</a>).
    /// </summary>
    private readonly List<(SettingsSection Section, Button Button)> _cardResets = [];

    /// <summary>
    /// The name on a row's reset glyph, so a lookup for the row's own control can exclude it
    /// (<a href="https://github.com/dseelinger/d47/issues/61">#61</a>).
    /// </summary>
    public const string RowResetName = "RowReset";

    /// <summary>
    /// The prefix on a row's info glyph, which carries the row key so two rows' callouts are
    /// distinguishable (asked for 2026-09-01).
    /// </summary>
    public const string RowInfoPrefix = "Info_";

    /// <summary>
    /// Whether a button is the row's <em>chrome</em> rather than the control the row is about —
    /// the reset glyph and the info glyph.
    /// <para>
    /// <b>One predicate rather than a list of exclusions in every caller.</b> Two tests once took
    /// the first Button in a row and got the reset glyph; adding the info glyph broke seven more
    /// that had each learned to exclude the reset one by name. A third mark would have broken them
    /// again. This is the question they were all asking.
    /// </para>
    /// </summary>
    public static bool IsRowChrome(Button button) =>
        button?.Name is { } name
        && (name == RowResetName || name.StartsWith(RowInfoPrefix, StringComparison.Ordinal));

    /// <summary>
    /// Whether a jump has revealed the folded rows for this session
    /// (<a href="https://github.com/dseelinger/d47/issues/60">#60</a>).
    /// <para>
    /// <b>A jump must unfold.</b> A hundred and seven help links point at rows and say "change X
    /// here"; landing on a row that is not drawn is the silent-link fault class that already cost
    /// three bugs in the help pass.
    /// </para>
    /// <para>
    /// <b>Session-only rather than writing the toggle on.</b> Following a link is not the
    /// Commander asking for a different settings page for ever, and a navigation that quietly
    /// changed a setting would be the fold breaking its own promise to touch nothing.
    /// </para>
    /// </summary>
    private bool _revealedByJump;

    /// <summary>
    /// The strip above the cards holding the page's own controls, or null where there are none
    /// (<a href="https://github.com/dseelinger/d47/issues/60">#60</a>). Held so it can be hidden
    /// when a filter leaves nothing in it — empty furniture at the top of the page is the same
    /// fault as an empty card, and a search for "audio mixer" should not answer with a toggle.
    /// </summary>
    private StackPanel? _pageStrip;
    private ViewStateStore? _viewStateStore;
    private ViewState _viewState = new();
    private AppPaths? _paths;

    /// <summary>
    /// Reopens the guided key setup from About (Phase 16). Null in the designer and
    /// in tests, which hides the button rather than offering one that does nothing.
    /// </summary>
    private Func<Task>? _setUpKeys;

    /// <summary>
    /// Where the hand-testing coverage record stands, when this process was asked to keep one.
    /// Null on every normal run, which is what keeps the row's button absent rather than dead.
    /// </summary>
    private Func<CoverageReport>? _coverage;

    private D47.Core.Actions.MacroStore? _macros;
    private D47.Core.Persona.OwnPersonaStore? _ownPersonas;

    /// <summary>
    /// The Commander's checklist, for the row that offers the panel. Null under the designer and
    /// in a test that is not about it, and the button is then absent rather than dead.
    /// </summary>
    private D47.Core.Checklists.ChecklistService? _checklists;

    /// <summary>
    /// Everything the switch editor needs: the file, the hardware to walk a switch against, the
    /// reconciler whose health line the cards show, and where a declined capture is written.
    /// Null under the designer and in a test that is not about it, and the button is then absent
    /// rather than dead.
    /// </summary>
    private SwitchEditing? _switches;

    private LoreEditing? _lore;

    /// <summary>
    /// What d47 remembers about the Commander, and the clock a hand-typed fact is stamped with
    /// (Phase 31). Null under the designer and in a test that is not about it, and the
    /// button is then absent rather than dead.
    /// </summary>
    private (D47.Core.Memory.MemoryBook Book, Func<DateTimeOffset> Now)? _memories;

    /// <summary>
    /// What the audio recorder has kept, and the clock a kept test case is stamped with
    /// (<a href="https://github.com/dseelinger/d47/issues/164">#164</a>). Null in every process
    /// that was not asked to record — which is every ordinary run — and the row itself is then
    /// absent, so this never decides whether a button is dead.
    /// </summary>
    private (D47.Core.Diagnostics.Recording.RecordingLog Log, Func<DateTimeOffset> Now)? _recording;

    /// <summary>
    /// What the debrief drafted, the clock an adoption is stamped with, and which core is aboard
    /// (<a href="https://github.com/dseelinger/d47/issues/162">#162</a>). Null under the designer
    /// and in a test that is not about it, and the button is then absent rather than dead.
    /// </summary>
    private (D47.Core.Debrief.DebriefBook Book, Func<DateTimeOffset> Now,
        Func<D47.Core.Persona.Persona> Core)? _debrief;

    /// <summary>
    /// The Commander's log (Phase 33). Null under the designer and in a test that is not
    /// about it, and the row then reads a folder with no way to write into it.
    /// </summary>
    private D47.Core.Logbook.LogbookBook? _logbook;

    /// <summary>
    /// Phrases d47 already answers to, so the editor can refuse a macro that would shadow
    /// one. Supplied rather than derived here: the settings surface only knows the
    /// capabilities that declare rows, and a phrase can come from one that does not.
    /// </summary>
    private IReadOnlyList<string> _reserved = [];

    /// <summary>True while controls are being written from settings rather than read from.</summary>
    private bool _refreshing;

    private int _activeSection = -1;

    public SettingsView()
    {
        InitializeComponent();

        // A plus and a minus (#223, reworked on the Commander's instruction 2026-09-01). Marked
        // here rather than in the axaml because Mark sets the tooltip *and* the accessible name
        // from one string, and a glyph-only control without an accessible name does not exist for
        // anybody who is not looking at it. Accent, because a clickable thing carries the accent
        // (#208).
        //
        // The words are the Commander's own: the names of the things, rather than the sentences
        // the chevrons needed in order to explain themselves.
        Controls.Glyphs.Mark(
            ExpandAll,
            Controls.Glyphs.ExpandAll,
            ThemeManager.AccentKey,
            "Expand all");

        // Filled, alone among these: a minus has no height, and a stretched-to-fit geometry with
        // no height collapses to nothing. See the note on Glyphs.CollapseAll.
        Controls.Glyphs.Mark(
            CollapseAll,
            Controls.Glyphs.CollapseAll,
            ThemeManager.AccentKey,
            "Collapse all",
            filled: true);
    }

    /// <summary>
    /// Binds the view to a live settings service. Called once; the view then follows the
    /// service rather than being told when to update.
    /// </summary>
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

        // Appended rather than slotted in beside the macro store it most resembles: the callers
        // pass these positionally, so a parameter added in the middle silently rebinds every
        // argument after it (remediation.md 11, item 9).
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

        settings.Changed += OnSettingsChanged;

        // **Symmetric, which it was not** (<a href="https://github.com/dseelinger/d47/issues/90">#90</a>).
        // Subscribing once in the constructor and unsubscribing on *every* detach is a
        // subscription that survives exactly one detach and is then gone for the life of the
        // view — so from that moment the page never hears a settings change again, and every
        // control showing a derived caption keeps whatever it drew at build time. The two
        // subscriptions further down this file already do it this way; this one did not.
        //
        // Unsubscribed before resubscribing rather than merely added, so an attach that arrives
        // without a matching detach cannot leave two handlers posting two refreshes.
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
    /// Binds a brush property to a theme resource, so a theme switch repaints controls built
    /// in code the same way DynamicResource repaints the ones built in markup.
    /// </summary>
    private IDisposable Themed(AvaloniaObject target, AvaloniaProperty property, string key) =>
        target.Bind(property, this.GetResourceObservable(key));

    /// <summary>
    /// Hangs the row's default on a control as a tooltip, in full.
    /// <para>
    /// A default is only useful if it says what it is, and the place it is shown is a
    /// placeholder inside a control sized for values rather than for sentences — so
    /// "(the system default - Virtual Desktop Audio)" arrives on screen as
    /// "(the system default - Virtual Deskt...". The control keeps the short form and the
    /// pointer gets the whole of it, which is the only way both fit.
    /// </para>
    /// <para>
    /// Set on every refresh rather than once, because several defaults are computed from other
    /// settings - the model's belongs to the selected provider, the ship AI name's to the core
    /// aboard - and a tooltip written at build time would keep answering for a provider the
    /// Commander has since changed.
    /// </para>
    /// </summary>
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
        // (https://github.com/dseelinger/d47/issues/60). "Show every setting" decides what the
        // whole page draws, and a Commander who cannot see the rest of the settings will not go
        // looking for the reason four rows into Interface.
        //
        // Which rows these are is declared on the row, not known here — a panel holding its own
        // list of which rows are special is a second list to keep in step.
        var pageRows = settings.Sections
            .SelectMany(section => section.Rows)
            .Where(row => row.PageTop)
            .ToList();

        if (pageRows.Count > 0)
        {
            var strip = new StackPanel { Spacing = 12, Margin = new Thickness(18, 0, 18, 6) };

            var first = true;

            foreach (var row in pageRows)
            {
                var view = BuildRow(SectionOwning(settings, row), row);

                _rows.Add(view);

                // **Beside the first page row rather than docked above the scroller** (the
                // Commander's instruction, 2026-09-01). Open-and-shut and "Show every setting" are
                // both about what the whole page draws, and two controls answering one question
                // belong on one line. A grid rather than a stack, so the row keeps whatever width
                // it wants and the glyphs take only what they need.
                if (first)
                {
                    // **A DockPanel, and that is the whole of the bug that shipped** (reported
                    // 2026-09-01 — *"things are running off to the right"*). This was a grid with a
                    // star column, and a star cannot be resolved against an unbounded width: the
                    // cards sit in a ScrollViewer that scrolls horizontally, so measure hands its
                    // contents infinity, and the row's own three-star caption and two-star control
                    // were laid out against it. A DockPanel gives its fill child what is actually
                    // left, which is the finite width the row had when it was a plain child of the
                    // strip.
                    var line = new DockPanel();

                    Detach(BulkExpand);

                    BulkExpand.Margin = new Thickness(0, 0, 12, 0);
                    BulkExpand.VerticalAlignment = VerticalAlignment.Center;
                    DockPanel.SetDock(BulkExpand, Dock.Left);

                    line.Children.Add(BulkExpand);
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
            // No page row to sit beside — the glyphs still have a page to open and shut, so they
            // go back where they were declared rather than disappearing.
            Detach(BulkExpand);

            BulkExpand.Margin = new Thickness(18, 0, 18, 6);
            Cards.Children.Add(BulkExpand);
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

    /// <summary>
    /// The capability a page-level row still belongs to. Lifting a row to the top of the page
    /// changes where it is drawn and nothing else — its help still opens its own capability's
    /// page, exactly as it would have from inside the card (#60).
    /// </summary>
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
            // Applied while building, not after painting: a card that flashes open and then
            // collapses is worse than one that never remembered (Phase 4).
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

        // One per card, not one per row. Every row in a card linked to that card's page, so a
        // card of nine rows carried nine question marks that went to the same place — noise
        // that made the one useful link harder to see rather than easier.
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

        // Stops the click reaching the header underneath, which would collapse the card the
        // Commander just asked to read about.
        docs.PointerPressed += (_, e) => e.Handled = true;

        headerRow.Children.Add(docs);

        // The gesture that matters when things have gone wrong
        // (https://github.com/dseelinger/d47/issues/61). A Commander who has been fiddling with
        // twenty-two Speech rows does not know which one did it, and "reset Speech" is what they
        // actually want to say.
        //
        // Present only while something on this card has been changed, so a card at its defaults
        // offers nothing to undo — the same rule as the row glyph below, and it doubles as a
        // quiet "you have changed something here".
        // The mark rather than the word (#69), and the same mark the row-level one uses: a card
        // reset and a row reset are the same promise at two scales, so they should not be a picture
        // and a word. Drawn at Small's size so it sits with the header text beside it.
        var reset = new Button
        {
            // Accent, like every other bare glyph whose only affordance is that it can be
            // pressed (#208). Muted read as "there is nothing here", which is exactly wrong on a
            // mark that is only drawn once something has been changed.
            Content = Glyphs.Draw(Glyphs.Reset, ThemeManager.AccentKey, TypeScale.Small),

            // Room for the stroke, which Made puts half of outside the box — see the note on
            // Glyphs.Reset. It matters now that the mark is a fuller circle: a clipped arc
            // reads as a flat edge where a clipped line end read as nothing.
            Padding = new Thickness(6, 2),
            MinWidth = 0,
            VerticalAlignment = VerticalAlignment.Center,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            IsVisible = CardHasChanges(section),
        };

        // With the mark rather than against it (#208). A Path with its own stroke ignores the
        // button's Foreground, so this paints nothing — but a dead property set to the opposite of
        // what is drawn is a line that tells the next reader the wrong thing.
        Themed(reset, Button.ForegroundProperty, ThemeManager.AccentKey);

        // The word was this button's accessible name by being its content; a Path has no text, so
        // without this a screen reader finds an unnamed button where it used to find "Reset".
        AutomationProperties.SetName(reset, $"Reset {title}");
        ToolTip.SetTip(reset, $"Put every {title} setting you have changed back to its default. Keys are untouched.");

        reset.Click += (_, _) =>
        {
            _settings!.ResetCard(section.Capability.Id, SettingsCaller.Panel);

            // And forget what has been said about whether this card is open (#223). Collapse all
            // writes a state for every card, and a card with a written state never falls back to
            // its own StartCollapsed again — so without this, one press of a bulk control buries
            // that default permanently and nothing anywhere brings it back. Nothing moves on
            // screen; the next launch decides.
            _viewState = _viewState.Forgetting(section.Capability.Id);
            _viewStateStore?.Save(_viewState);

            Refresh();
        };

        // Held so its visibility can follow the card's state, the same way each row's glyph
        // follows its own.
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

        // One place either route changes it. The header press is the Commander's; Reveal is a
        // help card's, and a second copy of these five lines is how the chevron ends up pointing
        // the wrong way at a card that is open.
        void Expand(bool expanded)
        {
            content.IsVisible = expanded;
            chevron.Text = expanded ? "▾" : "▸";

            // Recorded here as well as on disk, because a filter opens a card without being
            // asked and has to put it back the way the Commander left it.
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
        // Full text colour at the row-label size, not muted at help-text size. Set the way it
        // was, "While thinking" was the same weight and colour as the sentence under the row
        // above it, so it read as a stray remark rather than as the name of what follows.
        var heading = new TextBlock
        {
            Text = group,
            FontSize = TypeScale.Body,
            FontWeight = FontWeight.Medium,
        };
        Themed(heading, TextBlock.ForegroundProperty, ThemeManager.TextKey);

        var stack = new StackPanel { Spacing = 2, Margin = new Thickness(0, 18, 0, 4) };

        // The rule goes above the heading. Below it, the line separated the heading from the
        // rows it introduces and tied it to the ones before — which is the opposite of what a
        // heading does.
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
    }

    /// <summary>
    /// Brings the highlighted nav entry into view, and only when it is not already there
    /// (remediation.md 17, item 3).
    /// <para>
    /// <b>A scrollbar alone would not have finished the item.</b> The nav is a scroll-spy index:
    /// it highlights whichever card is topmost in the column beside it, so once the list is
    /// longer than its own viewport the highlight lands off screen and the nav is as useless
    /// below the fold as it was when it clipped — with the added confusion of nothing appearing
    /// to be selected.
    /// </para>
    /// <para>
    /// <b>Only when it is not already visible.</b> The Commander scrolling the nav by hand and the
    /// nav following the cards are the same scroller driven by two parties, and one that yanks
    /// itself back on every card that passes is worse than one that clips. So this asks first,
    /// and the ordinary case — scrolling within the entries already on screen — moves nothing.
    /// </para>
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
            // Not laid out yet, which is the case during Build. The first scroll settles it, and
            // the top of the list is already the right place to be looking.
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

        // Scrolled to whichever edge it went past, so the list moves the least it can. Landing a
        // half-scrolled entry against the edge it came from is what "the least it can" means.
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
    /// The two colours that say which section is being read: the item's fill, and the ink its
    /// name is written in.
    /// <para>
    /// Bound rather than fetched. <see cref="Res"/> resolves against the visual tree and this
    /// column is painted inside <see cref="Build"/> — which runs while the view is still being
    /// constructed, before the caller has handed it to the pane that hosts it — so every lookup
    /// came back null, and a null foreground is text that is laid out, counted and hit-tested
    /// and never drawn. The whole nav was blank until the first scroll repainted it against a
    /// tree that by then existed (bugs.md 3; AvatarView records the same trap).
    /// </para>
    /// <para>
    /// A binding also resolves the theme on its own, which is what retired the note that used to
    /// stand above <see cref="UpdateNavVisuals"/> asking <see cref="Refresh"/> to re-run it.
    /// </para>
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
            // No resource for "nothing", so the fill is dropped rather than bound. The pointer
            // handlers paint an inactive item on hover and this is the state they paint over.
            section.NavItem.Background = Brushes.Transparent;
        }
    }

    /// <summary>
    /// Shows one section, named by the capability that owns it — what a help card pressed on the
    /// Transcript page does (asked for 2026-08-23).
    /// <para>
    /// <b>Expanded before scrolled, and that order is the feature.</b> A card the Commander left
    /// collapsed is a card that scrolls to a heading with nothing under it, which reads exactly
    /// like a button that did not work — and the Commander pressed it precisely because they did
    /// not know where these rows were.
    /// </para>
    /// <para>
    /// An id this page has no section for does nothing rather than throwing. The ids come from
    /// shipped markup rather than from the registry, so a page naming a capability this build no
    /// longer registers is a stale link, and a stale link is worth a dead button rather than a
    /// crash on the settings page.
    /// </para>
    /// </summary>
    public void Reveal(string capabilityId)
    {
        var index = _sections.FindIndex(
            section => string.Equals(section.CapabilityId, capabilityId, StringComparison.Ordinal));

        if (index < 0)
        {
            return;
        }

        // A jump unfolds (#60). Help says "change X here" and points at a card; landing on one
        // whose rows are folded away is a link that goes nowhere, which is the silent-link fault
        // class the help pass already paid for three times. Session-only: following a link is not
        // the Commander asking for a different settings page for ever.
        if (!_revealedByJump)
        {
            _revealedByJump = true;
            Refresh();
        }

        _sections[index].Expand?.Invoke(true);

        // After the layout the expansion caused, not before it: CardTop reads the card's position
        // in the scroller's content, and the cards below one that just opened have not moved yet.
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

    /// <summary>
    /// The card's position in the scroller's <em>content</em>, which is not where it is on
    /// screen.
    /// <para>
    /// <b>This read <c>card.Bounds.Y + Cards.Bounds.Y</c>, and that counted the scroll twice.</b>
    /// A <see cref="ScrollViewer"/> scrolls by arranging its content at a negative offset, so
    /// <c>Cards.Bounds.Y</c> is the card column's margin <em>minus</em> however far the page has
    /// been scrolled — 20 at the top, and 20 minus 4,931 further down. Feeding that into a
    /// comparison against the offset made the test "has this card's head passed the top edge"
    /// come out as "is this card's top less than twice the offset", so the highlight ran ahead
    /// of the page and further ahead the further down it went: at the fourth section it named
    /// the seventh, and past the sixth it sat on the last one for the rest of the page.
    /// </para>
    /// <para>
    /// The margin is the answer instead. It is this class's own margin on its own column, it
    /// does not move, and content space is what both callers want — the spy compares it against
    /// the offset, and a nav click assigns it to the offset.
    /// </para>
    /// </summary>
    private double CardTop(Border card) => card.Bounds.Y + Cards.Margin.Top;

    /// <summary>
    /// Highlights the section the panel is actually showing — the topmost card still in view —
    /// rather than the last one clicked (Phase 4, "Settings Nav Menu").
    /// </summary>
    /// <summary>
    /// The card column tracks the viewport, between a floor and a ceiling.
    /// <para>
    /// Set in code because neither bound alone does it. MaxWidth on its own let zoom squeeze a
    /// row to 197 pixels — at 175% in a 900-pixel window there is only that much to lay out in —
    /// and at that width a row cannot hold its caption beside its control, so the caption
    /// collapsed and the control drew over it. MinWidth on its own, with scrolling enabled, made
    /// the scroll viewer measure with infinite width, so the cards took their maximum at every
    /// window size and scrolled sideways when they never needed to.
    /// </para>
    /// <para>
    /// Below the floor the panel scrolls. A scrollbar is a worse answer than fitting and a much
    /// better one than two controls in the same place.
    /// </para>
    /// </summary>
    private void OnScrollerSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        const double Floor = 420;
        const double Ceiling = 700;

        // The margin the cards are laid out with, both sides.
        var available = e.NewSize.Width - 56;

        Cards.Width = Math.Clamp(available, Floor, Ceiling);

        // And the pair of bulk controls above them takes the same width (#223), so it sits over
        // the cards rather than out at the column's right edge. The cards are left-aligned inside
        // a column that is usually wider than they are, so a right-aligned strip in the column
        // floats away from the thing it acts on — which is exactly how a control reads as
        // belonging to something else.
        BulkExpand.Width = Cards.Width;
    }

    /// <summary>The nav column's width, and the point below which it is not worth its space.</summary>
    private const double NavWidth = 224;

    private const double NavCollapsesBelow = 900;

    /// <summary>
    /// The floor with the nav and without it. The narrow one is the card floor of 420 plus the
    /// 56 of margin the cards are laid out with — the least this page can be and still hold a
    /// caption beside its control.
    /// </summary>
    private const double WideFloor = 700;

    private const double NarrowFloor = 476;

    /// <summary>Null until the first arrange, so the first pass always applies.</summary>
    private bool? _navShown;

    /// <summary>
    /// Collapses the nav column on a narrow page, and brings it back on a wide one.
    /// <para>
    /// This is what let the settings window be retired rather than merely relocated. The surface
    /// was a 224-pixel nav beside a 700-pixel minimum, opening at 1180; the panel window is 820
    /// and is meant to sit beside a running game. Ported unchanged, the second window would not
    /// have been removed so much as the first one made too big to keep on screen — so below 900
    /// the nav goes and the cards take the whole width, which at the default size is the state
    /// the Commander actually sees (Phase 12).
    /// </para>
    /// <para>
    /// Guarded on the state changing rather than run every pass, because the handler sets
    /// <c>MinWidth</c> and a minimum that feeds its own size-changed event is a layout loop.
    /// </para>
    /// </summary>
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

        // At the very bottom, the last card is the answer even when it is too short to ever
        // become topmost — the classic scroll-spy edge.
        if (Scroller.Offset.Y >= Scroller.Extent.Height - Scroller.Viewport.Height - 2)
        {
            SetActiveSection(_sections.Count - 1);
            return;
        }

        var offset = Scroller.Offset.Y;
        var topmost = 0;

        for (var i = 0; i < _sections.Count; i++)
        {
            // Topmost once its head has passed the top edge, with a little tolerance so a card
            // sitting exactly at the edge does not flicker between two answers.
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

    /// <summary>
    /// Open every card, or shut every card
    /// (<a href="https://github.com/dseelinger/d47/issues/223">#223</a>).
    /// <para>
    /// <b>A loop over the action each card already has.</b> <c>Expand</c> is stored on the section
    /// and is already called programmatically by the help-jump path, which has to unfold the card
    /// it lands on — so there is no new state here, and the chevron, the remembered set and the
    /// view state all move because they move for a header press.
    /// </para>
    /// <para>
    /// <b>These move cards and leave the fold alone</b>, which is the one thing to get right. The
    /// fold is a different axis: <see cref="SettingsFold"/> decides which *rows* a calm page shows
    /// at all, it is a persisted preference the Commander set, and its own rule is that folding is
    /// a pure display decision. A chrome button that flipped a setting as a side effect would be a
    /// different kind of act from opening a card, and the two are separately meaningful — every
    /// card open and still the calm row set is a reasonable thing to want.
    /// </para>
    /// <para>
    /// The cost of that separation, stated rather than discovered: pressing this with the fold on
    /// opens every card and still does not show every row. The answer to that reading badly is to
    /// make the fold's own control easier to find, never to have one button drive two axes.
    /// </para>
    /// <para>
    /// <b>It persists, exactly as clicking each header by hand does</b>, because it is a thing the
    /// Commander pressed on purpose. That does bury <c>Display.StartCollapsed</c> — a card whose
    /// state has been written never falls back to its default again — so the page's own reset
    /// clears the remembered states, which is what lets that default come back.
    /// </para>
    /// </summary>
    private void SetEveryCard(bool expanded)
    {
        foreach (var section in _sections)
        {
            section.Expand?.Invoke(expanded);
        }
    }

    /// <summary>
    /// Takes a control out of whatever is holding it, so it can be put somewhere else.
    /// <para>
    /// The bulk glyphs are declared in the axaml, docked above the scroller, and are moved into
    /// the page strip on every rebuild — a control belongs to one parent, and adding it to a
    /// second without this throws rather than moving it.
    /// </para>
    /// </summary>
    private static void Detach(Control control)
    {
        if (control.Parent is Avalonia.Controls.Panel panel)
        {
            panel.Children.Remove(control);
        }
    }

    private void OnExpandAllClick(object? sender, RoutedEventArgs e) => SetEveryCard(true);

    private void OnCollapseAllClick(object? sender, RoutedEventArgs e) => SetEveryCard(false);

    private void RememberCollapse(string capabilityId, bool expanded)
    {
        _viewState = _viewState.With(capabilityId, expanded);
        _viewStateStore?.Save(_viewState);
    }

    /// <summary>
    /// Re-reads every row from settings. Cheaper than rebuilding and, more importantly, it does
    /// not pull the control out from under whatever has focus.
    /// <para>
    /// Public because a row can go stale without any setting having changed: the disclosure
    /// naming what was found in <c>data/audio/</c> is read from the cue library, and the library
    /// is rebuilt when the Commander drops a file in (Phase 12). Every other caller
    /// arrives through <see cref="SettingsService.Changed"/>, which is why this is the only one
    /// that has to ask.
    /// </para>
    /// </summary>
    public void Refresh()
    {
        if (_settings is null)
        {
            return;
        }

        var showing = new int[_sections.Count];

        // Which sections the query names. Worked out before the rows because a section that
        // matches keeps all of them (change-requests.md 15).
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
                // A row that does not apply is absent, not disabled: a greyed-out control still
                // asserts that the setting exists (Phase 4).
                // DrawnElsewhere is checked here rather than left to the fold, because it is
                // not a fold: no "show every setting" reveals it. Another row's control holds it
                // (#217), and drawing it as well would offer one binding twice.
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

                // Only the survivors. Painting a query into a row nobody can see is work done
                // for a hidden control, and it would have to be undone before the row came back.
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

        // The section's own name, marked in both places it is written. Painted after the rows so
        // it happens on the same pass, and unconditionally so clearing the query takes the mark
        // off again — Paint with nothing to find puts the plain string back.
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
    /// Shows only the rows that match, and marks what the query found in each of them
    /// (Phase 12, "Search whichever tab you are looking at").
    /// <para>
    /// Settings filters where the transcript pages only highlight, and the difference is the
    /// design rather than an inconsistency: 92 rows across 14 sections is a haystack, and
    /// highlighting in place in a haystack is a scroll hunt with extra colour. That is an
    /// argument against highlighting <em>instead of</em> filtering, and not against doing both —
    /// once the filter has cut the haystack down, marking the hits is what tells the Commander
    /// which words survived on their behalf. See <see cref="Illuminate"/>.
    /// </para>
    /// <para>
    /// <b>A section name is a match too</b> (change-requests.md 15). Typing "Speech" used to find
    /// rows and not the card called Speech, so a search for a section's own name looked like it
    /// had found nothing at the top of the thing it was looking for. A named section keeps every
    /// row it has rather than only the ones that happen to repeat the word, because "Speech" is a
    /// Commander asking to be taken there — and its name is marked in the card and in the nav, so
    /// it is visible why the whole card survived.
    /// </para>
    /// </summary>
    /// <summary>Ninety-odd rows across fourteen sections. Always.</summary>
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

    /// <summary>
    /// Label, help or key. The key is in there because it is what the documentation, the voice
    /// router and a hand-edited settings file all call the row, so a Commander who arrived with
    /// one of those in hand can paste it in.
    /// </summary>
    /// <summary>
    /// Marks why a row survived the filter. The filter cuts the haystack down; this says which
    /// words in each survivor the query found — the two halves of one answer rather than two
    /// competing designs.
    /// <para>
    /// The key gets a line of its own only when it is the sole reason. When the label or the help
    /// already carries a highlight the row has explained itself, and adding the key underneath
    /// every one of them turns a filtered page into a page of identifiers.
    /// </para>
    /// </summary>
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

        // **The help is behind a glyph, so a query that only it answers has to bring it out.**
        // Matches() has always tested the help text, and since the callout it is no longer on
        // screen — so a row could stay behind a filter with every visible word on it disagreeing
        // with the query, which reads as the filter being broken rather than as a match the
        // Commander cannot see. That is the same rule, and the same reason, as the key line below.
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
    /// One block of caption text with the hits in it marked, or the plain string when there is
    /// no query. The same accent the transcript pages mark a hit with, because it is the same
    /// question being answered on a different surface.
    /// <para>
    /// Inlines are cleared first either way. A block left holding runs from the previous query
    /// renders those instead of its <see cref="TextBlock.Text"/>, so the row would keep a
    /// highlight for a string nobody is searching for any more.
    /// </para>
    /// </summary>
    private void Paint(TextBlock block, string markup)
    {
        // The sentence without its markup: what is read out, what is searched, and what is drawn
        // where there is no link to draw. Every offset below is into this rather than into the
        // written string, or a hit would be marked at the wrong place in any caption with a link
        // ahead of it (#65).
        var segments = D47.Core.Interface.HelpLinks.Parse(markup);
        var text = D47.Core.Interface.HelpLinks.Plain(markup);

        // A block composed of runs reports no Text of its own, and Text is what an automation
        // peer reads — so the name is set outright rather than left to be inferred. Without it,
        // marking a hit would quietly cost a screen reader the whole caption. It is the plain
        // sentence: a link's own accessible name must not replace the one the block carries.
        AutomationProperties.SetName(block, text);

        // Qualified: Avalonia.Controls has a TextSearch of its own, about typing to select an
        // item in a list, and it is the one that wins in this file's usings.
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

        // Text and Inlines both draw, one after the other. Filling the runs without dropping the
        // string leaves every filtered caption rendered twice — once plain, once marked — which
        // is what "MicrophoneMicrophone" on the filtered page turned out to be. Text goes first,
        // because setting it is itself a way of putting a run back.
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

    /// <summary>
    /// The same caption when some of it is a cross-reference (#65).
    /// <para>
    /// <b>The two markings have to compose rather than take turns.</b> A link inside a highlighted
    /// match and a match inside a link are both ordinary — searching for "priv" marks part of the
    /// word <em>Privacy</em>, which is also the link — so the runs are cut at <em>both</em> sets of
    /// boundaries and each piece carries whichever of the two it falls inside. Painting one and
    /// then the other would have the second erase the first.
    /// </para>
    /// <para>
    /// The jump itself is <see cref="ScrollTo"/>, which is the whole gesture the nav column already
    /// makes: select the section, expand the card, then scroll. A link needed no navigation, only
    /// a caller.
    /// </para>
    /// </summary>
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

            // Every boundary inside this stretch: where it starts, where it ends, and every edge of
            // every hit that falls in it. Sorted and de-duplicated, so a hit that exactly covers the
            // link produces one piece rather than three empty ones.
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

        // The click is on the block rather than per-run: a Run is not an input element in Avalonia,
        // so it has no pointer events of its own. Hit-testing the pointer against the block's
        // inlines is what turns "somewhere in this caption" into "on the link".
        var targets = segments.Where(segment => segment.Target is not null).ToList();

        if (targets.Count == 0)
        {
            return;
        }

        block.Cursor = new Cursor(StandardCursorType.Hand);

        // One handler per painted block, and Paint runs again on every filter keystroke - so the
        // old one is dropped rather than stacked, or a caption painted twenty times would jump
        // twenty times on one click.
        if (_linkHandlers.TryGetValue(block, out var previous))
        {
            block.PointerPressed -= previous;
        }

        EventHandler<PointerPressedEventArgs> handler = (_, e) =>
        {
            // Whichever section the first link on this caption names. Every one of the four in the
            // repository points at Privacy, and a caption with two different targets would want the
            // pointer tested against each run's bounds - deliberately not built until there is one.
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
    /// The click handler each linked caption currently carries, so repainting replaces it instead
    /// of adding a second one. Keyed weakly on the block itself, which is rebuilt with the card.
    /// </summary>
    private readonly Dictionary<TextBlock, EventHandler<PointerPressedEventArgs>> _linkHandlers = [];

    // Against the plain sentence rather than the written one (#65): searching the markup would let
    // a query match a capability id inside (privacy) and show a row whose visible text does not
    // contain the query anywhere.
    private bool Matches(SettingRow row) =>
        _query.Length == 0
        || row.Label.Contains(_query, StringComparison.OrdinalIgnoreCase)
        || D47.Core.Interface.HelpLinks.Plain(row.Help).Contains(_query, StringComparison.OrdinalIgnoreCase)
        || row.Warning?.Contains(_query, StringComparison.OrdinalIgnoreCase) == true
        || row.Key.Contains(_query, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// A card with nothing left in it goes, and so does its nav item — a sidebar still listing
    /// fourteen sections when three of them hold anything is a sidebar that has stopped telling
    /// the truth.
    /// <para>
    /// A filtered card is also opened, whatever the Commander last left it as, because a card
    /// collapsed over the row that matched is a filter that hides its own answer. The remembered
    /// state is not written while this happens, so clearing the query puts it back.
    /// </para>
    /// </summary>
    private void ApplyFilterToCards(int[] showing, bool[] named)
    {
        var filtering = _query.Length > 0;

        for (var i = 0; i < _sections.Count; i++)
        {
            var section = _sections[i];

            // Named counts even with nothing under it. A section whose every row is inapplicable
            // right now would otherwise answer a search for its own name by vanishing.
            var holds = showing[i] > 0 || named[i];

            // A card the fold has emptied is absent rather than an empty box (#60), which does
            // more for the anxiety than folding rows does — it takes Diagnostics, and VR with no
            // headset, off the page entirely. AppliesWhen's own doc already argues this shape:
            // "a row that does not apply is absent rather than disabled — a greyed-out control
            // still asserts the setting exists."
            var anyRows = showing[i] > 0;

            section.Card.IsVisible = (!filtering || holds) && anyRows;
            section.NavItem.IsVisible = (!filtering || holds) && anyRows;

            section.Content.IsVisible = filtering ? holds : !_collapsed.Contains(i);
        }
    }

    /// <summary>
    /// Which cards the Commander had shut when a filter opened them, so clearing it shuts them
    /// again. Held here rather than re-read from the view state because a card collapsed in this
    /// session and not yet written is still a card they collapsed.
    /// </summary>
    private readonly HashSet<int> _collapsed = [];

    /// <summary>
    /// The width a compact row's control is built to. Used as the floor for the control column
    /// so a narrow panel shrinks the caption rather than clipping the control itself.
    /// </summary>
    private const double StandardControlWidth = 190;

    /// <summary>
    /// Marks a caption-and-control row, so a test can find the rows this view builds rather than
    /// every three-column grid that happens to be in the tree. Public because the test asserting
    /// the column split is the only other thing that needs the name, and two spellings of it
    /// would fail by finding nothing rather than by failing.
    /// </summary>
    public const string CompactRowClass = "compact-row";

    /// <summary>
    /// The height every control that opens a list stands at, and the padding inside it.
    /// <para>
    /// Two numbers rather than each control's own, because the two implementations drifted the
    /// moment they had separate ones: a combo box at 32 beside a picker button at 33, one padded
    /// 12,5,0,7 and the other 11,6,11,6.
    /// </para>
    /// </summary>
    private const double ChoiceHeight = 32;

    private static readonly Thickness ChoicePadding = new(11, 6);

    /// <summary>
    /// One look for the two controls that open a list.
    /// <para>
    /// The picker button was dressed by hand to resemble the combo box beside it — "the default
    /// button chrome reads as disabled next to a real combo, which is the opposite of the truth"
    /// — and a resemblance maintained by hand is a resemblance that drifts. It had: the combo
    /// carried Fluent's own fill and a 60%-white border while the button carried d47's surface
    /// and a border two thirds darker, which on screen is a bright control beside a dim one.
    /// </para>
    /// <para>
    /// Both are dressed from d47's palette here, so the theme moves them together. The one thing
    /// this does not reach is the glyph: the combo's chevron is drawn by its template and the
    /// button's is a character, and they are the same size and colour without being the same
    /// shape. Nor does it reach the combo's pointer-over fill, which Fluent swaps from its own
    /// resources — worth a look on the captures before deciding whether that needs answering too.
    /// </para>
    /// </summary>
    /// <summary>
    /// Whether anything on this card has been changed from its default, which is what decides
    /// whether the card offers a way back (#61).
    /// <para>
    /// Rows that do not currently apply are not counted. A Commander looking at a Speech card
    /// configured for Edge is not offered a reset because of an ElevenLabs rate they set months
    /// ago on a row that is not on screen — and if they switch back, the offer returns with the
    /// row.
    /// </para>
    /// </summary>
    /// <summary>
    /// Whether the folded rows are on screen — because the Commander asked, or because a jump
    /// revealed them for this session (#60).
    /// </summary>
    private bool ShowingEverything =>
        _revealedByJump || (_settings?.Current.Ui.ShowEverySetting ?? true);

    private bool CardHasChanges(SettingsSection section) =>
        _settings is { } settings
        && section.Rows.Any(row => row.Applies(settings.Current) && settings.IsChanged(row.Key));

    private void DressAsAChoice(TemplatedControl control)
    {
        // Fixed rather than a floor. Both are one line of text and a glyph, and a floor let the
        // button stand a pixel taller than the combo because the chevron character's line box is
        // two pixels deeper than the label's — invisible on its own and obvious side by side.
        control.Height = ChoiceHeight;
        control.Padding = ChoicePadding;
        control.BorderThickness = new Thickness(1);
        control.CornerRadius = new CornerRadius(3);
        control.FontSize = TypeScale.Body;

        Themed(control, TemplatedControl.BackgroundProperty, ThemeManager.SurfaceAltKey);
        Themed(control, TemplatedControl.BorderBrushProperty, ThemeManager.BorderKey);
    }

    /// <summary>
    /// Fetches a speech model, reporting progress. Supplied by the window that has a host
    /// behind it; null under the designer and in a test that is not about downloading.
    /// </summary>
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
            // Said on the row rather than only in the docs: a Commander who asks d47 to change
            // this and gets refused should already know why.
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
            // The same pill for the other declaration a row can make (Phase 44): this
            // value is the Commander's who is flying, and a second Commander on this machine
            // will see their own here rather than this one.
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
            // The badge half of a row's Warning (#237): the same pill the declarations above
            // use, in the danger colour, so a hazard reads as one at a glance rather than as
            // another property tag.
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

        // **The help is behind a glyph now** (asked for 2026-09-01 — *"That is WAY too much
        // text"*). Push-to-talk's runs to eleven lines, and eleven lines of grey prose under every
        // row is a page nobody scans: the setting a Commander came for is buried in the
        // explanation of the setting above it. Not a word of it is cut; it is one press away.
        //
        // <b>This block still exists, and it is not the one in the callout.</b> A TextBlock has
        // one parent, and this one is the row's own — hidden until a search matches words only it
        // holds, which is the same evidence rule the key line already follows. A row that survived
        // a filter with nothing on it matching reads as the filter being broken.
        var help = new TextBlock
        {
            Text = row.Help,
            FontSize = TypeScale.Secondary,
            Margin = new Thickness(0, 2, 0, 0),
            TextWrapping = TextWrapping.Wrap,
            IsVisible = false,
        };
        Themed(help, TextBlock.ForegroundProperty, ThemeManager.TextMutedKey);

        // The sentence half (#237): under the help, in the danger colour, because the one thing
        // this line must not do is read as more background.
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
        // Matches() has always tested the key, and the key has never been on screen — so a row
        // could stay behind while every visible word on it disagreed with the query, which reads
        // as the filter being broken rather than as a match the Commander cannot see.
        var keyLine = new TextBlock
        {
            FontSize = TypeScale.Small,
            Margin = new Thickness(0, 2, 0, 0),
            TextWrapping = TextWrapping.Wrap,
            IsVisible = false,
        };
        Themed(keyLine, TextBlock.ForegroundProperty, ThemeManager.TextMutedKey);

        // A way back from this one row (https://github.com/dseelinger/d47/issues/61), on the
        // rows the Commander has actually changed and nowhere else. A glyph on all seventy-five
        // is noise; a glyph on the handful somebody has touched is useful, and doubles as a quiet
        // "you changed this" marker — which is knowable with no new state, because a set value is
        // already distinguishable from a default.
        //
        // Absent on a secret, where there is no default to go back to and forgetting a key is a
        // different and destructive act.
        // The callout's own copy of the words. Painted alongside the inline one so a query is
        // marked wherever the Commander is looking at them.
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
                // Named so anything looking for "the control this row is about" can tell this
                // apart from it. Two tests took the first Button in a row and got this instead,
                // which is the same hazard a Commander does not have and a search does.
                Name = RowResetName,

                // A stroked Path rather than U+21BA (#69). The character was whatever the installed
                // font carried - a different weight from the marks beside it, and a box on a machine
                // without it - and it could not be sized or coloured with them.
                // Accent, the same rule and the same reason as the card-level one above (#208).
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

            // The character used to be this button's accessible name by being its content; a Path
            // has no text, so a screen reader would have found an unnamed button where it used to
            // find one. Set outright rather than left to be inferred - the same fault, and the same
            // fix, as the run-composed caption at Paint.
            AutomationProperties.SetName(back, $"Reset {row.Label}");

            back.Click += (_, _) =>
            {
                // Every key the control holds, so resetting a merged row puts both halves back
                // rather than the one whose key happens to name the row (#217).
                foreach (var key in row.BoundKeys)
                {
                    _settings!.Reset(key, SettingsCaller.Panel);
                }

                Refresh();
            };

            header.Children.Add(back);

            // Folded into the row's refresh rather than set once, because whether this row has
            // been changed is exactly what a reset — or any other write — moves.
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
            // On the caption rather than on the label alone, so the help line under it answers
            // the hover too — the request was "the label or the description", and the two read
            // as one block.
            //
            // Folded into the row's own refresh, because this disclosure is a function of the
            // selected provider: a tip set once would go on describing Edge after ElevenLabs
            // was chosen, which is the exact staleness this row was rewritten to end.
            var describe = refresh;

            refresh = () =>
            {
                describe();
                ToolTip.SetTip(caption, hinted.Read(_settings!.Current));
            };

            ToolTip.SetShowDelay(caption, 250);

            // The pointer has to have something to be over. A StackPanel with no background is
            // transparent to hit-testing between its children, so the gaps in the caption would
            // swallow the hover and the tip would come and go as the pointer crossed them.
            caption.Background = Brushes.Transparent;
        }

        Control body;
        if (compact)
        {
            // Label and help on the left, the control on the right — the layout every settings
            // surface a Commander already knows uses for one-glance rows.
            //
            // The control column is a bounded share of the row rather than Auto. Auto asks the
            // control how wide it would like to be and gives it that, which is fine until a
            // choice label is a sentence: "Small (English only) - more accurate, slower - about
            // 466 MB to download" took 543 of 582 pixels and left the help text wrapping one
            // character per line. Three-fifths to the words, two-fifths to the control, and the
            // control right-aligned inside its share so short ones - a toggle, a stepper - sit
            // exactly where they did before.
            var grid = new Grid
            {
                ColumnDefinitions =
                [
                    new ColumnDefinition(3, GridUnitType.Star),
                    new ColumnDefinition(16, GridUnitType.Pixel),

                    // The floor is the width the controls are already built to; below it the
                    // caption yields instead, which is the lesser of the two bad narrow cases.
                    new ColumnDefinition(2, GridUnitType.Star) { MinWidth = StandardControlWidth },
                ],
            };

            // Load-bearing rather than decorative: RowWidthTests asserts the caption keeps the
            // larger share of every compact row, and it needs a way to say which grids those are.
            // Selecting on "three columns" instead caught control templates — a TextBox is itself
            // a three-column grid, inner-left content, text, inner-right content — and a glyph
            // put inside a box was read as a settings row starving its own caption.
            grid.Classes.Add(CompactRowClass);

            Grid.SetColumn(caption, 0);
            Grid.SetColumn(control, 2);
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
    /// The row's help, behind a lower-case <c>i</c> in a circle
    /// (asked for 2026-09-01 — <i>"use an info glyph … which goes away when clicked outside"</i>).
    /// <para>
    /// <b>A <see cref="Flyout"/> rather than a popup this class opens and closes.</b> Light
    /// dismissal is the whole of what was asked for — click anywhere else and it goes — and a
    /// flyout has it, along with Escape, placement that stays on screen, and a focus scope. Hand
    /// rolling those is how a callout ends up stuck open behind a scrolled card.
    /// </para>
    /// <para>
    /// <b>It carries the way out to the web page too.</b> The row already knew its anchor and
    /// nothing in the panel had ever offered it — <c>DocsAnchor</c> was read by the documentation
    /// gate and by no drawn control. So the short form is in the callout and the long form is one
    /// more press away, which is the split <see cref="DocsSite"/> already describes.
    /// </para>
    /// </summary>
    private Control Explains(CapabilityDescriptor capability, SettingRow row, TextBlock spoken)
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

        // The row's own anchor where it has one, the capability's page where it does not — a
        // row with no anchor still has somewhere to send the Commander, and it is better than
        // a link that is missing on the rows that most need explaining.
        page.Click += (_, _) => Process.Start(new ProcessStartInfo(
            DocsSite.Capability(capability.Id, row.DocsAnchor)) { UseShellExecute = true });

        var inside = new StackPanel
        {
            Spacing = 10,
            Children = { spoken, page },
        };

        var button = new Button
        {
            Name = RowInfoPrefix + row.Key.Replace('.', '_'),
            // The muted accent the pills beside it carry, not the bright one (asked for
            // 2026-09-01). This is a mark that says "there is more here if you want it", which is
            // a quieter thing than the reset glyph next to it — that one only appears on a row the
            // Commander has changed, and is worth noticing when it does.
            Content = Glyphs.Draw(Glyphs.Info, ThemeManager.AccentMutedKey, TypeScale.Secondary),

            // Room above and below for the stroke. Made stretches the geometry to the box and then
            // strokes it two units wide, so half of that lands outside the control — with no
            // vertical padding the header clipped it flat top and bottom.
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

        // A Path has no text, so a screen reader would find an unnamed button — the same fault,
        // and the same fix, as the reset glyph and the run-composed captions.
        AutomationProperties.SetName(button, $"About {row.Label}");
        ToolTip.SetTip(button, $"About {row.Label}");

        return button;
    }

    /// <summary>
    /// Says that a row is waiting on something that is not happening in this class — the gap
    /// reaction spends a model round trip between the Commander picking a core and that core
    /// saying its first word, and the affordance they touched is this row.
    /// <para>
    /// The glyph is made on the first call rather than built with every row: it animates
    /// whenever it is in the tree, visible or not, so ninety-odd of them waiting to be needed
    /// would be ninety-odd animations running for the life of the surface.
    /// </para>
    /// </summary>
    /// <summary>
    /// The control the Commander touches for a row, by key
    /// (<a href="https://github.com/dseelinger/d47/issues/37">#37</a>).
    /// <para>
    /// Internal and for tests only. A toggle that read its setting wrongly rendered off for ever
    /// while everything behind it worked, and the only place that fault was visible was on the
    /// drawn control — so a gate has to be able to reach one by name rather than by guessing at
    /// the visual tree.
    /// </para>
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
            // In the spacer column between the caption and the control, which is 16 wide and
            // holds nothing — so the glyph lands beside the control that was touched without
            // taking a pixel from either side of it.
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

        // Shut while it runs, like every other affordance that is working: picking a second core
        // before the first one has spoken queues a second round trip behind the first.
        if (view.Control is not null)
        {
            view.Control.IsEnabled = !busy;
        }
    }

    /// <summary>
    /// The control for a row, its refresh action, and whether it is compact enough to sit to
    /// the right of its own caption.
    /// </summary>
    private (Control Control, Action Refresh, bool Compact) BuildControl(SettingRow row, TextBlock message)
    {
        switch (row.Kind)
        {
            // The one row that offers a window instead of a value. Ninety-odd lines inline
            // would be ninety-odd lines to scroll past to reach the rest of Diagnostics.
            case SettingKind.Info when row.Key == DiagnosticsCapability.CoverageKey && _coverage is not null:
                return BuildCoverage(row);

            // The other row that offers a window instead of a value. A macro is a small
            // program, and a program does not fit in a settings row.
            case SettingKind.Info when row.Key == MacroCapability.ListKey && _macros is not null:
                return BuildMacros(row);

            // And the row that opens the persona editor. A core is a piece of writing rather than
            // a settings value, so it gets a window for the reason a macro does.
            case SettingKind.Info when row.Key == PersonaCapability.OwnKey && _ownPersonas is not null:
                return BuildOwnPersonas(row);

            // The third row that offers a window. A list of things to do is not a settings value
            // and never could be — and it is where accepting a proposal lives, which is an act
            // the model is not allowed to perform.
            case SettingKind.Info when row.Key == ChecklistCapability.SummaryKey && _checklists is not null:
                return BuildChecklists(row);

            // The fourth row that offers a window. A capture is not a settings value and never
            // could be — and assigning a switch is an act the model is not allowed to perform,
            // so it has to live somewhere the tool surface cannot reach.
            case SettingKind.Info when row.Key == SwitchCapability.ListKey && _switches is not null:
                return BuildSwitches(row);

            // The fifth row that offers a window. A note about a system is not a settings value
            // either — and writing one is the act that makes an entry the Commander's own word
            // rather than the model's, so it has to live where the tool surface cannot reach.
            case SettingKind.Info when row.Key == LoreCapability.BookKey && _lore is not null:
                return BuildLore(row);

            // The sixth row that offers a window. A fact about the Commander is not a settings
            // value, and typing one is the act that makes an entry their own word rather than
            // something d47 worked out — so, like the note above, it lives where the tool surface
            // cannot reach (Phase 31).
            case SettingKind.Info when row.Key == MemoryCapability.StoreKey && _memories is not null:
                return BuildMemories(row);

            // The seventh, and the only one whose button starts work rather than opening
            // something. Mining is seconds long and runs off this thread, so the row follows the
            // store rather than being refreshed by the press (Phase 32).

            // The eighth, and the only one behind which a button spends money. It opens a window
            // rather than acting, because item 4 of Phase 33 requires the figure to be seen before
            // the spend is agreed to and a settings row has nowhere to show one.
            case SettingKind.Info when row.Key == LogbookCapability.StoreKey && _logbook is not null:
                return BuildLogbook(row);

            // The ninth row that offers a window, and the only one that also clears what the
            // window shows. It is above the general pressable case rather than inside it because
            // reviewing comes first and deleting last: the common act is the one at the top, and
            // the one that cannot be undone is the one furthest from a stray click (#164).
            case SettingKind.Info when row.Key == PrivacyCapability.AudioRecordingKey && _recording is not null:
                return BuildAudioRecording(row);

            // The tenth, and the only one behind which nothing is written down by D47 at all until
            // the Commander presses something. Taking a proposal is the act that makes it their
            // own word, so like the note and the fact above it, it lives where the tool surface
            // cannot reach (#162).
            case SettingKind.Info when row.Key == DebriefCapability.DirectionsKey && _debrief is not null:
                return BuildDebrief(row);

            // An Info row that also clears the state it describes. Rendered from the row
            // rather than special-cased by key like the two above, because what is behind
            // this button is a method rather than a window the App has to own.
            case SettingKind.Info when row.Press is not null || row.PressAsync is not null:
                return BuildPressable(row, message);

            // A disclosure that is consulted rather than read. It has no control at all: the
            // value goes on the caption's tooltip, which BuildRow attaches once it has the
            // caption to attach it to.
            case SettingKind.Info when row.ValueAsHint:
                return (new Avalonia.Controls.Panel(), () => { }, true);

            case SettingKind.Info:
                return BuildInfo(row);

            case SettingKind.Toggle:
                return BuildToggle(row, message);

            case SettingKind.Choice when row.AllowsFreeText || row.IsOpenVocabulary:
                // Long or open vocabulary: the searchable picker, which stays usable when the
                // list is empty because the value can be typed (Phase 4).
                return BuildPickerButton(row, message);

            case SettingKind.Choice:
                return BuildComboBox(row, message);

            case SettingKind.Number:
                return BuildNumber(row, message);

            case SettingKind.Secret:
                return BuildSecret(row, message);

            // One control for both, which is the whole of #217: what a bind row asks is "press
            // the thing you want", and which mechanisms are armed to hear it follows from the row
            // rather than from a second builder.
            case SettingKind.Hotkey:
            case SettingKind.HotasButton:
                return BuildBind(row, message);

            default:
                return BuildText(row, message);
        }
    }

    /// <summary>
    /// The read-out an Info row shows. <b>A row with no binding has none</b> (#78): a button-only
    /// Info row — the changelog, set up keys — is made of its button, and reading a binding
    /// that is not there is how the About area killed the app at startup. The refresh is a no-op
    /// rather than a null read, and <see cref="BuildPressable"/> leaves the empty inset out
    /// altogether rather than drawing a blank panel above the button.
    /// </summary>
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

    /// <summary>
    /// A disclosure with the button that clears it. Refreshed on the press, so the row states
    /// the new answer rather than leaving the Commander to wonder whether anything happened.
    /// </summary>
    /// <summary>
    /// The lore summary, plus the way into the notes. Built like the checklist row above and for
    /// the same reason: what is behind the button is the Commander's own writing, and the panel
    /// is the only place it can be written.
    /// </summary>
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

            // The window writes the file; this is what puts the new count on the row without
            // waiting for something else to notice.
            refresh();
        };

        var stack = new StackPanel { Spacing = 8, Children = { inset, open } };

        return (stack, refresh, false);
    }

    /// <summary>
    /// The debrief summary, plus the way into the proposals. Built like the memory row above and
    /// for the same reason: what is behind the button becomes the Commander's own word by their
    /// pressing it, and the panel is the only place that can happen (#162).
    /// </summary>
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

            // The window writes the file; this is what puts the new count on the row without
            // waiting for something else to notice.
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

            // The window writes the file; this is what puts the new count on the row without
            // waiting for something else to notice.
            refresh();
        };

        var stack = new StackPanel { Spacing = 8, Children = { inset, open } };

        return (stack, refresh, false);
    }

    /// <summary>
    /// The Commander's log, behind a button (Phase 33).
    /// <para>
    /// A window rather than a row for the same reason habits get one, and one more: this is the
    /// only surface in d47 that spends real money on request, so it needs room for a quote, a
    /// window to cover, and a second button that stays dead until the first has answered.
    /// </para>
    /// </summary>
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

    /// <summary>
    /// What the audio recorder holds, the way into reviewing it, and the wipe
    /// (<a href="https://github.com/dseelinger/d47/issues/164">#164</a>).
    /// <para>
    /// Both buttons are built here rather than one of them coming from
    /// <see cref="BuildPressable"/>, because the order is the point: the summary, then the review
    /// that is done every recording, then the delete that is done once and cannot be taken back.
    /// </para>
    /// </summary>
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

            // Keeping a row changes what the summary says, and the window is where keeping
            // happens — so the row is re-read on the way out rather than left stating what was
            // true when it opened.
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

        // Along the bottom of the button rather than across the row, because it is the button's
        // work it is reporting. The panel below is left-aligned and sized by the button, so a
        // stretched bar inside it is exactly the button's width without anybody measuring it.
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

                // The whole surface rather than this row, because a press is not always about the
                // row it is on: binding a core to a ship changes what the row above says *and* what
                // the list below it says, and refreshing only the one pressed left the other one
                // stating the state before the press (Phase 35). A press is a rare, deliberate act,
                // so reading ninety rows once is not a cost anybody can perceive.
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

    /// <summary>Whether a long press is already running. One at a time, like the model download.</summary>
    private bool _pressing;

    /// <summary>
    /// A press that takes long enough to watch (#101).
    /// <para>
    /// <b>Everything here is what the first local-voice download did not do.</b> The button shuts
    /// while the work runs, so a Commander who saw nothing happen cannot start a second one over
    /// the first; the bar says how far it has got; and the surface is refreshed at the end, which
    /// is what turns <em>not downloaded</em> into <em>installed</em> without waiting for something
    /// else to redraw the page.
    /// </para>
    /// <para>
    /// The row is refreshed on the way out whatever happened, because a failure halfway through a
    /// download leaves a state worth reading as much as a success does.
    /// </para>
    /// </summary>
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

            // The whole surface, for the reason the plain press refreshes it: what a press changes
            // is not always the row it was on.
            Refresh();
        }
    }

    /// <summary>
    /// The coverage summary, plus the way into the whole list.
    /// <para>
    /// The button is built only when this process is recording, so on a normal run it is absent
    /// rather than present and doing nothing. The settings surface has no VR host to worry
    /// about — only the panel is rendered to the headset — so the dialog always has an owner.
    /// </para>
    /// </summary>
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

    /// <summary>
    /// The Commander's own cores, plus the way into the editor (remediation.md 11, item 9).
    /// </summary>
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

            // The editor writes the file; this is what puts the new summary on the row without
            // waiting for something else to notice.
            refresh();
        };

        return (new StackPanel { Spacing = 8, Children = { inset, open } }, refresh, false);
    }

    /// <summary>
    /// The macro summary, plus the way into the editor. Built the same way the coverage row is,
    /// and for the same reason: the thing behind the button does not belong inline.
    /// </summary>
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

            // The editor writes the file; this is what puts the new summary on the row without
            // waiting for something else to notice.
            refresh();
        };

        var stack = new StackPanel { Spacing = 8, Children = { inset, open } };

        return (stack, refresh, false);
    }

    /// <summary>
    /// The checklist summary, plus the way into the panel. Built like the macro row above and for
    /// the same reason: what is behind the button does not belong inline.
    /// </summary>
    private (Control, Action, bool) BuildChecklists(SettingRow row)
    {
        var (inset, refresh, _) = BuildInfo(row);

        // A tab of this panel since Phase 25, rather than a dialog over it: a Window cannot appear
        // in the headset at all, so the checklist was unreachable there for a Commander wearing
        // one.
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
            // Its own panel, found up the tree rather than handed in. This surface is built by
            // one method and instantiated twice - the window and the headset each get their own -
            // so a panel captured at build time would be the window's, and pressing this in the
            // headset would switch a tab on a screen nobody is looking at.
            if (this.GetSelfAndVisualAncestors().OfType<Panel.PanelView>().FirstOrDefault() is { } panel)
            {
                panel.Tab = D47.Core.Interface.PanelTab.Checklist;
            }

            // The tab writes the file; this is what puts the new summary on the row without
            // waiting for something else to notice.
            refresh();
        };

        var stack = new StackPanel { Spacing = 8, Children = { inset, open } };

        return (stack, refresh, false);
    }

    /// <summary>
    /// The switch summary, plus the way into the walk. Built like the macro row above and for the
    /// same reason: what is behind the button does not belong inline, and in this case it is a
    /// dialogue with a piece of hardware rather than a form.
    /// </summary>
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

            // The editor writes the file; this is what puts the new summary on the row without
            // waiting for something else to notice.
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

        // The closed box shows what fits in a fifth of the row, and some of these labels carry
        // the part that matters on the end of them: a speech model not on disk reads as
        // "Small (English only) - more accu", which is indistinguishable from one already
        // installed. The column cannot be widened enough to hold it without starving the
        // caption, so the whole label is on the tooltip.
        combo.SelectionChanged += (_, _) =>
            ToolTip.SetTip(combo, combo.SelectedItem as string);

        // Through ChoicesFor, not the bare list. A row may compute its choices — the model list
        // belongs to the selected provider's endpoint, and the persona list now includes the cores
        // the Commander wrote (remediation.md 11, item 9) — and reading the literal here left such
        // a row with an empty combo box that rendered as no combo box at all.
        var choices = row.ChoicesFor(_settings!.Current);

        // A clear item only where clearing means something. Provider and theme always hold a
        // value, so "(default: anthropic)" above "anthropic" would be the same answer twice.
        var clearable = row.IsClearable;

        var items = new List<string>();
        if (clearable)
        {
            items.Add(row.BareDefaultFor(_settings!.Current) is { } bare ? $"(default: {bare})" : "(default)");
        }

        // One describer for the whole list rather than one call per item: a row may label a
        // choice against the others beside it — the model rows mark the cheapest of what is
        // offered — and that is a property of the list, not of the line (#152).
        items.AddRange(choices.Select(row.DescriberFor(_settings!.Current)));
        combo.ItemsSource = items;

        var offset = clearable ? 1 : 0;

        // The rows that download something carry a progress bar, and only those. Built here
        // rather than in a generic slot because a bar every row could show is a bar every row
        // has to explain.
        //
        // Two of them now (#139): the speech model row, which has plumbing of its own that
        // predates the property, and any row declaring FetchChoiceAsync — the local voice build.
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

            // One handler with a branch rather than two handlers. Two raced: the first applied
            // the value and refreshed the controls, and the refresh guard then swallowed the
            // second - so the download never started and the row snapped back to none.
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

            // The column is bounded, so a label written as a sentence - the speech models state
            // their size and speed - is clipped at the closed control. The tip is where the rest
            // of it still lives without the row having to be as wide as its longest choice.
            ToolTip.SetTip(
                combo,
                combo.SelectedIndex >= 0 && combo.SelectedIndex < items.Count
                    ? items[combo.SelectedIndex]
                    : null);
        }, true);
    }

    /// <summary>
    /// Applies a speech model choice, downloading it first if it is not on disk.
    /// <para>
    /// The choice is the go-ahead: it states its size in the list it was made from, and the row
    /// shows what it is doing while it does it. There was a confirmation step, on the main
    /// window - which is the window this dialog is covering, so it was a question asked behind
    /// the thing that asked it.
    /// </para>
    /// <para>
    /// The setting is written only once the file is there, so a row can never name a model d47
    /// cannot load. A refusal or a failure puts it back to none and says why on the row.
    /// </para>
    /// </summary>
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

        // A row that carries its own fetch (#139). Ahead of the speech model's plumbing rather
        // than beside it, because the two are alternatives: a row has one thing to download.
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

        // Shut while it runs. A second choice mid-download would either be ignored - which
        // reads as a dead control - or start a second fetch over the first.
        combo.IsEnabled = false;

        bar.Value = 0;
        bar.IsVisible = true;

        Note(message, $"Fetching {model.Label} - about {model.ApproximateMegabytes} MB.");

        try
        {
            // A model already on disk comes straight back as AlreadyPresent, so there is no
            // need to ask the store separately whether this is a download at all.
            var progress = new Progress<ModelProgress>(report => bar.Value = report.Fraction);
            var result = await _downloadModel(model, progress);

            if (result.Outcome is ModelInstall.Installed or ModelInstall.AlreadyPresent)
            {
                // Written only now that the file is there, so the row can never name a model
                // d47 cannot load.
                Apply(row, chosen, message);

                // Nothing left to say. The row speaks only when something went wrong - a
                // change that worked is visible in the control that made it - and a line
                // describing a finished download is a line the Commander has to dismiss by
                // reading it.
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

    /// <summary>
    /// The same flow for a row that carries its own fetch
    /// (<a href="https://github.com/dseelinger/d47/issues/139">#139</a>).
    /// <para>
    /// Everything the speech model row above does, said once for any row rather than a second
    /// time for each: shut the control while it runs, draw the fraction, and write the setting
    /// <b>only</b> once the fetch says the choice can be applied. A failure refreshes the row back
    /// to what is really installed and says why on it — which for the local voice build means the
    /// Commander keeps the build they had, working, and can see that they did.
    /// </para>
    /// </summary>
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

                // Nothing left to say, for the reason the speech model row records: a change
                // that worked is visible in the control that made it.
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

    /// <summary>
    /// Says something on the row itself. <see cref="Apply"/> speaks only on failure, because a
    /// change that worked is visible in the control that made it - but a download is neither
    /// instant nor visible, so it has to narrate.
    /// </summary>
    private static void Note(TextBlock message, string text)
    {
        message.Text = text;
        message.IsVisible = true;
    }

    private (Control, Action, bool) BuildPickerButton(SettingRow row, TextBlock message)
    {
        // Trimmed, because the column is a fifth of a row and a voice name is whatever the
        // provider's account calls it — "Bill - Wise, Mature, Balanced — male, american" is one
        // of 473 real ones. Untrimmed it is not the text that overflows, it is the button: it
        // asks for the width of the whole string and gets it.
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

            // On the button, not on the panel inside it: the floor is what the control stands at
            // beside a combo box, and a floor set inside the padding made the narrowest picker
            // button 212 wide against the combo's 190.
            MinWidth = StandardControlWidth,
            HorizontalAlignment = HorizontalAlignment.Right,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Center,
        };

        DressAsAChoice(button);

        // Behind this is a window, which is a thing the headset's copy of this surface must not
        // open: a dialog on a desktop the Commander is not looking at is a dialog they cannot
        // answer. Marked rather than recognised, so the surface that honours it does not have to
        // know what a picker is (see OffscreenSurface.DesktopOnly).
        button.Classes.Add(Panel.OffscreenSurface.DesktopOnly);

        // Said rather than left to be guessed at. Gathering what goes in the picker can mean
        // asking the machine for its capture devices or a provider for its voices, and a button
        // that looks unchanged for a second reads as a button that did not take the click — so
        // it is shut, and something moves next to it, until the list is on screen.
        var busy = new BusyGlyph
        {
            IsVisible = false,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0),
        };
        Themed(busy, BusyGlyph.StrokeProperty, ThemeManager.AccentKey);

        button.Click += async (_, _) => await ChooseAsync(row, button, busy, message);

        // A DockPanel rather than a horizontal StackPanel, which is the other half of the same
        // bug: a StackPanel measures along its own direction with no limit at all, so the button
        // was told it could be as wide as it liked and believed it. The row's column had already
        // been settled at two fifths, and the button ran out past it and past the panel edge.
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

            // The button is one line in a column; a model id or a resolved device name is
            // routinely longer than it. Chosen or defaulted, the whole string is on the pointer.
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

            // The row's own range where it declares one, so a stepper never offers a click that
            // the store is only going to clamp away — an arrow that appears to do nothing reads
            // as a broken control rather than as a value already at its limit.
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

        // Applied on leaving the box rather than on every keystroke: a setting that persists per
        // character would write a file per character, and would reject half-typed URLs as it went.
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

            // The default is a placeholder, never a value, so "I have not chosen" stays
            // distinguishable from "I chose the default" (Phase 4).
            box.PlaceholderText = row.DefaultDisplayFor(_settings.Current) ?? string.Empty;
            ShowDefaultOnHover(box, row);
        }, false);
    }

    /// <summary>
    /// The key row, which is <see cref="SecretEditor"/> — the same control the first-run guide
    /// shows (Phase 16). Extracted rather than duplicated so the trim, the reveal, the
    /// write-only store and the real check cannot drift between the two surfaces.
    /// </summary>
    private (Control, Action, bool) BuildSecret(SettingRow row, TextBlock message)
    {
        // The editor reports its own failures inline, next to the box that caused them, so the
        // row's shared message line stays for everything else.
        message.IsVisible = false;

        var editor = new SecretEditor(row, _settings!);

        // A stored key changes what other rows can offer — the voice picker is the obvious one —
        // so the surface re-reads itself rather than waiting for the next open.
        editor.Changed += Refresh;

        return (editor, editor.Refresh, false);
    }

    /// <summary>
    /// The one bind control (<a href="https://github.com/dseelinger/d47/issues/217">#217</a>).
    /// <para>
    /// <b>Which listeners it arms comes from the row, not from a kind-specific builder.</b> A
    /// <see cref="SettingKind.Hotkey"/> row listens for a keystroke; one naming a
    /// <see cref="SettingKind.HotasButton"/> row in <see cref="SettingRow.AlsoBinds"/> also polls
    /// the controller, and takes whichever arrives first. So push-to-talk is one row that holds a
    /// key, a stick button, or both, and the other five bind rows are the same control with one
    /// listener armed.
    /// </para>
    /// <para>
    /// <b>Unbind clears every key the control holds</b>, which is what the word says. The
    /// alternative — a removable chip per bound gesture — is a second idiom on the page for a row
    /// nobody has both halves of by accident.
    /// </para>
    /// </summary>
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

            // A row that can only be filled from a controller is dead without one, and saying so
            // beats a button that does nothing. A row with a key half stays live either way.
            button.IsEnabled = row.Kind != SettingKind.HotasButton || _switches is not null;

            if (row.Kind == SettingKind.HotasButton && _switches is null)
            {
                button.Content = "No controllers";
            }
        }, true);
    }

    /// <summary>
    /// What a bind row is bound to, in the Commander's words — both halves when it holds two, in
    /// the order they are pressed for rather than the order they are stored in.
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

        // The work is "until the list is on screen", not "until the Commander has chosen": what
        // can take a moment is asking the machine for its capture devices or a provider for its
        // voices, and once the picker is up it is modal and speaks for itself.
        var listed = new TaskCompletionSource();

        var picking = PickerWindow.ShowAsync(
            owner,
            new PickerRequest
            {
                Prompt = row.Label,
                Help = row.Help,
                Choices = row.ChoicesFor(_settings.Current),

                // Read at open like the choices themselves: a label can depend on the provider
                // serving the list right now, and on the rest of the list beside it (#152).
                Describe = row.DescriberFor(_settings.Current),
                Current = _settings.Read(row.Key),
                DefaultDisplay = row.IsClearable ? row.BareDefaultFor(_settings.Current) : null,
                AllowsFreeText = row.AllowsFreeText,
                WhyEmpty = row.WhyNoChoicesFor(_settings.Current),

                // Read at open like the choices themselves, and for the same reason: which
                // properties the list has depends on the provider serving it right now (#146).
                Facet = row.Facet?.Invoke(_settings.Current),

                // Read at open rather than captured once, because both the price and the reason
                // it might be unavailable follow the selected provider.
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

        // A picker that throws on its way open never lists, and a glyph waiting for a list that
        // is not coming spins forever on a row nobody can use.
        _ = picking.ContinueWith(_ => listed.TrySetResult(), TaskScheduler.Default);

        // Hand-rolled here until Phase 12: shut, spinning, and two numbers nobody else shared.
        // The rule was always the general one, so it is stated in one place now.
        await Busy.While(button, busy, () => listed.Task);

        if (await picking is { } result)
        {
            Apply(row, result.Value, message);
        }

        button.Focus();
    }

    /// <summary>
    /// Binds by listening for a gesture, rather than by offering a list of key names. There is no
    /// way to type a key that does not exist, and no list to keep in step with a keyboard layout
    /// (Phase 4, "Hotkey Binding").
    /// <para>
    /// <b>Both listeners at once where the row has both</b>
    /// (<a href="https://github.com/dseelinger/d47/issues/217">#217</a>): a keystroke arriving at
    /// this control, and a controller walked for at 10 Hz. Whichever arrives first is what the
    /// Commander meant, and it is stored against the row that kind belongs to — which is why the
    /// two settings properties stay separate underneath one question.
    /// </para>
    /// <para>
    /// The stick half is <see cref="ButtonCapture"/>, unchanged and still the authority on what
    /// counts as a button: it ignores what was already held when the walk started, captures on
    /// <em>release</em> rather than on press, and declines a switch that stays where it is put with
    /// its own sentence. That sentence goes on the row's message line, which is where the modal
    /// bind window used to put it.
    /// </para>
    /// </summary>
    private async Task CaptureBindAsync(SettingRow row, Button button, TextBlock message)
    {
        if (TopLevel.GetTopLevel(this) is not { } top || _settings is null)
        {
            return;
        }

        var keys = row.Kind != SettingKind.HotasButton;

        // Whether a modifier pressed on its own is a binding here. It is on a polled row —
        // push-to-talk's own default is RightShift — and it is not on one claimed from the
        // whole system, which refuses a bare key outright.
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
        // Null the moment anything else arrives, because then it was a chord after all.
        Key? held = null;

        void OnKey(object? sender, KeyEventArgs e)
        {
            var modifier = e.Key is Key.LeftCtrl or Key.RightCtrl or Key.LeftShift or Key.RightShift
                or Key.LeftAlt or Key.RightAlt or Key.LWin or Key.RWin;

            if (modifier)
            {
                // On a polled row, told apart by which edge it arrives on rather than refused:
                // pressed, it is still someone assembling a chord; released with nothing else
                // pressed, it was the binding. Same idiom as the stick walk one method down,
                // which captures on release for the same reason — it is the edge that answers
                // the question rather than the one that raises it.
                //
                // Not on a system-wide row, where the service refuses a bare key anyway
                // (it would stop working in every other application, Elite included), so
                // binding one silently and having it rejected is worse than waiting.
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
            // Tunnelling: the gesture belongs to the binding, not to whatever control the click
            // left focused, so it has to be seen on the way down.
            //
            // And handled events too. This is the same top level that carries the push-to-talk
            // suppressor now that settings is a page of the main window rather than a window of
            // its own — the suppressor tunnels first and marks the key handled, so without this
            // the one rebind that could not be made is rebinding push-to-talk to the key it
            // already holds, which is exactly the rebind somebody attempting it is most likely
            // to try.
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
    /// The controller half of a capture: the same 10 Hz walk the modal bind window ran, on a timer
    /// this control owns. Faster would be answering a question the runtime cannot ask — the tick
    /// loop samples push-to-talk at exactly this rate.
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

            // Nothing is read until the device list stops changing: a single enumeration at
            // startup reported three of six devices on the bench (Phase 21, finding 1).
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
                // The decline is the answer, and it stays on the line. Cancelling the whole
                // capture would take the sentence away with it before it had been read.
                timer.Stop();
            }
        };

        timer.Start();

        return timer;
    }

    private bool Apply(SettingRow row, string? value, TextBlock message) =>
        Apply(row.Key, value, message);

    /// <summary>
    /// By key rather than by row, because one control can hold two of them (#217) and the message
    /// line, the redraw and the refusal are the same for both halves.
    /// </summary>
    private bool Apply(string key, string? value, TextBlock message)
    {
        if (_settings is null)
        {
            return false;
        }

        var result = _settings.Apply(key, value, SettingsCaller.Panel);

        // Only failures are worth *saying*. A message on every success would make the panel a log.
        message.IsVisible = !result.Ok;
        message.Text = result.Message;

        // **But every outcome is worth redrawing**
        // (<a href="https://github.com/dseelinger/d47/issues/90">#90</a>). This used to refresh on
        // failure alone, reasoning that "a change that worked is visible in the control that made
        // it" — true of a toggle or a text box, which hold what was typed into them, and false of
        // a picker button, whose caption is *derived*: it is the voice's name looked up from the
        // provider's catalogue, and nothing about clicking "use this" puts that name on the button.
        //
        // It was covered by the settings subscription, until the one detach that silently ended
        // that subscription. Both halves are fixed; this is the half that does not depend on a
        // subscription existing at all, which is why it is worth having even though the other is
        // now symmetric.
        Refresh();

        return result.Ok;
    }

    /// <summary>
    /// How this surface shows a capability's help, or null where nothing wired one.
    /// <para>
    /// Handed in rather than reached for, like every other way out of this page: the settings
    /// surface is a page of the panel and does not hold the panel, so it says what it wants and
    /// the host says how.
    /// </para>
    /// </summary>
    private Action<string>? _openHelp;

    /// <summary>
    /// Gives the card marks somewhere to go that is not a browser (asked for 2026-08-23).
    /// </summary>
    public void EnableHelp(Action<string> open) => _openHelp = open;

    /// <summary>
    /// A card's question mark. <b>In the panel, with a breadcrumb back</b> — it used to launch a
    /// browser, which took the Commander away from the row they were reading and did nothing at
    /// all on a surface with no browser to take them to.
    /// <para>
    /// The site is still the long form, and the drawn page ends with a card that says so. Falling
    /// out to it here is for a host that wired nothing, which is no host that ships.
    /// </para>
    /// </summary>
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
        /// Opens or closes this card, chevron and remembered state together — the header press
        /// and <see cref="SettingsView.Reveal"/> both go through it, so neither can leave the
        /// two disagreeing.
        /// </summary>
        public Action<bool>? Expand { get; init; }

        /// <summary>
        /// How the nav item is currently painted, or null before it has been painted at all —
        /// which is what makes the first pass apply and the rest of them cost nothing.
        /// </summary>
        public bool? PaintedActive { get; set; }

        /// <summary>
        /// The nav item's live brush subscriptions, held so the next state can drop them.
        /// Binding a property that is already bound leaves both bindings live, and this is
        /// repainted every time the Commander scrolls past a heading.
        /// </summary>
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

        /// <summary>Made the first time this row has something slow to say. See ShowBusy.</summary>
        public BusyGlyph? Busy { get; set; }

        /// <summary>The row's words, held so a query can be painted into them and taken out again.</summary>
        public TextBlock? Label { get; init; }

        /// <summary>
        /// The inline copy, drawn only when a search matched words only it holds. See Evidence.
        /// </summary>
        public TextBlock? Help { get; init; }

        /// <summary>The callout's copy — what a Commander reads when they press the glyph.</summary>
        public TextBlock? Spoken { get; init; }

        /// <summary>The settings key, drawn only when it is why this row survived. See Evidence.</summary>
        public TextBlock? KeyLine { get; init; }
    }
}
