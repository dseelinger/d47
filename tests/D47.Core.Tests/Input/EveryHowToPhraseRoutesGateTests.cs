using System.Text.RegularExpressions;
using D47.Core.Capabilities;
using D47.Core.Conversation;
using D47.Core.Help;
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

    /// <summary>Phrases the model answers correctly. Reviewed by hand.</summary>
    private static readonly (string Page, string Phrase)[] ModelAnswers = [];

    /// <summary>Phrases that did not route when this gate landed. An entry stays only with an open issue that will make it route.</summary>
    private static readonly (string Page, string Phrase)[] NotYetReviewed =
    [
        ("engineers", "how do I unlock Felicity Farseer"),
        ("mining", "mining target platinum above twenty five percent"),
        ("mining", "how many limpets for ten tonnes of osmium"),
        ("system-names", "what does Praea Euq XV-B c1-42 tell you"),
        ("bookmarks", "set course for Jameson Memorial"),
        ("bookmarks", "rename bookmark Current CG to Colonia Bridge"),
        ("bookmarks", "delete bookmark Current CG"),
        ("lore", "what happened at Hyades Sector DB-X d1-112"),
        ("lore", "remember that this is where I lost the Anaconda"),
        ("callouts", "stop telling me about limpets"),
        ("callouts", "tell me about limpets again"),
        ("checklists", "put buy a fuel scoop on my list"),
        ("checklists", "tick off the fuel scoop"),
        ("checklists", "remove it"),
        ("on-foot", "what does grade 3 on my Maverick cost"),
        ("captains-log", "captain's log, the Guardian site is two kilometres north of the landing pad"),
        ("captains-log", "captain's log"),
        ("captains-log", "Go ahead."),
        ("crew", "who is in my fighter bay"),
        ("crew", "comms Vasquez"),
        ("crew", "how is the fighter holding up"),
        ("conversation", "what is my current cargo worth"),
        ("engineering", "what does a grade 5 dirty drive cost"),
        ("engineering", "can I afford it"),
        ("characters", "comms Harlan"),
        ("characters", "are you there"),
        ("commander-log", "have I met Arden Vale before"),
        ("powerplay", "what does my next Powerplay rank give me"),
        ("powerplay", "what would Edmund Mahon give me"),
        ("ships", "what do I still need for the Ptarmigan"),
        ("exobiology", "what biology is on this body"),
        ("listening", "alpha bravo seven done"),
        ("binding-profiles", "save these bindings as sim pit"),
        ("binding-profiles", "load the sim pit bindings"),
        ("gap", "what am I missing"),
        ("surface-guidance", "which way to mark 1"),
        ("reminders", "remind me to buy limpets when I next dock"),
        ("reminders", "noted"),
        ("reminders", "remind me next time"),
        ("reminders", "when I next dock"),
        ("reminders", "next session"),
        ("reminders", "in twenty minutes"),
        ("galaxy", "where is the nearest material trader"),
        ("galaxy", "find me a neutron star"),
        ("learned-phrases", "wheels out"),
        ("learned-phrases", "drop the wheels"),
        ("learned-phrases", "yes"),
        ("learned-phrases", "cancel"),
        ("memory", "remember that I fly in open"),
        ("memory", "call me Commander, not Doug"),
        ("memory", "forget"),
        ("memory", "remember"),
        ("focus", "bring Elite back"),
        ("utilities", "set a timer for twenty minutes called fuel scoop"),
        ("routes", "plot a neutron route to Colonia"),
        ("goals", "my goal is to buy a Python"),
        ("goals", "I want to unlock all the engineers"),
        ("macros", "docking prep"),
        ("vr", "put the panel down"),
        ("vr", "lock it to my head"),
        ("vr", "move it left"),
        ("vr", "bring it nearer"),
        ("carrier", "comms captain"),
        ("carrier", "how much fuel have we got"),
        ("commanders", "switch to Kestrel Vane"),
        ("navigation", "plot a route to Shinrarta Dezhra"),
        ("logbook", "what did I do this week"),
        ("comms", "comms Tower"),
        ("comms", "are you there"),
        ("comms", "tell the wing I am on my way"),
        ("specifications", "what is the Python II like"),
        ("specifications", "how much does a 6A power plant draw"),
        ("specifications", "compare the Krait and the Python"),
        ("screen", "what's on my scanner"),
        ("screen", "what does that panel say"),
    ];

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
            .Where(phrase => !Routes(router, phrase) && !listed.Contains((page, phrase)))
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

    private static KeywordRouter Router() => new(TestSurface.For(new MemoryInstall()).Registry);

    private static bool Routes(KeywordRouter router, string phrase) =>
        router.MatchSetting(phrase) is not null
        || router.MatchToolCommand(phrase) is not null
        || router.Match(phrase, InputSource.Spoken) is not null
        || Walks(phrase);

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
