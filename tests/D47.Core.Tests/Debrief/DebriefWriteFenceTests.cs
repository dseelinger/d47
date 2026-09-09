using D47.Core.Debrief;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Debrief;

/// <summary>The fence, driven by attempting the writes it exists to refuse.</summary>
public class DebriefWriteFenceTests : IDisposable
{
    private const string Untouched = "// the original bytes, which must survive every attempt\n";

    private readonly string _folder = Path.Combine(
        Path.GetTempPath(), "d47-debrief-fence", Guid.NewGuid().ToString("N"));

    public DebriefWriteFenceTests() => Directory.CreateDirectory(Path.Combine(_folder, "data"));

    public void Dispose()
    {
        GC.SuppressFinalize(this);

        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    /// <summary>
    /// The names the issue calls out by hand, each one attempted against a real file that already has
    /// contents.
    /// </summary>
    public static TheoryData<string> Forbidden =>
    [
        "Guardrails.cs",
        "PromptAssembly.cs",
        "guardian-personas.md",
        "PersonaCatalog.cs",
        "settings.json",
        "memories.json",
        "ToolProfiles.cs",
        "standing-directions.txt",
        "standing-directions.json.bak",
    ];

    [Theory]
    [MemberData(nameof(Forbidden))]
    public void TheStoreRefusesToBeBuiltOverAnythingButItsOwnFile(string name)
    {
        var target = Path.Combine(_folder, "data", name);
        File.WriteAllText(target, Untouched);

        var refused = Assert.Throws<DebriefWriteRefused>(() =>
            new StandingDirectionsStore(target, NullLogger<StandingDirectionsStore>.Instance));

        Assert.Equal(Path.GetFullPath(target), Path.GetFullPath(refused.Attempted));

        // The half that matters.
        Assert.Equal(Untouched, File.ReadAllText(target));
    }

    /// <summary>
    /// The same name in the right folder is the one thing it may write, so the theory above is testing
    /// the rule rather than an inability to write at all.
    /// </summary>
    [Fact]
    public void TheOneAllowedFileIsWritten()
    {
        var target = Path.Combine(_folder, "data", DebriefWriteFence.FileName);

        var store = new StandingDirectionsStore(target, NullLogger<StandingDirectionsStore>.Instance);
        store.Write("F1", new StandingDirection("drafted-1", "Shorter answers in combat."));

        Assert.True(File.Exists(target));
        Assert.Contains("Shorter answers in combat.", File.ReadAllText(target), StringComparison.Ordinal);
    }

    [Fact]
    public void TheRightNameOutsideTheDataFolderIsRefused()
    {
        var elsewhere = Path.Combine(_folder, "src", DebriefWriteFence.FileName);
        Directory.CreateDirectory(Path.GetDirectoryName(elsewhere)!);

        Assert.Throws<DebriefWriteRefused>(() =>
            new StandingDirectionsStore(elsewhere, NullLogger<StandingDirectionsStore>.Instance));

        Assert.False(File.Exists(elsewhere));
    }

    /// <summary>
    /// Case is not a defence on Windows, so the allow-list compares case-insensitively and this says so
    /// out loud — a rule that only held for one spelling of a name would not be one.
    /// </summary>
    [Fact]
    public void TheAllowedNameIsMatchedWhateverItsCase()
    {
        var target = Path.Combine(_folder, "data", "Standing-Directions.JSON");

        var store = new StandingDirectionsStore(target, NullLogger<StandingDirectionsStore>.Instance);
        store.Write(null, new StandingDirection("drafted-1", "Keep it short."));

        Assert.True(File.Exists(target));
    }

    [Fact]
    public void NothingIsAPathAndIsRefusedAsOne()
    {
        Assert.False(DebriefWriteFence.Permits(null));
        Assert.False(DebriefWriteFence.Permits("   "));
        Assert.Throws<DebriefWriteRefused>(() => DebriefWriteFence.Enforce(null));
    }

    /// <summary>
    /// A refusal says which of the named rules it broke, which is what makes a failure here readable
    /// without going and reading the fence.
    /// </summary>
    [Fact]
    public void ARefusalNamesTheRuleItBroke()
    {
        var guardrails = Assert.Throws<DebriefWriteRefused>(() =>
            DebriefWriteFence.Enforce(Path.Combine(_folder, "data", "Guardrails.cs")));

        Assert.Contains("guardrails", guardrails.Why, StringComparison.OrdinalIgnoreCase);

        var pack = Assert.Throws<DebriefWriteRefused>(() =>
            DebriefWriteFence.Enforce(Path.Combine(_folder, "data", "guardian-personas.md")));

        Assert.Contains("twice", pack.Why, StringComparison.OrdinalIgnoreCase);
    }
}
