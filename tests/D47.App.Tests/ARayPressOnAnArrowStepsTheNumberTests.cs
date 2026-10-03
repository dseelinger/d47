using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Headset;
using D47.App.Panel;
using D47.App.Settings;
using D47.App.Theming;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Interface;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>A ray press on a number row's arrow steps the setting by one and leaves the arrow released (#690).</summary>
public class ARayPressOnAnArrowStepsTheNumberTests
{
    private static void Jobs() => Dispatcher.UIThread.RunJobs();

    private static void Render(VrPanelSurface surface)
    {
        surface.Board.Render();
        Jobs();
        surface.Board.Render();
    }

    private static (VrPanelSurface Surface, SettingsView View, SettingsService Settings) Headset()
    {
        var (settings, viewState, paths) = TestSurface.Create();

        settings.Apply(VrCapability.ModeKey, "full", SettingsCaller.Panel);
        settings.Apply(InterfaceCapability.ShowEverySettingKey, "true", SettingsCaller.Panel);
        settings.Apply(CalloutCapability.EnabledKey, "true", SettingsCaller.Panel);
        settings.Apply(CalloutCapability.NarratorKey, "true", SettingsCaller.Panel);
        settings.Apply(ConversationCapability.ProviderKey, "anthropic", SettingsCaller.Panel);

        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance).Apply(ThemeCatalog.Elite);

        var view = new SettingsView();
        view.Attach(settings, viewState, paths, () => new D47.Core.Coverage.CoverageReport([]));

        var surface = new VrPanelSurface(new PanelViewModel(), settings, _ => null, settingsPage: () => view);

        surface.Nav.Select(PanelTab.Settings);
        Jobs();
        Render(surface);

        return (surface, view, settings);
    }

    private static void PressOn(VrPanelSurface surface, Control control)
    {
        if (control.GetVisualAncestors().OfType<ScrollViewer>().FirstOrDefault() is { Content: Visual content } viewer
            && control.TranslatePoint(new Point(0, 0), content) is { } within)
        {
            var bottom = Math.Max(0, viewer.Extent.Height - viewer.Viewport.Height);
            viewer.Offset = viewer.Offset.WithY(Math.Clamp(within.Y - 20, 0, bottom));

            Render(surface);
        }

        var centre = control.TranslatePoint(
            new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), surface.Board.View);

        Assert.NotNull(centre);

        var (width, height) = surface.Size;

        Assert.True(surface.Press((float)(centre!.Value.X / width), (float)(centre.Value.Y / height)));

        Jobs();
        Render(surface);
    }

    private static RepeatButton Arrow(VrPanelSurface surface, SettingsView view, string key, string label, string name)
    {
        view.ShowPlaceOf(key);
        Jobs();
        Render(surface);

        var amount = view.GetVisualDescendants().OfType<TextBlock>()
            .First(text => text.Text == label && text.IsVisible)
            .GetVisualAncestors().Select(ancestor => ancestor.GetVisualDescendants().OfType<Amount>().ToList())
            .First(found => found.Count > 0)
            .Single();

        return amount.GetVisualDescendants().OfType<RepeatButton>()
            .Single(button => Avalonia.Automation.AutomationProperties.GetName(button) == name);
    }

    [AvaloniaTheory]
    [InlineData(CalloutCapability.NarratorSecondsKey, "The least time between narrations")]
    [InlineData(CalloutCapability.NarratorMaxSecondsKey, "The most time between narrations")]
    public void ARayPressOnEitherArrowOfANumberRowMovesItByOneStep(string key, string label)
    {
        var (surface, view, settings) = Headset();
        using var _ = surface;

        var before = decimal.Parse(settings.Read(key)!, System.Globalization.CultureInfo.InvariantCulture);

        var increase = Arrow(surface, view, key, label, "Increase");
        Render(surface);
        PressOn(surface, increase);

        Assert.Equal(before + 1, decimal.Parse(settings.Read(key)!, System.Globalization.CultureInfo.InvariantCulture));
        Assert.False(increase.IsPressed);

        var decrease = Arrow(surface, view, key, label, "Decrease");
        Render(surface);
        PressOn(surface, decrease);

        Assert.Equal(before, decimal.Parse(settings.Read(key)!, System.Globalization.CultureInfo.InvariantCulture));
        Assert.False(decrease.IsPressed);
    }

    [AvaloniaFact]
    public void ARayPressOnAStepperArrowMovesItAndLeavesTheArrowReleased()
    {
        var (surface, view, _) = Headset();
        using var _ = surface;

        var stepper = view.GetVisualDescendants().OfType<Stepper>().Single(found => found.Name == "AreaDropdown");

        stepper.IsVisible = true;
        Render(surface);

        var before = stepper.SelectedIndex;
        var next = stepper.GetVisualDescendants().OfType<RepeatButton>()
            .Single(button => Avalonia.Automation.AutomationProperties.GetName(button) == "Next");

        PressOn(surface, next);

        Assert.NotEqual(before, stepper.SelectedIndex);
        Assert.False(next.IsPressed);
    }
}
