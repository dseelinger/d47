using D47.Core.Stories;

namespace D47.App.Tests;

/// <summary>Invented stock stories: two offered, one waiting on an untracked event, each with a hidden layer.</summary>
internal static class StoryFixture
{
    public static readonly StoryCard Story = new()
    {
        Id = "the-test-story",
        Number = 1,
        Title = "The Test Story",
        Tone = "Quiet test",
        InYourWords = "I bought a Sidewinder with the last of my credits.",
        Beacon = "The beacon calls.",
    };

    public static readonly StoryCard Other = Story with
    {
        Id = "the-other-story",
        Number = 2,
        Title = "The Other Story",
        InYourWords = "I was somebody else.",
    };

    public static readonly StoryCard Waiting = Story with
    {
        Id = "the-waiting-story",
        Number = 3,
        Title = "The Waiting Story",
        InYourWords = "I hum near the swarm.",
        Requires = "thargoids",
    };

    public static readonly StorySecret Secret = new()
    {
        Id = Story.Id,
        Secret = "The ship's voice is the Commander's sister.",
        Weeks = "She hums a song from home.",
        Months = "She knows the old address.",
        Year = "She says the name.",
        End = "Keep her or let her go.",
    };

    public static readonly StoryCatalog Catalog = new(
        [Story, Other, Waiting],
        () => [Secret, Secret with { Id = Other.Id }, Secret with { Id = Waiting.Id }]);
}
