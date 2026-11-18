using D47.Core.Audio;
using D47.Core.Conversation;
using D47.Core.Persona;
using D47.Core.Tests.Conversation;
using Xunit;

namespace D47.Core.Tests.Audio;

/// <summary>A name whose accent was not asked about because the Commander was away is asked about the next time it is heard.</summary>
public sealed class ANameRefusedWhileAwayIsAskedAgainTests
{
    private static readonly string[] Accents = ["American", "French"];

    [Fact]
    public async Task TheNameIsAskedAboutOnceTheCommanderIsBack()
    {
        var model = FakeLlmProvider.Answering("1 = French\nG1 = male");
        var away = true;
        var gate = AwayGate.Wrap(model, () => away);
        var accents = new NameAccents
        {
            Ask = (names, givenNames, offered, token) =>
                VoicePairing.AskAccentsAsync(names, givenNames, offered, gate, null, null, null, null, token),
        };

        var refused = accents.WhenGivenNameAnswered("kokoro", Accents, "Lucien Marchand");
        Assert.NotNull(refused);
        await refused.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        Assert.Equal(0, model.CallCount);
        Assert.Null(accents.Get("kokoro", "Lucien Marchand"));
        Assert.Null(accents.SexOfGivenName("Lucien"));

        away = false;
        accents.Enqueue("kokoro", Accents, ["Lucien Marchand"]);

        await StreamingTtsProvider.Eventually(() => accents.Get("kokoro", "Lucien Marchand") is not null, "the name was never asked about again");

        Assert.Equal("French", accents.Get("kokoro", "Lucien Marchand")?.Accent);
        Assert.Equal("male", accents.SexOfGivenName("Lucien"));
        Assert.Equal(1, model.CallCount);
    }
}
