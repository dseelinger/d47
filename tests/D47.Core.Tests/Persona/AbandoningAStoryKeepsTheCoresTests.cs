using D47.Core.Adventures;
using D47.Core.Persona;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using static D47.Core.Tests.Adventures.AdventureFixtures;

namespace D47.Core.Tests.Persona;

public class AbandoningAStoryKeepsTheCoresTests : IDisposable
{
    private readonly string _folder = Path.Combine(
        Path.GetTempPath(), "d47-guardian-cores", Guid.NewGuid().ToString("N"));

    public AbandoningAStoryKeepsTheCoresTests()
    {
        Directory.CreateDirectory(_folder);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);

        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    private GuardianCores Open() => GuardianCores.Open(
        Path.Combine(_folder, "guardian-cores.json"), installExisted: false, NullLogger<GuardianCores>.Instance);

    [Fact]
    public void TheCoresAreStillAwakeAfterTheStoryIsAbandonedAndBegunAgain()
    {
        var cores = Open();
        var book = new AdventureBook(
            new AdventureStore(Path.Combine(_folder, "adventures.json"), NullLogger<AdventureStore>.Instance),
            NullLogger<AdventureBook>.Instance);

        book.Write("F1", LanternRoute());
        Assert.Null(book.Begin("F1", "the-lantern-route", Accepted));

        foreach (var journalEvent in BeaconFixture.Events())
        {
            cores.Apply(journalEvent);
            book.Observe(journalEvent, "F1");
        }

        Assert.Null(book.Abandon("F1", "the-lantern-route", Accepted.AddHours(1)));
        Assert.True(cores.CoresAwake);

        Assert.Null(book.Begin("F1", "the-lantern-route", Accepted.AddDays(1)));
        Assert.True(Open().CoresAwake);
    }
}
