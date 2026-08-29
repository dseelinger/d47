using D47.Core.Debrief;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Debrief;

/// <summary>
/// The fence, driven by attempting the writes it exists to refuse
/// (<a href="https://github.com/dseelinger/d47/issues/162">#162</a>).
/// <para>
/// <b>Every case here writes a real file first and asserts its bytes afterwards.</b> A test that
/// only caught an exception would prove the exception and not the fence: the thing that matters is
/// that the guardrails source, the persona pack and <c>settings.json</c> are byte-for-byte what
/// they were, which is the claim the issue makes twice and the one a later refactor could quietly
/// break.
/// </para>
/// <para>
/// It is also why the check is on the path rather than on the caller's intentions. The wording
/// that came before was a paragraph addressed to whoever wrote the next store, and this repository
/// has already recorded what that is worth: a default-deny rule written as prose stopped nothing,
/// and the fix was a control.
/// </para>
/// </summary>
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
    /// The names the issue calls out by hand, each one attempted against a real file that already
    /// has contents. The guardrails and the tool schemas are named twice in the design "because it
    /// is the rule that matters most"; the persona pack is the third, because persona writing
    /// lives twice and a loop editing either copy manufactures port drift.
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

        // The half that matters. An exception with the file rewritten underneath it would be a
        // fence that reported refusing and did not.
        Assert.Equal(Untouched, File.ReadAllText(target));
    }

    /// <summary>
    /// The same name in the right folder is the one thing it may write, so the theory above is
    /// testing the rule rather than an inability to write at all.
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

    /// <summary>
    /// The right name in the wrong folder is refused too. Without this the fence would be one
    /// <c>File.Move</c> away from writing anywhere on the disk that had the name spelled right.
    /// </summary>
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
    /// Case is not a defence on Windows, so the allow-list compares case-insensitively and this
    /// says so out loud — a rule that only held for one spelling of a name would not be one.
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
    /// A refusal says which of the named rules it broke, which is what makes a failure here
    /// readable without going and reading the fence.
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
