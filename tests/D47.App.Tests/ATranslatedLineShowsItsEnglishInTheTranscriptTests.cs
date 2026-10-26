using D47.App.Panel;
using Avalonia.Headless;
using D47.App.Controls;
using Xunit;

namespace D47.App.Tests;

public sealed class ATranslatedLineShowsItsEnglishInTheTranscriptTests
{
    [Fact]
    public void TheEnglishIsCarriedOnTheSegmentOfTheLineThatWasHeard()
    {
        var model = new PanelViewModel();
        model.Append("\n無事でよかった。\n", sourceKey: "story.clue", translation: "Good to see you made it.");

        var segment = Assert.Single(model.Segments(TranscriptPage.Conversation));

        Assert.Equal("Good to see you made it.", segment.Translation);
        Assert.Contains("無事でよかった。", segment.Text);
    }

    [Fact]
    public void TheCopiedTranscriptHoldsBothParagraphs()
    {
        var model = new PanelViewModel();
        model.Append("\n無事でよかった。\n", sourceKey: "story.clue", translation: "Good to see you made it.");

        Assert.Contains("無事でよかった。\n\nGood to see you made it.", model.TranscriptText);
    }

    [Fact]
    public void ALineWithoutATranslationHasNone()
    {
        var model = new PanelViewModel();
        model.Append("Fuel low.", sourceKey: "fuel.low");

        Assert.Null(Assert.Single(model.Segments(TranscriptPage.Conversation)).Translation);
    }

    [Fact]
    public void ATranslatedLineDoesNotMergeWithTheNext()
    {
        var model = new PanelViewModel();
        model.Append("\nA\n", sourceKey: "k", translation: "a");
        model.Append("\nB\n", sourceKey: "k", translation: "b");

        Assert.Equal(["a", "b"], model.Segments(TranscriptPage.Conversation).Select(s => s.Translation));
    }
}

public class AJapaneseLineIsDrawnWithItsGlyphsTests
{
    [Avalonia.Headless.XUnit.AvaloniaFact]
    public void TheJapaneseIsDrawnAboveItsEnglish()
    {
        var model = new PanelViewModel();
        var panel = new PanelView { DataContext = model };
        var window = new Avalonia.Controls.Window { Content = panel, Width = 900, Height = 600 };
        window.Show();

        model.Append("\n無事でよかった。\n", sourceKey: "story.clue", translation: "Good to see you made it.");
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        using var frame = window.CaptureRenderedFrame()!;
        frame.SaveCapture("japanese-line-with-translation.png");

        window.Close();
    }
}
