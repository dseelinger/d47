using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using D47.App.Panel;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// The search row's controls sit beside each other (remediation.md 11, item 2).
/// <para>
/// Reported with a picture: a hand's width of nothing between the two steppers, with the count
/// stranded to the right of both. <c>LastChildFill</c> overrides the last child's own
/// <c>Dock</c>, and the steppers were declared last — so one of them was the filling child and
/// stretched across the whole row.
/// </para>
/// <para>
/// Asserted on where the controls land rather than on the markup, because the markup looked
/// right: every one of them carried <c>Dock="Right"</c>, and one of them was being ignored.
/// </para>
/// </summary>
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

    /// <summary>
    /// The count and the two steppers are one group at the right end, in that order, with nothing
    /// but their own margins between them.
    /// </summary>
    [AvaloniaFact]
    public void TheCountAndTheSteppersAreOneGroup()
    {
        var panel = Searching();

        var count = Where(panel, "SearchCount");
        var previous = Where(panel, "SearchPrevious");
        var next = Where(panel, "SearchNext");

        Assert.True(count.Right <= previous.Left, "the count is not left of the previous stepper");
        Assert.True(previous.Right <= next.Left, "the steppers are the wrong way round");

        // Their declared margin and nothing else. A gap wider than that is the row's spare width
        // leaking between two controls that belong side by side.
        Assert.True(
            previous.Left - count.Right <= 8,
            $"{previous.Left - count.Right} pixels between the count and the previous stepper");

        Assert.True(
            next.Left - previous.Right <= 8,
            $"{next.Left - previous.Right} pixels between the two steppers");
    }

    /// <summary>
    /// And the spare width goes to the search box, which is the one control here that can use it —
    /// </summary>
    [AvaloniaFact]
    public void TheSpareWidthGoesToTheBox()
    {
        var narrow = Where(Searching(700), "SearchInput");
        var wider = Where(Searching(1400), "SearchInput");

        Assert.True(
            wider.Width > narrow.Width,
            $"the box did not take the extra room: {narrow.Width} at 700, {wider.Width} at 1400");
    }

    /// <summary>
    /// And it still shrinks when there is nothing to spare, which is the other half of why it is
    /// the child that gives: a narrow pane takes the width out of the box rather than out of the
    /// mode button beside it.
    /// </summary>
    [AvaloniaFact]
    public void TheBoxIsWhatGivesWhenThereIsNoRoom()
    {
        var panel = Searching(380);

        Assert.True(Where(panel, "SearchInput").Width <= 120);

        // The mode button keeps its own size rather than being squeezed to a sliver.
        Assert.True(
            panel.GetControl<Button>("ModeButton").Bounds.Width > 60,
            "the mode button was squeezed instead");
    }

    /// <summary>
    /// The steppers stay at the right-hand end rather than following the box, so they do not move
    /// under the pointer as a query narrows the count from "9 of 143" to "1 of 2".
    /// </summary>
    [AvaloniaFact]
    public void TheSteppersStayAtTheEnd()
    {
        var panel = Searching();

        var row = panel.GetControl<DockPanel>("SearchRow");
        var next = Where(panel, "SearchNext");

        Assert.True(
            row.Bounds.Width - next.Right <= 1,
            $"the last stepper is {row.Bounds.Width - next.Right} pixels short of the end");
    }
}
