using D47.Core.Adventures;
using D47.Core.Messages;
using D47.Core.Storage;
using D47.Core.Stories;
using D47.Core.Tests.Stories;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Messages;

/// <summary>An adventure store on disk in a temp folder, an in-memory message store and story store, and the orphan sweep over them.</summary>
[Trait("Category", "Integration")]
public sealed class MessageOrphanRig : IDisposable
{
    public MessageOrphanRig(string folderName)
    {
        Folder = Path.Combine(Path.GetTempPath(), folderName, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Folder);
        Adventures = new AdventureStore(Path.Combine(Folder, "adventures.json"), new DiskFileSystem(), NullLogger<AdventureStore>.Instance);
        Messages = new MessageStore(Path.Combine(Folder, "messages.json"), new MemoryFileSystem(), NullLogger<MessageStore>.Instance);
    }

    public string Folder { get; }

    public AdventureStore Adventures { get; }

    public MessageStore Messages { get; }

    public StoryStore Stories { get; } = InMemoryStories.Create();

    public int Sweep() =>
        Messages.RemoveOrphans(key => MessageOwnership.Owned(key, Adventures, Stories, () => StoryFixtures.Catalog));

    public void Dispose()
    {
        if (Directory.Exists(Folder))
        {
            Directory.Delete(Folder, recursive: true);
        }
    }
}
