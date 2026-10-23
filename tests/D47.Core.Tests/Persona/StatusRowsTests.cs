using D47.Core.Persona;
using Xunit;

namespace D47.Core.Tests.Persona;

public class StatusRowsTests
{
    [Fact]
    public void NoIntroductionsNamesNoCoreAndHasNoSecondLine()
    {
        var text = StatusRows.Introductions(0);

        Assert.Equal("No core has introduced itself yet", text.Block);
        Assert.Null(text.Second);
    }

    [Theory]
    [InlineData(1, "1 has so far")]
    [InlineData(7, "7 have so far")]
    public void IntroductionsCountWhatHasIntroducedItself(int introduced, string second)
    {
        var text = StatusRows.Introductions(introduced);

        Assert.Equal("Every core introduces itself again", text.Block);
        Assert.Equal(second, text.Second);
    }

    [Theory]
    [InlineData(0, "None yet. A core needs a name and a paragraph saying what it is like.")]
    [InlineData(1, "")]
    [InlineData(3, "")]
    [InlineData(12, "")]
    public void OwnCoresStateOnlyThatThereAreNone(int count, string expected)
    {
        string[] first = ["Vesper", "Halcyon", "Old Tam"];
        var names = Enumerable.Range(0, count).Select(i => i < 3 ? first[i] : $"Core {i}").ToList();

        Assert.Equal(expected, StatusRows.OwnCores(names));
    }
}
