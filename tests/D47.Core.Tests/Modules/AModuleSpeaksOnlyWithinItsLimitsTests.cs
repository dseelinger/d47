using D47.Core.Callouts;
using D47.Core.Configuration;
using D47.Core.Journal;
using D47.Core.Modules;
using Xunit;

namespace D47.Core.Tests.Modules;

public class AModuleSpeaksOnlyWithinItsLimitsTests
{
    private static readonly DateTimeOffset Start = DateTimeOffset.UnixEpoch;

    private static ReadyModule Module(string id, string name, int interval = 120) =>
        new($@"C:\modules\{id}", new ModuleManifest(1, id, name, "Ana Vey", "1.4.0", "run.exe", [], interval, [], null), $@"C:\modules\{id}\run.exe");

    private sealed class Rig
    {
        private readonly List<ModuleLine> _queued = [];

        public Rig(params ReadyModule[] modules)
        {
            Modules = modules;
            Settings = new D47Settings
            {
                Modules = modules.ToDictionary(m => m.Manifest.Id, _ => new ModuleSettings { Enabled = true }, StringComparer.Ordinal),
            };
            Callout = new ModuleSpeechCallout(new ModuleSpeechSources
            {
                Lines = () =>
                {
                    var lines = _queued.ToList();
                    _queued.Clear();
                    return lines;
                },
                Installed = () => Modules,
                Settings = () => Settings,
                AboardName = () => "Warden",
                Cores = () => [("warden", "Warden")],
                YourCores = () => [],
                Characters = () => [],
            });
        }

        public IReadOnlyList<InstalledModule> Modules { get; }

        public D47Settings Settings { get; set; }

        public ModuleSpeechCallout Callout { get; }

        public void Set(string id, Func<ModuleSettings, ModuleSettings> change) =>
            Settings = Settings with
            {
                Modules = new Dictionary<string, ModuleSettings>(Settings.Modules, StringComparer.Ordinal) { [id] = change(Settings.ModuleFor(id)) },
            };

        public List<Announcement> Say(TimeSpan at, params ModuleLine[] lines) => Say(at, priming: false, lines);

        public void Queue(params ModuleLine[] lines) => _queued.AddRange(lines);

        public List<Announcement> Say(TimeSpan at, bool priming, params ModuleLine[] lines)
        {
            Queue(lines);
            return [.. Callout.Examine(new CalloutContext(Start + at, priming, null, GameStatus.Unknown, NavRoute.None, []))];
        }
    }

    [Fact]
    public void AnUrgentRequestStillComesOutRoutine()
    {
        var rig = new Rig(Module("ledger", "Bounty ledger"));

        var said = Assert.Single(rig.Say(TimeSpan.Zero, new ModuleLine("ledger", "tally", "Three bounties open.", askedUrgent: true)));

        Assert.Equal(CalloutUrgency.Routine, said.Urgency);
    }

    [Fact]
    public void ASecondLineInsideTheModulesOwnLimitIsDropped()
    {
        var rig = new Rig(Module("ledger", "Bounty ledger", interval: 120));

        Assert.Single(rig.Say(TimeSpan.Zero, new ModuleLine("ledger", "a", "One.")));
        Assert.Empty(rig.Say(TimeSpan.FromSeconds(119), new ModuleLine("ledger", "b", "Two.")));
        Assert.Single(rig.Say(TimeSpan.FromSeconds(120), new ModuleLine("ledger", "c", "Three.")));
    }

    /// <summary>Dropped, not held: nothing from the early line comes out later.</summary>
    [Fact]
    public void ASecondLineInsideTheChosenIntervalIsDroppedNotHeld()
    {
        var rig = new Rig(Module("ledger", "Bounty ledger", interval: 120));
        rig.Set("ledger", m => m with { Interval = ModuleInterval.TenMinutes });

        rig.Say(TimeSpan.Zero, new ModuleLine("ledger", "a", "One."));

        Assert.Empty(rig.Say(TimeSpan.FromMinutes(5), new ModuleLine("ledger", "b", "Two.")));
        Assert.Empty(rig.Say(TimeSpan.FromMinutes(9)));

        var said = Assert.Single(rig.Say(TimeSpan.FromMinutes(10), new ModuleLine("ledger", "c", "Three.")));
        Assert.Equal("Three.", said.Text);
    }

