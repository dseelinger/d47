using D47.Core.Persona;
using Xunit;

namespace D47.Core.Tests.Persona;

/// <summary>When one scan finishes chapter one and wakes the cores, the waking waits until the beat's line is said.</summary>
public sealed class TheBeatIsToldBeforeTheCoresWakeTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    private static readonly D47.Core.Persona.Persona Archivist = PersonaCatalog.Resolve("archivist");

    [Fact]
    public void AWakingWithNoBeatOwedIsDueAtOnce()
    {
        var wakings = new WakingAfterTheBeat();
        wakings.Add(CoreWaking.Cores, Archivist, null, Now);

        Assert.Equal([(CoreWaking.Cores, Archivist)], wakings.Due(_ => true, Now));
        Assert.Empty(wakings.Due(_ => true, Now));
    }

    [Fact]
    public void AWakingWaitsWhileItsChapterIsOwedALine()
    {
        var wakings = new WakingAfterTheBeat();
        var owed = true;

        wakings.Add(CoreWaking.Cores, Archivist, "chapter-one", Now);

        Assert.Empty(wakings.Due(chapter => owed && chapter == "chapter-one", Now.AddSeconds(30)));

        owed = false;

        Assert.Equal([(CoreWaking.Cores, Archivist)], wakings.Due(_ => owed, Now.AddSeconds(40)));
    }

    [Fact]
    public void TheCoreIsTheOneResolvedAtTheScan()
    {
        var wakings = new WakingAfterTheBeat();
        wakings.Add(CoreWaking.Cores, Archivist, "chapter-one", Now);

        Assert.Equal(Archivist, Assert.Single(wakings.Due(_ => false, Now.AddSeconds(30))).Core);
    }

    [Fact]
    public void ALineThatIsNeverSaidHoldsTheWakingNoLongerThanTheLimit()
    {
        var wakings = new WakingAfterTheBeat();
        wakings.Add(CoreWaking.Cores, Archivist, "chapter-one", Now);

        Assert.Empty(wakings.Due(_ => true, Now + WakingAfterTheBeat.Longest - TimeSpan.FromSeconds(1)));
        Assert.Equal([(CoreWaking.Cores, Archivist)], wakings.Due(_ => true, Now + WakingAfterTheBeat.Longest));
    }

    [Fact]
    public void WakingsKeepTheirOrder()
    {
        var wakings = new WakingAfterTheBeat();
        wakings.Add(CoreWaking.Cores, Archivist, "chapter-one", Now);
        wakings.Add(CoreWaking.Heretic, null, null, Now.AddSeconds(1));

        Assert.Empty(wakings.Due(_ => true, Now.AddSeconds(2)));
        Assert.Equal([(CoreWaking.Cores, Archivist), (CoreWaking.Heretic, null)], wakings.Due(_ => false, Now.AddSeconds(3)));
    }
}
