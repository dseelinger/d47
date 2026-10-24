using D47.Core.Adventures;
using D47.Core.Persona;
using D47.Core.Tests.Conversation;
using Xunit;

namespace D47.Core.Tests.Stories;

/// <summary>The brief a cast member answers the Commander from carries the Commander's notes after the story's own description (#1600).</summary>
public sealed class TheCommandersNotesFollowACastMembersWhoTests
{
    private const string Who = "A courier riding along, dry and patient.";

    private const string Notes = "Calls the Commander boss and hates jump tunnels.";

    private static readonly DateTimeOffset Now = new(2026, 10, 24, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task TheNotesComeAfterWho()
    {
        var brief = await BriefOf(new AdventureSpeaker("rider", "Vale", Who) { Notes = Notes });

        Assert.Contains(Who, brief, StringComparison.Ordinal);
        Assert.Contains($"The Commander's notes on Vale: {Notes}", brief, StringComparison.Ordinal);
        Assert.True(brief.IndexOf(Who, StringComparison.Ordinal) < brief.IndexOf(Notes, StringComparison.Ordinal));
    }

    [Fact]
    public async Task AMemberWithoutNotesHasNoNotesLine() =>
        Assert.DoesNotContain("The Commander's notes", await BriefOf(new AdventureSpeaker("rider", "Vale", Who)), StringComparison.Ordinal);

    [Fact]
    public async Task TheMemberAnsweringCarriesTheNotesKeptForThemInThisStory()
    {
        using var fixtures = new StoryFixtures(new RoundScriptedLlmProvider(
            RoundScriptedLlmProvider.Saying(StoryFixtures.Spine),
            RoundScriptedLlmProvider.Saying(StoryFixtures.BeatsToTheBeacon),
            RoundScriptedLlmProvider.Saying(StoryFixtures.NothingObjected)));
        fixtures.Director.CastNotes = (fid, storyId, castId) => (fid, storyId, castId) == ("F1", StoryFixtures.Id, "dock-hand") ? Notes : null;

        Assert.Null(await fixtures.Director.PickAsync("F1", StoryFixtures.Id, StoryFixtures.Now, CancellationToken.None));

        Assert.Equal(Notes, fixtures.Director.Answering("F1", "dock-hand")!.Notes);
    }

    private static async Task<string> BriefOf(AdventureSpeaker member)
    {
        var line = new CastLine(_ => member, () => null);

        line.Heard(member.Id, "Hold the course.");

        return Assert.IsType<LineDecision.Taken>(await line.RouteAsync(D47.Core.Tests.Conversation.OnComms.Cast(member.Id, member.Name), "Who are you?", CancellationToken.None)).Speaker.Brief;
    }
}
