using Xunit;

namespace D47.Core.Tests.Debrief;

[Trait("Category", "Integration")]
public sealed class ADebriefStartsTheNextSessionEmptyTests
{
    [Fact]
    public void ARunEmptiesTheSession()
    {
        using var debrief = new ADebriefOnDisk();

        debrief.NoteASession();
        debrief.Host.Run(ADebriefOnDisk.Commander);

        Assert.Empty(debrief.Host.Session.Lines);
    }

    [Fact]
    public void ASecondRunDraftsNothingFromTheSameSignals()
    {
        using var debrief = new ADebriefOnDisk();

        debrief.NoteASession();
        debrief.Host.Run(ADebriefOnDisk.Commander);

        var drafted = debrief.Store.For(ADebriefOnDisk.Commander);
        Assert.NotEmpty(drafted);

        // Forgotten, so a second draft is not refused as a duplicate of the first.
        foreach (var entry in drafted)
        {
            debrief.Book.Forget(entry.Key);
        }

        debrief.Host.Run(ADebriefOnDisk.Commander);

        Assert.Empty(debrief.Store.For(ADebriefOnDisk.Commander));
    }
}