    [Fact]
    public void AChosenIntervalShorterThanTheModulesMinimumGivesWayToTheMinimum()
    {
        var rig = new Rig(Module("ledger", "Bounty ledger", interval: 900));
        rig.Set("ledger", m => m with { Interval = ModuleInterval.TenMinutes });

        rig.Say(TimeSpan.Zero, new ModuleLine("ledger", "a", "One."));

        Assert.Empty(rig.Say(TimeSpan.FromMinutes(10), new ModuleLine("ledger", "b", "Two.")));
        Assert.Single(rig.Say(TimeSpan.FromMinutes(15), new ModuleLine("ledger", "c", "Three.")));
    }

    [Fact]
    public void EachModuleIsSpacedOnItsOwn()
    {
        var rig = new Rig(Module("ledger", "Bounty ledger"), Module("tides", "Tide tables"));

        var said = rig.Say(TimeSpan.Zero, new ModuleLine("ledger", "a", "One."), new ModuleLine("tides", "a", "High water."), new ModuleLine("ledger", "b", "Two."));

        Assert.Equal(["One.", "High water."], said.Select(a => a.Text));
    }

    [Fact]
    public void MutedDropsEveryLine()
    {
        var rig = new Rig(Module("ledger", "Bounty ledger"));
        rig.Set("ledger", m => m with { Muted = true });

        Assert.Empty(rig.Say(TimeSpan.Zero, new ModuleLine("ledger", "a", "One.")));
        Assert.Empty(rig.Say(TimeSpan.FromHours(1), new ModuleLine("ledger", "b", "Two.")));
    }

    [Fact]
    public void AModuleThatIsOffOrNotInstalledSaysNothing()
    {
        var rig = new Rig(Module("ledger", "Bounty ledger"));
        rig.Set("ledger", m => m with { Enabled = false });

        Assert.Empty(rig.Say(TimeSpan.Zero, new ModuleLine("ledger", "a", "One."), new ModuleLine("ghost", "a", "Boo.")));
    }

    [Fact]
    public void AnotherModulesKeyPrefixCannotBeUsed()
    {
        var rig = new Rig(Module("ledger", "Bounty ledger"), Module("tides", "Tide tables"));

        var said = Assert.Single(rig.Say(TimeSpan.Zero, new ModuleLine("ledger", "module.tides.high", "High water.")));

        Assert.Equal("module.ledger.module.tides.high", said.Key);
        Assert.StartsWith("module.ledger.", said.Key, StringComparison.Ordinal);
    }

    /// <summary>A line the engine would refuse at startup does not use up the module's interval.</summary>
    [Fact]
    public void ALineDroppedWhilePrimingDoesNotCountAgainstTheModule()
    {
        var rig = new Rig(Module("ledger", "Bounty ledger"));

        Assert.Empty(rig.Say(TimeSpan.Zero, priming: true, new ModuleLine("ledger", "a", "One.")));
        Assert.Single(rig.Say(TimeSpan.FromSeconds(1), new ModuleLine("ledger", "b", "Two.")));
    }

    [Fact]
    public void FlavourBriefsHandsAModuleLineToNoModel()
    {
        var said = Assert.Single(new Rig(Module("ledger", "Bounty ledger")).Say(TimeSpan.Zero, new ModuleLine("ledger", "a", "Three bounties open.")));

        Assert.Null(FlavourBriefs.For(said, personalityEnabled: true, "CMDR Vale Oduya"));
    }

    /// <summary>A transcript keeps the text off the model's conversation feed as well.</summary>
    [Fact]
    public void TheTranscriptLineNamesTheSpeakerAndTagsTheModule()
    {
        var said = Assert.Single(new Rig(Module("ledger", "Bounty ledger")).Say(TimeSpan.Zero, new ModuleLine("ledger", "a", "Three bounties open.")));

        Assert.Equal("Warden [Bounty ledger]: Three bounties open.\n", said.Transcript);
        Assert.Null(said.ConversationLine);
        Assert.Equal(SpokenGroup.Module, said.Group);
    }

    [Fact]
    public void TheEngineQueuesItAsRoutine()
    {
        var rig = new Rig(Module("ledger", "Bounty ledger"));
        var engine = new CalloutEngine(Microsoft.Extensions.Logging.Abstractions.NullLogger<CalloutEngine>.Instance).Add(rig.Callout);

        rig.Queue(new ModuleLine("ledger", "a", "Three bounties open.", askedUrgent: true));
        engine.Tick(new CalloutContext(Start, false, null, GameStatus.Unknown, NavRoute.None, []));

        Assert.Equal(CalloutUrgency.Routine, Assert.Single(engine.Drain()).Urgency);
    }
}
