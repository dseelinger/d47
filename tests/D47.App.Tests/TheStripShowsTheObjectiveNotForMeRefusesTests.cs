using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using D47.App.Theming;
using D47.Core.Adventures;
using D47.Core.Interface;
using Xunit;
using static D47.App.Tests.NotForMeGroupFixture;

namespace D47.App.Tests;

/// <summary>The Stories tab's current-story strip draws the objective with Not for me under it, above the story controls, and draws no group without a refusable objective.</summary>
[Trait("Category", "Integration")]
public sealed class TheStripShowsTheObjectiveNotForMeRefusesTests
{
    [AvaloniaFact]
    public void TheObjectiveAndNotForMeSitAboveTheStoryRow()
    {
        using var look = AppLook.Put(ThemeCatalog.Elite, null);

        var surface = Open(TriggerKind.Arrive);
        Save(surface.Window, "not-for-me-under-objective-strip.png");

        var objective = Assert.IsAssignableFrom<TextBlock>(Starting(surface.Panel, "Arrive"));
        var refuse = Assert.IsAssignableFrom<Button>(Pressable(surface.Panel, "Not for me"));
        var read = Assert.IsAssignableFrom<Button>(Pressable(surface.Panel, "Read the chapter"));

        Assert.True(Bottom(objective, surface.Window) <= Top(refuse, surface.Window));
        Assert.True(Bottom(refuse, surface.Window) <= Top(read, surface.Window));

        surface.Window.Close();
    }

    [AvaloniaFact]
    public void WithoutARefusableObjectiveThereIsNoGroup()
    {
        using var look = AppLook.Put(ThemeCatalog.Elite, null);

        var surface = Open(TriggerKind.Beacon);
        Save(surface.Window, "not-for-me-under-objective-strip-none.png");

        Assert.Null(Pressable(surface.Panel, "Not for me"));
        Assert.Null(Starting(surface.Panel, "Scan"));
        Assert.NotNull(Pressable(surface.Panel, "Read the chapter"));

        surface.Window.Close();
    }
}
