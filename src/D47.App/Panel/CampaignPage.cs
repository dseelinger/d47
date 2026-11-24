using System.Collections.Immutable;
using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;
using D47.App.Controls;
using D47.App.Theming;
using D47.Core.Campaigns;
using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Journal;
using D47.Core.Knowledge;

namespace D47.App.Panel;

/// <summary>What the Campaign page reads, and the registry it calls the campaign tools through.</summary>
public sealed record CampaignSources(
    Func<CommanderGameState?> State,
    CampaignStore Campaigns,
    Func<CampaignFrontsSnapshot?> Fronts,
    Func<string?> SquadronFaction,
    Func<DateTimeOffset?> Tick,
    CapabilityRegistry Tools)
{
    public Func<DateTimeOffset> Now { get; init; } = () => DateTimeOffset.UtcNow;

    /// <summary>The front d47 proposes while none is chosen.</summary>
    public Func<FrontProposal?> Proposal { get; init; } = () => null;

    /// <summary>The next action for the chosen front.</summary>
    public Func<CampaignNextAction?> NextAction { get; init; } = () => null;
}

/// <summary>The message the Campaign page shows after a failed change, kept across the page being rebuilt.</summary>
public sealed class CampaignNote
{
    public string? Text { get; set; }
}

/// <summary>
/// Commander › BGS: the campaign faction, the front being worked or proposed, and the Fronts, Systems and
/// Chronicle sections. A step, a tile and the faction picker call the same protected tools as the phrases, as the Commander.
/// </summary>
public sealed class CampaignPage : UserControl, IPageSummary, IPageSection, IFilterablePage
{
    public const string RootKey = "campaign";

    public const string PickerKey = "campaign.faction";

    public const string ChangeName = "CampaignChangeFaction";
    public const string HeadName = "CampaignHead";
    public const string FrontHeadName = "CampaignFrontHead";
    public const string ChronicleHeadName = "CampaignChronicleHead";
    public const string TableId = "Commander.Campaign";
    public const string FrontsTableId = "Commander.Campaign.Fronts";
    public const string ChronicleTableId = "Commander.Campaign.Chronicle";
    public const string PreviousName = "CampaignPrevious";
    public const string NextName = "CampaignNext";
    public const string MessageName = "CampaignMessage";
    public const string SectionsName = "CampaignSections";
    public const string NoticeName = "CampaignNotice";
    public const string TakeItName = "CampaignTakeIt";
    public const string NextFrontName = "CampaignNextFront";
    public const string DropFrontName = "CampaignDropFront";
    public const string ChosenName = "CampaignChosen";
    public const string ProposedName = "CampaignProposed";
    public const string SwitchName = "CampaignSwitch";
    public const string OffName = "CampaignOff";

    /// <summary>The automation name of a row's Stepper.</summary>
    public static string StepperName(string system) => $"Intent {system}";

    /// <summary>The name of the TAKE tile on a front's row.</summary>
    public static string TakeName(Front front) => $"CampaignTake.{front.Kind}.{front.System}";

    private const double ProseWidth = 900;

    private readonly CampaignSources _sources;
    private readonly PanelPrompts? _prompts;
    private readonly bool _headset;
    private readonly ColumnWidthMemory? _columnWidths;
    private readonly Func<string, Task<bool>>? _copy;
    private readonly ColumnArrangementMemory? _arrangements;
    private readonly BgsWorkSection _work;
    private ArrangedTable<CampaignSystemRow>? _systemColumns;
    private ArrangedTable<CampaignModel.FrontRow>? _frontColumns;
    private ArrangedTable<CampaignModel.ChronicleRow>? _chronicleColumns;
    private string? _hubSystem;
    private string? _drawnHere;
    private readonly StackPanel _body = new() { Spacing = 20 };
    private readonly ScrollViewer _scroll;
    private readonly Segment _segment;
    private readonly Border _noticeSlot = new();
    private readonly Border _frontSlot = new();
    private readonly Border _sectionSlot = new();

    private object? _seenSnapshot;
    private string? _seenCampaignFaction;
    private bool _seenOff;
    private ImmutableDictionary<string, SystemIntent>? _seenIntents;
    private SystemStandings? _seenStandings;
    private string? _seenSquadron;
    private string? _seenSystem;
    private string? _seenFrontier;
    private DateTimeOffset? _seenTick;
    private long _seenHour;
    private string? _seenNote;
    private BgsWork? _seenWork;
    private long _seenMinute;
    private bool _headTicked = true;
    private IReadOnlyDictionary<string, IReadOnlyList<ChronicleWork>> _systemWork = new Dictionary<string, IReadOnlyList<ChronicleWork>>();
    private FrontsKey? _seenFronts;
    private string _query = string.Empty;
    private readonly CampaignNote _note;
    private TextBlock? _computed;
    private bool _reportShown;
    private int _page;
    private int _section;
    private IReadOnlyList<CampaignSystemRow> _rows = [];

    private string? _faction;
    private string? _frontier;
    private DateTimeOffset _now;
    private CampaignFrontsSnapshot? _snapshot;
    private Campaign _campaign = Campaign.None;
    private FrontProposal? _proposal;
    private CampaignNextAction? _action;

    /// <summary>What the parts of the page that follow the fronts are drawn from; a change in any of it redraws them and not the Systems table.</summary>
    private sealed record FrontsKey(
        IReadOnlyList<Front> Fronts,
        string Computed,
        ChosenFront? Chosen,
        IReadOnlyList<ChronicleEntry> Chronicle,
        FrontProposal? Proposal,
        string? Action)
    {
        public bool Same(FrontsKey? other) =>
            other is not null
            && Computed == other.Computed
            && Action == other.Action
            && ReferenceEquals(Chosen, other.Chosen)
            && ReferenceEquals(Chronicle, other.Chronicle)
            && Equals(Proposal, other.Proposal)
            && Fronts.SequenceEqual(other.Fronts);
    }

    public CampaignPage(
        CampaignSources sources,
        PanelPrompts? prompts = null,
        bool headset = false,
        ColumnWidthMemory? columnWidths = null,
        Func<string, Task<bool>>? copy = null,
        CampaignNote? note = null,
        ColumnArrangementMemory? columnArrangements = null)
    {
        _sources = sources;
        _note = note ?? new CampaignNote();
        _prompts = prompts;
        _headset = headset;
        _columnWidths = columnWidths;
        _copy = headset ? null : copy;
        _arrangements = columnArrangements;
        _work = new BgsWorkSection(headset, columnWidths, _copy, columnArrangements);
        _work.Changed += FillSection;
        _work.FilterChanged += () => _page = 0;

        _segment = new Segment
        {
            Name = SectionsName,
            ItemsSource = CampaignModel.Sections,
            SelectedIndex = CampaignModel.FrontsSection,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Height = headset ? TypeScale.MinimumTarget : Segment.StatusHeight,
        };
        _segment.SelectionChanged += (_, _) =>
        {
            _sectionWord.Choose();
            ShowSection(_segment.SelectedIndex);
        };

        Margin = new Thickness(14);
        _scroll = LoadoutPages.Scrolling(_body);
        Content = _scroll;
        Draw();
    }

    private void ShowSection(int index)
    {
        StopArranging();
        _section = index;
        _page = 0;
        FillSection();
        ReportSection();
    }

    private void ReportSection()
    {
        var section = _segment.Parent is not null && _section >= 0 && _section < CampaignModel.Sections.Count
            ? CampaignModel.Sections[_section]
            : string.Empty;
        _sectionWord.Set(this, section, section);
    }

    public string Summary { get; private set; } = string.Empty;

    public event EventHandler? SummaryChanged;

    private readonly PageSectionWord _sectionWord = new();

    public string OpenSection => _sectionWord.Word;

    public string OpenSectionId => _sectionWord.Id;

    bool IPageSection.SectionChosen => _sectionWord.Chosen;

    public bool OpenSectionById(string id)
    {
        var index = CampaignModel.Sections.ToList().IndexOf(id);

        if (index < 0)
        {
            return false;
        }

        _sectionWord.Unchoose();

        if (index != _section)
        {
            _segment.SelectedIndex = index;
            ShowSection(index);
        }

        return true;
    }

    public event EventHandler? OpenSectionChanged
    {
        add => _sectionWord.Changed += value;
        remove => _sectionWord.Changed -= value;
    }

    public bool Filters => !_campaign.Off && (_reportShown || _campaign.Chronicle.Count > 0 || (_faction is not null && WorkNow().Rows.Count > 0));

    public string FilterPlaceholder => "Search system or faction";

    public void Filter(string? query)
    {
        if (PageKit.Retarget(query, ref _query))
        {
            _page = 0;
            FillSection();
        }
    }

