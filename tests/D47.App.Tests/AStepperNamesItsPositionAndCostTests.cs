using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Settings;
using D47.App.Theming;
using D47.Core.Capabilities.Builtin;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>A stepper says where it stands, and what it costs to step onto a value, under itself (#336).</summary>
public class AStepperNamesItsPositionAndCostTests
{
    [AvaloniaFact]
    public void TheSpeechModelStepperNamesItsPositionAndWhatSteppingOntoItCosts()
    {
        var host = Open();

        var stepper = Row(host, "Speech model").GetVisualDescendants().OfType<Stepper>().First();

        Assert.Matches(@"^\d+ / \d+$", Position(stepper).Text);
        Assert.False(string.IsNullOrEmpty(Consequence(stepper).Text));
        Assert.True(Consequence(stepper).IsVisible);

        host.Close();
    }

    [AvaloniaFact]
    public void APersonaStepperNamesItsPositionAloneWhenNoChoiceHasACostToName()
    {
        var host = Open();
        host.View.Reveal(PersonaCapability.Id);
        Dispatcher.UIThread.RunJobs();

        var stepper = Row(host, "Persona").GetVisualDescendants().OfType<Stepper>().First();

        Assert.Matches(@"^\d+ / \d+$", Position(stepper).Text);
        Assert.True(string.IsNullOrEmpty(Consequence(stepper).Text));
        Assert.False(Consequence(stepper).IsVisible);

        host.Close();
    }

    [AvaloniaFact]
    public void SteppingThroughTheThirdOfSixModelsShowsThreeOfSix()
    {
        var host = Open();

        var stepper = Row(host, "Speech model").GetVisualDescendants().OfType<Stepper>().First();
        var next = stepper.GetVisualDescendants().OfType<RepeatButton>()
            .First(button => Avalonia.Automation.AutomationProperties.GetName(button) == "Next");

        stepper.SelectedIndex = -1;
        Dispatcher.UIThread.RunJobs();

        for (var i = 0; i < 3; i++)
        {
            next.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
        }

        Assert.Equal($"3 / {stepper.ItemsSource.Count}", Position(stepper).Text);

        host.Close();
    }

    /// <summary>Holding an arrow repeats the move — the repeat timing named in #336.</summary>
    [AvaloniaFact]
    public void EachArrowRepeatsFourHundredMillisecondsInAtEightASecond()
    {
        var host = Open();

        var stepper = Row(host, "Speech model").GetVisualDescendants().OfType<Stepper>().First();

        foreach (var arrow in stepper.GetVisualDescendants().OfType<RepeatButton>())
        {
            Assert.Equal(400, arrow.Delay);
            Assert.Equal(125, arrow.Interval);
        }

        host.Close();
    }

    private static TextBlock Position(Stepper stepper) =>
        stepper.GetVisualDescendants().OfType<TextBlock>().First(text => text.Name == "StepperPosition");

    private static TextBlock Consequence(Stepper stepper) =>
        stepper.GetVisualDescendants().OfType<TextBlock>().First(text => text.Name == "StepperConsequence");

    private static SettingsHost Open()
    {
        var (settings, viewState, paths) = TestSurface.Create();

        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance)
            .FollowSettings(settings);

        return SettingsHost.Open(settings, viewState, paths);
    }

    /// <summary>
    /// The compact row itself, by the class the view marks it with - not the first grid in the tree
    /// that happens to contain the words, which is the whole surface.
    /// </summary>
    private static Grid Row(SettingsHost host, string label) =>
        host.View
            .GetVisualDescendants()
            .OfType<Grid>()
            .Where(grid => grid.Classes.Contains(SettingsView.CompactRowClass))
            .First(grid => grid.GetVisualDescendants()
                .OfType<TextBlock>()
                .Any(text => text.Text == label));
}
