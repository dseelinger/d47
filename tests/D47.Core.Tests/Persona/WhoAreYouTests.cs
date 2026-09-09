using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Conversation;
using D47.Core.Persona;
using Xunit;

namespace D47.Core.Tests.Persona;

/// <summary>"Who are you?" has one answer and it is a name.</summary>
public class WhoAreYouTests
{
    private static async Task<string> AskAsync(TestSurface surface, string input)
    {
        var match = new KeywordRouter(surface.Registry).Match(input);

        Assert.NotNull(match);
        Assert.Equal(PersonaCapability.Id, match.CapabilityId);

        var result = await surface.Registry.InvokeAsync(
            match.ToolName, ToolArguments.Empty, TestContext.Current.CancellationToken);

        return result.Content;
    }

    [Theory]
    [InlineData("who are you")]
    [InlineData("who am I talking to")]
    [InlineData("which persona is this")]
    [InlineData("which core is this")]
    public async Task EveryWayOfAskingGetsTheNameAndNothingElse(string input)
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);

        Assert.Equal("I am Warden.", await AskAsync(surface, input));
    }

    [Fact]
    public async Task ItIsTheCoreAboard()
    {
        using var install = new TempInstall();
        var personas = new PersonaHost();
        var surface = TestSurface.For(install, personas: personas);

        personas.Apply(new PersonaSettings { Id = "cora" });

        Assert.Equal("I am Cora.", await AskAsync(surface, "who are you"));
    }

    [Fact]
    public async Task AShipAiNameTheCommanderSetIsWhatItAnswersWith()
    {
        using var install = new TempInstall();
        var personas = new PersonaHost();
        var surface = TestSurface.For(install, personas: personas);

        personas.Apply(new PersonaSettings { Id = "cora", ShipName = "Fred" });

        Assert.Equal("I am Fred.", await AskAsync(surface, "who are you"));
    }

    [Fact]
    public async Task AShipAiNameOfNothingButSpacesIsNoName()
    {
        // Whitespace is not a name.
        using var install = new TempInstall();
        var personas = new PersonaHost();
        var surface = TestSurface.For(install, personas: personas);

        personas.Apply(new PersonaSettings { Id = "kex", ShipName = "   " });

        Assert.Equal("I am Kex.", await AskAsync(surface, "who are you"));
    }
}