    /// <summary>
    /// Redraws the page when the report, the faction, the intents, the readings, the squadron, the tick, the current system,
    /// the message or the hour has changed; redraws only the front, the notice, the counts and the Fronts and Chronicle
    /// sections when only the fronts, the chosen front, the proposal, the next action or the chronicle has; otherwise
    /// updates only the time the fronts were computed.
    /// </summary>
    public bool Tick()
    {
        Refresh();
        var state = _sources.State();

        if (!(Equals(Drawn(_snapshot), _seenSnapshot)
            && _campaign.Faction == _seenCampaignFaction
            && _campaign.Off == _seenOff
            && ReferenceEquals(_campaign.Intents, _seenIntents)
            && ReferenceEquals(state?.Standings, _seenStandings)
            && _sources.SquadronFaction() == _seenSquadron
            && state?.Location.StarSystem == _seenSystem
            && _frontier == _seenFrontier
            && _sources.Tick() == _seenTick
            && _note.Text == _seenNote
            && Hour(_now) == _seenHour))
        {
            Draw();
            return true;
        }

        var workMoved = false;

        if (_faction is not null
            && !_campaign.Off
            && (!ReferenceEquals(state?.BgsWork, _seenWork) || (_section == CampaignModel.WorkSection && PageKit.Minute(_now) != _seenMinute)))
        {
            FillCounts();
            FillSection();
            workMoved = true;
        }

        if (!FrontsKeyNow().Same(_seenFronts))
        {
            DrawFronts();
            return true;
        }

        if (workMoved)
        {
            return true;
        }

        var computed = CampaignModel.ComputedText(_reportShown ? _snapshot : null, _sources.Tick());

        if (_computed is null || _computed.Text == computed)
        {
            return false;
        }

        _computed.Text = computed;
        return true;
    }

    /// <summary>Reads what the page is drawn from, other than the systems' figures.</summary>
    private void Refresh()
    {
        var state = _sources.State();
        _frontier = state?.Identity.FrontierId;
        _now = _sources.Now();
        _snapshot = _sources.Fronts();
        _campaign = _sources.Campaigns.For(_frontier);
        _faction = _frontier is { Length: > 0 } ? _campaign.FactionOr(_sources.SquadronFaction()) : null;

        _proposal = _sources.Proposal() is { } proposal
            && string.Equals(proposal.FrontierId, _frontier, StringComparison.Ordinal)
            && string.Equals(proposal.Faction, _faction, StringComparison.OrdinalIgnoreCase)
                ? proposal
                : null;

        _action = _campaign.Chosen is null ? null : _sources.NextAction();
    }

    private FactionReport? HeldReport() =>
        _faction is not null
        && _snapshot is { Report: { } held }
        && string.Equals(_snapshot.FrontierId, _frontier, StringComparison.Ordinal)
        && string.Equals(_snapshot.Faction, _faction, StringComparison.OrdinalIgnoreCase)
            ? held
            : null;

    /// <summary>Rebuilds the Systems table when the tick time becomes known or unknown, keeping the sort chosen.</summary>
    private void UseHead(bool ticked)
    {
        if (ticked == _headTicked)
        {
            return;
        }

        _headTicked = ticked;

        if (_systemColumns is not { } old)
        {
            return;
        }

        _systemColumns = null;

        if (old.SortedBy is { } sort)
        {
            SystemColumns.ShowSort(sort.Key, sort.Direction);
        }
    }

    private void StopArranging()
    {
        _systemColumns?.Reopen();
        _frontColumns?.Reopen();
        _chronicleColumns?.Reopen();
        _work.Reopen();
    }

    /// <summary>
    /// The work the WORK section lists, and the campaign faction's work by the name of each system in the report.
    /// The faction's work in a system the report lists is on Systems, so WORK leaves it out.
    /// </summary>
    private (IReadOnlyList<BgsWorkRow> Rows, IReadOnlyDictionary<string, IReadOnlyList<ChronicleWork>> BySystem) WorkNow()
    {
        var state = _sources.State();
        var tick = _sources.Tick();
        var work = state?.BgsWork ?? BgsWork.None;
        var named = _rows.Select(row => row.System).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var faction = _faction;

        if (faction is null)
        {
            return ([], new Dictionary<string, IReadOnlyList<ChronicleWork>>());
        }

        var from = BgsWork.PeriodStart(tick, _now);
        var bySystem = work.Entries
            .Where(entry => entry.At >= from && string.Equals(entry.Faction, faction, StringComparison.OrdinalIgnoreCase))
            .Select(entry => (Name: work.NameOf(entry.SystemAddress), Entry: entry))
            .Where(pair => pair.Name is not null && named.Contains(pair.Name))
            .GroupBy(pair => pair.Name!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => CampaignModel.WorkOf(group.Select(pair => pair.Entry)), StringComparer.OrdinalIgnoreCase);

        var rows = BgsWorkTable.Rows(work, tick, _now, state?.Standings ?? SystemStandings.Empty)
            .Where(row => !(string.Equals(row.Faction, faction, StringComparison.OrdinalIgnoreCase)
                && row.System is not null
                && named.Contains(row.System)))
            .ToList();

        return (rows, bySystem);
    }

    private FrontsKey FrontsKeyNow() => new(
        HeldReport() is null ? [] : _snapshot!.Fronts,
        CampaignModel.ComputedText(HeldReport() is null ? null : _snapshot, _sources.Tick()),
        _campaign.Chosen,
        _campaign.Chronicle,
        _proposal,
        _action is null ? null : _action.Sentence + "|" + _action.NotCounting);

    private void Draw()
    {
        Refresh();

        var state = _sources.State();
        var squadron = _sources.SquadronFaction();
        var tick = _sources.Tick();

        _seenSnapshot = Drawn(_snapshot);
        _seenCampaignFaction = _campaign.Faction;
        _seenOff = _campaign.Off;
        _seenIntents = _campaign.Intents;
        _seenStandings = state?.Standings;
        _seenSquadron = squadron;
        _seenSystem = state?.Location.StarSystem;
        _seenFrontier = _frontier;
        _seenTick = tick;
        _seenHour = Hour(_now);
        _seenNote = _note.Text;
        UseHead(tick is not null);

        var report = HeldReport();

        _reportShown = report is not null;
        _rows = report is null ? [] : CampaignSystemRows.Of(report, state?.Standings ?? SystemStandings.Empty, _campaign.Intents, _now, state?.Location.StarPos);

        var off = _campaign.Off;
        var shown = off ? CampaignModel.OffFaction(_campaign, squadron) : _faction;
        var summary = CampaignModel.Summary(shown, off, _headset);

        if (summary != Summary)
        {
            Summary = summary;
            SummaryChanged?.Invoke(this, EventArgs.Empty);
        }

        foreach (var kept in new Control[] { _noticeSlot, _frontSlot, _segment, _sectionSlot })
        {
            (kept.Parent as Avalonia.Controls.Panel)?.Children.Remove(kept);
        }

        _body.Children.Clear();

        var column = new StackPanel { Spacing = 20, MaxWidth = _headset ? double.PositiveInfinity : ProseWidth, HorizontalAlignment = HorizontalAlignment.Left };
        var page = new StackPanel { Spacing = 20 };
        _body.Children.Add(page);
        page.Children.Add(column);

        if (!off)
        {
            column.Children.Add(_noticeSlot);
        }

        column.Children.Add(Frame(shown, off, report is null ? null : _rows, report is null ? null : _snapshot, tick, _frontier));

        if (_note.Text is { } message)
        {
            var line = Typeset.Prose(message, TypeScale.Description, ThemeManager.WarnKey, wrap: true);
            line.Name = MessageName;
            column.Children.Add(line);
        }

        _noticeSlot.Child = null;
        _frontSlot.Child = null;
        _sectionSlot.Child = null;

        if (off || _faction is null)
        {
            if (off)
            {
                column.Children.Add(OffBlock(shown));
            }

            if (CampaignModel.NoFactionLine(_faction, off) is { } none)
            {
                column.Children.Add(Grey(none));
            }

            _sectionWord.Set(this, string.Empty, string.Empty);
            _seenFronts = FrontsKeyNow();
            return;
        }

        column.Children.Add(_frontSlot);
        page.Children.Add(_segment);
        page.Children.Add(_sectionSlot);
        ReportSection();

        FillFront();
        FillCounts();
        FillSection();
        _seenFronts = FrontsKeyNow();
    }

    /// <summary>Redraws the notice, the front block, the counts and, unless the Systems section is showing, the section.</summary>
    private void DrawFronts()
    {
        Refresh();
        FillFront();
        FillCounts();

        if (_section != CampaignModel.SystemsSection)
        {
            FillSection();
        }

        _seenFronts = FrontsKeyNow();
    }

    /// <summary>The parts of the snapshot the rows are drawn from; the fronts and the time they were computed are not.</summary>
    private static object? Drawn(CampaignFrontsSnapshot? snapshot) =>
        snapshot is null ? null : (snapshot.FrontierId, snapshot.Faction, snapshot.Report, snapshot.ReportFetchedAt, snapshot.Failure);

