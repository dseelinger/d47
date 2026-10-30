using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.Core.Capabilities.Builtin;
using Xunit;

namespace D47.App.Tests;

/// <summary>The star system page's heading has a copy glyph beside the system name (#1923).</summary>
[Trait("Category", "Integration")]
public sealed class TheStarSystemHeadingCopiesTheSystemNameTests
{
    [AvaloniaFact]
    public void PressingTheGlyphCopiesTheNameAndNamesTheResult()
    {
        var name = TheSystemPageListsItsStationsTests.Ltt7786Profile().Name;
        var clipboard = new RecordingClipboard();
        var (panel, window, _) = TheSystemPageListsItsStationsTests.Shown(clipboard: clipboard);

        var glyph = panel.GetVisualDescendants().OfType<Button>().Single(button => CopyGlyph.GetCopies(button) == name);
        Assert.Equal(CopyGlyph.Name, AutomationProperties.GetName(glyph));

        glyph.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

        var deadline = DateTime.UtcNow.AddSeconds(5);

        while (AutomationProperties.GetName(glyph) != CopyGlyph.Copied && DateTime.UtcNow < deadline)
        {
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(5);
        }

        Assert.Equal(name, clipboard.Last);
        Assert.Equal(CopyGlyph.Copied, AutomationProperties.GetName(glyph));

        window.Close();
    }

    [AvaloniaFact]
    public void WithNoClipboardNoGlyphIsDrawn()
    {
        var name = TheSystemPageListsItsStationsTests.Ltt7786Profile().Name;
        var (panel, window, _) = TheSystemPageListsItsStationsTests.Shown();

        Assert.DoesNotContain(panel.GetVisualDescendants().OfType<Button>(), button => CopyGlyph.GetCopies(button) == name);

        window.Close();
    }
}
