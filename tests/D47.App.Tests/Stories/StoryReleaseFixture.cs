using D47.Core.Stories;

namespace D47.App.Tests.Stories;

internal static class StoryReleaseFixture
{
    public static readonly string Id = StoryFixture.Story.Id;

    /// <summary>The test story with one cast member who has a picture, and one in two versions.</summary>
    public static readonly StorySecret Secret = StoryFixture.Secret with
    {
        Cast =
        [
            new StorySpeaker { Id = "ren", Name = "Ren", Who = "A dock hand.", Provider = StorySpeaker.Kokoro, Voice = "bm_george" },
            new StorySpeaker
            {
                Id = "cray",
                Who = "A test engineer.",
                Provider = StorySpeaker.Kokoro,
                Versions = new StorySpeakerVersions
                {
                    ForMan = new StorySpeakerVersion { Name = "Ellie", Voice = "af_heart" },
                    ForWoman = new StorySpeakerVersion { Name = "Ellis", Voice = "am_michael" },
                },
            },
        ],
    };

    /// <summary>The picture files <see cref="Secret"/> names.</summary>
    public static readonly string[] Pictures =
    [
        "the-test-story.ren.jpg",
        "the-test-story.cray.for-man.jpg",
        "the-test-story.cray.for-woman.jpg",
    ];

    public static void ServeAll(StoryRelease release)
    {
        release.ServeIndex(StoryFixture.Story);
        release.ServeSealed(Secret);

        foreach (var picture in Pictures)
        {
            release.Serve(picture, [1, 2, 3]);
        }
    }
}