    private Control Frame(string? faction, bool off, IReadOnlyList<CampaignSystemRow>? rows, CampaignFrontsSnapshot? snapshot, DateTimeOffset? tick, string? frontier)
    {
        var grid = new Grid { ColumnSpacing = Gaps.Tile, ColumnDefinitions = new ColumnDefinitions("*,*,*") };
        var failed = snapshot?.Failure is not null;

        Place(grid, 0, Datum("BGS faction", Typeset.Chrome(faction ?? "None", TypeScale.Body, faction is null ? ThemeManager.GreyKey : ThemeManager.WhiteKey, weight: FontWeight.Medium, trimming: TextTrimming.CharacterEllipsis)));
        Place(grid, 1, Datum("Its systems", Typeset.Mono(rows is null ? CampaignModel.Dash : CampaignModel.SystemsText(rows), TypeScale.Small, off ? ThemeManager.GreyKey : ThemeManager.WhiteKey)));

        if (off)
        {
            _computed = null;
            Place(grid, 2, Datum("Fronts computed", Typeset.Mono(CampaignModel.NotWhileOff, TypeScale.Small, ThemeManager.GreyKey)));
        }
        else
        {
            _computed = Typeset.Mono(CampaignModel.ComputedText(snapshot, tick), TypeScale.Small, failed ? ThemeManager.WarnKey : ThemeManager.WhiteKey);
            Place(grid, 2, Datum("Fronts computed", _computed));
        }

        var power = LabeledCheckBox.Caps(CampaignModel.OffSwitchLabel);
        power.Name = SwitchName;
        power.IsChecked = !off;
        power.MinHeight = TypeScale.MinimumTarget;
        power.VerticalAlignment = VerticalAlignment.Stretch;
        power.VerticalContentAlignment = VerticalAlignment.Center;
        power.IsEnabled = frontier is { Length: > 0 };
        power.Click += (_, _) => _ = Switch(power, off);

        var change = new Button
        {
            Name = ChangeName,
            Content = CampaignModel.FactionTileLabel(faction, off),
            MinHeight = TypeScale.MinimumTarget,
            VerticalAlignment = VerticalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Center,
            Padding = new Thickness(18, 0),
            IsEnabled = !off && frontier is { Length: > 0 } && _prompts is not null,
        };
        change.Click += (_, _) => OpenPicker(faction);

        var line = new Grid { ColumnSpacing = Gaps.Tile, ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto") };
        Grid.SetColumn(power, 1);
        Grid.SetColumn(change, 2);
        line.Children.Add(grid);
        line.Children.Add(power);
        line.Children.Add(change);

        return line;
    }

    /// <summary>The block under the top line while the campaign is off.</summary>
    private Control OffBlock(string? faction)
    {
        if (_headset)
        {
            var compact = new StackPanel
            {
                Spacing = 6,
                Children =
                {
                    Typeset.Chrome(CampaignModel.OffLabel, TypeScale.Meta, ThemeManager.GreyKey, weight: FontWeight.SemiBold, tracking: TypeScale.Meta * Fonts.ChromeTracking),
                    Typeset.Prose(CampaignModel.OffBody, TypeScale.Description, ThemeManager.WhiteKey, wrap: true),
                    Typeset.Prose(CampaignModel.OffCompact, TypeScale.Meta, ThemeManager.GreyKey, wrap: true),
                },
            };

            var border = new Border { Name = OffName, Padding = new Thickness(14, 10), Child = compact };
            ThemeInk.Bind(border, Border.BackgroundProperty, ThemeManager.SlabKey);

            return border;
        }

        var slab = Slab();
        slab.Children.Add(Typeset.Prose(CampaignModel.OffText(faction), TypeScale.Body, ThemeManager.WhiteKey, wrap: true));
        slab.Children.Add(Typeset.Prose(CampaignModel.OffKept, TypeScale.Description, ThemeManager.GreyKey, wrap: true));

        var slabBorder = new Border { Padding = new Thickness(18, 16), Child = slab };
        ThemeInk.Bind(slabBorder, Border.BackgroundProperty, ThemeManager.SlabKey);

        return new StackPanel
        {
            Name = OffName,
            Spacing = 12,
            Children =
            {
                Typeset.Chrome(CampaignModel.OffLabel, TypeScale.Meta, ThemeManager.GreyKey, weight: FontWeight.SemiBold, tracking: TypeScale.Meta * Fonts.ChromeTracking),
                slabBorder,
                Typeset.Prose(CampaignModel.OffHint, TypeScale.Description, ThemeManager.GreyKey, wrap: true),
            },
        };
    }

    private async Task Switch(CheckBox box, bool wasOff)
    {
        await Call(CampaignCapability.SwitchTool, new ToolArguments(new Dictionary<string, string>(StringComparer.Ordinal) { ["on"] = wasOff ? "true" : "false" })).ConfigureAwait(true);
        box.IsChecked = !_sources.Campaigns.For(_frontier).Off;
    }

    private static Border Datum(string label, TextBlock value)
    {
        var block = new Border
        {
            Padding = new Thickness(14, 12),
            Child = new StackPanel
            {
                Spacing = 4,
                Children =
                {
                    Typeset.Chrome(label.ToUpperInvariant(), TypeScale.Meta, ThemeManager.GreyKey, weight: FontWeight.SemiBold, tracking: TypeScale.Meta * Fonts.ChromeTracking),
                    value,
                },
            },
        };
        ThemeInk.Bind(block, Border.BackgroundProperty, ThemeManager.SlabKey);

        return block;
    }

    private TextBlock Grey(string text) => CappedText(Typeset.Prose(text, TypeScale.Body, ThemeManager.GreyKey, wrap: true));

    private TextBlock CappedText(TextBlock text)
    {
        if (!_headset)
        {
            text.MaxWidth = ProseWidth;
            text.HorizontalAlignment = HorizontalAlignment.Left;
        }

        return text;
    }

    private static DockPanel LegendLine()
    {
        var bar = new Border { Width = 3, Height = 16, VerticalAlignment = VerticalAlignment.Center };
        ThemeInk.Bind(bar, Border.BackgroundProperty, ThemeManager.AKey);

        var text = Typeset.Prose(CampaignModel.Legend, TypeScale.Description, ThemeManager.GreyKey, wrap: true, margin: new Thickness(8, 0, 0, 0));

        var line = new DockPanel();
        DockPanel.SetDock(bar, Dock.Left);
        line.Children.Add(bar);
        line.Children.Add(text);

        return line;
    }

    private void FillCounts()
    {
        if (_headset)
        {
            return;
        }

        var report = HeldReport();
        var counts = CampaignModel.SectionCounts(
            report is null ? 0 : _snapshot!.Fronts.Count,
            report is null ? 0 : _rows.Count,
            WorkNow().Rows.Count,
            _campaign.Chronicle.Count);

        _segment.Statuses = [.. counts.Select(count => (ChoiceStatus?)new ChoiceStatus(count, ChoiceTone.Grey))];
    }

    /// <summary>Fills the failed-fetch notice and the chosen or proposed front.</summary>
    private void FillFront()
    {
        var report = HeldReport();

        if (report is not null && _snapshot is { Failure: { } failure } snapshot)
        {
            _noticeSlot.Child = new Notice(NoticeLevel.Warning)
            {
                Name = NoticeName,
                Label = CampaignModel.FailedLabel,
                Text = CampaignModel.FailedText(_faction!, snapshot, _sources.Tick()),
                Detail = failure,
            };
        }
        else
        {
            _noticeSlot.Child = null;
        }

        _frontSlot.Child = _campaign.Chosen is { } chosen
            ? ChosenBlock(chosen, report is null ? null : _snapshot!.Fronts)
            : _proposal is { } proposal && report is not null
                ? ProposedBlock(proposal.Front, _snapshot!.Fronts)
                : null;
    }

    private SinceTick SinceTickOf(Front front) =>
        front.OwnReading && _sources.Tick() is { } tick && _sources.State()?.Standings.Since(front.System, tick) is { } since
            ? since
            : SinceTick.NoReading;

    private Control ChosenBlock(ChosenFront chosen, IReadOnlyList<Front>? fronts)
    {
        var front = fronts?.FirstOrDefault(candidate =>
            candidate.Kind == chosen.Kind && string.Equals(candidate.System, chosen.System, StringComparison.OrdinalIgnoreCase));

        if (_headset)
        {
            return HeadsetBlock(
                ChosenName,
                $"Chosen front · {CampaignFronts.Describe(chosen.Kind)}",
                chosen.System,
                front is null ? null : CampaignModel.DaysWonText(front),
                front is null
                    ? $"You are working the {CampaignFronts.Describe(chosen.Kind).ToLowerInvariant()} in {chosen.System}, but its front is not in the report D47 holds yet."
                    : _action?.Sentence ?? front.Reason,
                _action?.NotCounting,
                Tile(NextFrontName, "NEXT FRONT", CampaignCapability.NextFrontTool, showReply: true),
                Tile(DropFrontName, "DROP THE FRONT", CampaignCapability.DropFrontTool));
        }

        var slab = Slab();

        if (front is null)
        {
            slab.Children.Add(FrontLine(CampaignFronts.Describe(chosen.Kind), chosen.System, null, null));
            slab.Children.Add(Typeset.Prose(
                $"You are working the {CampaignFronts.Describe(chosen.Kind).ToLowerInvariant()} in {chosen.System}, but its front is not in the faction report D47 holds yet. D47 will say when it resolves.",
                TypeScale.Body,
                ThemeManager.GreyKey,
                wrap: true));
        }
        else
        {
            slab.Children.Add(FrontLine(CampaignFronts.Describe(front.Kind), front.System, CampaignModel.DaysWonText(front), CampaignModel.DaysWonText(front) is null ? null : CampaignModel.DaysWonNote(front, _now, SinceTickOf(front))));
            slab.Children.Add(Typeset.Prose(front.Reason, TypeScale.Body, ThemeManager.WhiteKey, wrap: true));

            if (_action is { } action)
            {
                var next = new Grid { ColumnDefinitions = new ColumnDefinitions("64,*"), RowDefinitions = new RowDefinitions("Auto,Auto"), ColumnSpacing = 12, RowSpacing = 4 };
                Place(next, 0, Typeset.Chrome("NEXT", TypeScale.Meta, ThemeManager.GreyKey, weight: FontWeight.SemiBold, tracking: TypeScale.Meta * Fonts.ChromeTracking, vertical: VerticalAlignment.Top));
                Place(next, 1, Typeset.Prose(action.Sentence, TypeScale.Body, ThemeManager.WhiteKey, wrap: true));

                if (action.NotCounting is { } excluded)
                {
                    var line = Typeset.Prose(excluded, TypeScale.Description, ThemeManager.GreyKey, wrap: true);
                    Grid.SetRow(line, 1);
                    Grid.SetColumn(line, 1);
                    next.Children.Add(line);
                }

                slab.Children.Add(next);
            }
        }

        var tiles = Tiles(
            Tile(NextFrontName, "NEXT FRONT", CampaignCapability.NextFrontTool, showReply: true),
            Tile(DropFrontName, "DROP THE FRONT", CampaignCapability.DropFrontTool),
            CampaignModel.ChosenPhrases);

        return Block(ChosenName, "Chosen front", CampaignModel.TicksText(chosen), slab, tiles);
    }

    private Control ProposedBlock(Front front, IReadOnlyList<Front> fronts)
    {
        var rank = fronts.ToList().FindIndex(candidate => candidate.Kind == front.Kind && string.Equals(candidate.System, front.System, StringComparison.OrdinalIgnoreCase)) + 1;

        if (_headset)
        {
            return HeadsetBlock(
                ProposedName,
                $"Proposed front · {CampaignFronts.Describe(front.Kind)}",
                front.System,
                rank > 0 ? $"Rank {rank} of {fronts.Count}" : null,
                front.Reason,
                null,
                Tile(TakeItName, "TAKE IT", CampaignCapability.TakeFrontTool),
                Tile(NextFrontName, "NEXT FRONT", CampaignCapability.NextFrontTool));
        }

        var slab = Slab();

        slab.Children.Add(FrontLine(CampaignFronts.Describe(front.Kind), front.System, rank > 0 ? $"Rank {rank} of {fronts.Count}" : null, null));
        slab.Children.Add(Typeset.Prose(front.Reason, TypeScale.Body, ThemeManager.WhiteKey, wrap: true));
        slab.Children.Add(Typeset.Prose(CampaignModel.ProposalNote, TypeScale.Description, ThemeManager.GreyKey, wrap: true));

        var tiles = Tiles(
            Tile(TakeItName, "TAKE IT", CampaignCapability.TakeFrontTool),
            Tile(NextFrontName, "NEXT FRONT", CampaignCapability.NextFrontTool),
            CampaignModel.ProposalPhrases(front));

        return Block(ProposedName, "Proposed front", "Not chosen yet", slab, tiles);
    }

    /// <summary>The big panel's front: one compact slab, with the two tiles stacked to its right.</summary>
    private static Control HeadsetBlock(string name, string label, string system, string? figure, string sentence, string? notCounting, Button first, Button second)
    {
        var head = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        head.Children.Add(Typeset.Chrome(label.ToUpperInvariant(), TypeScale.Meta, ThemeManager.GreyKey, weight: FontWeight.SemiBold, tracking: TypeScale.Meta * Fonts.ChromeTracking, vertical: VerticalAlignment.Center));
        head.Children.Add(Typeset.Chrome(system, TypeScale.Body, ThemeManager.CyanKey, vertical: VerticalAlignment.Center));

        if (figure is not null)
        {
            head.Children.Add(Typeset.Mono(figure, TypeScale.Small, ThemeManager.WhiteKey, vertical: VerticalAlignment.Center));
        }

        var slab = new StackPanel { Spacing = 6, Children = { head, Typeset.Prose(sentence, TypeScale.Description, ThemeManager.WhiteKey, wrap: true) } };

        if (notCounting is not null)
        {
            slab.Children.Add(Typeset.Prose(notCounting, TypeScale.Meta, ThemeManager.GreyKey, wrap: true));
        }

        var border = new Border { Padding = new Thickness(14, 10), Child = slab };
        ThemeInk.Bind(border, Border.BackgroundProperty, ThemeManager.SlabKey);

        foreach (var tile in new[] { first, second })
        {
            tile.HorizontalAlignment = HorizontalAlignment.Stretch;
            tile.VerticalAlignment = VerticalAlignment.Stretch;
            tile.HorizontalContentAlignment = HorizontalAlignment.Center;
        }

        var tiles = new Grid { RowDefinitions = new RowDefinitions("*,*"), RowSpacing = Gaps.Tile, Width = 170 };
        tiles.Children.Add(first);
        Grid.SetRow(second, 1);
        tiles.Children.Add(second);

        var block = new Grid { Name = name, ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = Gaps.Tile };
        Grid.SetColumn(tiles, 1);
        block.Children.Add(border);
        block.Children.Add(tiles);

        return block;
    }

    private static StackPanel Slab() => new() { Spacing = 10 };

    private static Control Block(string name, string title, string aside, StackPanel slab, Control tiles)
    {
        var head = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        head.Children.Add(Typeset.Chrome(title.ToUpperInvariant(), TypeScale.Meta, ThemeManager.GreyKey, weight: FontWeight.SemiBold, tracking: TypeScale.Meta * Fonts.ChromeTracking));
        var asideText = Typeset.Chrome(aside, TypeScale.Meta, ThemeManager.GreyKey);
        Grid.SetColumn(asideText, 1);
        head.Children.Add(asideText);

        var border = new Border { Padding = new Thickness(18, 16), Child = slab };
        ThemeInk.Bind(border, Border.BackgroundProperty, ThemeManager.SlabKey);

        return new StackPanel { Name = name, Spacing = 12, Children = { head, border, tiles } };
    }

    /// <summary>The front's kind, its system with a copy glyph, and a figure and note at the right.</summary>
    private Control FrontLine(string kind, string system, string? figure, string? note)
    {
        var line = new DockPanel { LastChildFill = false };

        void Left(Control control)
        {
            control.VerticalAlignment = VerticalAlignment.Center;
            control.Margin = new Thickness(0, 0, 10, 0);
            DockPanel.SetDock(control, Dock.Left);
            line.Children.Add(control);
        }

        Left(Typeset.Chrome(kind.ToUpperInvariant(), TypeScale.Meta, ThemeManager.GreyKey, weight: FontWeight.SemiBold, tracking: TypeScale.Meta * Fonts.ChromeTracking));
        Left(CopyGlyph.LinkBeside(Typeset.Chrome(system, TypeScale.Body, ThemeManager.CyanKey, weight: FontWeight.Medium), system, _copy));

        if (note is not null)
        {
            var text = Typeset.Prose(note, TypeScale.Meta, ThemeManager.CyanKey, vertical: VerticalAlignment.Center);
            DockPanel.SetDock(text, Dock.Right);
            line.Children.Add(text);
        }

        if (figure is not null)
        {
            var text = Typeset.Mono(figure, TypeScale.Small, ThemeManager.WhiteKey, vertical: VerticalAlignment.Center, margin: new Thickness(0, 0, 10, 0));
            DockPanel.SetDock(text, Dock.Right);
            line.Children.Add(text);
        }

        return line;
    }

    private Button Tile(string name, string label, string tool, bool showReply = false)
    {
        var tile = new Button { Name = name, Content = label, MinHeight = TypeScale.MinimumTarget, Padding = new Thickness(18, 0), VerticalContentAlignment = VerticalAlignment.Center };
        tile.Click += (_, _) => _ = Call(tool, new ToolArguments(new Dictionary<string, string>(StringComparer.Ordinal)), showReply);

        return tile;
    }

    private static StackPanel Tiles(Button first, Button second, string phrases)
    {
        var line = new StackPanel { Orientation = Orientation.Horizontal, Spacing = Gaps.Tile, Children = { first, second } };
        line.Children.Add(Typeset.Prose(phrases, TypeScale.Description, ThemeManager.GreyKey, wrap: false, vertical: VerticalAlignment.Center, margin: new Thickness(14, 0, 0, 0)));

        return line;
    }

    /// <summary>Calls a campaign tool as the Commander. A refusal is shown; so is the reply when <paramref name="showReply"/> is set.</summary>
    private async Task Call(string tool, ToolArguments arguments, bool showReply = false)
    {
        var result = await _sources.Tools
            .InvokeAsync(tool, arguments, CancellationToken.None, ToolCaller.Commander)
            .ConfigureAwait(true);

        _note.Text = result.IsError || showReply ? result.Content : null;
        _ = Tick();
    }

    private void FillSection()
    {
        var report = HeldReport();

        _seenWork = _sources.State()?.BgsWork;
        _seenMinute = PageKit.Minute(_now);

        _sectionSlot.Child = _section switch
        {
            CampaignModel.ChronicleSection => ChronicleSection(),
            CampaignModel.WorkSection => WorkSection(),
            _ when report is null => NoReport(),
            CampaignModel.SystemsSection => SystemsSection(),
            _ => FrontsSection(),
        };
    }

    private Control NoReport()
    {
        var panel = new StackPanel { Spacing = 20 };
        panel.Children.Add(Grey(CampaignModel.NoReportText));

        if (_snapshot?.Failure is { } failure && string.Equals(_snapshot.Faction, _faction, StringComparison.OrdinalIgnoreCase))
        {
            panel.Children.Add(Grey(failure));
        }

        return panel;
    }

    private StackPanel WorkSection()
    {
        var state = _sources.State();
        var (rows, _) = WorkNow();
        var input = new BgsWorkInput(
            rows,
            state?.BgsWork ?? BgsWork.None,
            _faction!,
            _sources.Tick(),
            _now,
            state?.Location.StarSystem,
            _query);

        var body = _work.Build(input, ref _page);
        var panel = new StackPanel { Spacing = 20, Children = { body } };

        if (_headset && _work.Count > FactionModel.PageSize)
        {
            panel.Children.Add(Pager(_work.Count));
        }

        return panel;
    }

    private StackPanel SystemsSection()
    {
        var panel = new StackPanel { Spacing = 20 };
        _systemWork = WorkNow().BySystem;

        var legend = LegendLine();

        if (!_headset)
        {
            legend.MaxWidth = ProseWidth;
            legend.HorizontalAlignment = HorizontalAlignment.Left;
        }

        var here = _sources.State()?.Location.StarSystem;
        var columns = SystemColumns;
        columns.Refresh();

        var matching = CampaignModel.Matching(_rows, _query);
        columns.Observe(_rows);
        var sorted = columns.Sort(matching);
        var (label, direction) = columns.SortedBy is { } sort
            ? (sort.Key, sort.Direction)
            : (CampaignModel.Heads(headset: false)[CampaignModel.DefaultOrder.Column].Label, CampaignModel.DefaultOrder.Direction);
        var hub = CampaignModel.Hub(sorted, label, direction, _headset);

        _drawnHere = here;
        _hubSystem = hub.System;

        panel.Children.Add(legend);
        panel.Children.Add(HubBlock(hub, here));

        if (_headset && !ShowsWork(columns))
        {
            panel.Children.Add(Typeset.Prose(CampaignModel.WorkUnderNames(_faction!, _sources.Tick()), TypeScale.Description, ThemeManager.GreyKey, wrap: true));
        }

        panel.Children.Add(LedgerTables.Arranger(columns, _rows));
        panel.Children.Add(Table(columns.Filter(sorted), columns, _rows));

        if (!_headset)
        {
            panel.Children.Add(Typeset.Prose(CampaignModel.Hint, TypeScale.Description, ThemeManager.GreyKey, wrap: true));
        }

        return panel;
    }

    private Border HubBlock(CampaignModel.HubReading hub, string? here)
    {
        var current = hub.System is not null && string.Equals(hub.System, here, StringComparison.OrdinalIgnoreCase);

        Control name = hub.System is null
            ? Typeset.Mono(CampaignModel.Dash, TypeScale.Body, ThemeManager.GreyKey)
            : Typeset.Chrome(hub.System, TypeScale.Body, current ? ThemeManager.CyanKey : ThemeManager.AKey, weight: FontWeight.Medium, trimming: TextTrimming.CharacterEllipsis);

        if (hub.System is not null && !_headset)
        {
            name = CopyGlyph.LinkBeside((TextBlock)name, hub.System, _copy);
        }

        var line = Typeset.Prose(hub.Line, TypeScale.Description, ThemeManager.GreyKey, wrap: true);
        var body = new WrapPanel { Orientation = Orientation.Horizontal };
        name.Margin = new Thickness(0, 0, 14, 0);
        name.VerticalAlignment = VerticalAlignment.Center;
        line.VerticalAlignment = VerticalAlignment.Center;
        body.Children.Add(name);
        body.Children.Add(line);

        var block = new Border
        {
            Padding = new Thickness(14, 12),
            Child = new StackPanel
            {
                Spacing = 4,
                Children =
                {
                    Typeset.Chrome(CampaignModel.HubLabel, TypeScale.Meta, ThemeManager.GreyKey, weight: FontWeight.SemiBold, tracking: TypeScale.Meta * Fonts.ChromeTracking),
                    body,
                },
            },
        };
        ThemeInk.Bind(block, Border.BackgroundProperty, ThemeManager.SlabKey);

        if (!_headset)
        {
            block.MaxWidth = ProseWidth;
            block.HorizontalAlignment = HorizontalAlignment.Left;
        }

        return block;
    }

    private Control Table(IReadOnlyList<CampaignSystemRow> sorted, ArrangedTable<CampaignSystemRow> columns, IReadOnlyList<CampaignSystemRow> all)
    {
        var table = new StackPanel { Spacing = Gaps.Tile };

        table.Children.Add(HeadOf(columns, new Thickness(15, 0, 12, 0)));

        if (sorted.Count == 0)
        {
            table.Children.Add(ColumnFilter.Empty(columns, all) ?? Grey(CampaignModel.NoSystemText));
            return Sideways(columns, table);
        }

        if (ColumnFilter.HidesRows(columns))
        {
            return Sideways(columns, table);
        }

        IReadOnlyList<CampaignSystemRow> shown = sorted;

        if (_headset)
        {
            _page = FactionModel.ClampPage(_page, sorted.Count);
            shown = [.. sorted.Skip(_page * FactionModel.PageSize).Take(FactionModel.PageSize)];
        }

        var rank = 0;

        foreach (var row in shown)
        {
            table.Children.Add(RowView(row, columns));
            rank++;

            if (rank == CampaignHub.Span && (!_headset || _page == 0) && sorted.Count > CampaignHub.Span)
            {
                table.Children.Add(FirstTenRule());
            }
        }

        if (_headset && sorted.Count > FactionModel.PageSize)
        {
            table.Children.Add(Pager(sorted.Count));
        }

        return Sideways(columns, table);
    }

    private static DockPanel FirstTenRule()
    {
        var mark = Typeset.Chrome(CampaignModel.FirstTenRule, TypeScale.MetaSmall, ThemeManager.GreyKey, weight: FontWeight.SemiBold, tracking: TypeScale.MetaSmall * Fonts.ChromeTracking);
        mark.VerticalAlignment = VerticalAlignment.Center;
        mark.Margin = new Thickness(0, 0, 10, 0);

        var rule = new Border { Height = 1, VerticalAlignment = VerticalAlignment.Center };
        ThemeInk.Bind(rule, Border.BackgroundProperty, ThemeManager.LineKey);

        var line = new DockPanel { Margin = new Thickness(0, 4) };
        DockPanel.SetDock(mark, Dock.Left);
        line.Children.Add(mark);
        line.Children.Add(rule);

        return line;
    }

    private static Border RowView(CampaignSystemRow row, ArrangedTable<CampaignSystemRow> columns)
    {
        var border = new Border
        {
            MinHeight = 50,
            Padding = new Thickness(12, 4, 12, 4),
            BorderThickness = new Thickness(3, 0, 0, 0),
            Child = columns.Row(row),
        };
        ThemeInk.Bind(border, Border.BackgroundProperty, ThemeManager.TileKey);
        ThemeInk.Bind(border, Border.BorderBrushProperty, ThemeManager.AKey);

        return border;
    }

    private static bool ShowsWork(ArrangedTable<CampaignSystemRow> columns) => columns.ShownFields.Any(field => field.Key == YourWorkKey);

    private bool IsHub(CampaignSystemRow row) =>
        _hubSystem is not null && string.Equals(row.System, _hubSystem, StringComparison.OrdinalIgnoreCase);

    private Control SystemCell(CampaignSystemRow row)
    {
        var current = string.Equals(row.System, _drawnHere, StringComparison.OrdinalIgnoreCase);
        var hub = IsHub(row);

        var name = Typeset.Chrome(
            row.System,
            TypeScale.ControlLarge,
            row.Old ? ThemeManager.GreyKey : current ? ThemeManager.CyanKey : ThemeManager.AKey,
            weight: FontWeight.Medium,
            trimming: TextTrimming.CharacterEllipsis);

        if (!_headset)
        {
            return new StackPanel { Spacing = 2, Children = { CopyGlyph.LinkBeside(name, row.System, _copy), ControlLine(row, _now, hub) } };
        }

        Control system = name;
        var under = !ShowsWork(SystemColumns) && _systemWork.TryGetValue(row.System, out var held) ? WorkStack(held, left: true) : null;

        if (hub)
        {
            var word = Typeset.Chrome(CampaignModel.HubMark, TypeScale.MetaSmall, ThemeManager.WhiteKey, weight: FontWeight.SemiBold, tracking: TypeScale.MetaSmall * Fonts.ChromeTracking);
            word.VerticalAlignment = VerticalAlignment.Center;
            word.Margin = new Thickness(8, 0, 0, 0);
            DockPanel.SetDock(word, Dock.Right);
            system = new DockPanel { Children = { word, name } };
        }

        if (under is not null)
        {
            system = new StackPanel { Spacing = 2, Children = { system, under } };
        }

        return system;
    }

    private static Control FigureCell(string text, CampaignSystemRow row) =>
        Typeset.Mono(text, TypeScale.ControlLarge, row.Old ? ThemeManager.GreyKey : ThemeManager.AKey, horizontal: HorizontalAlignment.Right, align: TextAlignment.Right);

    private static Control LeadCell(CampaignSystemRow row)
    {
        var lead = FigureCell(CampaignModel.LeadText(row.Lead), row);

        return row.Frozen
            ? new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Right,
                Spacing = 1,
                Children = { lead, Typeset.Prose("frozen", TypeScale.Meta, ThemeManager.GreyKey, horizontal: HorizontalAlignment.Right) },
            }
            : lead;
    }

