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
using D47.Core.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// Neither arrow ever carries a tooltip, and the value cell carries one only while the value is
/// actually clipped — never the whole control's, which used to float the selected item a second time
/// 20px from where it is already shown (#382).
/// </summary>
public class AStepperOnlyTipsWhatItClipsTests
{
    [AvaloniaFact]
    public void NeitherArrowEverCarriesATip()
    {
        var host = OpenWith("small.en");

        var stepper = Row(host, "Speech model").GetVisualDescendants().OfType<Stepper>().First();

        foreach (var arrow in stepper.GetVisualDescendants().OfType<RepeatButton>())
        {
            Assert.Null(ToolTip.GetTip(arrow));
        }

        host.Close();
    }

    [AvaloniaFact]
    public void TheValueCellCarriesNoTipWhenTheValueFits()
    {
        var (settings, viewState, paths) = TestSurface.Create();

        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance)
            .FollowSettings(settings);

        var host = SettingsHost.Open(settings, viewState, paths);
        host.View.Reveal(SpeechCapability.Id);
        Dispatcher.UIThread.RunJobs();

        // Provider names are a word or two, well short of the column — nothing here should ever clip.
        var stepper = Row(host, "Voice provider").GetVisualDescendants().OfType<Stepper>().First();
        var value = Value(stepper);

        Assert.Null(ToolTip.GetTip(value));

        host.Close();
    }

    [AvaloniaFact]
    public void TheWholeStepperCarriesNoTipOfItsOwn()
    {
        var host = OpenWith("small.en");

        var stepper = Row(host, "Speech model").GetVisualDescendants().OfType<Stepper>().First();

        Assert.Null(ToolTip.GetTip(stepper));

        host.Close();
    }

    private static TextBlock Value(Stepper stepper) =>
        stepper.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Name == "StepperValue");

    private static SettingsHost OpenWith(string model)
    {
        var (settings, viewState, paths) = TestSurface.Create();

        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance)
            .FollowSettings(settings);

        var host = SettingsHost.Open(settings, viewState, paths);

        settings.Apply("listening.model", model, SettingsCaller.Panel);
        Dispatcher.UIThread.RunJobs();

        return host;
    }

    private static Grid Row(SettingsHost host, string label) =>
        host.View
            .GetVisualDescendants()
            .OfType<Grid>()
            .Where(grid => grid.Classes.Contains(SettingsView.CompactRowClass))
            .First(grid => grid.GetVisualDescendants()
                .OfType<TextBlock>()
                .Any(text => text.Text == label));
}
