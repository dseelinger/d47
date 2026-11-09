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
}

/// <summary>The message the Campaign page shows after a failed change, kept across the page being rebuilt.</summary>
public sealed class CampaignNote
{
    public string? Text { get; set; }
}

/// <summary>
/// Commander › Campaign: the campaign faction, its systems and an intent Stepper for each. A step and the faction
/// picker call the same protected tools as the phrases, as the Commander.
/// </summary>
public sealed class CampaignPage : UserControl, IPageSummary, IFilterablePage
{
    public const string RootKey = "campaign";

    public const string PickerKey = "campaign.faction";

    public const string ChangeName = "CampaignChangeFaction";
    public const string HeadName = "CampaignHead";
    public const string TableId = "Commander.Campaign";
    public const string PreviousName = "CampaignPrevious";
    public const string NextName = "CampaignNext";
    public const string MessageName = "CampaignMessage";

    /// <summary>The automation name of a row's Stepper.</summary>
    public static string StepperName(string system) => $"Intent {system}";

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

    private readonly CampaignSources _sources;
    private readonly PanelPrompts? _prompts;
    private readonly bool _headset;
    private readonly ColumnWidthMemory? _columnWidths;
    private readonly Func<string, Task<bool>>? _copy;
    private readonly SortHeader<CampaignSystemRow> _head;
    private readonly StackPanel _body = new() { Spacing = 20 };
    private readonly ScrollViewer _scroll;

    private object? _seenSnapshot;
    private Campaign? _seenCampaign;
    private SystemStandings? _seenStandings;
    private string? _seenSquadron;
    private string? _seenSystem;
    private string? _seenFrontier;
    private DateTimeOffset? _seenTick;
    private long _seenHour;
    private string? _seenNote;
    private string _query = string.Empty;
    private readonly CampaignNote _note;
    private TextBlock? _computed;
    private bool _reportShown;
    private int _page;
    private IReadOnlyList<CampaignSystemRow> _rows = [];

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
        _head.Changed += Draw;

