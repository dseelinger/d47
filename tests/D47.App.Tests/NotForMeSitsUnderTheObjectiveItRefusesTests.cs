using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using D47.App.Theming;
using D47.Core.Adventures;
using D47.Core.Interface;
using Xunit;
using static D47.App.Tests.NotForMeGroupFixture;

namespace D47.App.Tests;

/// <summary>On a stock story's chapter page, Not for me sits under the objective it refuses and Story on is in a row below. Captures go to <see cref="TestSurface.CaptureDirectory"/>.</summary>
[Trait("Category", "Integration")]
public sealed class NotForMeSitsUnderTheObjectiveItRefusesTests
{
    [AvaloniaFact]
    public void TheNextGroupHoldsTheObjectiveAndNotForMe()
    {
        using var look = AppLook.Put(ThemeCatalog.Elite, null);

        var surface = Open(TriggerKind.Arrive);
        ReadChapter(surface);
        Save(surface.Window, "not-for-me-under-objective-chapter.png");

        var head = Assert.IsAssignableFrom<TextBlock>(Starting(surface.Panel, "Next"));
        var objective = Assert.IsAssignableFrom<TextBlock>(Starting(surface.Panel, "Arrive"));
        var refuse = Assert.IsAssignableFrom<Button>(Pressable(surface.Panel, "Not for me"));
        var storyOn = Assert.Single(surface.Panel.GetVisualDescendants().OfType<CheckBox>(), box => box.Content is TextBlock { Text: "Story on" });

        Assert.True(Top(head, surface.Window) < Top(objective, surface.Window));
        Assert.True(Bottom(objective, surface.Window) <= Top(refuse, surface.Window));
        Assert.True(Bottom(refuse, surface.Window) <= Top(storyOn, surface.Window));

        surface.Window.Close();
    }

    [AvaloniaFact]
    public void TheBeaconScanOffersNoNotForMe()
    {
        using var look = AppLook.Put(ThemeCatalog.Elite, null);

        var surface = Open(TriggerKind.Beacon);
        ReadChapter(surface);

        Assert.Null(Pressable(surface.Panel, "Not for me"));

        surface.Window.Close();
    }
}
