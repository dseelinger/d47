using D47.App.Panel;
using Xunit;

namespace D47.App.Tests;

public sealed class TheTranscriptCanBeDrawnWhileALineIsAddedTests
{
    [Fact]
    public async Task SegmentsReadWhileAppendRunsDoNotThrow()
    {
        var panel = new PanelViewModel();
        var done = false;

        var writer = Task.Run(() =>
        {
            for (var i = 0; i < 20000; i++)
            {
                panel.Append("word " + i + " ");
            }
            Volatile.Write(ref done, true);
        }, TestContext.Current.CancellationToken);

        var reader = Task.Run(() =>
        {
            while (!Volatile.Read(ref done))
            {
                panel.Segments(TranscriptPage.Conversation);
                panel.Segments(TranscriptPage.Conversation, framed: false);
            }
        }, TestContext.Current.CancellationToken);

        await Task.WhenAll(writer, reader);
    }
}