    /// <summary>The work lines of one system: right aligned in the column, left aligned under the name on the headset. Grey <c>none</c> without any.</summary>
    private static StackPanel WorkStack(IReadOnlyList<ChronicleWork> work, bool left)
    {
        var align = left ? HorizontalAlignment.Left : HorizontalAlignment.Right;
        var stack = new StackPanel { Spacing = 2, HorizontalAlignment = align };

        foreach (var line in CampaignModel.WorkLines(work))
        {
            stack.Children.Add(Typeset.Mono(
                line,
                TypeScale.Meta,
                work.Count == 0 ? ThemeManager.GreyKey : ThemeManager.WhiteKey,
                horizontal: align,
                align: left ? TextAlignment.Left : TextAlignment.Right));
        }

        return stack;
    }

    private static TextBlock ControlLine(CampaignSystemRow row, DateTimeOffset now, bool hub)
    {
        var line = new TextBlock { FontFamily = new FontFamily(Fonts.ProseFamily), FontSize = TypeScale.Meta, TextWrapping = TextWrapping.Wrap };
        ThemeInk.Bind(line, TextBlock.ForegroundProperty, ThemeManager.GreyKey);
        line.Inlines!.Add(new Run(row.Controls ? "Controlled" : "Not controlled"));

        if (row.Old && row.Updated is { } at)
        {
            var age = new Run(FactionModel.Ago(now - at));
            ThemeInk.Bind(age, TextElement.ForegroundProperty, ThemeManager.WarnKey);

            var label = new Run(CampaignModel.OldLabel) { FontFamily = new FontFamily(Fonts.ChromeFamily), FontSize = TypeScale.MetaSmall };
            ThemeInk.Bind(label, TextElement.ForegroundProperty, ThemeManager.WarnKey);

            line.Inlines.Add(new Run(" · "));
            line.Inlines.Add(age);
            line.Inlines.Add(new Run(" · "));
            line.Inlines.Add(label);
        }

        if (hub)
        {
            var mark = new Run(CampaignModel.HubMark) { FontFamily = new FontFamily(Fonts.ChromeFamily), FontSize = TypeScale.MetaSmall, FontWeight = FontWeight.SemiBold };
            ThemeInk.Bind(mark, TextElement.ForegroundProperty, ThemeManager.WhiteKey);

            line.Inlines.Add(new Run(" · "));
            line.Inlines.Add(mark);
        }

        return line;
    }

