using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using D47.App.Panel;
using D47.App.Theming;
using D47.Core.Capabilities;
using D47.Core.Conversation;

namespace D47.App.Controls;

/// <summary>What D47's estimate went on in one window, by purpose: the page SPEND BREAKDOWN opens.</summary>
public sealed class SpendBreakdownPage : DialogPage, IPageSummary
{
    public const string TableId = "Spend.ByPurpose";

    public const string HeadName = "SpendPurposeHead";

    private readonly SpendLedger _ledger;
    private readonly TimeZoneInfo _zone;
    private readonly SortHeader<SpendPurposeShare> _head = new(HeadName, SpendBreakdownModel.PurposeColumns, SpendBreakdownModel.ByDefault);
    private readonly Stepper _window;
    private readonly Border _figures = new() { MinHeight = TypeScale.MinimumTarget, Padding = new Thickness(14, 0) };
    private readonly Border _body = new() { Margin = new Thickness(0, 12, 0, 0) };
    private bool _headset;
    private bool _drawn;
    private double _barWidth;

    public SpendBreakdownPage(SpendLedger ledger, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(ledger);
        ArgumentNullException.ThrowIfNull(zone);

        _ledger = ledger;
        _zone = zone;
        Title = "Spend breakdown";

        var names = Windows().Select(window => window.Name).ToList();

        _window = new Stepper
        {
            Name = "SpendWindow",
            ItemsSource = names,
            SelectedIndex = Math.Max(0, names.IndexOf(SpendBreakdownModel.OpeningWindow)),
            Width = Stepper.MaximumWidth,
            VerticalAlignment = VerticalAlignment.Top,
        };
        _window.SelectionChanged += (_, _) => Draw();

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

        var headset = PanelView.OnHeadset(this);

        if (!_drawn || headset != _headset)
        {
            _headset = headset;
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

        var layout = new TableLayout(TableId, Columns(), spacing: _headset ? 12 : 16);
        var header = CreditsKit.HeaderRow(_head, layout, column => column == 0 ? HorizontalAlignment.Left : HorizontalAlignment.Right);

        var list = new StackPanel { Name = "SpendPurposeRows", Spacing = Gaps.Tile };

        foreach (var share in _head.Sort(SpendBreakdownModel.InDefaultOrder(purposes)))
        {
            list.Children.Add(Row(layout, share, figures.Dollars));
        }

        _body.Child = LoadoutPages.HeaderAndList(top, header, list, _headset ? null : layout, out var scroll);
        FollowRows(header, scroll);
    }

    private IReadOnlyList<TableColumn> Columns() =>
    [
        new("Purpose", ColumnFit.Fill) { Min = _headset ? 220 : 260 },
        new("Charges", ColumnFit.Fixed) { Fixed = _headset ? 100 : 120 },
        new("Share", ColumnFit.Fixed) { Fixed = _headset ? 90 : 110 },
        new("Cost", ColumnFit.Fixed) { Fixed = _headset ? 150 : 170 },
    ];

    /// <summary>In the headset the header is kept as wide as the rows, so both report one width to the table.</summary>
    private void FollowRows(Grid header, ScrollViewer rows)
    {
        if (!_headset)
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
            SelectedIndex = SpendBreakdownModel.ByPurposeSection,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Height = _headset ? TypeScale.MinimumTarget : Segment.StatusHeight,
            Margin = new Thickness(0, 6, 0, 0),
        };

        if (!_headset)
        {
            section.Statuses =
            [
                new ChoiceStatus(SpendBreakdownModel.PurposesText(figures.Purposes), ChoiceTone.Grey),
                new ChoiceStatus(SpendBreakdownModel.ChargesText(figures.Charges), ChoiceTone.Grey),
            ];
        }

        // EVERY CHARGE is drawn and cannot be chosen until its section is built.
        section.SetOptionEnabled(1, false);

        return section;
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
        ThemeInk.Bind(border, Border.BackgroundProperty, ThemeManager.SlabKey);

        return border;
    }

    private static StackPanel PurposeCell(SpendPurposeShare share)
    {
        var notRecorded = SpendBreakdownModel.NotRecorded(share);

        var name = CreditsKit.Line(SpendBreakdownModel.NameOf(share), Fonts.ProseFamily, TypeScale.Secondary, notRecorded ? ThemeManager.GreyKey : ThemeManager.WhiteKey);

        var kind = CreditsKit.Line(SpendBreakdownModel.KindWord(share.Kind), Fonts.ChromeFamily, TypeScale.MetaSmall, ThemeManager.GreyKey);
        kind.FontWeight = FontWeight.SemiBold;
        kind.LetterSpacing = TypeScale.MetaSmall * 0.06;

        var cell = new StackPanel { Spacing = 1, Children = { name, kind } };

        if (notRecorded)
        {
            cell.Children.Add(RoutingKit.Ink(SpendBreakdownModel.NotRecordedLine, TypeScale.Meta, ThemeManager.GreyKey, wrap: true));
        }

        return cell;
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
