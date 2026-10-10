using Xunit;

namespace D47.Core.Tests.Debrief;

[Trait("Category", "Integration")]
public sealed class TheDebriefRecordsOnlyWhileItIsOnTests
{
    [Fact]
    public void ASessionNotedWhileOnIsDrafted()
    {
        using var debrief = new ADebriefOnDisk();

        debrief.NoteASession();

        Assert.Equal(3, debrief.Host.Session.Lines.Count);

        debrief.Host.Run(ADebriefOnDisk.Commander);

        Assert.Contains(debrief.Store.For(ADebriefOnDisk.Commander), entry => entry.Because == ADebriefOnDisk.Correction);
        Assert.True(debrief.Store.For(ADebriefOnDisk.Commander).Count > 1);
    }

    [Fact]
    public void NothingNotedWhileOffIsKept()
    {
        using var debrief = new ADebriefOnDisk();

        debrief.Switch(on: false);
        debrief.NoteASession();

        Assert.Empty(debrief.Host.Session.Lines);

        debrief.Switch(on: true);
        debrief.Host.Run(ADebriefOnDisk.Commander);

        Assert.Empty(debrief.Store.For(ADebriefOnDisk.Commander));
    }

    [Fact]
    public void ARunWhileOffDraftsNothing()
    {
        using var debrief = new ADebriefOnDisk();

        debrief.NoteASession();
        debrief.Switch(on: false);
        debrief.Host.Run(ADebriefOnDisk.Commander);

        Assert.Empty(debrief.Store.For(ADebriefOnDisk.Commander));
    }
}