    private StackPanel FrontsSection()
    {
        var panel = new StackPanel { Spacing = 20 };
        var fronts = _snapshot!.Fronts;

        if (fronts.Count == 0)
        {
            panel.Children.Add(Grey(CampaignModel.NoFrontsText));
            return panel;
        }

        var rows = CampaignModel.FrontRows(fronts, _campaign.Chosen, CampaignSystemRows.Distances(_snapshot.Report!, _sources.State()?.Location.StarPos));
        var matching = string.IsNullOrWhiteSpace(_query)
            ? rows
            : [.. rows.Where(row => row.Front.System.Contains(_query.Trim(), StringComparison.OrdinalIgnoreCase))];
        var columns = FrontColumns;
        columns.Refresh();

        columns.Observe(rows);
        var sorted = columns.Shown(matching);
        var table = new StackPanel { Spacing = Gaps.Tile };

        table.Children.Add(HeadOf(columns, new Thickness(12, 0)));

        if (sorted.Count == 0)
        {
            table.Children.Add(ColumnFilter.Empty(columns, rows) ?? Grey(CampaignModel.NoMatchingFrontText));
        }

        IReadOnlyList<CampaignModel.FrontRow> shown = sorted;

        if (_headset)
        {
            _page = FactionModel.ClampPage(_page, sorted.Count);
            shown = [.. sorted.Skip(_page * FactionModel.PageSize).Take(FactionModel.PageSize)];
        }

        if (!ColumnFilter.HidesRows(columns))
        {
            foreach (var row in shown)
            {
                table.Children.Add(RowBorder(columns.Row(row)));
            }

            if (_headset && sorted.Count > FactionModel.PageSize)
            {
                table.Children.Add(Pager(sorted.Count));
            }
        }

        panel.Children.Add(LedgerTables.Arranger(columns, rows));
        panel.Children.Add(Sideways(columns, table));

        if (!_headset)
        {
            panel.Children.Add(CappedText(Typeset.Prose(CampaignModel.FrontsHint(fronts), TypeScale.Description, ThemeManager.GreyKey, wrap: true)));
        }

        return panel;
    }

