using D47.App.Panel;
using D47.Core.Callouts;
using Xunit;

namespace D47.App.Tests;

public class ACommsLineKeepsItsOriginInTheTranscriptTests
{
    [Fact]
    public void ACommsLineIsASegmentWithThatOrigin()
    {
        var model = new PanelViewModel();
        model.Append("\nNo fire zone entered.\n", speaker: "Jameson Memorial", sourceKey: "message.npc", origin: AnnouncementOrigin.Comms);

        var segment = Assert.Single(model.Segments(TranscriptPage.Conversation));

        Assert.Equal(AnnouncementOrigin.Comms, segment.Origin);
    }

    [Fact]
    public void LinesOfDifferentOriginsDoNotMerge()
    {
        var model = new PanelViewModel();
        model.Append("\nA\n", speaker: "Tower", sourceKey: "k", origin: AnnouncementOrigin.Comms);
        model.Append("\nB\n", speaker: "Tower", sourceKey: "k", origin: AnnouncementOrigin.InventedChatter);

        Assert.Equal(
            [AnnouncementOrigin.Comms, AnnouncementOrigin.InventedChatter],
            model.Segments(TranscriptPage.Conversation).Select(s => s.Origin));
    }

    [Fact]
    public void ALineWithNoOriginHasNone()
    {
        var model = new PanelViewModel();
        model.Append("Fuel low.", sourceKey: "fuel.low");

        Assert.Equal(AnnouncementOrigin.None, Assert.Single(model.Segments(TranscriptPage.Conversation)).Origin);
    }

    [Fact]
    [Trait("Category", "Gate")]
    public void SayAsyncHandsTheOriginToTheTranscript()
    {
        var raise = Assert.Single(AppSource.CodeLines("CalloutSaid?.Invoke("));

        Assert.Contains("written.Origin", raise.Text, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "Gate")]
    public void EveryComposedChatterLineIsTaggedInvented()
    {
        var composed = AppSource.CodeLinesIn(
            Path.Combine(AppSource.RepositoryRoot(), "src", "D47.App", "Voice", "LineWriter.cs"),
            "Origin = AnnouncementOrigin.InventedChatter");

        Assert.Single(composed);
    }
}
