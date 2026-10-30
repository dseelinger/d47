using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using D47.App.Settings;
using D47.App.Theming;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>The setup wizard's talk button step offers the comms key.</summary>
[Trait("Category", "Integration")]
public class TheSetupWizardOffersTheCommsKeyTests
{
    [AvaloniaFact]
    public void TheTalkButtonStepOffersASlotToBindTheCommsKey()
    {
        var (settings, _, _, _, _) = TestSurface.CreateFull();
        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance).FollowSettings(settings);

        var wizard = new SetupWizard(settings);
        var host = wizard.Show();

        wizard.Go(SetupWizard.Step.TalkButton);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var names = wizard.GetVisualDescendants().OfType<Control>().Select(control => control.Name).ToList();

        Assert.Contains("SetupCommsKeySlot", names);
        Assert.Contains("SetupCommsStickSlot", names);
        Assert.Contains("SetupCommsKeyChip", names);

        using var frame = host.CaptureRenderedFrame()!;
        frame.SaveCapture("setup-wizard-comms-key.png");

        host.Close();
    }
}
