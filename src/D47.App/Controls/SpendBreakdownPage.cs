using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using D47.App.Panel;
using D47.App.Theming;
using D47.Core.Capabilities;
using D47.Core.Conversation;

namespace D47.App.Controls;

/// <summary>What D47's estimate went on in one window, by purpose and charge by charge: the page SPEND BREAKDOWN opens.</summary>
public sealed class SpendBreakdownPage : DialogPage, IPageSummary
{
    public const string TableId = "Spend.ByPurpose";

    public const string ChargeTableId = "Spend.EveryCharge";

    public const string HeadName = "SpendPurposeHead";

    public const string ChargeHeadName = "SpendChargeHead";

    public const string PreviousName = "SpendChargesPrevious";

    public const string NextName = "SpendChargesNext";

    private readonly SpendLedger _ledger;
    private readonly TimeZoneInfo _zone;
    private readonly SortHeader<SpendPurposeShare> _head = new(HeadName, SpendBreakdownModel.PurposeColumns, SpendBreakdownModel.ByDefault);
    private readonly SortHeader<SpendEntry> _chargeHead = new(ChargeHeadName, SpendBreakdownModel.ChargeColumns, SpendBreakdownModel.ChargesByDefault);
    private readonly SpendChargeList _charges = new();
    private readonly Stepper _window;
    private readonly Border _figures = new() { MinHeight = TypeScale.MinimumTarget, Padding = new Thickness(14, 0) };
    private readonly Border _body = new() { Margin = new Thickness(0, 12, 0, 0) };
    private int _section = SpendBreakdownModel.ByPurposeSection;
    private bool _drawn;
    private double _barWidth;
    private ColumnWidthMemory? _widths;

    public SpendBreakdownPage(SpendLedger ledger, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(ledger);
        ArgumentNullException.ThrowIfNull(zone);

        _ledger = ledger;
        _zone = zone;
        Title = "Spend breakdown";
        Loaded += (_, _) => Refit();

        var names = Windows().Select(window => window.Name).ToList();

        _window = new Stepper
        {
            Name = "SpendWindow",
            ItemsSource = names,
            SelectedIndex = Math.Max(0, names.IndexOf(SpendBreakdownModel.OpeningWindow)),
            Width = Stepper.MaximumWidth,
            VerticalAlignment = VerticalAlignment.Top,
        };
        _window.SelectionChanged += (_, _) =>
        {
            _charges.WindowChanged();
            Draw();
        };

        ThemeInk.Bind(_figures, Border.BackgroundProperty, ThemeManager.SlabKey);

        // The Stepper stays in place across redraws, so an arrow kept pressed keeps stepping.
        var windowRow = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*"),
            ColumnSpacing = 2,
            Margin = new Thickness(0, 0, 14, 0),
        };
        Grid.SetColumn(_figures, 1);
        windowRow.Children.Add(_window);
        windowRow.Children.Add(_figures);

        var page = new Grid { RowDefinitions = new RowDefinitions("Auto,*") };
        Grid.SetRow(_body, 1);
        page.Children.Add(windowRow);
        page.Children.Add(_body);
        Content = page;

        _head.Changed += Draw;
        _chargeHead.Changed += () =>
        {
            _charges.SortBy(_chargeHead.Order);
            Draw();
        };

