using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;

namespace D47.Core.Stories;

public static class InMemoryStories
{
    /// <summary>An empty <see cref="StoryStore"/> over a <see cref="MemoryFileSystem"/>.</summary>
    public static StoryStore Create() =>
        StoryStore.Open("stories.json", new MemoryFileSystem(), NullLogger<StoryStore>.Instance);
}
