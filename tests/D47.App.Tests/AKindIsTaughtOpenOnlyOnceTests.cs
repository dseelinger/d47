using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.Core.Journal;
using Xunit;

namespace D47.App.Tests;

/// <summary>What this means opens the first time a kind is selected in a run, and is folded after (#832).</summary>
public sealed class AKindIsTaughtOpenOnlyOnceTests
{
    private static JournalEvent Event(string kind, int second) =>
        new(new DateTimeOffset(2026, 8, 27, 12, 3, second, TimeSpan.Zero),
            kind,
            JsonDocument.Parse($$"""{"event":"{{kind}}"}""").RootElement);

    private static (PanelView Panel, PanelViewModel Model, Window Window) Shown()
    {
        var log = new JournalLog();

        // Music is the newest, so the entry selected on load teaches nothing.
        log.Add([Event("FSDJump", 1), Event("FSDJump", 2), Event("Docked", 3), Event("Music", 4), Event("Music", 5)]);

        var model = new PanelViewModel { JournalNoise = true, JournalSource = noise => log.Read(noise) };
        var panel = new PanelView { DataContext = model };
        var window = new Window { Content = panel, Width = 1180, Height = 800 };

        window.Show();
        panel.Page = TranscriptPage.Journal;
        Dispatcher.UIThread.RunJobs();

        return (panel, model, window);
    }

    private static void Select(PanelView panel, PanelViewModel model, JournalEntry entry)
    {
        var list = panel.GetVisualDescendants().OfType<ListBox>().Single(box => box.Name == "JournalList");

        list.SelectedIndex = list.ItemsSource!.Cast<JournalEntry>().ToList().FindIndex(line => ReferenceEquals(line, entry));
        Dispatcher.UIThread.RunJobs();
    }

    private static Control? Paragraph(PanelView panel) =>
        panel.GetVisualDescendants().OfType<TextBlock>().SingleOrDefault(block => block.Name == "ReadingParagraph");

    private static JournalEntry Of(PanelViewModel model, string kind, int skip = 0) =>
        model.Journal.Where(entry => entry.Kind == kind).Skip(skip).First();

    [AvaloniaFact]
    public void TheFirstJumpIsReadOpenAndTheNextOneFolded()
    {
        var (panel, model, window) = Shown();
        Assert.DoesNotContain("FSDJump", model.TaughtKinds);

        Select(panel, model, Of(model, "FSDJump"));
        Assert.True(Paragraph(panel)!.IsEffectivelyVisible);

        Select(panel, model, Of(model, "FSDJump", 1));
        Assert.False(Paragraph(panel)!.IsEffectivelyVisible);

        Select(panel, model, Of(model, "Music"));
        Select(panel, model, Of(model, "FSDJump"));
        Assert.False(Paragraph(panel)!.IsEffectivelyVisible);

        window.Close();
    }

    [AvaloniaFact]
    public void ADockingAfterAJumpIsReadOpen()
    {
        var (panel, model, window) = Shown();

        Select(panel, model, Of(model, "FSDJump"));
        Select(panel, model, Of(model, "Docked"));

        Assert.True(Paragraph(panel)!.IsEffectivelyVisible);

        window.Close();
    }

    [AvaloniaFact]
    public void AKindWithNoParagraphHasNoBandAndIsNotRecorded()
    {
        var (panel, model, window) = Shown();
        var before = model.TaughtKinds.ToList();

        Select(panel, model, Of(model, "Music"));
        Assert.Null(Paragraph(panel));

        Select(panel, model, Of(model, "Music", 1));
        Assert.Null(Paragraph(panel));
        Assert.Equal(before, model.TaughtKinds.ToList());

        window.Close();
    }
}