    private IReadOnlyList<TableField<CampaignModel.FrontRow>> FrontFields()
    {
        var heads = CampaignModel.FrontHeads();

        return
        [
            StarSystemPage.Headed(heads[0], Fixed("Rank", 48, 44), row => FrontMono(row, row.Rank.ToString(CultureInfo.InvariantCulture), ThemeManager.WhiteKey), right: true),
            StarSystemPage.Headed(heads[1], Fixed("Kind", 110, 120), row => Typeset.Prose(CampaignFronts.Describe(row.Front.Kind), TypeScale.ControlLarge, FrontInk(row), wrap: true))
                with { Filters = [ValueLists.One<CampaignModel.FrontRow>("Kind", row => CampaignFronts.Describe(row.Front.Kind), "No kind")] },
            StarSystemPage.Headed(heads[2], Filled("System", 150, 120), FrontSystemCell)
                with { Filters = [ValueLists.One<CampaignModel.FrontRow>("System", row => row.Front.System, "No system")] },
            StarSystemPage.Headed(heads[3], new TableColumn("Distance") { Whole = true }, row => FrontMono(row, CampaignModel.DistanceText(row.Distance), ThemeManager.AKey), right: true),
            StarSystemPage.Headed(heads[4], Filled("Reason", 190, 140), row => Typeset.Prose(row.Front.Reason, TypeScale.ControlLarge, FrontInk(row), wrap: true))
                with { Filters = [ValueLists.One<CampaignModel.FrontRow>("Reason", row => row.Front.Reason, "No reason")] },
            StarSystemPage.Headed(heads[5], Fixed("Data age", 140, 120), AgeCell, right: true),
            Added("Opponent", Filled("Opponent", 140, 120), row => TableCells.Text(row.Front.Opponent), SortColumn<CampaignModel.FrontRow>.Text("Opponent", row => row.Front.Opponent))
                with { Filters = [ValueLists.One<CampaignModel.FrontRow>("Opponent", row => row.Front.Opponent, "No opponent")] },
            Added("At stake", Filled("At stake", 140, 120), row => TableCells.Text(row.Front.Stake), SortColumn<CampaignModel.FrontRow>.Text("At stake", row => row.Front.Stake))
                with { Filters = [ValueLists.One<CampaignModel.FrontRow>("At stake", row => row.Front.Stake, "No stake")] },
            Added(
                "Lead",
                Fixed("Lead", 84, 70),
                row => row.Front.Lead is null ? TableCells.Missing() : Typeset.Mono(CampaignModel.LeadText(row.Front.Lead), TypeScale.Control, ThemeManager.WhiteKey, horizontal: HorizontalAlignment.Right, align: TextAlignment.Right),
                SortColumn<CampaignModel.FrontRow>.Figure("Lead", row => row.Front.Lead, SortDirection.Descending))
                with { Alignment = HorizontalAlignment.Right },
            Added("Intent", Fixed("Intent", 130, 130), row => TableCells.Text(CampaignModel.Label(row.Front.Intent)), SortColumn<CampaignModel.FrontRow>.Text("Intent", row => CampaignModel.Label(row.Front.Intent)))
                with { Filters = [ValueLists.One<CampaignModel.FrontRow>("Intent", row => CampaignModel.Label(row.Front.Intent), "No intent")] },
            Added(
                "Controls",
                Fixed("Controls", 96, 96),
                row => TableCells.Flag(row.Front.Controls),
                SortColumn<CampaignModel.FrontRow>.Figure<int>("Controls", row => row.Front.Controls ? 1 : 0, SortDirection.Descending))
                with { Filters = [ValueLists.Flag<CampaignModel.FrontRow>("Controls", row => row.Front.Controls, "Not known")] },
            Added(
                "States",
                Filled("States", 140, 120),
                row => TableCells.List(row.Front.States),
                SortColumn<CampaignModel.FrontRow>.Figure<int>("States", row => row.Front.States.Count, SortDirection.Descending))
                with { Filters = [ValueLists.Many<CampaignModel.FrontRow>("States", row => row.Front.States, "No state")] },
            new TableField<CampaignModel.FrontRow>("Take", Fixed("Take", 80, 96), row => Centred(TakeCell(row))),
        ];
    }

