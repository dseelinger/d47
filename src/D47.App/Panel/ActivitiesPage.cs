using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Theming;
using D47.Core.Activities;
using D47.Core.Utilities;

namespace D47.App.Panel;

/// <summary>Every catalogue activity with when it was last done and whether it may be suggested (#587).</summary>
public sealed class ActivitiesPage : UserControl
{
    /// <summary>Below this width a row is two columns.</summary>
    public const double NarrowBelow = 760;

    private const double DateColumn = 150;

    private const double AgeColumn = 200;

    private const double SuggestColumn = 76;

    private readonly ActivityLedger _ledger;

    private readonly Func<string?> _commander;

    private readonly Func<DateTimeOffset> _now;

    private readonly StackPanel _page = new() { Spacing = 2, MaxWidth = 900, HorizontalAlignment = HorizontalAlignment.Stretch };

    private bool _narrow;

    private bool _writing;

    public ActivitiesPage(ActivityLedger ledger, Func<string?> commander, Func<DateTimeOffset>? now = null)
    {
        _ledger = ledger;
        _commander = commander;
        _now = now ?? (() => DateTimeOffset.Now);

        Content = new ScrollViewer
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
            Dispatcher.UIThread.Post(Fill);
        }

        return base.ArrangeOverride(finalSize);
    }

    /// <summary>The ledger raises this from whichever thread wrote; a tick on this page's own toggle is skipped.</summary>
    private void Follow()
    {
        if (!_writing)
        {
            Dispatcher.UIThread.Post(Fill);
        }
    }

    private void Fill()
    {
        _page.Children.Clear();

        var commander = _commander();
        var corpus = _ledger.CorpusFrom(commander);
        var now = _now();

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

        Themed(since, TextBlock.ForegroundProperty, ThemeManager.GreyKey);
        header.Children.Add(since);

        var rule = new Border { Height = 1, Margin = new Thickness(0, 0, 0, 4) };
        Themed(rule, Border.BackgroundProperty, ThemeManager.AKey);

        _page.Children.Add(header);
        _page.Children.Add(rule);
        _page.Children.Add(Head());

        var rows = ActivityCatalogue.All
            .Select(entry => (Entry: entry, At: _ledger.LastDone(entry.Key, commander)))
            .OrderBy(pair => pair.At is null)
            .ThenBy(pair => pair.At)
            .ToList();

        foreach (var (entry, at) in rows)
        {
            _page.Children.Add(Row(entry, at, corpus, now, commander));
        }
    }

    private Control Head()
    {
        var head = ListRow.Head(_narrow ? "Activity · Last done" : "Activity");
        var activity = (Control)head.Child!;
        var grid = new Grid { ColumnDefinitions = Columns(), ColumnSpacing = _narrow ? 0 : 16 };

        head.Child = grid;
        grid.Children.Add(activity);

        if (!_narrow)
        {
            AddCell(grid, HeadText("Last done"), 1);
        }

        var suggest = HeadText("Suggest");
        suggest.HorizontalAlignment = HorizontalAlignment.Center;
        AddCell(grid, suggest, _narrow ? 1 : 3);

        return head;
    }

    private static Control HeadText(string text)
    {
        var holder = ListRow.Head(text);
        var label = (Control)holder.Child!;
        holder.Child = null;
        return label;
    }

    private ColumnDefinitions Columns() => _narrow
        ? [new ColumnDefinition(1, GridUnitType.Star), new ColumnDefinition(SuggestColumn, GridUnitType.Pixel)]
        :
        [
            new ColumnDefinition(1, GridUnitType.Star),
            new ColumnDefinition(DateColumn, GridUnitType.Pixel),
            new ColumnDefinition(AgeColumn, GridUnitType.Pixel),
            new ColumnDefinition(SuggestColumn, GridUnitType.Pixel),
        ];

    private Border Row(
        ActivityEntry entry,
        DateTimeOffset? at,
        DateTimeOffset? corpus,
        DateTimeOffset now,
        string? commander)
    {
        var suggested = _ledger.IsSuggested(entry.Key, commander);

        var name = ListRow.Name(new TextBlock
        {
            Text = entry.Name,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
        });

        Inked(name, suggested);

        var box = new CheckBox
        {
            IsChecked = suggested,
            Classes = { "bare" },
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };

        AutomationProperties.SetName(box, $"Suggest {entry.Name}");

        var row = ListRow.Dress(new Border
        {
            MinHeight = _narrow ? 52 : TypeScale.MinimumTarget,
            Padding = new Thickness(12, 0, 0, 0),
            BorderThickness = new Thickness(1),
            BorderBrush = Brushes.Transparent,
        });

        var grid = new Grid { ColumnDefinitions = Columns(), ColumnSpacing = _narrow ? 0 : 16 };
        row.Child = grid;

        if (_narrow)
        {
            var left = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 6) };
            left.Children.Add(name);
            left.Children.Add(When(at, corpus, now));
            grid.Children.Add(left);
        }
        else
        {
            grid.Children.Add(name);

            if (at is { } done)
            {
                AddCell(grid, DateText(done, 14), 1);
                AddCell(grid, Plain(ActivityAge.Say(done, now), 14), 2);
            }
            else
            {
                var none = Plain(NotSince(corpus), 14);
                Grid.SetColumnSpan(none, 2);
                AddCell(grid, none, 1);
            }
        }

        AddCell(grid, box, _narrow ? 1 : 3);

        AutomationProperties.SetName(row, $"{entry.Name}, {(at is { } last ? Date(last) : "not in your journals")}");

        box.IsCheckedChanged += (_, _) =>
        {
            var on = box.IsChecked == true;

            if (commander is not { Length: > 0 })
            {
                return;
            }

            _writing = true;

            try
            {
                _ledger.SetSuggested(entry.Key, commander, on);
            }
            finally
            {
                _writing = false;
            }

            Inked(name, on);
        };

        row.Tapped += (_, e) =>
        {
            if (e.Source is Visual source && source.FindAncestorOfType<CheckBox>(includeSelf: true) is null)
            {
                box.IsChecked = box.IsChecked != true;
            }
        };

        box.GotFocus += (_, e) =>
        {
            if (e.NavigationMethod is NavigationMethod.Tab or NavigationMethod.Directional)
            {
                Themed(row, Border.BorderBrushProperty, ThemeManager.CyanKey);
            }
        };

        box.LostFocus += (_, _) => row.BorderBrush = Brushes.Transparent;

        return row;
    }

    private static void Inked(TextBlock name, bool suggested) =>
        Themed(name, TextBlock.ForegroundProperty, suggested ? ThemeManager.WhiteKey : ThemeManager.GreyKey);

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

    private static TextBlock DateText(DateTimeOffset at, double size)
    {
        var block = new TextBlock
        {
            Text = Date(at),
            FontFamily = new FontFamily(Fonts.MonoFamily),
            FontSize = size,
            VerticalAlignment = VerticalAlignment.Center,
        };

        Themed(block, TextBlock.ForegroundProperty, ThemeManager.AKey);
        return block;
    }

    private static TextBlock Plain(string text, double size)
    {
        var block = new TextBlock
        {
            Text = text,
            FontFamily = new FontFamily(Fonts.ProseFamily),
            FontSize = size,
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
        };

        Themed(block, TextBlock.ForegroundProperty, ThemeManager.GreyKey);
        return block;
    }

    private static void AddCell(Grid grid, Control cell, int column)
    {
        Grid.SetColumn(cell, column);
        grid.Children.Add(cell);
    }

    private static void Themed(AvaloniaObject target, AvaloniaProperty property, string key) =>
        target.Bind(property, Application.Current!.Resources.GetResourceObservable(key));
}
