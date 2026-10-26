using D47.Core.Adventures;
using D47.Core.Callouts;
using D47.Core.Conversation;
using D47.Core.Persona;
using D47.Core.Tests.Conversation;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Persona;

public class AJapaneseCastMemberSpeaksJapaneseAndTheShipHearsEnglishTests
{
    private const string Japanese = "ビーコンは静かになった。";

    private const string English = "The beacon went quiet.";

    private static readonly AdventureSpeaker Sora = new("sora", "Sora Tanabe", "A salvager.") { Language = "ja" };

    private static (TurnLoop Loop, CastLine Line) Build(ILlmProvider provider)
    {
        var surface = TestSurface.For(new MemoryInstall());
        var line = new CastLine(castId => castId == Sora.Id ? Sora : null, () => null, () => new ShipFacts { ScoopFitted = false });

        var loop = new TurnLoop(
            surface.Registry,
            surface.Router,
            new LlmAvailabilityState(providerConfigured: true),
            new SpendTracker(),
            PriceTable.Default,
            NullLogger<TurnLoop>.Instance,
            provider,
            clock: new ManualTurnClock())
        {
            Persona = "You are the ship's AI.",
            ShipAiNames = () => ["Warden"],
            CommsSelected = () => OnComms.Cast(Sora.Id, Sora.Name),
        };

        loop.Lines.Add(line);

        return (loop, line);
    }

    [Fact]
    public async Task TheTranslationIsYieldedBeforeTheJapaneseTheVoiceSpeaks()
    {
        var (loop, _) = Build(FakeLlmProvider.Answering($"{Japanese}\n///\n{English}"));

        var events = await OnComms.SayAsync(loop, "what happened?");

        var translation = events.FindIndex(e => e is TurnEvent.Translation);
        var delta = events.FindIndex(e => e is TurnEvent.TextDelta);

        Assert.True(translation >= 0 && translation < delta);
        Assert.Equal(English, ((TurnEvent.Translation)events[translation]).Text);
        Assert.Equal(Japanese, Assert.Single(events.OfType<TurnEvent.TextDelta>()).Text);
        Assert.Equal(English, Assert.Single(events.OfType<TurnEvent.Completed>()).Result.Text);
    }

    [Fact]
    public async Task TheBriefAsksForTheShapeInTheMembersLanguage()
    {
        var provider = FakeLlmProvider.Answering($"{Japanese}\n///\n{English}");
        var (loop, _) = Build(provider);

        await OnComms.SayAsync(loop, "what happened?");

        var persona = provider.LastRequest!.Prompt.Persona;
        Assert.Contains("understand the Commander's English but answer only in Japanese", persona, StringComparison.Ordinal);
        Assert.Contains("///", persona, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ATranslationThatMayNotBeSaidSilencesTheReply()
    {
        var (loop, _) = Build(FakeLlmProvider.Answering($"{Japanese}\n///\nWe'll top up on the fuel scoop."));

        var events = await OnComms.SayAsync(loop, "what happened?");

        Assert.Empty(events.OfType<TurnEvent.TextDelta>());
        Assert.Empty(events.OfType<TurnEvent.Translation>());
    }

    [Fact]
    public async Task AReplyWithNoTranslationIsNotSpoken()
    {
        var (loop, _) = Build(FakeLlmProvider.Answering(Japanese));

        var events = await OnComms.SayAsync(loop, "what happened?");

        Assert.Empty(events.OfType<TurnEvent.TextDelta>());
    }

    [Fact]
    public async Task TheShipHearsTheEnglishAndTheMembersOwnExchangeKeepsBoth()
    {
        var provider = FakeLlmProvider.Answering($"{Japanese}\n///\n{English}");
        var (loop, _) = Build(provider);

        await OnComms.SayAsync(loop, "what happened?");
        await OnComms.SayAsync(loop, "and then?");

        var memberPrompt = provider.LastRequest!.Prompt.Persona;
        Assert.Contains($"{Sora.Name}: {Japanese} ({English})", memberPrompt, StringComparison.Ordinal);

        await OnComms.AskAsync(loop, "what did she say?");

        var shipPrompt = provider.LastRequest!.Prompt;
        var text = string.Join(
            '\n',
            shipPrompt.History.SelectMany(message => message.Content).OfType<ConversationContent.Text>().Select(part => part.Value));

        Assert.Contains(English, text, StringComparison.Ordinal);
        Assert.DoesNotContain(Japanese, text, StringComparison.Ordinal);
    }
}
