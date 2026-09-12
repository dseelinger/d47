using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Knowledge;

public class ASystemNameIsFoundInASentenceTests
{
    private static readonly string[] Nothing = [];

    private static IReadOnlyList<string> Found(string text, params string[] known) =>
        [.. SystemNameFinder.Find(text, known).Select(hit => hit.Name)];

    [Theory]
    [InlineData("Dryafea PO-X d2-0")]
    [InlineData("Synuefe XR-H d11-102")]
    [InlineData("Col 285 Sector AB-C d1-23")]
    [InlineData("Pru Aescs NC-M d7-192")]
    [InlineData("Musca Dark Region PJ-P b6-0")]
    [InlineData("Pipe (stem) Sector ZE-A d89")]
    public void AProceduralNameIsFoundWithTheWordsEitherSideLeftOut(string name)
    {
        var text = $"the next stop is {name} and then home";
        var hit = Assert.Single(SystemNameFinder.Find(text, Nothing));

        Assert.Equal(name, hit.Name);
        Assert.Equal(text.IndexOf(name, StringComparison.Ordinal), hit.Start);
        Assert.Equal(name.Length, hit.Length);
    }

    [Theory]
    [InlineData("HIP 12099")]
    [InlineData("HD 43193")]
    [InlineData("LHS 3447")]
    [InlineData("Wolf 359")]
    [InlineData("LTT 9455")]
    [InlineData("Gliese 868")]
    [InlineData("BD+03 2338")]
    [InlineData("LP 71-165")]
    [InlineData("Ross 128")]
    public void ACatalogueNameIsOneHit(string name)
    {
        Assert.Equal([name], Found($"plot a route to {name}, please"));
    }

    [Fact]
    public void ACataloguePrefixBeforeAWordIsNoHit()
    {
        Assert.Empty(Found("the HD video is ready"));
    }

    [Theory]
    [InlineData("Deciat")]
    [InlineData("Shinrarta Dezhra")]
    [InlineData("Achenar")]
    public void AHandNamedSystemIsFoundFromTheTable(string name)
    {
        Assert.Equal([name], Found($"we are flying to {name} tonight"));
    }

    [Theory]
    [InlineData("we are flying to deciat tonight")]
    [InlineData("we are flying to Deciatu tonight")]
    [InlineData("we are flying to undeciat tonight")]
    public void TheTableMatchesExactCaseAndWholeWordsOnly(string text)
    {
        Assert.Empty(Found(text));
    }

    [Fact]
    public void AOneWordTableNameStartingASentenceIsNotAHitUnlessItIsKnown()
    {
        Assert.Empty(Found("Deciat is where Farseer is"));
        Assert.Equal(["Deciat"], Found("Deciat is where Farseer is", "Deciat"));
    }

    [Fact]
    public void AKnownNameIsFoundInAnyCaseAndTakesTheKnownSpelling()
    {
        const string text = "two jumps from deciat's star";

        var hit = Assert.Single(SystemNameFinder.Find(text, ["Deciat"]));

        Assert.Equal("Deciat", hit.Name);
        Assert.Equal(text.IndexOf("deciat", StringComparison.Ordinal), hit.Start);
        Assert.Equal("Deciat".Length, hit.Length);
    }

    [Fact]
    public void TheLongerNameWinsOnce()
    {
        Assert.Equal(["Shinrarta Dezhra"], Found("the permit for Shinrarta Dezhra is Elite", "Dezhra"));
    }

    [Fact]
    public void ANameInBackticksIsFoundLikeProse()
    {
        Assert.Equal(["Synuefe XR-H d11-102"], Found("jump to `Synuefe XR-H d11-102` next"));
    }

    [Fact]
    public void HitsComeInTextOrder()
    {
        var hits = SystemNameFinder.Find("from HIP 12099 to Dryafea PO-X d2-0 via Achenar", Nothing);

        Assert.Equal(["HIP 12099", "Dryafea PO-X d2-0", "Achenar"], hits.Select(hit => hit.Name));
    }
}
