using D47.Core.Persona;
using Xunit;

namespace D47.Core.Tests.Persona;

/// <summary>The ship AI's name or "D47" at the front of an utterance, compared on letters and digits (#1179).</summary>
public class TheShipAiAnswersToItsNameOrD47Tests
{
    private static readonly string[] Names = ["Cora", ShipAiAddressing.CallSign];

    [Theory]
    [InlineData("Cora, take us in", "take us in")]
    [InlineData("cora take us in", "take us in")]
    [InlineData("D47, request docking", "request docking")]
    [InlineData("D-47, request docking", "request docking")]
    [InlineData("d47 request docking", "request docking")]
    [InlineData("D 47: request docking", "request docking")]
    [InlineData("\"Cora,\" captain, how's our fuel?", "captain, how's our fuel?")]
    [InlineData("Cora", "")]
    [InlineData("Cora?", "")]
    public void TheNameComesOffTheFront(string said, string rest) =>
        Assert.Equal(rest, ShipAiAddressing.Opens(said, Names));

    [Theory]
    [InlineData("Cora's fuel is low")]
    [InlineData("Corazon, take us in")]
    [InlineData("D470 request docking")]
    [InlineData("request docking, Cora")]
    [InlineData("")]
    public void AnythingElseIsNotTheName(string said) =>
        Assert.Null(ShipAiAddressing.Opens(said, Names));

    [Fact]
    public void AShorterNameDoesNotClaimALongerOne() =>
        Assert.Null(ShipAiAddressing.Opens("D47 request docking", ["D4"]));

    [Fact]
    public void ATwoWordNameIsMatchedWhole() =>
        Assert.Equal("status", ShipAiAddressing.Opens("Iron Maiden, status", ["Iron", "Iron Maiden"]));
}
