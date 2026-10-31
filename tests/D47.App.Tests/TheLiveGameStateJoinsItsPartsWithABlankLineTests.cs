using Xunit;

namespace D47.App.Tests;

public sealed class TheLiveGameStateJoinsItsPartsWithABlankLineTests
{
    private static readonly string Blank = Environment.NewLine + Environment.NewLine;

    private static string? Nested(string? situation, string? actions) =>
        (situation, actions) switch
        {
            (null, null) => null,
            (null, var only) => only,
            (var only, null) => only,
            var (both, and) => both + Blank + and,
        };

    [Fact]
    public void PartsThatAreAllPresentJoinAsTheNestedFormDid()
    {
        string?[] parts = ["a", "b", "c", "d"];

        Assert.Equal(Nested("a", Nested("b", Nested("c", "d"))), AppHost.Join(parts));
    }

    [Fact]
    public void NullPartsAreLeftOutAsTheNestedFormDid()
    {
        Assert.Equal(Nested("a", Nested(null, Nested("c", null))), AppHost.Join("a", null, "c", null));
        Assert.Equal(Nested(null, Nested(null, "z")), AppHost.Join(null, null, "z"));
    }

    [Fact]
    public void PartsThatAreAllNullJoinToNull()
    {
        Assert.Null(AppHost.Join(null, null, null));
    }
}
