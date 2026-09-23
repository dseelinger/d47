using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using D47.App.Settings;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// A place whose rows a fold is hiding gets its own "Show N more" rather than the whole page unfolding
/// at once (#221).
/// </summary>
public sealed class UnfoldingOneSectionsHiddenSettingsTests
{
    private static void Jobs() => Avalonia.Threading.Dispatcher.UIThread.RunJobs();

    private const string ActingArea = "Acting on the game";
    private const string MayDoPlace = "What D47 may do";
    private const string KeyboardLabel = "Let D47 press keys in Elite";

    private const string AiArea = "The ship's AI";
    private const string EffortLabel = "Never think harder than this";

    private static int AreaIndex(string title) => SettingsLayout.Areas.ToList().FindIndex(a => a.Title == title);

    /// <summary>A page folded the way a fresh install shows it, the same arrangement
    /// <see cref="TheFoldToggleIsAtTheTopTests"/> uses — <see cref="SettingsHost.Open"/> unfolds
    /// everything by default, so the toggle is switched back off once the view exists.</summary>
    private static (SettingsHost Host, SettingsService Settings) Folded()
    {
        var (settings, viewState, paths) = TestSurface.Create();
        var host = SettingsHost.Open(settings, viewState, paths);

        settings.Apply(InterfaceCapability.ShowEverySettingKey, "false", SettingsCaller.Panel);
        host.View.Refresh();

        return (host, settings);
    }

    private static Border Card(SettingsView view, string heading) =>
        ((StackPanel)view.FindControl<Control>("Cards")!).Children
            .OfType<Border>()
            .First(card => card.GetVisualDescendants().OfType<TextBlock>()
                .Any(text => string.Equals(text.Text, heading, StringComparison.OrdinalIgnoreCase)));

    private static Button FoldButton(Border card) =>
        card.GetVisualDescendants().OfType<Button>()
            .First(button => button.Content is string text && text.StartsWith("Show", StringComparison.Ordinal));

    private static bool LabelShown(SettingsView view, string label) =>
        view.GetVisualDescendants().OfType<TextBlock>()
            .Any(text => text.IsEffectivelyVisible && text.Text == label);

    [AvaloniaFact]
    public void AFullyFoldedPlaceKeepsItsCardTitleAndButtonWithNoRows()
    {
        var (host, _) = Folded();

        host.View.SelectArea(AreaIndex(ActingArea));
        Jobs();

        var card = Card(host.View, MayDoPlace);

        Assert.True(FoldButton(card).IsVisible);
        Assert.False(LabelShown(host.View, KeyboardLabel));

        host.Close();
    }

    [AvaloniaFact]
    public void PressingShowMoreDrawsOnlyThatPlacesRows()
    {
        var (host, _) = Folded();

        host.View.SelectArea(AreaIndex(ActingArea));
        Jobs();

        var button = FoldButton(Card(host.View, MayDoPlace));

        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Jobs();

        Assert.True(LabelShown(host.View, KeyboardLabel));
        Assert.Equal("Show fewer", button.Content);

        host.View.SelectArea(AreaIndex(AiArea));
        Jobs();

        Assert.False(LabelShown(host.View, EffortLabel), "another place's fold must not have opened too");

        host.Close();
    }

    [AvaloniaFact]
    public void TurningOnShowEverySettingRemovesTheButton()
    {
        var (host, settings) = Folded();

        host.View.SelectArea(AreaIndex(ActingArea));
        Jobs();

        var card = Card(host.View, MayDoPlace);
        Assert.True(FoldButton(card).IsVisible);

        settings.Apply(InterfaceCapability.ShowEverySettingKey, "true", SettingsCaller.Panel);
        Jobs();

        Assert.False(FoldButton(card).IsVisible);
        Assert.True(LabelShown(host.View, KeyboardLabel));

        host.Close();
    }

    [AvaloniaFact]
    public void RevealUnfoldsItsOwnPlaceAndLeavesOthersFolded()
    {
        var (host, _) = Folded();

        host.View.Reveal(ConversationCapability.Id);
        Jobs();

        Assert.True(LabelShown(host.View, EffortLabel));

        host.View.SelectArea(AreaIndex(ActingArea));
        Jobs();

        Assert.False(LabelShown(host.View, KeyboardLabel), "revealing one place must not unfold another");

        host.Close();
    }
}
