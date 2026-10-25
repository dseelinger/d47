using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using D47.App.Panel;
using Xunit;

namespace D47.App.Tests;

/// <summary>The search row's controls sit beside each other.</summary>
public class TheSearchRowSitsTogetherTests
{
    private static PanelView Searching(double width = 1200)
    {
        var model = new PanelViewModel();

        for (var line = 0; line < 60; line++)
        {
            model.Append($"Line {line}: the beacon is quiet and the turn is done.\n");
        }

        var panel = new PanelView { DataContext = model };
        var window = new Window { Content = panel, Width = width, Height = 500 };

        window.Show();
        panel.EnableSearch();
        Dispatcher.UIThread.RunJobs();

        panel.GetControl<TextBox>("SearchInput").Text = "turn";
        Dispatcher.UIThread.RunJobs();

        return panel;
    }

    private static Rect Where(PanelView panel, string name)
    {
        var control = panel.GetControl<Control>(name);
        var row = panel.GetControl<DockPanel>("SearchRow");
        var at = control.TranslatePoint(default, row);

        Assert.NotNull(at);

        return new Rect(at.Value, control.Bounds.Size);
    }

    /// <summary>The field holds the count and the clear glyph; the steppers follow it, previous first, 2px apart.</summary>
    [AvaloniaFact]
    public void TheCountAndTheClearGlyphAreInsideTheField()
    {
        var panel = Searching();

        var field = Where(panel, "SearchInput");
        var count = Where(panel, "SearchCount");
        var clear = Where(panel, "SearchClear");
        var previous = Where(panel, "SearchPrevious");
        var next = Where(panel, "SearchNext");

        Assert.True(field.Contains(count), "the count is outside the field");
        Assert.True(field.Contains(clear), "the clear glyph is outside the field");
        Assert.True(count.Right <= clear.Left, "the count is not left of the clear glyph");
        Assert.InRange(previous.Left - field.Right, 1.5, 2.5);
        Assert.InRange(next.Left - previous.Right, 1.5, 2.5);
        Assert.Equal(44, previous.Width, 0.5);
        Assert.Equal(44, next.Width, 0.5);
    }

    /// <summary>The row reads: field, previous, next, divider, copy, help.</summary>
    [AvaloniaFact]
    public void TheRowReadsInUseOrder()
    {
        var panel = Searching();
        panel.EnableDonation(() => { });
        Dispatcher.UIThread.RunJobs();

        var order = new[] { "SearchInput", "SearchPrevious", "SearchNext", "SearchDivider", "CopyButton" }
            .Select(name => Where(panel, name))
            .ToList();

        for (var i = 1; i < order.Count; i++)
        {
            Assert.True(order[i - 1].Right <= order[i].Left + 0.5, $"item {i} is not right of item {i - 1}");
        }

        Assert.True(panel.GetControl<Control>("SearchDivider").IsVisible);
        Assert.Equal(1, Where(panel, "SearchDivider").Width, 0.5);
    }

    /// <summary>
    /// The field shrinks when there is nothing to spare: a narrow pane takes the width out of the box rather than out of the mode
    /// button beside it.
    /// </summary>
    [AvaloniaFact]
    public void TheBoxIsWhatGivesWhenThereIsNoRoom()
    {
        var panel = Searching(380);

        Assert.True(Where(panel, "SearchInput").Width <= 120);

        // The readings keep their own size rather than being squeezed to a sliver.
        Assert.True(
            panel.GetControl<D47.App.Controls.TextChoice>("ModeReadings").Bounds.Width > 60,
            "the readings were squeezed instead");
    }

    /// <summary>The page actions end the row, so the steppers and the field stay put as the count changes.</summary>
    [AvaloniaFact]
    public void ThePageActionsKeepTheEnd()
    {
        var panel = Searching();

        var row = panel.GetControl<DockPanel>("SearchRow");
        var copy = Where(panel, "CopyButton");
        var next = Where(panel, "SearchNext");

        Assert.True(row.Bounds.Width - copy.Right <= 1, "the copy button is short of the end");
        Assert.True(next.Right <= copy.Left, "the steppers are not left of the page actions");
    }

    /// <summary>
    /// The Transcript's field is 340 wide at any width with room for it, and the row ends 16px short of the comms
    /// indicator, which ends the title line (#430, #1602).
    /// </summary>
    [AvaloniaTheory]
    [InlineData(1200)]
    [InlineData(1400)]
    [InlineData(2400)]
    public void TheTranscriptsFieldIsAFixedWidth(double width)
    {
        var panel = Searching(width);

        var field = panel.GetControl<TextBox>("SearchInput");
        var row = panel.GetControl<DockPanel>("SearchRow");
        var bar = panel.GetControl<DockPanel>("TitleLine");
        var comms = panel.GetControl<Control>("CommsFull");
        var right = row.TranslatePoint(new Point(row.Bounds.Width, 0), bar)!.Value.X;
        var commsLeft = comms.TranslatePoint(default, bar)!.Value.X;

        // Within the pixel that layout rounding moves an edge by.
        Assert.InRange(field.Bounds.Width - PanelView.TranscriptSearchWidth, -1, 1);
        Assert.InRange(commsLeft - 16 - right, -1, 1);
        Assert.InRange(bar.Bounds.Width - (commsLeft + comms.Bounds.Width), -1, 1);
    }

    /// <summary>A narrow pane narrows the field to its floor and then the comms indicator, so no action overlaps the field (#1602).</summary>
    [AvaloniaFact]
    public void TheCommsIndicatorGivesAfterTheField()
    {
        var panel = Searching(480);

        var field = Where(panel, "SearchInput");

        foreach (var name in new[] { "SearchPrevious", "SearchNext", "CopyButton" })
        {
            Assert.False(Where(panel, name).Intersects(field), $"{name} overlaps the field");
        }

        var bar = panel.GetControl<DockPanel>("TitleLine");
        var row = panel.GetControl<DockPanel>("SearchRow");
        var comms = panel.GetControl<Control>("CommsFull");

        Assert.True(
            row.TranslatePoint(new Point(row.Bounds.Width, 0), bar)!.Value.X <= comms.TranslatePoint(default, bar)!.Value.X,
            "the search row runs under the indicator");
    }
}
