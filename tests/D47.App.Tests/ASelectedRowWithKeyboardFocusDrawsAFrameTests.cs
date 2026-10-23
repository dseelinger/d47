using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using D47.App.Theming;
using Xunit;

namespace D47.App.Tests;

/// <summary>A selected list row with keyboard focus draws a 2px Knock frame inset 4; no other row state does (#1533).</summary>
public sealed class ASelectedRowWithKeyboardFocusDrawsAFrameTests
{
    public enum Form
    {
        Border,
        Button,
    }

    private static (Control Row, Window Window) Shown(Form form, bool selected, bool enabled = true)
    {
        Control row;

        if (form == Form.Border)
        {
            row = ListRow.Dress(new Border { Child = new TextBlock { Text = "Row" } }, selected);
            row.Focusable = true;
        }
        else
        {
            var button = new Button { Content = "Row" };
            button.Classes.Add(ListRow.Class);
            button.Classes.Set(ListRow.SelectedClass, selected);
            row = button;
        }

        row.IsEnabled = enabled;

        var window = new Window { Content = new StackPanel { Children = { row } }, Width = 400, Height = 200 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        return (row, window);
    }

    private static Border? Frame(Control row) =>
        AdornerLayer.GetAdornerLayer(row)?.Children
            .OfType<Border>()
            .SingleOrDefault(adorner => ReferenceEquals(AdornerLayer.GetAdornedElement(adorner), row));

    [AvaloniaTheory]
    [InlineData(Form.Border)]
    [InlineData(Form.Button)]
    public void AKeyboardFocusedSelectedRowShowsTheKnockFrame(Form form)
    {
        using var look = AppLook.Put();
        var (row, window) = Shown(form, selected: true);

        row.Focus(NavigationMethod.Directional);
        Dispatcher.UIThread.RunJobs();

        var frame = Frame(row);
        Assert.NotNull(frame);
        Assert.Equal(2, frame!.BorderThickness.Left);
        Assert.Equal(4, frame.Margin.Left);
        Assert.Equal(4, frame.Margin.Bottom);
        Assert.False(frame.IsHitTestVisible);
        Assert.Equal(
            Assert.IsAssignableFrom<ISolidColorBrush>(Avalonia.Application.Current!.FindResource("D47.Knock")).Color,
            Assert.IsAssignableFrom<ISolidColorBrush>(frame.BorderBrush).Color);

        window.Close();
    }

    [AvaloniaTheory]
    [InlineData(Form.Border)]
    [InlineData(Form.Button)]
    public void AClickFocusedSelectedRowShowsNoFrame(Form form)
    {
        using var look = AppLook.Put();
        var (row, window) = Shown(form, selected: true);

        row.Focus(NavigationMethod.Pointer);
        Dispatcher.UIThread.RunJobs();

        Assert.Null(Frame(row));

        window.Close();
    }

    [AvaloniaTheory]
    [InlineData(Form.Border)]
    [InlineData(Form.Button)]
    public void AnUnselectedKeyboardFocusedRowShowsNoFrame(Form form)
    {
        using var look = AppLook.Put();
        var (row, window) = Shown(form, selected: false);

        row.Focus(NavigationMethod.Directional);
        Dispatcher.UIThread.RunJobs();

        Assert.Null(Frame(row));

        window.Close();
    }

    [AvaloniaTheory]
    [InlineData(Form.Border)]
    [InlineData(Form.Button)]
    public void TheFrameTakesNoLayoutSpace(Form form)
    {
        using var look = AppLook.Put();
        var (row, window) = Shown(form, selected: true);
        var before = row.Bounds;

        row.Focus(NavigationMethod.Directional);
        Dispatcher.UIThread.RunJobs();

        Assert.NotNull(Frame(row));
        Assert.Equal(before, row.Bounds);

        window.Close();
    }
}
