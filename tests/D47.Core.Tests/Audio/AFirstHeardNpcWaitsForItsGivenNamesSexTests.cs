using D47.Core.Audio;
using D47.Core.Callouts;
using D47.Core.Storage;
using Xunit;

namespace D47.Core.Tests.Audio;

/// <summary>A named NPC is cast by the sex of their given name on their first line, waiting up to a ceiling for the answer.</summary>
public class AFirstHeardNpcWaitsForItsGivenNamesSexTests
{
    private static readonly string[] Accents = ["American"];

    private static readonly VoiceInfo[] Listed =
    [
        new("m1", "M1", "american"),
        new("f1", "F1", "american"),
        new("m2", "M2", "american"),
        new("f2", "F2", "american"),
        new("m3", "M3", "american"),
        new("f3", "F3", "american"),
    ];

    private static bool IsFemale(string? voiceId) => voiceId is not null && voiceId.StartsWith('f');

    private sealed class Rig
    {
        public Rig(NameAccents names)
        {
            Names = names;
            Cast = new VoiceCast
            {
                DefaultVoice = "ship-ai",
                Pool = VoicePool.From(Listed),
                Voices = Listed.ToDictionary(voice => voice.Id, StringComparer.OrdinalIgnoreCase),
                Feminine = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "f1", "f2", "f3" },
                ReadingOfName = name => names.Get("p", name),
                SexOfGivenName = names.SexOfGivenName,
                NameUnknown = name => names.Enqueue("p", Accents, [name]),
                GivenNameAnswer = name => names.WhenGivenNameAnswered("p", Accents, name),
            };
        }

        public NameAccents Names { get; }

        public VoiceCast Cast { get; }

