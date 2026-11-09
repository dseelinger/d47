using System.Collections.Immutable;
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
/// Commander › Campaign: the campaign faction, the front being worked or proposed, and the Fronts, Systems and
/// Chronicle sections. A step, a tile and the faction picker call the same protected tools as the phrases, as the Commander.
/// </summary>
public sealed class CampaignPage : UserControl, IPageSummary, IFilterablePage
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

    /// <summary>The automation name of a row's Stepper.</summary>
    public static string StepperName(string system) => $"Intent {system}";

    /// <summary>The name of the TAKE tile on a front's row.</summary>
    public static string TakeName(Front front) => $"CampaignTake.{front.Kind}.{front.System}";

    private static readonly TableColumn[] DesktopColumns =
    [
        new("System", ColumnFit.Fill) { Min = 180 },
        new("Influence", ColumnFit.Fixed) { Fixed = 84 },
        new("Lead", ColumnFit.Fixed) { Fixed = 84 },
        new("State", ColumnFit.Fixed) { Fixed = 130 },
        new("Intent", ColumnFit.Fixed) { Fixed = 300 },
    ];

    private static readonly TableColumn[] HeadsetColumns =
    [
        new("System", ColumnFit.Fill) { Min = 150 },
        new("Influence", ColumnFit.Fixed) { Fixed = 70 },
        new("Lead", ColumnFit.Fixed) { Fixed = 70 },
        new("State", ColumnFit.Fixed) { Fixed = 110 },
        new("Intent", ColumnFit.Fixed) { Fixed = 300 },
    ];

    private static readonly TableColumn[] FrontColumns =
    [
        new("Rank", ColumnFit.Fixed) { Fixed = 48 },
        new("Kind", ColumnFit.Fixed) { Fixed = 140 },
        new("System", ColumnFit.Fill) { Min = 150 },
        new("Reason", ColumnFit.Fill) { Min = 190 },
        new("Data age", ColumnFit.Fixed) { Fixed = 140 },
        new("Take", ColumnFit.Fixed) { Fixed = 90 },
    ];

    private static readonly TableColumn[] HeadsetFrontColumns =
    [
        new("Rank", ColumnFit.Fixed) { Fixed = 44 },
        new("Kind", ColumnFit.Fixed) { Fixed = 120 },
        new("System", ColumnFit.Fill) { Min = 140 },
        new("Reason", ColumnFit.Fill) { Min = 180 },
        new("Data age", ColumnFit.Fixed) { Fixed = 120 },
        new("Take", ColumnFit.Fixed) { Fixed = 96 },
    ];

    private static readonly TableColumn[] ChronicleColumns =
    [
        new("Ended", ColumnFit.Fixed) { Fixed = 76 },
        new("Kind", ColumnFit.Fixed) { Fixed = 110 },
        new("System", ColumnFit.Fixed) { Fixed = 150 },
        new("Outcome", ColumnFit.Fill) { Min = 200 },
        new("Ticks", ColumnFit.Fixed) { Fixed = 56 },
        new("Your work", ColumnFit.Fixed) { Fixed = 190 },
    ];

    private static readonly TableColumn[] HeadsetChronicleColumns =
    [
        new("Ended", ColumnFit.Fixed) { Fixed = 70 },
        new("Kind", ColumnFit.Fixed) { Fixed = 100 },
        new("System", ColumnFit.Fixed) { Fixed = 140 },
        new("Outcome", ColumnFit.Fill) { Min = 120 },
        new("Ticks", ColumnFit.Fixed) { Fixed = 50 },
        new("Your work", ColumnFit.Fixed) { Fixed = 180 },
    ];

    private readonly CampaignSources _sources;
    private readonly PanelPrompts? _prompts;
    private readonly bool _headset;
    private readonly ColumnWidthMemory? _columnWidths;
    private readonly Func<string, Task<bool>>? _copy;
    private readonly SortHeader<CampaignSystemRow> _head;
    private readonly SortHeader<CampaignModel.FrontRow> _frontHead;
    private readonly SortHeader<CampaignModel.ChronicleRow> _chronicleHead;
    private readonly StackPanel _body = new() { Spacing = 20 };
    private readonly ScrollViewer _scroll;
    private readonly Segment _segment;
    private readonly Border _noticeSlot = new();
    private readonly Border _frontSlot = new();
    private readonly Border _sectionSlot = new();

    private object? _seenSnapshot;
    private string? _seenCampaignFaction;
    private ImmutableDictionary<string, SystemIntent>? _seenIntents;
    private SystemStandings? _seenStandings;
    private string? _seenSquadron;
    private string? _seenSystem;
    private string? _seenFrontier;
    private DateTimeOffset? _seenTick;
    private long _seenHour;
    private string? _seenNote;
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
        CampaignNote? note = null)
    {
        _sources = sources;
        _note = note ?? new CampaignNote();
        _prompts = prompts;
        _headset = headset;
        _columnWidths = columnWidths;
        _copy = headset ? null : copy;
        _head = new SortHeader<CampaignSystemRow>(HeadName, CampaignModel.Heads(headset), CampaignModel.DefaultOrder);
        _head.Changed += FillSection;
        _frontHead = new SortHeader<CampaignModel.FrontRow>(FrontHeadName, CampaignModel.FrontHeads(), CampaignModel.FrontOrder);
        _frontHead.Changed += FillSection;
        _chronicleHead = new SortHeader<CampaignModel.ChronicleRow>(ChronicleHeadName, CampaignModel.ChronicleHeads(), CampaignModel.ChronicleOrder);
        _chronicleHead.Changed += FillSection;

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
            _section = _segment.SelectedIndex;
            _page = 0;
            FillSection();
        };

        Margin = new Thickness(14);
        _scroll = LoadoutPages.Scrolling(_body);
        Content = _scroll;
        Draw();
    }

    public string Summary { get; private set; } = string.Empty;

    public event EventHandler? SummaryChanged;

    public bool Filters => _reportShown || _campaign.Chronicle.Count > 0;

    public string FilterPlaceholder => "Search system";

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

        if (!FrontsKeyNow().Same(_seenFronts))
        {
            DrawFronts();
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
        _seenIntents = _campaign.Intents;
        _seenStandings = state?.Standings;
        _seenSquadron = squadron;
        _seenSystem = state?.Location.StarSystem;
        _seenFrontier = _frontier;
        _seenTick = tick;
        _seenHour = Hour(_now);
        _seenNote = _note.Text;

        var report = HeldReport();

        _reportShown = report is not null;
        _rows = report is null ? [] : CampaignSystemRows.Of(report, state?.Standings ?? SystemStandings.Empty, _campaign.Intents, _now);

        var summary = _faction is null
            ? "Choose the faction your campaign works for."
            : _headset
                ? $"Your campaign for {_faction}."
                : $"Your campaign for {_faction}: the front you chose, and what to do next.";

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

        var column = new StackPanel { Spacing = 20, MaxWidth = _headset ? double.PositiveInfinity : 900, HorizontalAlignment = HorizontalAlignment.Left };
        _body.Children.Add(column);

        column.Children.Add(_noticeSlot);
        column.Children.Add(Frame(_faction, report is null ? null : _rows, report is null ? null : _snapshot, tick, _frontier));

        if (_note.Text is { } message)
        {
            var line = Typeset.Prose(message, TypeScale.Description, ThemeManager.WarnKey, wrap: true);
            line.Name = MessageName;
            column.Children.Add(line);
        }

        _noticeSlot.Child = null;
        _frontSlot.Child = null;
        _sectionSlot.Child = null;

        if (_faction is null)
        {
            column.Children.Add(Grey(CampaignModel.NoFactionText));
            _seenFronts = FrontsKeyNow();
            return;
        }

        column.Children.Add(_frontSlot);
        column.Children.Add(_segment);
        column.Children.Add(_sectionSlot);

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

    private Control Frame(string? faction, IReadOnlyList<CampaignSystemRow>? rows, CampaignFrontsSnapshot? snapshot, DateTimeOffset? tick, string? frontier)
    {
        var grid = new Grid { ColumnSpacing = Gaps.Tile, ColumnDefinitions = new ColumnDefinitions("*,*,*") };
        var failed = snapshot?.Failure is not null;

        Place(grid, 0, Datum("Campaign faction", Typeset.Chrome(faction ?? "None", TypeScale.Body, faction is null ? ThemeManager.GreyKey : ThemeManager.WhiteKey, weight: FontWeight.Medium, trimming: TextTrimming.CharacterEllipsis)));
        Place(grid, 1, Datum("Its systems", Typeset.Mono(rows is null ? CampaignModel.Dash : CampaignModel.SystemsText(rows), TypeScale.Small, ThemeManager.WhiteKey)));
        _computed = Typeset.Mono(CampaignModel.ComputedText(snapshot, tick), TypeScale.Small, failed ? ThemeManager.WarnKey : ThemeManager.WhiteKey);
        Place(grid, 2, Datum("Fronts computed", _computed));

        var change = new Button
        {
            Name = ChangeName,
            Content = faction is null ? "CHOOSE FACTION" : "CHANGE FACTION",
            MinHeight = TypeScale.MinimumTarget,
            VerticalAlignment = VerticalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Center,
            Padding = new Thickness(18, 0),
            IsEnabled = frontier is { Length: > 0 } && _prompts is not null,
        };
        change.Click += (_, _) => OpenPicker(faction);

        var line = new Grid { ColumnSpacing = Gaps.Tile, ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        Grid.SetColumn(change, 1);
        line.Children.Add(grid);
        line.Children.Add(change);

        return line;
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

    private static TextBlock Grey(string text) => Typeset.Prose(text, TypeScale.Body, ThemeManager.GreyKey, wrap: true);

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
        var counts = CampaignModel.SectionCounts(report is null ? 0 : _snapshot!.Fronts.Count, report is null ? 0 : _rows.Count, _campaign.Chronicle.Count);

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
            slab.Children.Add(FrontLine(CampaignFronts.Describe(front.Kind), front.System, CampaignModel.DaysWonText(front), CampaignModel.DaysWonText(front) is null ? null : CampaignModel.DaysWonNote(front, _now)));
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
        Left(CopyGlyph.Beside(Typeset.Chrome(system, TypeScale.Body, ThemeManager.CyanKey, weight: FontWeight.Medium), system, _copy));

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

        _sectionSlot.Child = _section switch
        {
            CampaignModel.ChronicleSection => ChronicleSection(),
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

    private StackPanel SystemsSection()
    {
        var panel = new StackPanel { Spacing = 20 };

        panel.Children.Add(LegendLine());
        panel.Children.Add(Table(_now, _sources.State()?.Location.StarSystem, _frontier!));

        if (!_headset)
        {
            panel.Children.Add(Typeset.Prose(CampaignModel.Hint, TypeScale.Description, ThemeManager.GreyKey, wrap: true));
        }

        return panel;
    }

    private Control Table(DateTimeOffset now, string? here, string frontier)
    {
        var sorted = _head.Sort(CampaignModel.Matching(_rows, _query));
        var layout = new TableLayout(TableId, _headset ? HeadsetColumns : DesktopColumns, _headset ? null : _columnWidths, _headset ? 14 : 16);
        var table = new StackPanel { Spacing = Gaps.Tile };

        table.Children.Add(CreditsKit.HeaderRow(_head, layout, i => i is 1 or 2 ? HorizontalAlignment.Right : HorizontalAlignment.Left));

        if (sorted.Count == 0)
        {
            table.Children.Add(Grey(CampaignModel.NoSystemText));
            return table;
        }

        IReadOnlyList<CampaignSystemRow> shown = sorted;

        if (_headset)
        {
            _page = FactionModel.ClampPage(_page, sorted.Count);
            shown = [.. sorted.Skip(_page * FactionModel.PageSize).Take(FactionModel.PageSize)];
        }

        foreach (var row in shown)
        {
            table.Children.Add(RowView(row, layout, now, here, frontier));
        }

        if (_headset && sorted.Count > FactionModel.PageSize)
        {
            table.Children.Add(Pager(sorted.Count));
        }

        return table;
    }

    private Border RowView(CampaignSystemRow row, TableLayout layout, DateTimeOffset now, string? here, string frontier)
    {
        var grid = layout.Row([]);
        var ink = row.Old ? ThemeManager.GreyKey : ThemeManager.WhiteKey;
        var figures = row.Old ? ThemeManager.GreyKey : ThemeManager.AKey;
        var current = string.Equals(row.System, here, StringComparison.OrdinalIgnoreCase);

        var name = Typeset.Chrome(
            row.System,
            TypeScale.ControlLarge,
            row.Old ? ThemeManager.GreyKey : current ? ThemeManager.CyanKey : ThemeManager.AKey,
            weight: FontWeight.Medium,
            trimming: TextTrimming.CharacterEllipsis);

        Control system = _headset
            ? name
            : new StackPanel { Spacing = 2, Children = { CopyGlyph.Beside(name, row.System, _copy), ControlLine(row, now) } };

        var lead = Typeset.Mono(CampaignModel.LeadText(row.Lead), TypeScale.ControlLarge, figures, horizontal: HorizontalAlignment.Right, align: TextAlignment.Right);
        Control leadCell = row.Frozen
            ? new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Right,
                Spacing = 1,
                Children = { lead, Typeset.Prose("frozen", TypeScale.Meta, ThemeManager.GreyKey, horizontal: HorizontalAlignment.Right) },
            }
            : lead;

        CreditsKit.Place(grid, 0, system);
        CreditsKit.Place(grid, 1, Typeset.Mono(CampaignModel.InfluenceText(row.Influence), TypeScale.ControlLarge, figures, horizontal: HorizontalAlignment.Right, align: TextAlignment.Right));
        CreditsKit.Place(grid, 2, leadCell);
        CreditsKit.Place(grid, 3, Typeset.Prose(row.State, TypeScale.ControlLarge, ink, wrap: true));
        CreditsKit.Place(grid, 4, IntentStepper(row, frontier));

        var border = new Border
        {
            MinHeight = 50,
            Padding = new Thickness(12, 4, 12, 4),
            BorderThickness = new Thickness(3, 0, 0, 0),
            Child = grid,
        };
        ThemeInk.Bind(border, Border.BackgroundProperty, ThemeManager.TileKey);
        ThemeInk.Bind(border, Border.BorderBrushProperty, ThemeManager.AKey);

        return border;
    }

    private static TextBlock ControlLine(CampaignSystemRow row, DateTimeOffset now)
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

        var rows = CampaignModel.FrontRows(fronts, _campaign.Chosen);
        var matching = string.IsNullOrWhiteSpace(_query)
            ? rows
            : [.. rows.Where(row => row.Front.System.Contains(_query.Trim(), StringComparison.OrdinalIgnoreCase))];
        var sorted = _frontHead.Sort(matching);
        var layout = new TableLayout(FrontsTableId, _headset ? HeadsetFrontColumns : FrontColumns, _headset ? null : _columnWidths, _headset ? 14 : 16);
        var table = new StackPanel { Spacing = Gaps.Tile };

        table.Children.Add(CreditsKit.HeaderRow(_frontHead, layout, i => i is 0 or 4 ? HorizontalAlignment.Right : HorizontalAlignment.Left));

        if (sorted.Count == 0)
        {
            table.Children.Add(Grey(CampaignModel.NoMatchingFrontText));
        }

        IReadOnlyList<CampaignModel.FrontRow> shown = sorted;

        if (_headset)
        {
            _page = FactionModel.ClampPage(_page, sorted.Count);
            shown = [.. sorted.Skip(_page * FactionModel.PageSize).Take(FactionModel.PageSize)];
        }

        foreach (var row in shown)
        {
            table.Children.Add(FrontRowView(row, layout));
        }

        if (_headset && sorted.Count > FactionModel.PageSize)
        {
            table.Children.Add(Pager(sorted.Count));
        }

        panel.Children.Add(table);

        if (!_headset)
        {
            panel.Children.Add(Typeset.Prose(CampaignModel.FrontsHint(fronts), TypeScale.Description, ThemeManager.GreyKey, wrap: true));
        }

        return panel;
    }

    private Border FrontRowView(CampaignModel.FrontRow row, TableLayout layout)
    {
        var front = row.Front;
        var old = CampaignModel.IsOld(front, _now);
        var grid = layout.Row([]);
        var ink = old ? ThemeManager.GreyKey : ThemeManager.WhiteKey;

        var name = Typeset.Chrome(
            front.System,
            TypeScale.ControlLarge,
            old ? ThemeManager.GreyKey : row.Chosen ? ThemeManager.CyanKey : ThemeManager.AKey,
            weight: FontWeight.Medium,
            trimming: TextTrimming.CharacterEllipsis);

        var age = new StackPanel { Spacing = 2, HorizontalAlignment = HorizontalAlignment.Right };
        age.Children.Add(Typeset.Prose(
            CampaignModel.AgeText(front, _now),
            TypeScale.Meta,
            old ? ThemeManager.WarnKey : front.OwnReading ? ThemeManager.CyanKey : ThemeManager.GreyKey,
            horizontal: HorizontalAlignment.Right,
            align: TextAlignment.Right,
            wrap: true));

        if (old)
        {
            age.Children.Add(Typeset.Chrome(CampaignModel.OldLabel, TypeScale.MetaSmall, ThemeManager.WarnKey, horizontal: HorizontalAlignment.Right));
        }

        Control action;

        if (row.Chosen)
        {
            action = Typeset.Chrome("CHOSEN", TypeScale.Small, ThemeManager.CyanKey, horizontal: HorizontalAlignment.Right, tracking: TypeScale.Small * Fonts.ChromeTracking);
        }
        else
        {
            var take = new Button { Name = TakeName(front), Content = "TAKE", MinHeight = TypeScale.MinimumTarget, HorizontalAlignment = HorizontalAlignment.Right };
            take.Click += (_, _) => _ = Call(
                CampaignCapability.TakeFrontTool,
                new ToolArguments(new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["system"] = front.System,
                    ["kind"] = front.Kind.ToString(),
                }));
            action = take;
        }

        CreditsKit.Place(grid, 0, Typeset.Mono(row.Rank.ToString(System.Globalization.CultureInfo.InvariantCulture), TypeScale.ControlLarge, old ? ThemeManager.GreyKey : ThemeManager.WhiteKey, horizontal: HorizontalAlignment.Right, align: TextAlignment.Right));
        CreditsKit.Place(grid, 1, Typeset.Prose(CampaignFronts.Describe(front.Kind), TypeScale.ControlLarge, ink, wrap: true));
        CreditsKit.Place(grid, 2, CopyGlyph.Beside(name, front.System, _copy));
        CreditsKit.Place(grid, 3, Typeset.Prose(front.Reason, TypeScale.ControlLarge, old ? ThemeManager.GreyKey : ThemeManager.WhiteKey, wrap: true));
        CreditsKit.Place(grid, 4, age);
        CreditsKit.Place(grid, 5, action);

        return RowBorder(grid);
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
        var sorted = _chronicleHead.Sort(matching);
        var layout = new TableLayout(ChronicleTableId, _headset ? HeadsetChronicleColumns : ChronicleColumns, _headset ? null : _columnWidths, _headset ? 14 : 16);
        var table = new StackPanel { Spacing = Gaps.Tile };

        table.Children.Add(CreditsKit.HeaderRow(_chronicleHead, layout, i => i is 4 or 5 ? HorizontalAlignment.Right : HorizontalAlignment.Left));

        if (sorted.Count == 0)
        {
            table.Children.Add(Grey(CampaignModel.NoMatchingFrontText));
        }

        IReadOnlyList<CampaignModel.ChronicleRow> shown = sorted;

        if (_headset)
        {
            _page = FactionModel.ClampPage(_page, sorted.Count);
            shown = [.. sorted.Skip(_page * FactionModel.PageSize).Take(FactionModel.PageSize)];
        }

        foreach (var row in shown)
        {
            table.Children.Add(ChronicleRowView(row, layout));
        }

        if (_headset && sorted.Count > FactionModel.PageSize)
        {
            table.Children.Add(Pager(sorted.Count));
        }

        panel.Children.Add(table);

        if (!_headset)
        {
            panel.Children.Add(Typeset.Prose(CampaignModel.ChronicleHint, TypeScale.Description, ThemeManager.GreyKey, wrap: true));
        }

        return panel;
    }

    private Border ChronicleRowView(CampaignModel.ChronicleRow row, TableLayout layout)
    {
        var entry = row.Entry;
        var grid = layout.Row([]);

        var system = new StackPanel
        {
            Spacing = 2,
            Children =
            {
                CopyGlyph.Beside(Typeset.Chrome(entry.System, TypeScale.ControlLarge, ThemeManager.AKey, weight: FontWeight.Medium, trimming: TextTrimming.CharacterEllipsis), entry.System, _copy),
                Typeset.Prose(entry.Opponent ?? "No opponent", TypeScale.Meta, ThemeManager.GreyKey, trimming: TextTrimming.CharacterEllipsis),
            },
        };

        var work = new StackPanel { Spacing = 2, HorizontalAlignment = HorizontalAlignment.Right };

        foreach (var line in CampaignModel.WorkLines(entry))
        {
            work.Children.Add(Typeset.Mono(line, TypeScale.Meta, ThemeManager.WhiteKey, horizontal: HorizontalAlignment.Right, align: TextAlignment.Right));
        }

        CreditsKit.Place(grid, 0, Typeset.Mono(CampaignModel.EndedText(row.Ended), TypeScale.ControlLarge, ThemeManager.WhiteKey));
        CreditsKit.Place(grid, 1, Typeset.Prose(entry.Kind, TypeScale.ControlLarge, ThemeManager.WhiteKey));
        CreditsKit.Place(grid, 2, system);
        CreditsKit.Place(grid, 3, Typeset.Prose(entry.Outcome, TypeScale.ControlLarge, ThemeManager.WhiteKey, wrap: true));
        CreditsKit.Place(grid, 4, Typeset.Mono(entry.Ticks.Count.ToString(System.Globalization.CultureInfo.InvariantCulture), TypeScale.ControlLarge, ThemeManager.WhiteKey, horizontal: HorizontalAlignment.Right, align: TextAlignment.Right));
        CreditsKit.Place(grid, 5, work);

        if (string.IsNullOrWhiteSpace(entry.Legend))
        {
            return RowBorder(grid);
        }

        var bordered = RowBorder(grid);
        var legend = Typeset.Prose(entry.Legend, TypeScale.Description, ThemeManager.GreyKey, wrap: true);

        legend.Margin = new Thickness(0, 4, 0, 4);
        bordered.Child = new StackPanel { Spacing = 2, Children = { grid, legend } };

        return bordered;
    }

    private static Border RowBorder(Grid grid)
    {
        var border = new Border { MinHeight = 50, Padding = new Thickness(12, 4, 12, 4), Child = grid };
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
            Prompt = "Campaign faction",
            Help = CampaignModel.PickerHelp,
            Choices = [.. choices.Select(choice => choice.Name)],
            DetailOf = name => details.GetValueOrDefault(name),
            Current = current,
            DefaultDisplay = squadron is { Length: > 0 } ? $"your squadron's faction, {squadron}" : null,
            AllowsFreeText = true,
        };

        _prompts.Pick(PickerKey, "Campaign faction", request, result =>
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

    private static long Hour(DateTimeOffset now) => now.ToUnixTimeSeconds() / 3600;

    private static void Place(Grid grid, int column, Control cell)
    {
        Grid.SetColumn(cell, column);
        grid.Children.Add(cell);
    }
}