        Margin = new Thickness(14);
        _scroll = LoadoutPages.Scrolling(_body);
        Content = _scroll;
        Draw();
    }

    public string Summary { get; private set; } = string.Empty;

    public event EventHandler? SummaryChanged;

    public bool Filters => _rows.Count > 0;

    public string FilterPlaceholder => "Search system";

    public void Filter(string? query)
    {
        if (PageKit.Retarget(query, ref _query))
        {
            _page = 0;
            Draw();
        }
    }

    /// <summary>
    /// Redraws when the report, the campaign, the readings, the squadron, the tick, the current system, the message or
    /// the hour has changed; otherwise updates only the time the fronts were computed.
    /// </summary>
    public bool Tick()
    {
        var state = _sources.State();
        var frontier = state?.Identity.FrontierId;
        var tick = _sources.Tick();

        if (Equals(Drawn(_sources.Fronts()), _seenSnapshot)
            && ReferenceEquals(_sources.Campaigns.For(frontier), _seenCampaign)
            && ReferenceEquals(state?.Standings, _seenStandings)
            && _sources.SquadronFaction() == _seenSquadron
            && state?.Location.StarSystem == _seenSystem
            && frontier == _seenFrontier
            && tick == _seenTick
            && _note.Text == _seenNote
            && Hour(_sources.Now()) == _seenHour)
        {
            var computed = CampaignModel.ComputedText(_reportShown ? _sources.Fronts() : null, tick);

            if (_computed is null || _computed.Text == computed)
            {
                return false;
            }

            _computed.Text = computed;
            return true;
        }

        Draw();
        return true;
    }

    private void Draw()
    {
        var state = _sources.State();
        var frontier = state?.Identity.FrontierId;
        var now = _sources.Now();
        var snapshot = _sources.Fronts();
        var campaign = _sources.Campaigns.For(frontier);
        var squadron = _sources.SquadronFaction();
        var tick = _sources.Tick();

        _seenSnapshot = Drawn(snapshot);
        _seenCampaign = campaign;
        _seenStandings = state?.Standings;
        _seenSquadron = squadron;
        _seenSystem = state?.Location.StarSystem;
        _seenFrontier = frontier;
        _seenTick = tick;
        _seenHour = Hour(now);
        _seenNote = _note.Text;

        var faction = frontier is { Length: > 0 } ? campaign.FactionOr(squadron) : null;
        var report = faction is not null
            && snapshot is { Report: { } held }
            && string.Equals(snapshot.FrontierId, frontier, StringComparison.Ordinal)
            && string.Equals(snapshot.Faction, faction, StringComparison.OrdinalIgnoreCase)
                ? held
                : null;

        _reportShown = report is not null;
        _rows = report is null ? [] : CampaignSystemRows.Of(report, state?.Standings ?? SystemStandings.Empty, campaign.Intents, now);

        var summary = faction is null
            ? "Choose the faction your campaign works for."
            : $"Your campaign for {faction}: what you mean to do in each of its systems.";

        if (summary != Summary)
        {
            Summary = summary;
            SummaryChanged?.Invoke(this, EventArgs.Empty);
        }

        _body.Children.Clear();

        var column = new StackPanel { Spacing = 20, MaxWidth = _headset ? double.PositiveInfinity : 900, HorizontalAlignment = HorizontalAlignment.Left };
        _body.Children.Add(column);

        column.Children.Add(Frame(faction, report is null ? null : _rows, snapshot is not null && report is not null ? snapshot : null, tick, frontier));

        if (_note.Text is { } message)
        {
            var line = Typeset.Prose(message, TypeScale.Description, ThemeManager.WarnKey, wrap: true);
            line.Name = MessageName;
            column.Children.Add(line);
        }

        if (faction is null)
        {
            column.Children.Add(Grey(CampaignModel.NoFactionText));
            return;
        }

        if (report is null)
        {
            column.Children.Add(Grey(CampaignModel.NoReportText));

            if (snapshot?.Failure is { } failure && string.Equals(snapshot.Faction, faction, StringComparison.OrdinalIgnoreCase))
            {
                column.Children.Add(Grey(failure));
            }

            return;
        }

        column.Children.Add(LegendLine());
        column.Children.Add(Table(now, state?.Location.StarSystem, frontier!));

        if (!_headset)
        {
            column.Children.Add(Typeset.Prose(CampaignModel.Hint, TypeScale.Description, ThemeManager.GreyKey, wrap: true));
        }
    }

    /// <summary>The parts of the snapshot the rows are drawn from; the fronts and the time they were computed are not.</summary>
    private static object? Drawn(CampaignFrontsSnapshot? snapshot) =>
        snapshot is null ? null : (snapshot.FrontierId, snapshot.Faction, snapshot.Report, snapshot.ReportFetchedAt, snapshot.Failure);

    private Control Frame(string? faction, IReadOnlyList<CampaignSystemRow>? rows, CampaignFrontsSnapshot? snapshot, DateTimeOffset? tick, string? frontier)
    {
        var grid = new Grid { ColumnSpacing = Gaps.Tile, ColumnDefinitions = new ColumnDefinitions("*,*,*") };

        Place(grid, 0, Datum("Campaign faction", Typeset.Chrome(faction ?? "None", TypeScale.Body, faction is null ? ThemeManager.GreyKey : ThemeManager.WhiteKey, weight: FontWeight.Medium, trimming: TextTrimming.CharacterEllipsis)));
        Place(grid, 1, Datum("Its systems", Typeset.Mono(rows is null ? CampaignModel.Dash : CampaignModel.SystemsText(rows), TypeScale.Small, ThemeManager.WhiteKey)));
        _computed = Typeset.Mono(CampaignModel.ComputedText(snapshot, tick), TypeScale.Small, ThemeManager.WhiteKey);
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

            var label = new Run("OLD REPORT") { FontFamily = new FontFamily(Fonts.ChromeFamily), FontSize = TypeScale.MetaSmall };
            ThemeInk.Bind(label, TextElement.ForegroundProperty, ThemeManager.WarnKey);

            line.Inlines.Add(new Run(" · "));
            line.Inlines.Add(age);
            line.Inlines.Add(new Run(" · "));
            line.Inlines.Add(label);
        }

        return line;
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
            _seenCampaign = _sources.Campaigns.For(frontier);
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
            Draw();
        };
        next.Click += (_, _) =>
        {
            _page++;
            Draw();
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
