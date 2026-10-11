using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Headset;
using D47.App.Panel;
using D47.Core;
using D47.Core.Capabilities.Builtin;
using D47.Core.Checklists;
using D47.Core.Configuration;
using D47.Core.Interface;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

[Trait("Category", "Integration")]
public class TheHeadsetSearchesAndCopiesTests
{
    private static (VrPanelSurface Panel, PanelView View, RecordingClipboard Clipboard) Headset(
        PanelViewModel model,
        ChecklistService? checklists = null)
    {
        var (settings, _, _) = TestSurface.Create();

        settings.Apply(VrCapability.ModeKey, "full", SettingsCaller.Panel);

        var clipboard = new RecordingClipboard();
        var panel = new VrPanelSurface(
            model, settings, _ => null, dumpTo: TestSurface.VrDump, clipboard: clipboard, checklists: checklists);

        var view = (PanelView)typeof(VrPanelSurface)
            .GetField("_view", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .GetValue(panel)!;

        return (panel, view, clipboard);
    }

    private static PanelViewModel Said()
    {
        var model = new PanelViewModel();

        model.Append("Fuel at 12 percent.\n");
        model.Append("\n> where is the fuel\n");
        model.Append("Fixture Anchorage has fuel.");

        return model;
    }

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

    private static T Named<T>(PanelView view, string name)
        where T : Control =>
        view.FindControl<T>(name) ?? throw new InvalidOperationException($"no {name}");

    [AvaloniaFact]
    public void TheTranscriptSearchFindsAndStepsThroughMatchesOnTheHeadset()
    {
        var (panel, view, _) = Headset(Said());
        using var _disposable = panel;

        Serve(panel);

        var box = Named<TextBox>(view, "SearchInput");

        Assert.True(box.IsVisible);

        box.Text = "fuel";
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("1 of 3", Named<TextBlock>(view, "SearchCount").Text);

        Named<Button>(view, "SearchNext").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("2 of 3", Named<TextBlock>(view, "SearchCount").Text);
    }

    [AvaloniaFact]
    public void CopyPageOnTheHeadsetPutsTheTranscriptOnTheClipboard()
    {
        var (panel, view, clipboard) = Headset(Said());
        using var _disposable = panel;

        Serve(panel);

        var copy = Named<Button>(view, "CopyButton");

        Assert.True(copy.IsVisible);

        copy.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.Contains("Fixture Anchorage has fuel.", clipboard.Last, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public void TheSearchSteppersAndCopyPageAreAsTallAsAFingerTarget()
    {
        var (panel, view, _) = Headset(Said());
        using var _disposable = panel;

        Serve(panel);

        foreach (var name in new[] { "SearchPrevious", "SearchNext", "CopyButton" })
        {
            Assert.True(Named<Button>(view, name).MinHeight >= 44, name);
        }
    }

    [AvaloniaFact]
    public void AChecklistFilterTypedOnTheHeadsetNarrowsTheLines()
    {
        var paths = new AppPaths(TempFolders.Create("d47-headset-filter"));
        paths.EnsureCreated();

        var checklists = new ChecklistService(
            new ChecklistStore(
                Path.Combine(paths.Data, "checklist.json"),
                new MemoryFileSystem(),
                NullLogger<ChecklistStore>.Instance),
            new ChecklistProposalStore(
                Path.Combine(paths.Data, "checklist-proposals.json"),
                new MemoryFileSystem(),
                NullLogger<ChecklistProposalStore>.Instance),
            () => null);

        checklists.AddNote(ChecklistScope.Universal, "buy limpets");
        checklists.AddNote(ChecklistScope.Universal, "fit a fuel scoop");

        var (panel, view, _) = Headset(new PanelViewModel(), checklists);
        using var _disposable = panel;

        PanelPhrases.Apply("show me the checklist", panel.Nav);
        panel.Nav.Drill(ChecklistPage.AllLists);
        Serve(panel);

        var box = Named<TextBox>(view, "SearchInput");

        Assert.True(box.IsVisible);
        Assert.Contains("buy limpets", Drawn(view), StringComparison.Ordinal);

        box.Text = "scoop";
        Serve(panel);

        Assert.DoesNotContain("buy limpets", Drawn(view), StringComparison.Ordinal);
        Assert.Contains("fuel scoop", Drawn(view), StringComparison.Ordinal);
    }

    private static string Drawn(PanelView view) =>
        string.Join(
            "\n",
            view.GetVisualDescendants().OfType<ChecklistPage>().Single()
                .GetVisualDescendants().OfType<TextBlock>().Select(block => block.Text));
}
