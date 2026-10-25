using System.Text.Json;
using D47.App;
using D47.Core.Audio;
using D47.Core.Callouts;
using D47.Core.Configuration;
using D47.Core.Conversation;
using D47.Core.Journal;
using D47.Core.Persona;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// An NPC answering on the SYSTEM or TARGET channel is heard in the voice, and from the voice group, a message from them
/// is heard in.
/// </summary>
public class AReplyIsVoicedAsTheSendersIncomingMessageTests
{
    private static readonly DateTimeOffset Now = new(3312, 5, 1, 12, 0, 0, TimeSpan.Zero);

    /// <summary>The Carrier and NPCs groups spoken by different providers, so a reply from the wrong one shows.</summary>
    private static readonly SpeechSettings Speech = new()
    {
        Provider = TtsProviderCatalog.KokoroId,
        GroupProviders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [VoiceGroups.Carrier.Id] = TtsProviderCatalog.EdgeId,
            [VoiceGroups.Npcs.Id] = TtsProviderCatalog.KokoroId,
        },
    };

    private static readonly TargetedShip Pilot = new("Cobra Mk III", "Jessa Corvane", "Dangerous", "Federation", "Wanted");

    /// <summary>One cast per provider, with pools that share no voice.</summary>
    private sealed class Casts
    {
        public VoiceCast Npcs { get; } = Cast("n_heart", "n_adam", "n_emma", "n_george");

        public VoiceCast Carrier { get; } = Cast("c_heart", "c_adam", "c_emma", "c_george");

        public VoiceCast For(Announcement announcement) =>
            AppHost.CastFor(announcement, Speech, provider => provider switch
            {
                TtsProviderCatalog.EdgeId => Carrier,
                TtsProviderCatalog.KokoroId => Npcs,
                _ => throw new InvalidOperationException($"No cast for {provider}."),
            });

        public VoiceCast Of(VoiceGroup group) => group == VoiceGroup.Carrier ? Carrier : Npcs;

        private static VoiceCast Cast(params string[] pool) => new()
        {
            Pool = pool,
            Voices = new Dictionary<string, VoiceInfo>(StringComparer.OrdinalIgnoreCase)
            {
                [pool[0]] = new(pool[0], "Heart", "en-US", "Female"),
                [pool[1]] = new(pool[1], "Adam", "en-AU", "Male"),
                [pool[2]] = new(pool[2], "Emma", "en-GB", "Female"),
                [pool[3]] = new(pool[3], "George", "en-IE", "Male"),
            },
        };
    }

    private static IncomingMessages Reader() => new()
    {
        Enabled = () => true,
        IncludeNpcs = () => true,
        CarrierName = "Sacred Fire",
        CarrierCallSign = "BNH-T2F",
    };

    private static Announcement Received(string from, string channel)
    {
        var json = JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["timestamp"] = "3312-05-01T12:00:00Z",
            ["event"] = "ReceiveText",
            ["From"] = from,
            ["Message"] = "Keep your distance, pilot.",
            ["Channel"] = channel,
        });

        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return Assert.IsType<Announcement>(Reader().Read(parsed!));
    }

    /// <summary>What <c>Announcer.SayAsync</c> does with an incoming message before speaking it.</summary>
    private static VoiceSelection Hear(Announcement incoming, Casts casts, NpcsHeardHere roster)
    {
        var voice = SpeakerAccent.VoiceOf(casts.For(incoming), incoming);
        roster.Heard(incoming, voice.VoiceId, Now);
        return voice;
    }

    private static string? Accent(Casts casts, Announcement heardAs) => AppHost.AccentHeardAs(heardAs, casts.For);

    private static Speaker Taken(LineDecision decision) => Assert.IsType<LineDecision.Taken>(decision).Speaker;

    private static void HeardInOneVoice(Announcement incoming, VoiceSelection heard, (VoiceSelection Voice, VoiceRole SpeakingAs) reply, VoiceGroup group, Casts casts)
    {
        Assert.Equal(heard.VoiceId, reply.Voice.VoiceId);
        Assert.Equal(incoming.Voice, reply.SpeakingAs);
        Assert.Contains(reply.Voice.VoiceId!, casts.Of(group).Pool);

        // The pipeline synthesises a reply with the provider for VoiceGroups.Of(SpeakingAs).
        Assert.Equal(group, VoiceGroups.Of(incoming.Voice, incoming.CommsChannel));
        Assert.Equal(group, VoiceGroups.Of(reply.SpeakingAs));
    }

    [Theory]
    [InlineData("$npc_name_decorate:#name=Ilse Bruhn;", "Ilse Bruhn", "npc", VoiceGroup.Npcs)]
    [InlineData("$npc_name_decorate:#name=Ilse Bruhn;", "Ilse Bruhn", "unlisted", VoiceGroup.Npcs)]
    [InlineData("Sacred Fire BNH-T2F", "Sacred Fire BNH-T2F", "npc", VoiceGroup.Carrier)]
    public async Task AnNpcOnTheSystemChannelAnswersInTheVoiceItsMessageWasHeardIn(string from, string name, string channel, VoiceGroup group)
    {
        var casts = new Casts();
        var roster = new NpcsHeardHere();
        var incoming = Received(from, channel);
        var heard = Hear(incoming, casts, roster);

        var line = new ChatterLine(() => "Diaguandri", roster: () => roster, accentHeardAs: heardAs => Accent(casts, heardAs));
        var selected = new CommsSelection(CommsChannel.System, new CommsSubChannel(name, name));
        var speaker = Taken(await line.RouteAsync(selected, "what do you want", TestContext.Current.CancellationToken));

        var reply = AppHost.HeardVoiceOf(Assert.IsType<Announcement>(speaker.HeardAs), casts.For);

        HeardInOneVoice(incoming, heard, reply, group, casts);
        Assert.Contains(SpeakerAccent.Sentence(casts.Of(group).AccentOf(reply.Voice.VoiceId))!, speaker.Brief, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ATargetNeverHeardIsVoicedAsAMessageFromItsPilotOnTheNpcChannel()
    {
        var casts = new Casts();
        var roster = new NpcsHeardHere();
        var line = new TargetLine(() => Pilot, roster: () => roster, accentHeardAs: heardAs => Accent(casts, heardAs));
        var selected = new CommsSelection(CommsChannel.Target, new CommsSubChannel(CommsChannels.TargetKey, "Jessa Corvane"));

        var speaker = Taken(await line.RouteAsync(selected, "who are you", TestContext.Current.CancellationToken));
        var heardAs = Assert.IsType<Announcement>(speaker.HeardAs);
        var incoming = Received("$npc_name_decorate:#name=Jessa Corvane;", "npc");

        Assert.Equal(
            (incoming.Voice, incoming.Speaker, incoming.SpeakerIsPlayer, incoming.SpeakerAllegiance, incoming.CommsChannel),
            (heardAs.Voice, heardAs.Speaker, heardAs.SpeakerIsPlayer, heardAs.SpeakerAllegiance, heardAs.CommsChannel));

        // The target answers first, then sends a message in the same system.
        var reply = AppHost.HeardVoiceOf(heardAs, casts.For);
        var heard = Hear(incoming, casts, roster);

        HeardInOneVoice(incoming, heard, reply, VoiceGroup.Npcs, casts);
        Assert.Contains(SpeakerAccent.Sentence(casts.Npcs.AccentOf(reply.Voice.VoiceId))!, speaker.Brief, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ATargetHeardOnCommsAnswersInTheVoiceItsMessageWasHeardIn()
    {
        var casts = new Casts();
        var roster = new NpcsHeardHere();
        var incoming = Received("$npc_name_decorate:#name=Jessa Corvane;", "unlisted");
        var heard = Hear(incoming, casts, roster);

        var line = new TargetLine(() => Pilot, roster: () => roster, accentHeardAs: heardAs => Accent(casts, heardAs));
        var selected = new CommsSelection(CommsChannel.Target, new CommsSubChannel(CommsChannels.TargetKey, "Jessa Corvane"));
        var speaker = Taken(await line.RouteAsync(selected, "who are you", TestContext.Current.CancellationToken));

        var heardAs = Assert.IsType<Announcement>(speaker.HeardAs);
        Assert.Equal("unlisted", heardAs.CommsChannel);
        HeardInOneVoice(incoming, heard, AppHost.HeardVoiceOf(heardAs, casts.For), VoiceGroup.Npcs, casts);
    }
}
