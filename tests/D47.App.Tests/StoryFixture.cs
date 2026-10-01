using D47.Core.Stories;

namespace D47.App.Tests;

/// <summary>Invented stock stories, two of them, each with a hidden layer.</summary>
internal static class StoryFixture
{
    public static readonly StoryCard Story = new()
    {
        Id = "the-test-story",
        Number = 1,
        Title = "The Test Story",
        Genre = "Whydunit",
        Level = "new",
        Tone = "Quiet test",
        Core = "archivist",
        Blurb = "A voice in the cockpit knows a song it should not.",
        InYourWords = "I bought a Sidewinder with the last of my credits.",
        Beacon = "The beacon calls.",
    };

    public static readonly StoryCard Other = Story with
    {
        Id = "the-other-story",
        Number = 2,
        Title = "The Other Story",
        Genre = "Buddy Love",
        Blurb = "Two pilots, one hull, and a debt neither will name.",
        InYourWords = "I was somebody else.",
    };

    public static readonly StorySecret Secret = new()
    {
        Id = Story.Id,
        Secret = "The ship's voice is the Commander's sister.",
        End = "Keep her or let her go.",
        Beats = new StoryBeats { Midpoint = "She remembers the old address on the far side of the Bubble." },
        Clues =
        [
            new("She hums a song from home.", StorySpeaker.Ship),
            new("She knows the old address.", StorySpeaker.Narrator),
        ],
        Finale = [new("She says the name.", StorySpeaker.Ship)],
        Options = [new StoryOption { Id = "keep", Label = "Keep her", After = "She stays aboard for good." }],
    };

    public static readonly StoryCatalog Catalog = new([Story, Other], () => [Secret, Secret with { Id = Other.Id }]);

    public static readonly StoryCatalog Empty = new([], () => []);
}
