using System.Text.RegularExpressions;
using D47.Core.Actions;
using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Conversation;
using D47.Core.Help;
using D47.Core.Input;
using D47.Core.Journal;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Input;

/// <summary>
/// The how-to band of every capability page, other than the five control pages that have their own
/// gate, quotes phrases to say. Each must route without the model, or be listed.
/// </summary>
[Trait("Category", "Gate")]
public partial class EveryHowToPhraseRoutesGateTests
{
    private static readonly string[] Covered = ["flight-controls", "ship-systems", "panels", "srv", "on-foot-controls"];

    /// <summary>Phrases the model answers correctly: reviewed by hand, or a row in the scenario routing table.</summary>
    private static readonly (string Page, string Phrase)[] ModelAnswers =
    [
        ("debrief", "from now on, give me the distance first"),
        ("memory", "remember that I hate mining"),
        ("screen", "what does that panel say"),
        ("mining", "mining target platinum above twenty five percent"),
        ("mining", "how many limpets for ten tonnes of osmium"),
        ("system-names", "what does Praea Euq XV-B c1-42 tell you"),
        ("on-foot", "what does grade 3 on my Maverick cost"),
        ("galaxy", "find me a neutron star"),
        ("routes", "plot a neutron route to Colonia"),
        ("utilities", "set a timer for twenty minutes called fuel scoop"),
    ];

    /// <summary>Phrases that did not route when this gate landed. An entry stays only with an open issue that will make it route.</summary>
    private static readonly (string Page, string Phrase)[] NotYetReviewed =
    [
        ("bookmarks", "rename bookmark Current CG to Colonia Bridge"), // no issue filed; see the #1903 commit
        ("binding-profiles", "save these bindings as sim pit"), // no issue filed; see the #1903 commit
        ("binding-profiles", "load the sim pit bindings"), // no issue filed; see the #1903 commit
        ("navigation", "plot a course to Shinrarta Dezhra"), // no issue filed; see the #1903 commit
        ("comms", "tell the wing I am on my way"), // no issue filed; see the #1903 commit
    ];

    private static readonly HashSet<(string Page, string Phrase)> CommsKeyPhrases = new(new EntryComparer())
    {
        ("comms", "are you there"),
        ("carrier", "how much fuel have we got"),
        ("crew", "how is the fighter holding up"),
        ("characters", "are you there"),
    };

    public static TheoryData<string> Pages() => [.. PageNames()];

    private static List<string> PageNames() =>
        [.. Directory.GetFiles(Path.Combine(RepositoryRoot(), "docs", "capabilities"), "*.md")
            .Select(file => Path.GetFileNameWithoutExtension(file))
            .Where(page => !Covered.Contains(page))
            .Order(StringComparer.Ordinal)];

