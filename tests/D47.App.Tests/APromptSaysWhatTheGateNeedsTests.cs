using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.Core.Interface;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// The panel shows the host's wording rather than a sentence of its own (remediation.md 10,
/// item 12).
/// <para>
/// The wording itself is Core's and is asserted in <c>APromptSaysWhatWouldOpenTheGateTests</c>.
/// This is the other half, and the half the defect was actually in: the page held one compiled-in
/// string, so no amount of correct wording elsewhere would have reached the Commander.
/// </para>
/// </summary>
public class APromptSaysWhatTheGateNeedsTests
{
    private static PanelView Waiting(PanelViewModel model)
    {
        var panel = new PanelView { DataContext = model };
        var window = new Window { Content = panel, Width = 900, Height = 700 };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        panel.Prompts.Enter(
            new EntryRequest("add:line", "line", "Add a line", null, string.Empty, EntrySurface.Voice),
            _ => { });

        Dispatcher.UIThread.RunJobs();

        return panel;
    }

    private static bool Says(PanelView panel, string text) =>
        panel.GetVisualDescendants()
            .OfType<TextBlock>()
            .Any(block => string.Equals(block.Text, text, StringComparison.Ordinal));

    [AvaloniaFact]
    public void ThePageShowsWhatTheHostSaysWouldOpenTheGate()
    {
        var panel = Waiting(new PanelViewModel { ListeningPrompt = "Hold [ and say it." });

        Assert.True(Says(panel, "Hold [ and say it."), "the prompt did not show the host's wording");
    }

    /// <summary>
    /// The defect verbatim. A prompt open under push-to-talk claimed to be listening while the
    /// gate was shut.
    /// </summary>
    [AvaloniaFact]
    public void ThePageDoesNotClaimToBeListeningOnItsOwnAuthority()
    {
        var panel = Waiting(new PanelViewModel { ListeningPrompt = "Hold [ and say it." });

        Assert.False(
            Says(panel, "Say it — I am listening."),
            "the prompt claimed to be listening while push-to-talk was the mode");
    }

    /// <summary>
    /// A surface whose host has said nothing yet still has to say something, and what it says has
    /// to be true in every mode — so it names neither a key nor a state.
    /// </summary>
    [AvaloniaFact]
    public void AnUnfurnishedSurfaceFallsBackToSomethingTrueInEveryMode()
    {
        var panel = Waiting(new PanelViewModel());

        Assert.True(Says(panel, PanelPrompts.WaitingFallback));
    }
}
