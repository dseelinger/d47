using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.App.Theming;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>Shift+Enter starts a new line; Enter still sends; Up and Down walk history only from the
/// box's first and last visual line.</summary>
public class TheAskBoxTakesMoreThanOneLineTests
{
    private static (Window Window, PanelView View, PanelViewModel Model) Open()
    {
        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance)
            .Apply(TestSurface.Settings().Current.Ui.Theme);

        var model = new PanelViewModel();
        var view = new PanelView { DataContext = model };
        var window = new Window { Content = view, Width = 900, Height = 700 };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        return (window, view, model);
    }

    private static TextBox Box(PanelView view) => (TextBox)view.FindControl<Control>("AskBox")!;

    private static TextPresenter Presenter(PanelView view) =>
        Box(view).GetVisualDescendants().OfType<TextPresenter>().First();

    private static void Press(PanelView view, Key key, KeyModifiers modifiers = KeyModifiers.None)
    {
        Box(view).RaiseEvent(new KeyEventArgs
        {
            RoutedEvent = InputElement.KeyDownEvent,
            Key = key,
            KeyModifiers = modifiers,
        });

        Dispatcher.UIThread.RunJobs();
    }

    private static void Send(PanelView view, PanelViewModel model, string text)
    {
        model.AskText = text;
        Dispatcher.UIThread.RunJobs();

        Press(view, Key.Enter);
    }

    [AvaloniaFact]
    public void ShiftEnterInsertsANewlineAndDoesNotSend()
    {
        var (window, view, model) = Open();

        var sent = new List<string>();
        model.AskRequested += () => sent.Add(model.AskText);

        model.AskText = "where am I";
        Dispatcher.UIThread.RunJobs();

        Box(view).CaretIndex = model.AskText.Length;
        Press(view, Key.Enter, KeyModifiers.Shift);

        Assert.Empty(sent);
        Assert.StartsWith("where am I", Box(view).Text);
        Assert.NotEqual("where am I", Box(view).Text);

        window.Close();
    }

    [AvaloniaFact]
    public void EnterSendsAMultiLineValueWhole()
    {
        var (window, view, model) = Open();

        var sent = new List<string>();
        model.AskRequested += () => sent.Add(model.AskText);

        model.AskText = "where am I\nand what is my fuel";
        Dispatcher.UIThread.RunJobs();

        Press(view, Key.Enter);

        Assert.Equal(["where am I\nand what is my fuel"], sent);

        window.Close();
    }

    [AvaloniaFact]
    public void UpOnTheFirstVisualLineWalksBack()
    {
        var (window, view, model) = Open();

        Send(view, model, "where am I");

        model.AskText = "second question";
        Dispatcher.UIThread.RunJobs();

        Box(view).CaretIndex = 0;
        Press(view, Key.Up);

        Assert.Equal("where am I", model.AskText);

        window.Close();
    }

    [AvaloniaFact]
    public void UpOnALowerLineOfAWrappedQuestionMovesTheCaretInstead()
    {
        var (window, view, model) = Open();

        Send(view, model, "where am I");

        var longQuestion = string.Join(" ", Enumerable.Repeat("frameshift drive charging", 20));
        model.AskText = longQuestion;
        Dispatcher.UIThread.RunJobs();

        var layout = Presenter(view).TextLayout;
        Assert.True(layout.TextLines.Count > 1, "the value must wrap onto more than one visual line");

        var lastLine = layout.TextLines[^1];
        Box(view).CaretIndex = lastLine.FirstTextSourceIndex;
        Press(view, Key.Up);

        Assert.Equal(longQuestion, model.AskText);

        window.Close();
    }

    [AvaloniaFact]
    public void DownOnTheLastVisualLineWalksForward()
    {
        var (window, view, model) = Open();

        Send(view, model, "where am I");
        Send(view, model, "what is my fuel");

        Press(view, Key.Up);
        Press(view, Key.Up);
        Assert.Equal("where am I", model.AskText);

        Box(view).CaretIndex = model.AskText.Length;
        Press(view, Key.Down);

        Assert.Equal("what is my fuel", model.AskText);

        window.Close();
    }

    [AvaloniaFact]
    public void DownOnAnUpperLineOfAWrappedQuestionMovesTheCaretInstead()
    {
        var (window, view, model) = Open();

        Send(view, model, "where am I");
        Send(view, model, "what is my fuel");

        var longQuestion = string.Join(" ", Enumerable.Repeat("frameshift drive charging", 20));
        model.AskText = longQuestion;
        Dispatcher.UIThread.RunJobs();

        var layout = Presenter(view).TextLayout;
        Assert.True(layout.TextLines.Count > 1, "the value must wrap onto more than one visual line");

        Box(view).CaretIndex = 0;
        Press(view, Key.Down);

        Assert.Equal(longQuestion, model.AskText);

        window.Close();
    }

    [AvaloniaFact]
    public void ARecalledMultiLineQuestionAppearsWholeWithTheCaretAtItsEnd()
    {
        var (window, view, model) = Open();

        Send(view, model, "where am I\nand what is my fuel");

        Box(view).CaretIndex = 0;
        Press(view, Key.Up);

        Assert.Equal("where am I\nand what is my fuel", model.AskText);
        Assert.Equal("where am I\nand what is my fuel".Length, Box(view).CaretIndex);

        window.Close();
    }

    [AvaloniaFact]
    public void PastedCarriageReturnLineFeedsAndTypedNewlinesCollapseIntoOneEntry()
    {
        var (window, view, model) = Open();

        Send(view, model, "where am I\r\nand what is my fuel");
        Send(view, model, "where am I\nand what is my fuel");
        Send(view, model, "something else");

        Press(view, Key.Up);
        Press(view, Key.Up);

        Assert.Equal("where am I\nand what is my fuel", model.AskText);

        window.Close();
    }

    [AvaloniaFact]
    public void AFiveLineValueStaysWithinTheBoxsMaxHeight()
    {
        var (window, view, model) = Open();

        model.AskText = "one\ntwo\nthree\nfour\nfive";
        Dispatcher.UIThread.RunJobs();

        Assert.True(
            Box(view).Bounds.Height <= PanelView.AskBoxMaxHeight,
            $"expected height <= {PanelView.AskBoxMaxHeight}, got {Box(view).Bounds.Height}");

        window.Close();
    }
}
