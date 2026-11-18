using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using D47.App.Controls;
using D47.App.Theming;
using D47.Core.Activities;
using D47.Core.Utilities;

namespace D47.App.Panel;

/// <summary>Every catalogue activity with when it was last done, the undated ones first (#587).</summary>
public sealed class ActivitiesPage : UserControl
{
    /// <summary>Below this width a row is two columns.</summary>
    public const double NarrowBelow = 760;

    public const string TableId = "Activities";

    /// <summary>The width the title row keeps; the table fills the page.</summary>
    public const double Cap = 900;

    private readonly ActivityLedger _ledger;

    private readonly Func<string?> _commander;

    private readonly Func<DateTimeOffset> _now;

    private readonly StackPanel _page = new() { Spacing = 2, HorizontalAlignment = HorizontalAlignment.Stretch };

    private static readonly IReadOnlyList<SortColumn<ActivityRow>> WideHeadings =
    [
        SortColumn<ActivityRow>.Text("Activity", row => row.Entry.Name),
        SortColumn<ActivityRow>.Figure<DateTimeOffset>("Last done", row => row.SortKey, SortDirection.Ascending),
    ];

    private static readonly IReadOnlyList<SortColumn<ActivityRow>> NarrowHeadings =
    [
        SortColumn<ActivityRow>.Text("Activity · Last done", row => row.Entry.Name),
    ];

    private readonly ArrangedTable<ActivityRow> _wide;

    private readonly ArrangedTable<ActivityRow> _narrowTable;

    private DateTimeOffset? _corpus;

    private DateTimeOffset _today;

    private bool _narrow;

    public ActivitiesPage(
        ActivityLedger ledger,
        Func<string?> commander,
        Func<DateTimeOffset>? now = null,
        ColumnWidthMemory? columnWidths = null,
        bool embedded = false,
        ColumnArrangementMemory? columnArrangements = null)
    {
        var widths = columnArrangements?.Headset == true ? null : columnWidths;

        _ledger = ledger;
        _commander = commander;
        _now = now ?? (() => DateTimeOffset.Now);
        _wide = new ArrangedTable<ActivityRow>(TableId, WideFields(), columnArrangements, widths, 16, sortedByDefault: "LastDone");
        _narrowTable = new ArrangedTable<ActivityRow>(TableId, NarrowFields(), columnArrangements, widths);
        _wide.Changed += Fill;
        _narrowTable.Changed += Fill;

        Content = embedded
            ? _page
            : new ScrollViewer
            {
                Content = new Border { Padding = new Thickness(14), Child = _page },
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            };

        Fill();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        ResetSorts();
        _ledger.Changed += Follow;
        Fill();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _ledger.Changed -= Follow;
        base.OnDetachedFromVisualTree(e);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var narrow = finalSize.Width < NarrowBelow;

        if (narrow != _narrow)
        {
            _narrow = narrow;
            ResetSorts();
            Dispatcher.UIThread.Post(Fill);
        }

        return base.ArrangeOverride(finalSize);
    }

    private ArrangedTable<ActivityRow> Columns => _narrow ? _narrowTable : _wide;

    private void ResetSorts()
    {
        _wide.ShowSort(null, SortDirection.Ascending);
        _narrowTable.ShowSort(null, SortDirection.Ascending);
    }

    /// <summary>The ledger raises this from whichever thread folded.</summary>
    private void Follow() => Dispatcher.UIThread.Post(Fill);

    private void Fill()
    {
        _page.Children.Clear();

        var columns = Columns;
        columns.Refresh();

        var commander = _commander();
        var corpus = _corpus = _ledger.CorpusFrom(commander);
        _today = _now();

        var header = new WrapPanel { Margin = new Thickness(0, 0, 0, 10) };

        var title = TitleText.Build("Activities", TypeScale.Heading, TitleRank.Screen);
        title.Margin = new Thickness(0, 0, 16, 0);
        header.Children.Add(title);

        var since = new TextBlock
        {
            Text = corpus is { } start ? $"From your journals since {Date(start)}." : "No journals read yet.",
            FontFamily = new FontFamily(Fonts.ProseFamily),
            FontSize = TypeScale.Tip,
            VerticalAlignment = VerticalAlignment.Bottom,
            TextWrapping = TextWrapping.Wrap,
        };

        ThemeInk.Bind(since, TextBlock.ForegroundProperty, ThemeManager.GreyKey);
        header.Children.Add(since);

        var rule = new Border { Height = 1, Margin = new Thickness(0, 0, 0, 4) };
        ThemeInk.Bind(rule, Border.BackgroundProperty, ThemeManager.AKey);

        _page.Children.Add(PageFooter.Capped(header, Cap));
        _page.Children.Add(rule);

        if (LedgerTables.Arranger(columns) is { Children.Count: > 0 } arranger)
        {
            _page.Children.Add(arranger);
        }

        var table = new StackPanel { Spacing = 2 };
        table.Children.Add(Head(columns));

        var rows = ActivityCatalogue.All
            .Select(entry => new ActivityRow(entry, _ledger.LastDone(entry.Key, commander)))
            .OrderBy(row => row.SortKey)
            .ToList();

        foreach (var row in columns.Sort(rows))
        {
            table.Children.Add(Row(row, columns));
        }

        _page.Children.Add(LedgerTables.Sideways(columns, table));
    }

