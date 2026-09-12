using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Knowledge;

public class SystemsInPlayAreGatheredWhenAskedTests
{
    [Fact]
    public void EachNameComesOnceWhateverItsCase()
    {
        var systems = new SystemsInPlay();

        systems.Add(() => ["Deciat", "Sol"]);
        systems.Add(() => ["DECIAT", "sol", "Achenar"]);

        Assert.Equal(3, systems.Snapshot().Count);
        Assert.Contains("Achenar", systems.Snapshot());
    }

    [Fact]
    public void NullAndWhitespaceAreDropped()
    {
        var systems = new SystemsInPlay();

        systems.Add(() => [null, "", "   ", "Sol"]);

        Assert.Equal(["Sol"], systems.Snapshot());
    }

    [Fact]
    public void ASourceIsReadAtSnapshotTime()
    {
        var current = "Sol";
        var systems = new SystemsInPlay();

        systems.Add(() => [current]);

        Assert.Equal(["Sol"], systems.Snapshot());

        current = "Deciat";

        Assert.Equal(["Deciat"], systems.Snapshot());
    }
}
