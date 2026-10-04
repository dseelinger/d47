using System.Text.Json;
using D47.Core.Configuration;
using Xunit;

namespace D47.Core.Tests.Persona;

public class AnAutomaticCovasVoiceIsRecastOnceTests
{
    [Fact]
    public void ASettingsFileWrittenBeforeTheFlagLoadsWithItUnset()
    {
        var settings = JsonSerializer.Deserialize<PersonaSettings>("{\"voicesPaired\":true}");

        Assert.NotNull(settings);
        Assert.False(settings.CovasRecastChecked);
    }
}
