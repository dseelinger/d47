namespace D47.Core.Podcasts;

/// <summary>A named group of suggested shows.</summary>
public sealed record SuggestedGroup(string Name, IReadOnlyList<PodcastSearchResult> Shows);

/// <summary>The shows offered on Media › Podcasts › Add a show, in three groups. Showing them contacts nothing.</summary>
public static class SuggestedPodcasts
{
    public static IReadOnlyList<SuggestedGroup> Groups { get; } =
    [
        new("Elite Dangerous",
        [
            Show("Lave Radio: an Elite Dangerous podcast", "Lave Radio", "https://anchor.fm/s/108adc6a0/podcast/rss"),
            Show("Loose Screws - The Dangerousest", "Loose Screws", "https://anchor.fm/s/d698f68/podcast/rss"),
            Show("Hutton Orbital Radio", "Hutton Truckers", "https://feeds.feedburner.com/HuttonOrbital"),
        ]),
        new("Science fiction",
        [
            Show("Welcome to Night Vale", "Night Vale Presents", "https://feeds.simplecast.com/byk9wCEi"),
            Show("Mission To Zyxx", "Mission To Zyxx", "https://feeds.simplecast.com/ZL7iUDiH"),
            Show("Wolf 359", "Kinda Evil Genius Productions", "https://feeds.megaphone.fm/wolf359radio"),
            Show("The Bright Sessions", "Atypical Artists", "https://rss.art19.com/the-bright-sessions"),
            Show("DERELICT", "Night Rocket Productions", "https://feeds.megaphone.fm/SBP4341397353"),
            Show("Limetown", "Two-Up", "https://feeds.megaphone.fm/PPY5621646923"),
            Show("Escape Pod", "Escape Artists Foundation", "https://escapepod.org/feed/podcast/"),
            Show("Clarkesworld Magazine", "Clarkesworld Magazine", "https://clarkesworldmagazine.com/?feed=podcast"),
            Show("Imaginary Worlds", "Eric Molinsky", "https://feeds.megaphone.fm/ADV7475920787"),
        ]),
        new("Science fact",
        [
            Show("StarTalk with Neil deGrasse Tyson", "Neil deGrasse Tyson", "https://feeds.simplecast.com/4T39_jAj"),
            Show("Planetary Radio", "The Planetary Society", "https://www.omnycontent.com/d/playlist/d95da206-8ee8-4ba5-ba8d-ad1200b4e5a4/cf13d5f5-6040-458d-ab5a-ad200189747d/b75c9f7f-4a63-438e-b506-ad2001897499/podcast.rss"),
            Show("NASA's Curious Universe", "NASA", "https://feeds.megaphone.fm/NATIONALAERONAUTICSANDSPACEADMINISTRATION8162188566"),
            Show("Astronomy Cast", "Fraser Cain and Dr. Pamela Gay", "https://rss.libsyn.com/shows/18112/destinations/11189.xml"),
            Show("Daniel and Kelly's Extraordinary Universe", "iHeartPodcasts", "https://www.omnycontent.com/d/playlist/e73c998e-6e60-432f-8610-ae210140c5b1/f5d5fac6-77be-47e6-9aee-ae32006cd8c3/b26cbbeb-86eb-4b97-9b34-ae32006cd8d6/podcast.rss"),
            Show("Into the Impossible With Brian Keating", "Big Bang Productions", "https://feeds.megaphone.fm/intotheimpossible"),
            Show("Radiolab", "WNYC Studios", "https://feeds.simplecast.com/EmVW7VGp"),
            Show("Ologies with Alie Ward", "Alie Ward", "https://feeds.simplecast.com/FO6kxYGj"),
            Show("Science Vs", "Spotify Studios", "https://feeds.megaphone.fm/sciencevs"),
            Show("Short Wave", "NPR", "https://feeds.npr.org/510351/podcast.xml"),
            Show("Unexplainable", "Vox", "https://feeds.megaphone.fm/VMP9331026707"),
            Show("The Infinite Monkey Cage", "BBC Radio 4", "https://podcasts.files.bbci.co.uk/b00snr0w.rss"),
            Show("Science Friday", "Science Friday and WNYC Studios", "https://feeds.simplecast.com/h18ZIZD_"),
            Show("The Quanta Podcast", "Quanta Magazine", "https://quantapodcast.quantamagazine.org/"),
            Show("Sean Carroll's Mindscape", "Sean Carroll", "https://rss.libsyn.com/shows/604590/destinations/5264190.xml"),
        ]),
    ];

    private static PodcastSearchResult Show(string title, string author, string feed) =>
        new(title, author, new Uri(feed), null, null);
}
