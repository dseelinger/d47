using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Settings;
using Xunit;

namespace D47.App.Tests;

/// <summary>A <c>place:</c> link in a group's help opens the settings place it names (#1499).</summary>
[Trait("Category", "Integration")]
public sealed class AHelpLinkOpensThePlaceItNamesTests
{
    private static void Jobs() => Dispatcher.UIThread.RunJobs();

    private static TextBlock Note(SettingsView view, string groupTitle) =>
        view.GetVisualDescendants().OfType<StackPanel>()
            .Where(panel => panel.Name == SettingsView.GroupHeadName)
            .Single(head => head.GetVisualDescendants().OfType<Button>()
                .Any(button => AutomationProperties.GetName(button) == $"Reset {groupTitle}"))
            .GetVisualDescendants().OfType<TextBlock>()
            .Single(block => block.Inlines is { Count: > 0 } || block.Text?.Contains("Who voices") == true
                || block.Text?.Contains("Where D47 synthesises") == true);

    private static void Press(Window window, TextBlock block, string label)
    {
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Jobs();

        var start = block.Inlines!.Text!.IndexOf(label, StringComparison.Ordinal);
        var rect = block.TextLayout.HitTestTextRange(start, label.Length).First();
        var at = block.TranslatePoint(rect.Center, window)!.Value;

        window.MouseMove(at);
        window.MouseDown(at, MouseButton.Left);
        window.MouseUp(at, MouseButton.Left);
        Jobs();
    }

    [AvaloniaTheory]
    [InlineData("voice", "Voice", "Providers › Speech synthesis", "speech-synthesis")]
    [InlineData("speech-synthesis", "Provider", "Your core › Its voice", "voice")]
    public void PressingTheLinkOpensTheOtherPlace(string open, string group, string label, string expected)
    {
        var (settings, viewState, paths) = TestSurface.Create();
        var host = SettingsHost.Open(settings, viewState, paths);

        host.View.RevealPlace(open);
        Jobs();

        Assert.Equal(open, host.View.SectionIds[host.View.ActiveSection]);

        var note = Note(host.View, group);
        var text = note.Inlines!.Text!;

        Assert.Contains(label, text);
        Assert.DoesNotContain("](", text);
        Assert.DoesNotContain("place:", text);

        Press(host.Window, note, label);

        Assert.Equal(expected, host.View.SectionIds[host.View.ActiveSection]);

        host.Close();
    }

    [AvaloniaFact]
    public void TheGroupHelpHasNoMarkupBeforeAnythingIsTyped()
    {
        var (settings, viewState, paths) = TestSurface.Create();
        var host = SettingsHost.Open(settings, viewState, paths);

        host.View.RevealPlace("voice");
        Jobs();

        var text = string.Concat(host.View.GetVisualDescendants().OfType<TextBlock>()
            .Select(block => block.Inlines is { Count: > 0 } ? block.Inlines.Text : block.Text));

        Assert.DoesNotContain("](", text);
        Assert.DoesNotContain("place:", text);

        host.Close();
    }
}