        public Task? Waits(string sender, bool isPlayer = false, VoiceRole role = VoiceRole.Comms) =>
            Cast.SexAnswerFor(sender, isPlayer, role);
    }

    private static NameAccents Model(
        Func<IReadOnlyList<string>, IReadOnlyDictionary<string, string>?> sexes,
        List<IReadOnlyList<string>>? asked = null,
        Task? delay = null) =>
        new()
        {
            Ask = async (names, givenNames, accents, token) =>
            {
                asked?.Add(givenNames);

                if (delay is not null)
                {
                    await delay;
                }

                return sexes(givenNames) is { } answered
                    ? new NameAnswers(names.ToDictionary(name => name, _ => new NameReading("", "unknown")), answered)
                    : null;
            },
        };

    private static IReadOnlyDictionary<string, string> All(IReadOnlyList<string> given, string sex) =>
        given.ToDictionary(name => name, _ => sex);

    [Fact]
    public async Task AGivenNameWithARecordedSexIsCastFromThatSexWithNoWait()
    {
        var rig = new Rig(Model(given => All(given, "female")));
        rig.Names.Enqueue("p", Accents, ["Marianne Hobbs"]);
        await StreamingTtsProvider.Eventually(() => rig.Names.SexOfGivenName("Marianne") == "female", "the sex was never recorded");

        Assert.Null(rig.Waits("Marianne Okafor"));
        Assert.True(IsFemale(rig.Cast.ForSender("Marianne Okafor", isPlayer: false).VoiceId));
    }

    [Fact]
    public async Task TwoSendersWithOneGivenNameAskTheModelOnceBetweenThem()
    {
        var asked = new List<IReadOnlyList<string>>();
        var rig = new Rig(Model(given => All(given, "male"), asked));

        var first = rig.Waits("Dan Roberts");
        var second = rig.Waits("Dan Okafor");

        Assert.NotNull(first);
        Assert.NotNull(second);
        await first;
        await second;

        Assert.Equal(["Dan"], asked.SelectMany(given => given));
    }

    [Fact]
    public async Task AnIndeterminateGivenNameDrawsWithoutRegardToSexAndDoesNotWait()
    {
        var rig = new Rig(Model(given => All(given, "indeterminate")));
        await rig.Waits("Pat Okafor")!;

        Assert.Null(rig.Waits("Pat Smith"));

        var drawn = new[] { "Pat Smith", "Pat Jones", "Pat Lee", "Pat Wu", "Pat Roe", "Pat Fox" }
            .Select(name => rig.Cast.ForSender(name, isPlayer: false).VoiceId)
            .ToArray();

        Assert.Contains(drawn, voice => IsFemale(voice));
        Assert.Contains(drawn, voice => !IsFemale(voice));
    }

    [Fact]
    public async Task AnAnswerWithinTheCeilingGivesTheFirstLineAMansVoice()
    {
        var release = new TaskCompletionSource();
        var time = new ManualTime();
        var rig = new Rig(Model(given => All(given, "male"), delay: release.Task));

        var wait = SpeakerAccent.WaitForNameAsync(rig.Waits("Dan Roberts")!, time);

        time.Advance(TimeSpan.FromSeconds(2));
        Assert.False(wait.IsCompleted);

        release.SetResult();
        await wait;

        Assert.False(IsFemale(rig.Cast.ForSender("Dan Roberts", isPlayer: false).VoiceId));
    }

    [Fact]
    public async Task AModelThatNeverAnswersIsWaitedForOnlyUntilTheCeiling()
    {
        var time = new ManualTime();
        var rig = new Rig(Model(given => All(given, "male"), delay: new TaskCompletionSource().Task));

        var wait = SpeakerAccent.WaitForNameAsync(rig.Waits("Dan Roberts")!, time);

        time.Advance(NameAccents.AnswerCeiling - TimeSpan.FromMilliseconds(1));
        Assert.False(wait.IsCompleted);

        time.Advance(TimeSpan.FromMilliseconds(1));
        await wait;

        Assert.NotNull(rig.Cast.ForSender("Dan Roberts", isPlayer: false).VoiceId);
    }

    [Fact]
    public void ACallerThatCannotBeCastByNameNeverWaits()
    {
        var rig = new Rig(Model(given => All(given, "male")));

        Assert.Null(rig.Waits("Dan Roberts", isPlayer: true));
        Assert.Null(rig.Waits("Dan Roberts", role: VoiceRole.TowerControl));

        rig.Cast.Assign(VoiceRole.Comms, "m1");
        Assert.Null(rig.Waits("Dan Roberts"));
    }

    [Fact]
    public async Task AGivenNameAlreadyAskedAndNotAnsweredDoesNotWaitAgain()
    {
        var rig = new Rig(Model(_ => null));

        await rig.Waits("Dan Roberts")!;

        Assert.Null(rig.Waits("Dan Okafor"));
    }

    [Fact]
    public void WithNoLanguageModelNothingWaits()
    {
        var rig = new Rig(new NameAccents());

        Assert.Null(rig.Waits("Dan Roberts"));
    }

    [Fact]
    public async Task ASenderAlreadyVoicedInThisSystemDoesNotWait()
    {
        var rig = new Rig(Model(given => All(given, "male")));
        await rig.Waits("Dan Roberts")!;
        rig.Cast.ForSender("Dan Roberts", isPlayer: false);

        Assert.Null(rig.Waits("Dan Roberts"));
    }

    [Fact]
    public void ACalloutInTheSameBatchIsNotHeldBehindAnNpcLine()
    {
        var rig = new Rig(Model(given => All(given, "male")));
        var npc = new Announcement("message.npc", "Hello") { Speaker = "Dan Roberts", Voice = VoiceRole.Comms };
        var callout = new Announcement("ship.fuel", "Fuel low");

        var (ready, held) = SpeakerAccent.SplitByNameWait([npc, callout], _ => rig.Cast);

        Assert.Equal([callout], ready);
        Assert.Equal([npc], held.Select(item => item.Line));
    }

    [Fact]
    public async Task SexesStoredBeforeGivenNamesWereKeptSeedTheGivenNames()
    {
        var files = new MemoryFileSystem();
        var file = Path.Combine(@"C:\d47-memory", "name-accents.json");
        var given = Path.Combine(@"C:\d47-memory", "given-name-sexes.json");
        files.WriteText(file, """{"p":{"Marianne Hobbs":{"accent":"","sex":"female"},"Keel":{"accent":"","sex":"unknown"}}}""");

        var names = new NameAccents(files, file, givenFile: given);

        Assert.Equal("female", names.SexOfGivenName("Marianne"));
        Assert.Null(names.SexOfGivenName("Keel"));

        names.Ask = (n, g, _, _) => Task.FromResult<NameAnswers?>(new NameAnswers(new Dictionary<string, NameReading>(), All(g, "male")));
        names.Enqueue("p", Accents, ["Dan Roberts"]);
        await StreamingTtsProvider.Eventually(
            () => new NameAccents(files, file, givenFile: given).SexOfGivenName("Dan") == "male", "the sex was never saved");

        var restarted = new NameAccents(files, file, givenFile: given);

        Assert.Equal("male", restarted.SexOfGivenName("Dan"));
        Assert.Equal("female", restarted.SexOfGivenName("Marianne"));
    }
}
