using D47.Core.Adventures;
using D47.Core.Callouts;
using D47.Core.Journal;
using D47.Core.Tests.Adventures;
using D47.Core.Tests.Conversation;
using Xunit;
using static D47.Core.Tests.Stories.StoryFixtures;

namespace D47.Core.Tests.Stories;

internal static class PauseSupport
{
    public static StoryFixtures Picked(out string chapter)
    {
        var fixtures = new StoryFixtures(new RoundScriptedLlmProvider(
            RoundScriptedLlmProvider.Saying(Spine),
            RoundScriptedLlmProvider.Saying(BeatsToTheBeacon)));

        Assert.Null(fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None).GetAwaiter().GetResult());
        chapter = Assert.Single(fixtures.Stories.Current("F1")!.Chapters);
        return fixtures;
    }

    public static CalloutContext At(DateTimeOffset now, params JournalEvent[] events) =>
        new(now, false, new CommanderGameState(new CommanderIdentity("F1", "Tester")), GameStatus.Unknown, NavRoute.None, events);

    public static JournalEvent FirstBeat(StoryFixtures fixtures, string chapter, DateTimeOffset at) =>
        AdventureFixtures.Jump(fixtures.Book.Store.Find("F1", chapter)!.Beats[0].Trigger.SystemAddress!.Value, at);

    /// <summary>What the callout says for these events, and thirty seconds on when the settle has passed.</summary>
    public static List<Announcement> Said(AdventureCallout callout, DateTimeOffset at, params JournalEvent[] events) =>
        [.. callout.Examine(At(at, events)), .. callout.Examine(At(at.AddSeconds(30)))];
}
