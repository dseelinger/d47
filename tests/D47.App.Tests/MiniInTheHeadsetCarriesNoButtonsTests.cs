using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Headset;
using D47.App.Panel;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using Xunit;

namespace D47.App.Tests;

/// <summary> The headset's mini panel carries no buttons, the same as the flat overlay. </summary>
public class MiniInTheHeadsetCarriesNoButtonsTests
{
    private static (VrPanelSurface Panel, PanelView View) Headset(string mode)
    {
        var (settings, _, _) = TestSurface.Create();
        settings.Apply(VrCapability.ModeKey, mode, SettingsCaller.Panel);

        var panel = new VrPanelSurface(
            new PanelViewModel(),
            settings,
            _ => null,
            dumpTo: TestSurface.CaptureDirectory);

        var view = (PanelView)typeof(VrPanelSurface)
            .GetField("_view", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .GetValue(panel)!;

        Serve(panel);

        return (panel, view);
    }

    /// <summary>One frame into a buffer nobody reads.</summary>
    private static void Serve(VrPanelSurface panel)
    {
        Dispatcher.UIThread.RunJobs();

        var (width, height) = panel.Size;
        var buffer = new byte[width * height * 4];

        unsafe
        {
            fixed (byte* pixels = buffer)
            {
                panel.Draw((IntPtr)pixels, width * 4);
                panel.Draw((IntPtr)pixels, width * 4);
            }
        }

        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>Every exact <c>Button</c> in the tree that is currently drawn.</summary>
    private static IReadOnlyList<Button> VisibleButtons(PanelView view) =>
        [.. view.GetVisualDescendants()
            .OfType<Button>()
            .Where(button => button.GetType() == typeof(Button) && button.IsVisible)];

    [AvaloniaFact]
    public void MiniCarriesNoButtons()
    {
        var (_, view) = Headset("mini");

        // Named in the message, because "one button survived" is the failure this is most likely to see again
        // and the name is the whole diagnosis.
        Assert.True(
            VisibleButtons(view).Count == 0,
            $"still showing: {string.Join(", ", VisibleButtons(view).Select(button => button.Name ?? "unnamed"))}");
    }

    [AvaloniaFact]
    public void TheBigPanelKeepsThem()
    {
        var (_, view) = Headset("full");

        Assert.NotEmpty(VisibleButtons(view));
    }

    /// <summary>
    /// A <c>CheckBox</c> is not a button and must survive, which is the distinction <c>PanelView</c>'s
    /// selector is written for: a checklist line's tick shows whether it is done, so removing it would
    /// take away the data rather than make room for it.
    /// </summary>
    [AvaloniaFact]
    public void WhatIsNotAButtonIsUntouched()
    {
        var (_, view) = Headset("mini");

        Assert.All(
            view.GetVisualDescendants().OfType<CheckBox>(),
            box => Assert.NotEqual(typeof(Button), box.GetType()));
    }
}
