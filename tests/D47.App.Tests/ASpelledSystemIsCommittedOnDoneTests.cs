using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;

using D47.App.Panel;
using D47.Core.Interface;
using Xunit;

namespace D47.App.Tests;

public class ASpelledSystemIsCommittedOnDoneTests
{
    private static (PanelPrompts Prompts, List<string> Committed) Open(string initial)
    {
        var nav = new PanelNavigator();

        nav.Register(PanelTab.Transcript, new NavCrumb("root", "Root"));

        var layer = new Avalonia.Controls.Panel();
        var prompts = new PanelPrompts(nav, layer);
        var committed = new List<string>();

        prompts.Enter(SpellSystemEntry.Request(initial), committed.Add);

        var page = prompts.Build(nav.Trail[^1])!;

        new Window
        {
            Content = new Avalonia.Controls.Panel { Children = { page, layer } },
            Width = 1400,
            Height = 900,
        }.Show();

        Dispatcher.UIThread.RunJobs();

        return (prompts, committed);
    }

    [AvaloniaFact]
    public void SpellingThenDoneCommitsTheSpelledName()
    {
        var (prompts, committed) = Open(string.Empty);

        Assert.True(prompts.IsOpen);

        prompts.Hear(new Heard("sierra oscar lima", 1, Final: true));
        prompts.Hear(new Heard("done", 1, Final: true));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(["sol"], committed);
        Assert.False(prompts.IsOpen);
    }

    [AvaloniaFact]
    public void TheHeardNameIsAlreadyInTheBoxToCorrect()
    {
        var (prompts, committed) = Open("Colonai");

        prompts.Hear(new Heard("delete", 1, Final: true));
        prompts.Hear(new Heard("alpha", 1, Final: true));
        prompts.Hear(new Heard("done", 1, Final: true));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(["Colonaa"], committed);
    }

    [AvaloniaFact]
    public void AbandoningTheEntryCommitsNothing()
    {
        var (prompts, committed) = Open(string.Empty);

        prompts.Abandon();
        Dispatcher.UIThread.RunJobs();

        Assert.Empty(committed);
        Assert.False(prompts.IsOpen);
    }
}