        Draw();
    }

    public override string Crumb => "Spend breakdown";

    public string Summary => SpendBreakdownModel.Summary;

    event EventHandler? IPageSummary.SummaryChanged
    {
        add { }
        remove { }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Refit();
    }

    /// <summary>Draws the page when it has not been drawn, or the panel it sits in now differs in headset or width memory from the last draw.</summary>
    private void Refit()
    {
        var headset = PanelView.OnHeadset(this);
        var widths = PanelView.ColumnWidthsOf(this);

        if (!_drawn || headset != _charges.Headset || widths != _widths)
        {
            _charges.Headset = headset;
            _widths = widths;
            Draw();
        }
    }

    /// <summary>The five windows, ending now on the ledger's clock.</summary>
    private IReadOnlyList<SpendPeriod> Windows() =>
        [.. _ledger.Immediate(_zone).Select(window => window.Period), .. _ledger.Windows(_zone).Select(window => window.Period)];

    private void Draw()
    {
        _drawn = true;

        var windows = Windows();
        var window = windows[Math.Clamp(_window.SelectedIndex, 0, windows.Count - 1)];
        var purposes = _ledger.ByPurpose(window);
        var figures = SpendBreakdownModel.WindowFigures.Of(purposes);

        _figures.Child = Figures(window, figures);

        var top = new StackPanel { Spacing = 12 };
        top.Children.Add(CreditsKit.Note(SpendBreakdownModel.Hint, 0, full: true));

        if (!figures.Any)
        {
            var nothing = RoutingKit.Ink(SpendBreakdownModel.NothingText(window.Name), TypeScale.Secondary, ThemeManager.WhiteKey, wrap: true);
            nothing.Name = "SpendNothing";
            nothing.Margin = new Thickness(0, 6, 0, 0);
            top.Children.Add(nothing);
            top.Children.Add(CreditsKit.Note(SpendBreakdownModel.NothingHint, 0, full: true));
            _body.Child = LoadoutPages.Scrolling(top);
            return;
        }

        top.Children.Add(Sections(figures));

        if (_section == SpendBreakdownModel.EveryChargeSection)
        {
            DrawCharges(top, window);
        }
        else
        {
            DrawPurposes(top, purposes, figures);
        }
    }

    private void DrawPurposes(StackPanel top, IReadOnlyList<SpendPurposeShare> purposes, SpendBreakdownModel.WindowFigures figures)
    {
        top.Children.Add(CreditsKit.Note(SpendBreakdownModel.PressHint, 0, full: true));

        var layout = new TableLayout(TableId, PurposeTable(), _widths, spacing: _charges.Headset ? 12 : 16);
        var header = CreditsKit.HeaderRow(_head, layout, column => column == 0 ? HorizontalAlignment.Left : HorizontalAlignment.Right);

        var list = new StackPanel { Name = "SpendPurposeRows", Spacing = Gaps.Tile };

        foreach (var share in _head.Sort(SpendBreakdownModel.InDefaultOrder(purposes)))
        {
            list.Children.Add(Row(layout, share, figures.Dollars));
        }

        _body.Child = LoadoutPages.HeaderAndList(top, header, list, _charges.Headset ? null : layout, out var scroll);
        FollowRows(header, scroll);
    }

    private void DrawCharges(StackPanel top, SpendPeriod window)
    {
        var page = _charges.Of(_ledger.Charges(window));

        top.Children.Add(Results(page));

        var layout = new TableLayout(ChargeTableId, ChargeTable(), _widths, spacing: _charges.Headset ? 12 : 16);
        var header = CreditsKit.HeaderRow(
            _chargeHead,
            layout,
            column => column == SpendBreakdownModel.ChargeCostColumn ? HorizontalAlignment.Right : HorizontalAlignment.Left);

        var rows = new StackPanel { Name = "SpendChargeRows", Spacing = Gaps.Tile };

        foreach (var entry in page.Rows)
        {
            rows.Children.Add(ChargeRow(layout, entry));
        }

        var list = new StackPanel { Children = { rows } };

        if (page.Listed > 0)
        {
            list.Children.Add(Pager(page));
        }

        _body.Child = LoadoutPages.HeaderAndList(top, header, list, _charges.Headset ? null : layout, out var scroll);
        FollowRows(header, scroll);
    }

    /// <summary>A column that is a set width in the headset and, on the desktop, takes spare width and has a grip, no narrower than that width.</summary>
    private TableColumn Sized(string key, double desktop, double headset) =>
        _charges.Headset ? new TableColumn(key, ColumnFit.Fixed) { Fixed = headset } : new TableColumn(key) { Min = desktop };

    private IReadOnlyList<TableColumn> PurposeTable() =>
    [
        new("Purpose", ColumnFit.Fill) { Min = _charges.Headset ? 220 : 260 },
        Sized("Charges", 120, 100),
        Sized("Share", 110, 90),
        Sized("Cost", 170, 150),
    ];

    private IReadOnlyList<TableColumn> ChargeTable() =>
    [
        Sized("Time", 150, 130),
        new("Purpose", ColumnFit.Fill) { Min = _charges.Headset ? 150 : 170 },
        Sized("Model", 220, 190),
        new("Used", ColumnFit.Fill) { Min = _charges.Headset ? 250 : 360 },
        Sized("Cost", 110, 100),
    ];

    /// <summary>In the headset the header is kept as wide as the rows, so both report one width to the table.</summary>
    private void FollowRows(Grid header, ScrollViewer rows)
    {
        if (!_charges.Headset)
        {
            return;
        }

        rows.VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Visible;

        void Follow()
        {
            if (rows.Bounds.Width > 0)
            {
                _barWidth = rows.Bounds.Width - rows.Viewport.Width;
            }

            header.Margin = new Thickness(14, 0, 14 + _barWidth, 0);
        }

        Follow();
        rows.ScrollChanged += (_, _) => Follow();
    }

    /// <summary>BY PURPOSE and EVERY CHARGE, with their status lines on the desktop.</summary>
    private Segment Sections(SpendBreakdownModel.WindowFigures figures)
    {
        var section = new Segment
        {
            Name = "SpendSection",
            ItemsSource = SpendBreakdownModel.Sections,
            SelectedIndex = _section,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Height = _charges.Headset ? TypeScale.MinimumTarget : Segment.StatusHeight,
            Margin = new Thickness(0, 6, 0, 0),
        };

        if (!_charges.Headset)
        {
            section.Statuses =
            [
                new ChoiceStatus(SpendBreakdownModel.PurposesText(figures.Purposes), ChoiceTone.Grey),
                new ChoiceStatus(SpendBreakdownModel.ChargesText(figures.Charges), ChoiceTone.Grey),
            ];
        }

        section.SelectionChanged += (_, _) =>
        {
            if (section.SelectedIndex >= 0 && section.SelectedIndex != _section)
            {
                _section = section.SelectedIndex;
                Draw();
            }
        };

        return section;
    }

    /// <summary>Every charge filtered to <paramref name="share"/>'s purpose and kind.</summary>
    private void ListCharges(SpendPurposeShare share)
    {
        _charges.FilterTo(share.Purpose, share.Kind);
        _section = SpendBreakdownModel.EveryChargeSection;
        Draw();
    }

    /// <summary>The window's range at the left, and the number of charges and the total at the right.</summary>
    private DockPanel Figures(SpendPeriod window, SpendBreakdownModel.WindowFigures figures)
    {
        var any = figures.Any;

        var range = CreditsKit.Mono(SpendBreakdownModel.RangeText(window, _zone), TypeScale.Small, ThemeManager.GreyKey, right: false);
        range.Name = "SpendRange";
        range.VerticalAlignment = VerticalAlignment.Center;
        range.Margin = new Thickness(0, 0, 16, 0);

        var charges = CreditsKit.Mono(SpendBreakdownModel.ChargesText(figures.Charges), TypeScale.Small, any ? ThemeManager.WhiteKey : ThemeManager.GreyKey, right: false);
        charges.Name = "SpendCharges";
        charges.VerticalAlignment = VerticalAlignment.Center;

        var total = CreditsKit.Mono(SpendBreakdownModel.TotalText(figures), TypeScale.Figure, any ? ThemeManager.AKey : ThemeManager.GreyKey, right: false);
        total.Name = "SpendTotal";
        total.VerticalAlignment = VerticalAlignment.Center;

        var right = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 16, Children = { charges, total } };
        DockPanel.SetDock(right, Dock.Right);

        return new DockPanel { LastChildFill = true, Children = { right, range } };
    }

    /// <summary>
    /// The count, then what spoken lines come to and the SHOW SPOKEN LINES checkbox; filtered to a purpose, its total
    /// and unpriced count and the SHOW EVERY PURPOSE tile.
    /// </summary>
    private DockPanel Results(SpendChargePage page)
    {
        Control action;
        var left = new DockPanel { LastChildFill = true, VerticalAlignment = VerticalAlignment.Center };

        if (page.Purpose is { } purpose)
        {
            var count = CreditsKit.Mono(SpendBreakdownModel.FilteredText(purpose), TypeScale.Small, ThemeManager.WhiteKey, right: false);
            count.Name = "SpendResultsCount";
            left.Children.Add(count);

            count.Margin = new Thickness(0, 0, 16, 0);
            var cost = CreditsKit.Mono(
                SpendBreakdownModel.CostText(purpose),
                TypeScale.Small,
                SpendBreakdownModel.Priced(purpose) ? ThemeManager.AKey : ThemeManager.GreyKey,
                right: false);
            cost.Name = "SpendResultsCost";
            left.Children.Add(cost);

            if (SpendBreakdownModel.UnpricedText(purpose) is { } unpriced)
            {
                cost.Margin = new Thickness(0, 0, 16, 0);
                left.Children.Add(CreditsKit.Mono(unpriced, TypeScale.Small, ThemeManager.GreyKey, right: false));
            }

            var every = new Button { Name = "SpendShowEveryPurpose", Content = SpendBreakdownModel.ShowEveryPurpose, MinHeight = TypeScale.MinimumTarget };
            every.Click += (_, _) =>
            {
                _charges.ShowEveryPurpose();
                Draw();
            };
            action = every;
        }
        else
        {
            var count = CreditsKit.Mono(SpendBreakdownModel.ChargesText(page.Listed), TypeScale.Small, ThemeManager.WhiteKey, right: false);
            count.Name = "SpendResultsCount";
            count.Margin = new Thickness(0, 0, 16, 0);
            left.Children.Add(count);

            var hint = CreditsKit.Note(SpendBreakdownModel.SpokenText(page.Spoken, _charges.ShowSpoken), 0, full: true);
            hint.Name = "SpendResultsHint";
            hint.VerticalAlignment = VerticalAlignment.Center;
            left.Children.Add(hint);

            var spoken = LabeledCheckBox.Caps(SpendBreakdownModel.ShowSpokenLines);
            spoken.Name = "SpendShowSpoken";
            spoken.MinHeight = TypeScale.MinimumTarget;
            spoken.IsChecked = _charges.ShowSpoken;
            spoken.IsCheckedChanged += (_, _) =>
            {
                _charges.ShowSpoken = spoken.IsChecked == true;
                Draw();
            };
            action = spoken;
        }

        action.VerticalAlignment = VerticalAlignment.Center;
        action.Margin = new Thickness(16, 0, 0, 0);
        DockPanel.SetDock(action, Dock.Right);

        return new DockPanel { Name = "SpendResults", MinHeight = TypeScale.MinimumTarget, LastChildFill = true, Children = { action, left } };
    }

    private Grid Pager(SpendChargePage page)
    {
        var readout = CreditsKit.Mono(page.Readout, TypeScale.Tip, ThemeManager.GreyKey, right: false);
        readout.Name = "SpendChargesReadout";
        readout.VerticalAlignment = VerticalAlignment.Center;

        var previous = new Button { Name = PreviousName, Content = "◄ PREVIOUS", Height = TypeScale.MinimumTarget, IsEnabled = page.HasPrevious };
        var next = new Button { Name = NextName, Content = "NEXT ►", Height = TypeScale.MinimumTarget, IsEnabled = page.HasNext };

        previous.Click += (_, _) =>
        {
            _charges.PreviousPage();
            Draw();
        };
        next.Click += (_, _) =>
        {
            _charges.NextPage();
            Draw();
        };

        var tiles = new StackPanel { Orientation = Orientation.Horizontal, Spacing = Gaps.Tile, Children = { previous, next } };
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Margin = new Thickness(0, 8, 0, 0) };
        Grid.SetColumn(tiles, 1);
        grid.Children.Add(readout);
        grid.Children.Add(tiles);

        return grid;
    }

    private Border Row(TableLayout layout, SpendPurposeShare share, decimal window)
    {
        var grid = layout.Row([]);
        grid.VerticalAlignment = VerticalAlignment.Center;

        CreditsKit.Place(grid, 0, PurposeCell(share));
        CreditsKit.Place(grid, 1, CreditsKit.Mono(share.Charges.ToString("N0", System.Globalization.CultureInfo.CurrentCulture), TypeScale.Small, ThemeManager.WhiteKey, right: true));
        CreditsKit.Place(grid, 2, CreditsKit.Mono(SpendBreakdownModel.ShareText(share, window), TypeScale.Small, ThemeManager.WhiteKey, right: true));
        CreditsKit.Place(grid, 3, CostCell(share));

        var border = new Border
        {
            Name = $"SpendPurpose.{share.Purpose ?? "none"}.{share.Kind}",
            MinHeight = TypeScale.MinimumTarget,
            Padding = new Thickness(14, 6),
            Child = grid,
        };
        Avalonia.Automation.AutomationProperties.SetName(border, SpendBreakdownModel.NameOf(share));

        ListRow.Dress(border);
        ListRow.Opens(border, () => ListCharges(share));

        return border;
    }

    private Border ChargeRow(TableLayout layout, SpendEntry entry)
    {
        var grid = layout.Row([]);
        grid.VerticalAlignment = VerticalAlignment.Center;

        CreditsKit.Place(grid, 0, CreditsKit.Mono(SpendBreakdownModel.TimeText(entry, _zone), TypeScale.Small, ThemeManager.GreyKey, right: false));
        CreditsKit.Place(grid, 1, ChargePurposeCell(entry));
        CreditsKit.Place(grid, 2, ModelCell(entry));
        CreditsKit.Place(grid, 3, UsedCell(entry));
        CreditsKit.Place(grid, 4, CreditsKit.Mono(
            SpendBreakdownModel.ChargeCostText(entry),
            TypeScale.Small,
            entry.Priced ? ThemeManager.AKey : ThemeManager.GreyKey,
            right: true));

        var border = new Border
        {
            MinHeight = TypeScale.MinimumTarget,
            Padding = new Thickness(14, 6),
            Child = grid,
        };
        ThemeInk.Bind(border, Border.BackgroundProperty, ThemeManager.SlabKey);

        return border;
    }

    private static StackPanel PurposeCell(SpendPurposeShare share)
    {
        var notRecorded = SpendBreakdownModel.NotRecorded(share);

        var name = CreditsKit.Line(SpendBreakdownModel.NameOf(share), Fonts.ProseFamily, TypeScale.Secondary, notRecorded ? ThemeManager.GreyKey : ThemeManager.WhiteKey);

        var cell = new StackPanel { Spacing = 1, Children = { name, Caps(SpendBreakdownModel.KindWord(share.Kind)) } };

        if (notRecorded)
        {
            cell.Children.Add(RoutingKit.Ink(SpendBreakdownModel.NotRecordedLine, TypeScale.Meta, ThemeManager.GreyKey, wrap: true));
        }

        return cell;
    }

    private static StackPanel ChargePurposeCell(SpendEntry entry)
    {
        var name = CreditsKit.Line(
            SpendPurposes.NameOf(entry.Purpose),
            Fonts.ProseFamily,
            TypeScale.Secondary,
            entry.Purpose is null ? ThemeManager.GreyKey : ThemeManager.WhiteKey);

        var cell = new StackPanel { Spacing = 1, Children = { name } };

        if (SpendBreakdownModel.StepWord(entry) is { } step)
        {
            cell.Children.Add(Caps(step));
        }

        return cell;
    }

    private static Control ModelCell(SpendEntry entry)
    {
        if (SpendBreakdownModel.ProviderLine(entry) is not { } provider)
        {
            return CreditsKit.Line(SpendBreakdownModel.ModelText(entry), Fonts.ProseFamily, TypeScale.Secondary, ThemeManager.WhiteKey);
        }

        return new StackPanel
        {
            Spacing = 1,
            Children =
            {
                CreditsKit.Mono(SpendBreakdownModel.ModelText(entry), TypeScale.Small, ThemeManager.WhiteKey, right: false),
                CreditsKit.Line(provider, Fonts.ProseFamily, TypeScale.Meta, ThemeManager.GreyKey),
            },
        };
    }

    private static StackPanel UsedCell(SpendEntry entry)
    {
        var cell = new StackPanel
        {
            Spacing = 1,
            Children = { CreditsKit.Mono(SpendBreakdownModel.UsedText(entry), TypeScale.Small, ThemeManager.WhiteKey, right: false) },
        };

        if (SpendBreakdownModel.UsedDetail(entry) is { } detail)
        {
            cell.Children.Add(CreditsKit.Mono(detail, TypeScale.Meta, ThemeManager.GreyKey, right: false));
        }

        return cell;
    }

    /// <summary>A chrome 11 Grey caps line under a name: a kind or a story step.</summary>
    private static TextBlock Caps(string text)
    {
        var line = CreditsKit.Line(text, Fonts.ChromeFamily, TypeScale.MetaSmall, ThemeManager.GreyKey);
        line.FontWeight = FontWeight.SemiBold;
        line.LetterSpacing = TypeScale.MetaSmall * 0.06;

        return line;
    }

    private static StackPanel CostCell(SpendPurposeShare share)
    {
        var priced = SpendBreakdownModel.Priced(share);
        var cell = new StackPanel { HorizontalAlignment = HorizontalAlignment.Right };

        cell.Children.Add(CreditsKit.Mono(SpendBreakdownModel.CostText(share), priced ? TypeScale.Secondary : TypeScale.Small, priced ? ThemeManager.AKey : ThemeManager.GreyKey, right: true));

        if (SpendBreakdownModel.UnpricedText(share) is { } unpriced)
        {
            cell.Children.Add(CreditsKit.Mono(unpriced, TypeScale.Meta, ThemeManager.GreyKey, right: true));
        }

        return cell;
    }
}
