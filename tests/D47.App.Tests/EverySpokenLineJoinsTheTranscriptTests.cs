using D47.App;
using D47.Core.Audio;
using D47.Core.Callouts;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// Every line d47 speaks reaches the Transcript from <c>SayAsync</c>, including in-game comms and
/// overheard chatter, named by <see cref="AppHost.ConversationSpeaker"/>.
/// </summary>
public class EverySpokenLineJoinsTheTranscriptTests
{
    [Fact]
    public void SayAsyncRaisesTheTranscriptLineAfterTheVoiceHasTheAnnouncement()
    {
        var say = Body("private async Task<SpokenClip?> SayAsync(");

        var announce = say.IndexOf("Voice.AnnounceAsync(", StringComparison.Ordinal);
        var raised = say.IndexOf("CalloutSaid?.Invoke(", StringComparison.Ordinal);

        Assert.True(announce >= 0 && raised > announce);
    }

    [Fact]
    public void OnlySayAsyncRaisesTheTranscriptLine()
    {
        var raisers = File.ReadAllLines(Path.Combine(RepositoryRoot(), "src", "D47.App", "AppHost.cs"))
            .Select(line => line.Trim())
            .Where(line => line.Contains("CalloutSaid?.Invoke(", StringComparison.Ordinal))
            .ToList();

        Assert.Single(raisers);
    }

    [Fact]
    public void ATowerLineIsNamedTower()
    {
        var announcement = new Announcement("carrier.departure", "Sacred Fire clear. Safe flying, Commander.")
        {
            Voice = VoiceRole.TowerControl,
        };

        Assert.Equal("Tower", AppHost.ConversationSpeaker(announcement));
    }

    [Fact]
    public void ARelayIsNamedForItsSender()
    {
        var announcement = new Announcement("message.npc", "Clear skies out there.")
        {
            Voice = VoiceRole.Comms,
            Speaker = "Ilse Bruhn",
            CommsChannel = "npc",
        };

        Assert.Equal("Ilse Bruhn", AppHost.ConversationSpeaker(announcement));
    }

    [Fact]
    public void ADirectMessageIsNamedForItsSender()
    {
        var announcement = new Announcement("message.player", "Vex says: Meet at the beacon.")
        {
            Voice = VoiceRole.Comms,
            Speaker = "Vex",
            SpeakerIsPlayer = true,
            CommsChannel = "player",
        };

        Assert.Equal("Vex", AppHost.ConversationSpeaker(announcement));
    }

    [Fact]
    public void ANarratedLineIsNamedTheNarrator()
    {
        var announcement = new Announcement("story.end.x", "The beacon went dark.") { Voice = VoiceRole.Narrator };

        Assert.Equal("Narrator", AppHost.ConversationSpeaker(announcement));
    }

    [Fact]
    public void AnOrdinaryCalloutWithNoSpeakerIsNamedD47()
    {
        Assert.Equal("D47", AppHost.ConversationSpeaker(new Announcement("fuel.low", "Fuel scoop advised.")));
    }

    [Fact]
    public void AStoryEndingIsSpokenInTheVoiceItWasWrittenFor()
    {
        var lines = Body("private async Task SpeakStoryLinesAsync(");

        Assert.Contains("Voice = voice", lines, StringComparison.Ordinal);
    }

    private static string Body(string signature)
    {
        var source = File.ReadAllText(Path.Combine(RepositoryRoot(), "src", "D47.App", "AppHost.cs"));
        var start = source.IndexOf(signature, StringComparison.Ordinal);

        Assert.True(start >= 0, $"{signature} not found in AppHost.cs");

        var end = source.IndexOf("\n    }", start, StringComparison.Ordinal);

        return source[start..end];
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "d47.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
               ?? throw new InvalidOperationException(
                   $"Could not find the repository root: no d47.slnx above {AppContext.BaseDirectory}.");
    }
}
