using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using D47.App.Controls;
using D47.App.Theming;
using Xunit;

namespace D47.App.Tests;

[Trait("Category", "Integration")]
public class ACopyGlyphBesideALongNameStaysInsideItsColumnTests
{
    private static readonly TableColumn[] Columns =
    [
        new("system", ColumnFit.Fixed) { Fixed = 120 },
        new("reason", ColumnFit.Fill),
    ];

    private static (Control Beside, TextBlock Name, Button Glyph, Window Window, TableGrid Row) Draw(string system)
    {
        var name = new TextBlock { Text = system, TextTrimming = TextTrimming.CharacterEllipsis };
        var beside = CopyGlyph.Beside(name, system, _ => Task.FromResult(true));
        var row = new TableLayout("test.copy", Columns).Row([beside, new TextBlock { Text = "reason" }]);
        var window = new Window { Content = row, Width = 600, Height = 100 };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        return (beside, name, (Button)((Grid)beside).Children[1], window, row);
    }

    private static double Right(Control control, Visual ancestor) =>
        control.TranslatePoint(new Point(control.Bounds.Width, 0), ancestor)!.Value.X;

    [AvaloniaFact]
    public void ALongNameAndItsGlyphEndAtOrBeforeTheColumnEdge()
    {
        var (_, name, glyph, _, row) = Draw("Col 285 Sector AA-A c10-27 With A Very Long Tail Name");
        var edge = row.ColumnDefinitions[0].ActualWidth;

        Assert.True(Right(name, row) <= edge + 0.5);
        Assert.True(Right(glyph, row) <= edge + 0.5);
    }

    [AvaloniaFact]
    public void AShortNamesGlyphSitsWithinTheSpacingAfterIt()
    {
        var (_, name, glyph, _, row) = Draw("Sol");
        var gap = glyph.TranslatePoint(new Point(0, 0), row)!.Value.X - Right(name, row);

        Assert.InRange(gap, 0, 6.5);
    }
}