    private static Control Head(ArrangedTable<ActivityRow> columns)
    {
        var head = ListRow.Head(string.Empty);

        head.Padding = new Thickness(13, 0, 1, 0);
        head.Child = columns.Header();

        return head;
    }

    private IReadOnlyList<TableField<ActivityRow>> NarrowFields() =>
    [
        new("Activity", new TableColumn("Activity", ColumnFit.Fill), NarrowCell) { Label = NarrowHeadings[0].Label, Sort = NarrowHeadings[0] },
    ];

    private IReadOnlyList<TableField<ActivityRow>> WideFields() =>
    [
        new("Activity", new TableColumn("Activity", ColumnFit.Fill), row => NameOf(row.Entry)) { Label = WideHeadings[0].Label, Sort = WideHeadings[0] },
        new("LastDone", new TableColumn("LastDone"), LastDoneCell)
        {
            Label = WideHeadings[1].Label,
            Sort = WideHeadings[1],
            More = [new("Age", new TableColumn("Age"), AgeCell)],
        },
    ];

    private static TextBlock NameOf(ActivityEntry entry)
    {
        var name = ListRow.Name(new TextBlock
        {
            Text = entry.Name,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
        });

        ThemeInk.Bind(name, TextBlock.ForegroundProperty, ThemeManager.WhiteKey);

        return name;
    }

    private Control NarrowCell(ActivityRow row)
    {
        var left = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 6) };
        left.Children.Add(NameOf(row.Entry));
        left.Children.Add(When(row.At, _corpus, _today));

        return left;
    }

    private Control LastDoneCell(ActivityRow row)
    {
        if (row.At is { } done)
        {
            return DateText(done, 14);
        }

        var none = Plain(NotSince(_corpus), 14);
        Grid.SetColumnSpan(none, 2);

        return none;
    }

    private Control AgeCell(ActivityRow row) =>
        row.At is { } done ? Plain(ActivityAge.Say(done, _today), 14) : new Border();

    private Border Row(ActivityRow source, ArrangedTable<ActivityRow> columns)
    {
        var row = ListRow.Dress(new Border
        {
            MinHeight = _narrow ? 52 : TypeScale.MinimumTarget,
            Padding = new Thickness(12, 0, 0, 0),
            BorderThickness = new Thickness(1),
            BorderBrush = Brushes.Transparent,
        });

        row.Child = columns.Row(source);

        AutomationProperties.SetName(row, $"{source.Entry.Name}, {(source.At is { } last ? Date(last) : "not in your journals")}");

        return row;
    }

    private static Control When(DateTimeOffset? at, DateTimeOffset? corpus, DateTimeOffset now)
    {
        if (at is not { } done)
        {
            return Plain(NotSince(corpus), 13);
        }

        var date = DateText(done, 13);
        date.Margin = new Thickness(0, 0, 12, 0);

        return new WrapPanel { Children = { date, Plain(ActivityAge.Say(done, now), 13) } };
    }

    private static string NotSince(DateTimeOffset? corpus) =>
        corpus is { } start ? $"Not in your journals since {Date(start)}" : "Not in your journals";

    private static string Date(DateTimeOffset at) =>
        GalacticTime.Galactic(at.ToUniversalTime()).ToString("d MMM yyyy", CultureInfo.InvariantCulture);

    private static TextBlock DateText(DateTimeOffset at, double size) =>
        Typeset.Mono(Date(at), size, ThemeManager.AKey, vertical: VerticalAlignment.Center);

    private static TextBlock Plain(string text, double size) =>
        Typeset.Prose(text, size, ThemeManager.GreyKey, wrap: true, vertical: VerticalAlignment.Center);
}

/// <summary>One catalogue activity and when it was last done.</summary>
internal readonly record struct ActivityRow(ActivityEntry Entry, DateTimeOffset? At)
{
    /// <summary>The date, or a value before every date for an activity with none.</summary>
    public DateTimeOffset SortKey => At ?? DateTimeOffset.MinValue;
}
