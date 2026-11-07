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
}