    private string FrontInk(CampaignModel.FrontRow row) => CampaignModel.IsOld(row.Front, _now) ? ThemeManager.GreyKey : ThemeManager.WhiteKey;

    private Control FrontMono(CampaignModel.FrontRow row, string text, string ink) =>
        Typeset.Mono(text, TypeScale.ControlLarge, CampaignModel.IsOld(row.Front, _now) ? ThemeManager.GreyKey : ink, horizontal: HorizontalAlignment.Right, align: TextAlignment.Right);

    private Control FrontSystemCell(CampaignModel.FrontRow row)
    {
        var front = row.Front;
        var name = Typeset.Chrome(
            front.System,
            TypeScale.ControlLarge,
            CampaignModel.IsOld(front, _now) ? ThemeManager.GreyKey : row.Chosen ? ThemeManager.CyanKey : ThemeManager.AKey,
            weight: FontWeight.Medium,
            wrap: true);

        return CopyGlyph.LinkBeside(name, front.System, _copy);
    }

    private Control AgeCell(CampaignModel.FrontRow row)
    {
        var front = row.Front;
        var old = CampaignModel.IsOld(front, _now);
        var age = new StackPanel { Spacing = 2, HorizontalAlignment = HorizontalAlignment.Right };
        age.Children.Add(Typeset.Prose(
            CampaignModel.AgeText(front, _now, SinceTickOf(front)),
            TypeScale.Meta,
            old ? ThemeManager.WarnKey : front.OwnReading ? ThemeManager.CyanKey : ThemeManager.GreyKey,
            horizontal: HorizontalAlignment.Right,
            align: TextAlignment.Right,
            wrap: true));

        if (old)
        {
            age.Children.Add(Typeset.Chrome(CampaignModel.OldLabel, TypeScale.MetaSmall, ThemeManager.WarnKey, horizontal: HorizontalAlignment.Right));
        }

        return age;
    }

    private Control TakeCell(CampaignModel.FrontRow row)
    {
        if (row.Chosen)
        {
            return Typeset.Chrome("CHOSEN", TypeScale.Small, ThemeManager.CyanKey, horizontal: HorizontalAlignment.Right, tracking: TypeScale.Small * Fonts.ChromeTracking);
        }

        var front = row.Front;
        var take = new Button { Name = TakeName(front), Content = "TAKE", MinHeight = TypeScale.MinimumTarget, HorizontalAlignment = HorizontalAlignment.Right };
        take.Click += (_, _) => _ = Call(
            CampaignCapability.TakeFrontTool,
            new ToolArguments(new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["system"] = front.System,
                ["kind"] = front.Kind.ToString(),
            }));

