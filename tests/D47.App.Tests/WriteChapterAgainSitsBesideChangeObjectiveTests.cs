using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using D47.App.Panel;
using D47.App.Theming;
using D47.Core.Adventures;
using D47.Core.Capabilities.Builtin;
using D47.Core.Interface;
using Xunit;
using static D47.App.Tests.NotForMeGroupFixture;

namespace D47.App.Tests;

/// <summary>A story chapter's objective offers Write chapter again beside Change objective; one press sets the chapter aside, and a short story does not get it.</summary>
[Trait("Category", "Integration")]
public sealed class WriteChapterAgainSitsBesideChangeObjectiveTests
{
    private static void Press(Surface surface, string label)
    {
        var button = Assert.IsAssignableFrom<Button>(Pressable(surface.Panel, label));
        button.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void TheButtonSitsOnTheStripAndTheChapterPage()
    {
        using var look = AppLook.Put(ThemeCatalog.Elite, null);

        var surface = Open(TriggerKind.Arrive);

        Assert.NotNull(Pressable(surface.Panel, "Write chapter again"));

        ReadChapter(surface);

        var write = Assert.IsAssignableFrom<Button>(Pressable(surface.Panel, "Write chapter again"));
        Assert.NotNull(Pressable(surface.Panel, "Change objective"));
        Assert.True(write.Bounds.Height >= 44);

        surface.Window.Close();
    }

    [AvaloniaFact]
    public void ThePressSetsTheChapterAsideToBeWrittenAgain()
    {
        using var look = AppLook.Put(ThemeCatalog.Elite, null);

        var surface = Open(TriggerKind.Arrive);
        Press(surface, "Write chapter again");

        Assert.NotNull(surface.Stories!.Stories!.Stories.Current("F1")!.Rewrite);

        surface.Window.Close();
    }

    [AvaloniaFact]
    public void TheBeaconScanDoesNotOfferIt()
    {
        using var look = AppLook.Put(ThemeCatalog.Elite, null);

        var surface = Open(TriggerKind.Beacon);

        Assert.Null(Pressable(surface.Panel, "Write chapter again"));

        surface.Window.Close();
    }

    [AvaloniaFact]
    public void TheVoiceToolSetsTheChapterAside()
    {
        using var look = AppLook.Put(ThemeCatalog.Elite, null);

        var desk = new AdventureCapability.AdventureDesk();
        var surface = Open(TriggerKind.Arrive, desk: desk);

        Assert.Null(desk.RewriteChapter());
        Assert.NotNull(surface.Stories!.Stories!.Stories.Current("F1")!.Rewrite);

        surface.Window.Close();
    }
}
