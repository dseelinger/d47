using D47.Core.Capabilities;
using Xunit;

namespace D47.Core.Tests;

public class ToolArgumentReadersTests
{
    private static ToolArguments With(string value) =>
        new(new Dictionary<string, string> { ["x"] = value });

    [Fact]
    public void AnAbsentArgumentReadsAsNullFromEveryReader()
    {
        var none = ToolArguments.Empty;

        Assert.Null(none.Text("x"));
        Assert.Null(none.Int32("x"));
        Assert.Null(none.Double("x"));
        Assert.Null(none.Boolean("x"));
    }

    [Fact]
    public void AWhitespaceValueReadsAsNullFromText()
    {
        Assert.Null(With("   ").Text("x"));
    }

    [Fact]
    public void TextComesBackUntrimmed()
    {
        Assert.Equal(" Sol ", With(" Sol ").Text("x"));
    }

    [Fact]
    public void AValueThatDoesNotParseReadsAsNull()
    {
        var word = With("lots");

        Assert.Null(word.Int32("x"));
        Assert.Null(word.Double("x"));
        Assert.Null(word.Boolean("x"));
    }

    [Fact]
    public void AValueThatParsesComesBackAsItsType()
    {
        Assert.Equal(7, With("7").Int32("x"));
        Assert.Equal(1.5, With("1.5").Double("x"));
        Assert.True(With("true").Boolean("x"));
        Assert.False(With("false").Boolean("x"));
    }
}