    [Theory]
    [MemberData(nameof(Pages))]
    public void EveryQuotedPhraseRoutesOrIsListed(string page)
    {
        var registry = TestSurface.For(new MemoryInstall()).Registry;
        var fault = NotRegistered(page, registry.Find(page) is not null);
        Assert.True(fault is null, fault);

        var router = Router();
        var listed = Listed();
        var unlisted = Quoted(page)
            .Where(phrase => !Routes(router, phrase) && !AnswersAnExchange(page, phrase) && !SaidOnTheCommsKey(page, phrase) && !listed.Contains((page, phrase)))
            .ToList();

        Assert.True(unlisted.Count == 0, $"{page}.md quotes phrases that reach the model: {string.Join(", ", unlisted.Select(p => $"\"{p}\""))}");
    }

    [Fact]
    public void EveryListedPhraseIsStillQuotedAndStillDoesNotRoute()
    {
        var router = Router();
        var stale = Stale(
            Listed(),
            page => Quoted(page),
            phrase => Routes(router, phrase));

        Assert.True(stale.Count == 0, $"Stale entries: {string.Join(", ", stale.Select(e => $"{e.Page}: \"{e.Phrase}\""))}");
    }

    [Fact]
    public void AnEntryThePageNoLongerQuotesIsStale()
    {
        var stale = Stale([("a", "gone")], _ => ["other"], _ => false);

        Assert.Equal([("a", "gone")], stale);
    }

    [Fact]
    public void AnEntryThatNowRoutesIsStale()
    {
        var stale = Stale([("a", "now routes")], _ => ["now routes"], _ => true);

        Assert.Equal([("a", "now routes")], stale);
    }

    [Fact]
    public void AListedPhraseThatStillReachesTheModelIsNotStale()
    {
        Assert.Empty(Stale([("a", "model")], _ => ["model"], _ => false));
    }

    [Fact]
    public void APageTheSurfaceDoesNotRegisterFailsByName()
    {
        Assert.Equal("the surface does not register unwired", NotRegistered("unwired", false));
        Assert.Null(NotRegistered("wired", true));
    }

    [Fact]
    public void ThePagesComeFromTheFilesWithoutTheFiveCovered()
    {
        var pages = PageNames();

        Assert.NotEmpty(pages);
        Assert.All(Covered, page => Assert.DoesNotContain(page, pages));
    }

    private static string? NotRegistered(string page, bool registered) =>
        registered ? null : $"the surface does not register {page}";

    private static List<(string Page, string Phrase)> Stale(
        IEnumerable<(string Page, string Phrase)> entries,
        Func<string, List<string>> quoted,
        Func<string, bool> routes) =>
        [.. entries.Where(entry => !quoted(entry.Page).Contains(entry.Phrase, StringComparer.OrdinalIgnoreCase) || routes(entry.Phrase))];

    private static HashSet<(string Page, string Phrase)> Listed() =>
        new([.. ModelAnswers, .. NotYetReviewed], new EntryComparer());

    /// <summary>The router, with the things the pages tell the Commander to make first (a checklist line, bookmarks, a macro, a mark on the body, a carrier, a pilot on duty, a character and a campaign faction seen in two systems) and the fixed quick searches.</summary>
    private static KeywordRouter Router()
    {
        var surface = TestSurface.For(new MemoryInstall());
        surface.ChecklistService.AddNote(D47.Core.Checklists.ChecklistScope.Universal, "buy a fuel scoop");

        const string commander = "F1";
        var files = new MemoryFileSystem();
        var bookmarks = new BookmarkStore(@"C:\gate/bookmarks.json", files, NullLogger<BookmarkStore>.Instance);
        bookmarks.Add(commander, "Jameson Memorial", "Shinrarta Dezhra", DateTimeOffset.UnixEpoch);
        bookmarks.Add(commander, "Current CG", "Colonia", DateTimeOffset.UnixEpoch);

        var macros = new MacroStore(@"C:\gate/macros.json", files, NullLogger<MacroStore>.Instance);
        macros.Save([new Macro { Name = "docking prep", Steps = [new MacroStep("hardpoints", DesiredState.Off)] }]);
        macros.Poll([]);

        var state = new CommanderGameState(new CommanderIdentity(commander, "Fixture"));
        Assert.True(JournalEvent.TryParse(
            "{\"timestamp\":\"2026-08-16T10:00:00Z\",\"event\":\"Touchdown\",\"SystemAddress\":2175107336563,\"BodyID\":37,\"Latitude\":0,\"Longitude\":0}",
            NullLogger.Instance,
            out var touchdown));
        state.Apply(touchdown!, null);

        var campaigns = new D47.Core.Campaigns.CampaignStore(@"C:\gate/campaigns.json", files, NullLogger<D47.Core.Campaigns.CampaignStore>.Instance);
        campaigns.SetFaction(commander, "Hutton Truckers", clearIntents: false);

        foreach (var system in new[] { "Lave", "Shinrarta Dezhra" })
        {
            Assert.True(JournalEvent.TryParse(
                $"{{\"timestamp\":\"2026-08-16T09:00:00Z\",\"event\":\"FSDJump\",\"StarSystem\":\"{system}\",\"SystemAddress\":1,\"StarPos\":[0,0,0],\"Factions\":[{{\"Name\":\"Hutton Truckers\",\"Influence\":0.4}}]}}",
                NullLogger.Instance,
                out var jump));
            state.Apply(jump!, null);
        }
        var marks = new SurfaceMarks();
        marks.Add(state.Surface, null, new SurfaceFix(0, 0, 1_000_000), DateTimeOffset.Parse("2026-08-16T10:30:00Z", System.Globalization.CultureInfo.InvariantCulture));

        var channels = new CommsChannels();
        channels.Update(new CommsInputs
        {
            CarrierOwned = true,
            Crew = new ShipCrew { Members = [new CrewMember("Vance Ilo", 1, 4, Active: true)] },
            Characters = [new D47.Core.Characters.CustomCharacter("0a1b2c3d", "Harlan", null, null, true, null)],
        });

        return new(
            surface.Registry,
            () => ChecklistCapability.Phrases(surface.ChecklistService)
                .Concat(BookmarkCourse.Phrases(bookmarks, () => commander))
                .Concat(BookmarksCapability.Phrases(bookmarks, () => commander))
                .Concat(MacroCapability.Phrases(macros))
                .Concat(SurfaceGuidanceCapability.Phrases(marks, () => state))
                .Concat(CommsCapability.ChannelPhrases(channels))
                .Concat(CommandersCapability.Phrases(
                    () => new Dictionary<string, CommanderSighting>
                    {
                        [commander] = new(commander, "Fixture", DateTimeOffset.UnixEpoch, null, null),
                        ["F2"] = new("F2", "Kestrel Vane", DateTimeOffset.UnixEpoch, null, null),
                    },
                    () => commander))
                .Concat(CampaignCapability.Phrases(campaigns, () => state))
                .Concat(D47.Core.Knowledge.QuickSearches.Phrases()));
    }

    private static bool Routes(KeywordRouter router, string phrase) =>
        router.MatchSetting(phrase) is not null
        || router.MatchToolCommand(phrase) is not null
        || router.Match(phrase, InputSource.Spoken) is not null
        || Walks(phrase)
        || LogsOrReadsTheCaptainsLog(phrase)
        || LooksUpByName(phrase)
        || ReadsAJournalReminder(phrase)
        || NamesTheCampaignFaction(phrase)
        || AnswersAFiredReminder(phrase);

    /// <summary>Whether the phrase is a reply the turn loop takes while a teaching exchange is open, before the router and the model.</summary>
    private static bool AnswersAnExchange(string page, string phrase) =>
        page == "learned-phrases" && phrase.ToUpperInvariant() is "YES" or "CANCEL";

    /// <summary>Whether the phrase is said holding the comms key, which sends it to the selected party and never to the router.</summary>
    private static bool SaidOnTheCommsKey(string page, string phrase) =>
        CommsKeyPhrases.Contains((page, phrase));

    /// <summary>Whether the turn loop's model-free captain's log grammar takes the phrase, before the router and the model.</summary>
    private static bool LogsOrReadsTheCaptainsLog(string phrase) => D47.Core.CaptainsLog.CaptainsLogPhrase.Read(phrase) is not null;

    /// <summary>Whether the turn loop's model-free campaign faction grammar takes the phrase, before the router and the model.</summary>
    private static bool NamesTheCampaignFaction(string phrase) => CampaignFactionPhrase.Read(phrase) is not null;

    /// <summary>Whether the turn loop's model-free journal reminder grammar takes the phrase, with timers and alarms off.</summary>
    private static bool ReadsAJournalReminder(string phrase) => D47.Core.Reminders.JournalReminderPhrase.Read(phrase, timersRegistered: false) is not null;

    /// <summary>Whether a registered tool holds the phrase as a command that is heard only while a reminder or warning waits.</summary>
    private static bool AnswersAFiredReminder(string phrase) =>
        TestSurface.For(new MemoryInstall()).Registry.All
            .SelectMany(capability => capability.Descriptor.Tools)
            .SelectMany(tool => tool.Commands)
            .Any(command => command.When is not null && string.Equals(command.Phrase, phrase, StringComparison.OrdinalIgnoreCase));

    /// <summary>Whether the turn loop's model-free lookup by hull, module or blueprint name takes the phrase, before the router and the model.</summary>
    private static bool LooksUpByName(string phrase) => D47.Core.Knowledge.NamedLookupPhrase.Read(phrase) is not null;

    /// <summary>Whether a walkthrough starts on the phrase; the app asks it before the router.</summary>
    private static bool Walks(string phrase) => new Walkthrough().Take(phrase, DateTimeOffset.UnixEpoch) is not null;

    /// <summary>The quoted phrases in the how-to band's Say lines.</summary>
    private static List<string> Quoted(string page)
    {
        var markdown = File.ReadAllText(Path.Combine(RepositoryRoot(), "docs", "capabilities", $"{page}.md"));
        var sections = HelpLibrary.ParseHowTo(markdown, page)?.Sections ?? [];

        return [.. sections
            .Where(section => section.Say is not null)
            .SelectMany(section => Quote().Matches(section.Say!).Select(quote => quote.Groups[1].Value))
            .Distinct(StringComparer.OrdinalIgnoreCase)];
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "d47.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("No d47.slnx above the test binary.");
    }

    [GeneratedRegex(@"""([^""]+)""")]
    private static partial Regex Quote();

    private sealed class EntryComparer : IEqualityComparer<(string Page, string Phrase)>
    {
        public bool Equals((string Page, string Phrase) x, (string Page, string Phrase) y) =>
            string.Equals(x.Page, y.Page, StringComparison.Ordinal)
            && string.Equals(x.Phrase, y.Phrase, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode((string Page, string Phrase) obj) =>
            HashCode.Combine(obj.Page, obj.Phrase.ToUpperInvariant());
    }
}
