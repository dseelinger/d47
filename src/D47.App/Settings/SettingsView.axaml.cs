using System.Diagnostics;
using D47.Core.Listening;
using Avalonia;
using Avalonia.Controls;
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
/// lists it was built for — models now, voices and devices in later phases (list.md Phase 4).
/// Free text is only used where the value genuinely is free text.
/// </para>
/// </summary>
public partial class SettingsView : UserControl, D47.App.Panel.IFilterablePage
{

    private readonly List<SectionView> _sections = [];
    private readonly List<RowView> _rows = [];

    private SettingsService? _settings;
    private ViewStateStore? _viewStateStore;
    private ViewState _viewState = new();
    private AppPaths? _paths;

    /// <summary>
    /// Reopens the guided key setup from About (list.md Phase 16). Null in the designer and
    /// in tests, which hides the button rather than offering one that does nothing.
    /// </summary>
    private Func<Task>? _setUpKeys;

    /// <summary>
    /// Where the hand-testing coverage record stands, when this process was asked to keep one.
    /// Null on every normal run, which is what keeps the row's button absent rather than dead.
    /// </summary>
    private Func<CoverageReport>? _coverage;

    private D47.Core.Actions.MacroStore? _macros;

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
        Func<Task>? setUpKeys = null)
    {
        _setUpKeys = setUpKeys;
        _downloadModel = downloadModel;
        _settings = settings;
        _viewStateStore = viewState;
        _viewState = viewState.Load();
        _paths = paths;
        _coverage = coverage;
        _macros = macros;
        _checklists = checklists;
        _switches = switches;
        _reserved = reservedPhrases ?? [];

        StorageLine.Text =
            $"Saved as you go, to {paths.SettingsFile}. Keys are encrypted separately in secrets.json, "
            + "and how this panel is left is remembered in view-state.json.";

        Build();

        settings.Changed += OnSettingsChanged;
        DetachedFromVisualTree += (_, _) => settings.Changed -= OnSettingsChanged;
    }

    private void OnSettingsChanged(SettingsChanged change) => Dispatcher.UIThread.Post(Refresh);

    /// <summary>A brush fetched at call time, so state changes pick up the current theme.</summary>
    private IBrush? Res(string key) => this.FindResource(key) as IBrush;

    /// <summary>
    /// Binds a brush property to a theme resource, so a theme switch repaints controls built
    /// in code the same way DynamicResource repaints the ones built in markup.
    /// </summary>
    private void Themed(AvaloniaObject target, AvaloniaProperty property, string key) =>
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

        foreach (var section in settings.Sections)
        {
            var title = section.Capability.Display.PanelTitle ?? section.Capability.Name;
            var (card, content) = BuildCard(section, title, _sections.Count);

            Cards.Children.Add(card);

            var nav = BuildNavItem(_sections.Count, title);
            NavItems.Children.Add(nav.Item);

            _sections.Add(
                new SectionView(section.Capability.Id, title, card, content, nav.Item, nav.Bar, nav.Text));
        }

        SetActiveSection(_sections.Count > 0 ? 0 : -1);
        Refresh();
    }

    private (Border Card, StackPanel Content) BuildCard(SettingsSection section, string title, int index)
    {
        var content = new StackPanel
        {
            Spacing = 18,
            Margin = new Thickness(18, 4, 18, 18),
            // Applied while building, not after painting: a card that flashes open and then
            // collapses is worse than one that never remembered (list.md Phase 4).
            IsVisible = _viewState.IsExpanded(section.Capability.Id, section.Capability.Display.StartCollapsed),
        };

        if (!content.IsVisible)
        {
            _collapsed.Add(index);
        }

        string? currentGroup = null;

        foreach (var row in section.Rows)
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

        var header = new Border
        {
            Padding = new Thickness(14, 11),
            Background = Brushes.Transparent,
            Cursor = new Cursor(StandardCursorType.Hand),
            Child = headerRow,
        };

        header.PointerPressed += (_, _) =>
        {
            content.IsVisible = !content.IsVisible;
            chevron.Text = content.IsVisible ? "▾" : "▸";

            // Recorded here as well as on disk, because a filter opens a card without being
            // asked and has to put it back the way the Commander left it.
            if (content.IsVisible)
            {
                _collapsed.Remove(index);
            }
            else
            {
                _collapsed.Add(index);
            }

            RememberCollapse(section.Capability.Id, expanded: content.IsVisible);
        };

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

        return (card, content);
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
    }

    /// <summary>
    /// Also re-run by <see cref="Refresh"/>: these brushes are fetched, not bound, so a theme
    /// switch has to repaint them or the active item keeps the old theme's colours.
    /// </summary>
    private void UpdateNavVisuals()
    {
        for (var i = 0; i < _sections.Count; i++)
        {
            var section = _sections[i];
            var active = i == _activeSection;

            section.NavBar.Opacity = active ? 1 : 0;
            section.NavItem.Background = active ? Res(ThemeManager.SurfaceAltKey) : Brushes.Transparent;
            section.NavText.Foreground = Res(active ? ThemeManager.TextKey : ThemeManager.TextMutedKey);
            section.NavText.FontWeight = active ? FontWeight.Medium : FontWeight.Normal;
        }
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

    /// <summary>The card's position in the scroller's content space, margins included.</summary>
    private double CardTop(Border card) => card.Bounds.Y + Cards.Bounds.Y - 8;

    /// <summary>
    /// Highlights the section the panel is actually showing — the topmost card still in view —
    /// rather than the last one clicked (list.md Phase 4, "Settings Nav Menu").
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
    /// the Commander actually sees (list.md Phase 12).
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

    private void RememberCollapse(string capabilityId, bool expanded)
    {
        _viewState = _viewState.With(capabilityId, expanded);
        _viewStateStore?.Save(_viewState);
    }

    private void OnOpenDataFolderClick(object? sender, RoutedEventArgs e)
    {
        if (_paths is not null)
        {
            Process.Start(new ProcessStartInfo(_paths.Data) { UseShellExecute = true });
        }
    }

    /// <summary>
    /// What this build is. Modal on whatever window is hosting this surface rather than
    /// free-floating, so it cannot be lost behind the app that raised it.
    /// </summary>
    private async void OnAboutClick(object? sender, RoutedEventArgs e)
    {
        if (_paths is not null && TopLevel.GetTopLevel(this) is Window owner)
        {
            await new Controls.AboutWindow(_paths, _setUpKeys).ShowDialog(owner);
        }
    }

    /// <summary>
    /// Re-reads every row from settings. Cheaper than rebuilding and, more importantly, it does
    /// not pull the control out from under whatever has focus.
    /// <para>
    /// Public because a row can go stale without any setting having changed: the disclosure
    /// naming what was found in <c>data/audio/</c> is read from the cue library, and the library
    /// is rebuilt when the Commander drops a file in (list.md Phase 12). Every other caller
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

        UpdateNavVisuals();

        var showing = new int[_sections.Count];

        _refreshing = true;
        try
        {
            foreach (var row in _rows)
            {
                // A row that does not apply is absent, not disabled: a greyed-out control still
                // asserts that the setting exists (list.md Phase 4).
                var shown = row.Row.Applies(_settings.Current) && Matches(row.Row);

                row.Container.IsVisible = shown;
                row.Refresh();

                if (shown && row.Section >= 0)
                {
                    showing[row.Section]++;
                }
            }
        }
        finally
        {
            _refreshing = false;
        }

        ApplyFilterToCards(showing);
    }

    /// <summary>What the surface is being filtered by, or empty when it is not.</summary>
    private string _query = string.Empty;

    /// <summary>
    /// Shows only the rows that match (list.md Phase 12, "Search whichever tab you are looking
    /// at").
    /// <para>
    /// Settings filters where the transcript pages highlight, and the difference is the design
    /// rather than an inconsistency: 92 rows across 14 sections is a haystack, and highlighting
    /// in place in a haystack is a scroll hunt with extra colour.
    /// </para>
    /// </summary>
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
    private bool Matches(SettingRow row) =>
        _query.Length == 0
        || row.Label.Contains(_query, StringComparison.OrdinalIgnoreCase)
        || row.Help.Contains(_query, StringComparison.OrdinalIgnoreCase)
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
    private void ApplyFilterToCards(int[] showing)
    {
        var filtering = _query.Length > 0;

        for (var i = 0; i < _sections.Count; i++)
        {
            var section = _sections[i];
            var holds = showing[i] > 0;

            section.Card.IsVisible = !filtering || holds;
            section.NavItem.IsVisible = !filtering || holds;

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


        var help = new TextBlock
        {
            Text = row.Help,
            FontSize = TypeScale.Secondary,
            Margin = new Thickness(0, 2, 0, 0),
            TextWrapping = TextWrapping.Wrap,
            IsVisible = !string.IsNullOrWhiteSpace(row.Help),
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

        var caption = new StackPanel { Spacing = 0 };
        caption.Children.Add(header);
        caption.Children.Add(help);

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

        return new RowView(row, container, refresh) { Control = control, Body = body };
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

            // An Info row that also clears the state it describes. Rendered from the row
            // rather than special-cased by key like the two above, because what is behind
            // this button is a method rather than a window the App has to own.
            case SettingKind.Info when row.Press is not null:
                return BuildPressable(row);

            case SettingKind.Info:
                return BuildInfo(row);

            case SettingKind.Toggle:
                return BuildToggle(row, message);

            case SettingKind.Choice when row.AllowsFreeText || row.ChoiceSource is not null:
                // Long or open vocabulary: the searchable picker, which stays usable when the
                // list is empty because the value can be typed (list.md Phase 4).
                return BuildPickerButton(row, message);

            case SettingKind.Choice:
                return BuildComboBox(row, message);

            case SettingKind.Number:
                return BuildNumber(row, message);

            case SettingKind.Secret:
                return BuildSecret(row, message);

            case SettingKind.Hotkey:
                return BuildHotkey(row, message);

            default:
                return BuildText(row, message);
        }
    }

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

        return (inset, () => text.Text = row.Binding!.Read(_settings!.Current), false);
    }

    /// <summary>
    /// A disclosure with the button that clears it. Refreshed on the press, so the row states
    /// the new answer rather than leaving the Commander to wonder whether anything happened.
    /// </summary>
    private (Control, Action, bool) BuildPressable(SettingRow row)
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

        press.Click += (_, _) =>
        {
            row.Press!();
            refresh();
        };

        var stack = new StackPanel { Spacing = 8, Children = { inset, press } };

        return (stack, refresh, false);
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
                await new Controls.CoverageWindow(_coverage()).ShowDialog(owner);
            }
        };

        var stack = new StackPanel { Spacing = 8, Children = { inset, open } };

        return (stack, refresh, false);
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

            await new Controls.MacroWindow(_macros) { ReservedPhrases = _reserved }.ShowDialog(owner);

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

        var open = new Button
        {
            Name = "OpenChecklist",
            Content = "Open the checklist",
            FontSize = TypeScale.Body,
            Padding = new Thickness(10, 4),
            HorizontalAlignment = HorizontalAlignment.Left,
        };

        open.Click += async (_, _) =>
        {
            if (_checklists is null || TopLevel.GetTopLevel(this) is not Window owner)
            {
                return;
            }

            await new Controls.ChecklistWindow(_checklists).ShowDialog(owner);

            // The panel writes the file; this is what puts the new summary on the row without
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
                editing.Store, editing.Reader, editing.Reconciler, editing.Now, editing.ExportPath)
                .ShowDialog(owner);

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

        var choices = row.Choices;

        // A clear item only where clearing means something. Provider and theme always hold a
        // value, so "(default: anthropic)" above "anthropic" would be the same answer twice.
        var clearable = row.IsClearable;

        var items = new List<string>();
        if (clearable)
        {
            items.Add(row.BareDefaultFor(_settings!.Current) is { } bare ? $"(default: {bare})" : "(default)");
        }

        items.AddRange(choices.Select(row.LabelForChoice));
        combo.ItemsSource = items;

        var offset = clearable ? 1 : 0;

        // Only one row downloads anything, so only one row carries a progress bar. Built here
        // rather than in a generic slot because a bar every row could show is a bar every row
        // has to explain.
        var downloads = string.Equals(row.Key, ListeningCapability.ModelKey, StringComparison.Ordinal);

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
                : row.LabelForChoice(current);

            // The button is one line in a column; a model id or a resolved device name is
            // routinely longer than it. Chosen or defaulted, the whole string is on the pointer.
            ToolTip.SetTip(button, current is null ? null : row.LabelForChoice(current));

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
            // distinguishable from "I chose the default" (list.md Phase 4).
            box.PlaceholderText = row.DefaultDisplayFor(_settings.Current) ?? string.Empty;
            ShowDefaultOnHover(box, row);
        }, false);
    }

    /// <summary>
    /// The key row, which is <see cref="SecretEditor"/> — the same control the first-run guide
    /// shows (list.md Phase 16). Extracted rather than duplicated so the trim, the reveal, the
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

    private (Control, Action, bool) BuildHotkey(SettingRow row, TextBlock message)
    {
        var button = new Button { MinWidth = 150, HorizontalContentAlignment = HorizontalAlignment.Center };
        var clear = new Button { Content = "Unbind" };

        button.Click += async (_, _) => await CaptureHotkeyAsync(row, button, message);
        clear.Click += (_, _) => Apply(row, null, message);

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
            var bound = _settings!.Read(row.Key);
            button.Content = bound is null ? "Press to bind" : Gestures.Describe(bound);
        }, true);
    }

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
                Describe = row.ChoiceLabel,
                Current = _settings.Read(row.Key),
                DefaultDisplay = row.IsClearable ? row.BareDefaultFor(_settings.Current) : null,
                AllowsFreeText = row.AllowsFreeText,
                WhyEmpty = row.WhyNoChoicesFor(_settings.Current),

                // Read at open rather than captured once, because both the price and the reason
                // it might be unavailable follow the selected provider.
                Audition = row.Audition is { } audition
                    ? new PickerAudition
                    {
                        Play = audition.Play,
                        Label = audition.Label(_settings.Current),
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
    /// Binds a gesture by listening for one, rather than by offering a list of key names. There
    /// is no way to type a key that does not exist, and no list to keep in step with a keyboard
    /// layout (list.md Phase 4, "Hotkey Binding").
    /// </summary>
    private async Task CaptureHotkeyAsync(SettingRow row, Button button, TextBlock message)
    {
        if (TopLevel.GetTopLevel(this) is not { } top)
        {
            return;
        }

        var previous = button.Content;
        button.Content = "Press a key…";

        var captured = new TaskCompletionSource<KeyGesture?>();

        void OnKey(object? sender, KeyEventArgs e)
        {
            // A modifier on its own is someone still assembling the chord, not a binding.
            if (e.Key is Key.LeftCtrl or Key.RightCtrl or Key.LeftShift or Key.RightShift
                or Key.LeftAlt or Key.RightAlt or Key.LWin or Key.RWin)
            {
                return;
            }

            e.Handled = true;
            captured.TrySetResult(e.Key == Key.Escape ? null : new KeyGesture(e.Key, e.KeyModifiers));
        }

        // Tunnelling: the gesture belongs to the binding, not to whatever control the click left
        // focused, so it has to be seen on the way down.
        //
        // And handled events too. This is the same top level that carries the push-to-talk
        // suppressor now that settings is a page of the main window rather than a window of its
        // own — the suppressor tunnels first and marks the key handled, so without this the one
        // rebind that could not be made is rebinding push-to-talk to the key it already holds,
        // which is exactly the rebind somebody attempting it is most likely to try.
        top.AddHandler(KeyDownEvent, OnKey, RoutingStrategies.Tunnel, handledEventsToo: true);

        try
        {
            var gesture = await captured.Task;

            if (gesture is not null)
            {
                Apply(row, gesture.ToString(), message);
            }
        }
        finally
        {
            top.RemoveHandler(KeyDownEvent, OnKey);
            button.Content = previous;
            Refresh();
        }
    }

    private bool Apply(SettingRow row, string? value, TextBlock message)
    {
        if (_settings is null)
        {
            return false;
        }

        var result = _settings.Apply(row.Key, value, SettingsCaller.Panel);

        // Only failures are worth saying. A change that worked is visible in the control that
        // made it, and the panel is not a log.
        message.IsVisible = !result.Ok;
        message.Text = result.Message;

        if (!result.Ok)
        {
            // A rejected value never reached the store, so the control has to stop showing it.
            Refresh();
        }

        return result.Ok;
    }

    private static void OpenDocs(CapabilityDescriptor capability)
    {
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
        Border NavItem,
        Border NavBar,
        TextBlock NavText);

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
    }
}