        return take;
    }

    private Control ChronicleSection()
    {
        var panel = new StackPanel { Spacing = 20 };

        if (_campaign.Chronicle.Count == 0)
        {
            panel.Children.Add(Grey(CampaignModel.NoChronicleText));
            return panel;
        }

        var rows = CampaignModel.ChronicleRows(_campaign);
        var matching = string.IsNullOrWhiteSpace(_query)
            ? rows
            : [.. rows.Where(row => row.Entry.System.Contains(_query.Trim(), StringComparison.OrdinalIgnoreCase))];
        var columns = ChronicleColumns;
        columns.Refresh();

        columns.Observe(rows);
        var sorted = columns.Shown(matching);
        var table = new StackPanel { Spacing = Gaps.Tile };

        table.Children.Add(HeadOf(columns, new Thickness(12, 0)));

        if (sorted.Count == 0)
        {
            table.Children.Add(ColumnFilter.Empty(columns, rows) ?? Grey(CampaignModel.NoMatchingFrontText));
        }

        IReadOnlyList<CampaignModel.ChronicleRow> shown = sorted;

        if (_headset)
        {
            _page = FactionModel.ClampPage(_page, sorted.Count);
            shown = [.. sorted.Skip(_page * FactionModel.PageSize).Take(FactionModel.PageSize)];
        }

        if (!ColumnFilter.HidesRows(columns))
        {
            foreach (var row in shown)
            {
                table.Children.Add(ChronicleRowView(row, columns));
            }

            if (_headset && sorted.Count > FactionModel.PageSize)
            {
                table.Children.Add(Pager(sorted.Count));
            }
        }

        panel.Children.Add(LedgerTables.Arranger(columns, rows));
        panel.Children.Add(Sideways(columns, table));

        if (!_headset)
        {
            panel.Children.Add(Typeset.Prose(CampaignModel.ChronicleHint, TypeScale.Description, ThemeManager.GreyKey, wrap: true));
        }

        return panel;
    }

    private IReadOnlyList<TableField<CampaignModel.ChronicleRow>> ChronicleFields()
    {
        var heads = CampaignModel.ChronicleHeads();

        return
        [
            StarSystemPage.Headed(heads[0], Fixed("Ended", 76, 70), row => Centred(Typeset.Mono(CampaignModel.EndedText(row.Ended), TypeScale.ControlLarge, ThemeManager.WhiteKey))),
            StarSystemPage.Headed(heads[1], Fixed("Kind", 110, 100), row => Typeset.Prose(row.Entry.Kind, TypeScale.ControlLarge, ThemeManager.WhiteKey))
                with { Filters = [ValueLists.One<CampaignModel.ChronicleRow>("Kind", row => row.Entry.Kind, "No kind")] },
            StarSystemPage.Headed(heads[2], Fixed("System", 150, 140), ChronicleSystemCell)
                with
                {
                    Filters =
                    [
                        ValueLists.One<CampaignModel.ChronicleRow>("System", row => row.Entry.System, "No system"),
                        ValueLists.One<CampaignModel.ChronicleRow>("Opponent", row => row.Entry.Opponent, "No opponent"),
                    ],
                },
            StarSystemPage.Headed(heads[3], Filled("Outcome", 200, 120), row => Typeset.Prose(row.Entry.Outcome, TypeScale.ControlLarge, ThemeManager.WhiteKey, wrap: true))
                with { Filters = [ValueLists.One<CampaignModel.ChronicleRow>("Outcome", row => row.Entry.Outcome, "No outcome")] },
            StarSystemPage.Headed(
                heads[4],
                Fixed("Ticks", 56, 50),
                row => Typeset.Mono(row.Entry.Ticks.Count.ToString(CultureInfo.InvariantCulture), TypeScale.ControlLarge, ThemeManager.WhiteKey, horizontal: HorizontalAlignment.Right, align: TextAlignment.Right),
                right: true),
            StarSystemPage.Headed(heads[5], Fixed("Your work", 190, 180), ChronicleWorkCell, right: true)
                with { Filters = [ValueLists.Many<CampaignModel.ChronicleRow>("Your work", row => CampaignModel.WorkKinds(row.Entry.Work), "No work")] },
        ];
    }

    private Control ChronicleSystemCell(CampaignModel.ChronicleRow row)
    {
        var entry = row.Entry;

        return new StackPanel
        {
            Spacing = 2,
            Children =
            {
                CopyGlyph.LinkBeside(Typeset.Chrome(entry.System, TypeScale.ControlLarge, ThemeManager.AKey, weight: FontWeight.Medium, trimming: TextTrimming.CharacterEllipsis), entry.System, _copy),
                Typeset.Prose(entry.Opponent ?? "No opponent", TypeScale.Meta, ThemeManager.GreyKey, trimming: TextTrimming.CharacterEllipsis),
            },
        };
    }

    private static Control ChronicleWorkCell(CampaignModel.ChronicleRow row)
    {
        var work = new StackPanel { Spacing = 2, HorizontalAlignment = HorizontalAlignment.Right };

        foreach (var line in CampaignModel.WorkLines(row.Entry))
        {
            work.Children.Add(Typeset.Mono(line, TypeScale.Meta, ThemeManager.WhiteKey, horizontal: HorizontalAlignment.Right, align: TextAlignment.Right));
        }

        return work;
    }

    private static Border ChronicleRowView(CampaignModel.ChronicleRow row, ArrangedTable<CampaignModel.ChronicleRow> columns)
    {
        var grid = columns.Row(row);

        if (string.IsNullOrWhiteSpace(row.Entry.Legend))
        {
            return RowBorder(grid);
        }

        var legend = Typeset.Prose(row.Entry.Legend, TypeScale.Description, ThemeManager.GreyKey, wrap: true);

        legend.Margin = new Thickness(0, 4, 0, 4);

        return RowBorder(new StackPanel { Spacing = 2, Children = { grid, legend } });
    }

    private static Border RowBorder(Control content)
    {
        var border = new Border { MinHeight = 50, Padding = new Thickness(12, 4, 12, 4), Child = content };
        ThemeInk.Bind(border, Border.BackgroundProperty, ThemeManager.TileKey);

        return border;
    }

    private Stepper IntentStepper(CampaignSystemRow row, string frontier)
    {
        var stepper = new Stepper
        {
            Width = 300,
            HorizontalAlignment = HorizontalAlignment.Left,
            ItemsSource = CampaignModel.Labels(row.Intent, row.IntentIsDefault),
            SelectedIndex = (int)row.Intent,
            IsInherited = row.IntentIsDefault,
        };
        AutomationProperties.SetName(stepper, StepperName(row.System));

        stepper.SelectionChanged += (_, _) =>
        {
            var intent = CampaignModel.Intents[stepper.SelectedIndex];

            // Updated in place: a redraw would replace the Stepper while its arrow is held.
            stepper.ItemsSource = CampaignModel.Labels(intent, isDefault: false);
            stepper.IsInherited = false;
            _ = SetIntent(row.System, intent, frontier);
        };

        return stepper;
    }

    private async Task SetIntent(string system, SystemIntent intent, string frontier)
    {
        var arguments = new ToolArguments(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["system"] = system,
            ["intent"] = intent.ToString(),
        });

        var result = await _sources.Tools
            .InvokeAsync(CampaignCapability.SetIntentTool, arguments, CancellationToken.None, ToolCaller.Commander)
            .ConfigureAwait(true);

        if (result.IsError)
        {
            _note.Text = result.Content;
            Draw();
            return;
        }

        if (_note.Text is not null)
        {
            _note.Text = null;
            Draw();
            return;
        }

        // The Stepper already shows the intent, so the store's change needs no redraw.
        if (frontier == _seenFrontier)
        {
            _seenIntents = _sources.Campaigns.For(frontier).Intents;
        }
    }

    private void OpenPicker(string? current)
    {
        if (_prompts is null)
        {
            return;
        }

        var squadron = _sources.SquadronFaction();
        var choices = CampaignModel.FactionChoices(squadron, current, _sources.State()?.Standings ?? SystemStandings.Empty);
        var details = choices.ToDictionary(choice => choice.Name, choice => choice.Detail, StringComparer.OrdinalIgnoreCase);

        var request = new PickerRequest
        {
            Prompt = "BGS faction",
            Help = CampaignModel.PickerHelp,
            Choices = [.. choices.Select(choice => choice.Name)],
            DetailOf = name => details.GetValueOrDefault(name),
            Current = current,
            DefaultDisplay = squadron is { Length: > 0 } ? $"your squadron's faction, {squadron}" : null,
            AllowsFreeText = true,
        };

        _prompts.Pick(PickerKey, "BGS faction", request, result =>
        {
            if ((result.Value ?? squadron) is { Length: > 0 } chosen)
            {
                _ = SetFaction(chosen);
            }
        });
    }

    private async Task SetFaction(string faction)
    {
        var arguments = new ToolArguments(new Dictionary<string, string>(StringComparer.Ordinal) { ["faction"] = faction });

        var result = await _sources.Tools
            .InvokeAsync(CampaignCapability.SetFactionTool, arguments, CancellationToken.None, ToolCaller.Commander)
            .ConfigureAwait(true);

        _note.Text = result.IsError ? result.Content : null;
        _page = 0;
        Draw();
    }

    private Grid Pager(int total)
    {
        var readout = CreditsKit.Mono(FactionModel.PageReadout(_page, total), TypeScale.Tip, ThemeManager.GreyKey, right: false);
        readout.VerticalAlignment = VerticalAlignment.Center;

        var previous = new Button { Name = PreviousName, Content = "◄ PREVIOUS", Height = TypeScale.MinimumTarget, IsEnabled = _page > 0 };
        var next = new Button
        {
            Name = NextName,
            Content = "NEXT ►",
            Height = TypeScale.MinimumTarget,
            IsEnabled = FactionModel.ClampPage(_page + 1, total) > _page,
        };
        previous.Click += (_, _) =>
        {
            _page--;
            FillSection();
        };
        next.Click += (_, _) =>
        {
            _page++;
            FillSection();
        };

        var tiles = new StackPanel { Orientation = Orientation.Horizontal, Spacing = Gaps.Tile, Children = { previous, next } };
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Margin = new Thickness(0, 8, 0, 0) };
        Grid.SetColumn(tiles, 1);
        grid.Children.Add(readout);
        grid.Children.Add(tiles);

        return grid;
    }

    private const string YourWorkKey = "Your work";

    private TableColumn Fixed(string key, double desktop, double headset) =>
        new(key, ColumnFit.Fixed) { Fixed = _headset ? headset : desktop };

    private TableColumn Filled(string key, double desktop, double headset) =>
        new(key, ColumnFit.Fill) { Min = _headset ? headset : desktop };

    private static Control Centred(Control cell)
    {
        cell.VerticalAlignment = VerticalAlignment.Center;

        return cell;
    }

    private static TableField<T> Added<T>(string key, TableColumn column, Func<T, Control> cell, SortColumn<T> sort) =>
        new(key, column, row => Centred(cell(row))) { Label = sort.Label, Sort = sort, Filters = [], ByDefault = false };

    private ArrangedTable<T> Arranged<T>(string id, IReadOnlyList<TableField<T>> fields, string headName, string sortedByDefault)
    {
        var table = new ArrangedTable<T>(id, fields, _arrangements, _headset ? null : _columnWidths, _headset ? 14 : 16, sortedByDefault, headName);
        table.Changed += FillSection;
        table.FilterChanged += () => _page = 0;

        return table;
    }

    private ArrangedTable<CampaignSystemRow> SystemColumns =>
        _systemColumns ??= Arranged(TableId, SystemFields(_headTicked), HeadName, CampaignModel.Heads(headset: false)[CampaignModel.DefaultOrder.Column].Label);

    private ArrangedTable<CampaignModel.FrontRow> FrontColumns =>
        _frontColumns ??= Arranged(FrontsTableId, FrontFields(), FrontHeadName, CampaignModel.FrontHeads()[CampaignModel.FrontOrder.Column].Label);

    private ArrangedTable<CampaignModel.ChronicleRow> ChronicleColumns =>
        _chronicleColumns ??= Arranged(ChronicleTableId, ChronicleFields(), ChronicleHeadName, CampaignModel.ChronicleHeads()[CampaignModel.ChronicleOrder.Column].Label);

    private IReadOnlyList<TableField<CampaignSystemRow>> SystemFields(bool ticked)
    {
        Func<string, int> workOn = system => _systemWork.TryGetValue(system, out var held) ? held.Count : 0;
        var desktop = CampaignModel.Heads(headset: false, ticked, workOn);
        var heads = _headset ? CampaignModel.Heads(headset: true, ticked, workOn) : desktop;
        var state = _headset ? 4 : 5;

        return
        [
            StarSystemPage.Headed(heads[0], Filled("System", 180, 120), SystemCell)
                with
                {
                    Filters =
                    [
                        ValueLists.One<CampaignSystemRow>("System", row => row.System, "No system"),
                        .. _headset ? [] : (IEnumerable<ValueList<CampaignSystemRow>>)[ValueLists.Flag<CampaignSystemRow>("Controls", row => row.Controls, "Not known")],
                    ],
                },
            StarSystemPage.Headed(heads[1], new TableColumn("Distance") { Whole = true }, row => FigureCell(CampaignModel.DistanceText(row.Distance), row), right: true),
            StarSystemPage.Headed(heads[2], Fixed("Influence", 84, 70), row => FigureCell(CampaignModel.InfluenceText(row.Influence), row), right: true),
            StarSystemPage.Headed(heads[3], Fixed("Lead", 84, 70), LeadCell, right: true),
            StarSystemPage.Headed(desktop[4], Fixed(YourWorkKey, 190, 180), row => WorkStack(_systemWork.TryGetValue(row.System, out var held) ? held : [], left: false), right: true)
                with
                {
                    ByDefault = !_headset,
                    Filters = [ValueLists.Many<CampaignSystemRow>("Your work", row => CampaignModel.WorkKinds(_systemWork.TryGetValue(row.System, out var held) ? held : []), "No work")],
                },
            StarSystemPage.Headed(heads[state], Fixed("State", 130, 110), row => Typeset.Prose(row.State, TypeScale.ControlLarge, row.Old ? ThemeManager.GreyKey : ThemeManager.WhiteKey, wrap: true))
                with { Filters = [ValueLists.One<CampaignSystemRow>("State", row => row.State, "No state")] },
            StarSystemPage.Headed(heads[state + 1], Fixed("Intent", 300, 300), row => IntentStepper(row, _frontier!)),
            Added(
                "Updated",
                Fixed("Updated", 110, 110),
                row => row.Updated is { } at
                    ? Typeset.Mono(at.UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), TypeScale.Control, ThemeManager.WhiteKey)
                    : TableCells.Missing(),
                SortColumn<CampaignSystemRow>.Figure<DateTimeOffset>("Updated", row => row.Updated, SortDirection.Descending)),
        ];
    }

    /// <summary>The header row; in the headset it is inset to exactly the width of the rows it heads, so both report one width to the table.</summary>
    private TableGrid HeadOf<T>(ArrangedTable<T> columns, Thickness rowInset)
    {
        var header = columns.Header();
        header.MinHeight = TypeScale.MinimumTarget;
        header.Margin = _headset ? rowInset : new Thickness(14, 0);

        return header;
    }

    /// <summary>On the desktop a table wider than the page scrolls sideways, header and rows together.</summary>
    private Control Sideways<T>(ArrangedTable<T> columns, Control table) =>
        _headset ? table : new TableScroll(columns.Layout, table);

    private static long Hour(DateTimeOffset now) => now.ToUnixTimeSeconds() / 3600;

    private static void Place(Grid grid, int column, Control cell)
    {
        Grid.SetColumn(cell, column);
        grid.Children.Add(cell);
    }
}
