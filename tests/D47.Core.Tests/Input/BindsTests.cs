using D47.Core.Input;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Input;

/// <summary>
/// The bindings parser and the three traps architecture.md D4 names. Every test here builds a
/// real folder layout on disk, because the traps are entirely about which file gets chosen.
/// </summary>
public class BindsTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "d47-binds-tests", Guid.NewGuid().ToString("N"));

    private string Bindings => Path.Combine(_root, "Options", "Bindings");

    private string Game => Path.Combine(_root, "Game");

    public BindsTests()
    {
        Directory.CreateDirectory(Bindings);
        Directory.CreateDirectory(Game);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
            // A leftover temp folder is not worth failing a test over.
        }

        GC.SuppressFinalize(this);
    }

    private void StartPreset(string fileName, string preset) =>
        File.WriteAllText(Path.Combine(Bindings, fileName), preset + "\n" + preset + "\n");

    private static void Binds(string path, params (string Action, string Key, string[] Modifiers)[] actions)
    {
        var body = string.Join("\n", actions.Select(action =>
        {
            var modifiers = string.Join("", action.Modifiers.Select(modifier =>
                $"""<Modifier Device="Keyboard" Key="{modifier}" />"""));

            return $$"""
                      <{{action.Action}}>
                        <Primary Device="Keyboard" Key="{{action.Key}}">{{modifiers}}</Primary>
                        <Secondary Device="{NoDevice}" Key="" />
                      </{{action.Action}}>
                    """;
        }));

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, $"<Root PresetName=\"Test\">\n{body}\n</Root>");
    }

    private EliteBinds Resolve() =>
        BindsResolver.Resolve(Bindings, [Game], NullLogger.Instance);

    // ---- Trap 1: the active preset is named, not assumed --------------------------------

    [Fact]
    public void TheActivePresetIsReadFromStartPresetRatherThanAssumedToBeCustom()
    {
        StartPreset("StartPreset.4.start", "KeyboardMouseOnly");

        Binds(Path.Combine(Bindings, "Custom.4.2.binds"), ("YawLeftButton", "Key_Q", []));
        Binds(Path.Combine(Game, "ControlSchemes", "KeyboardMouseOnly.binds"), ("YawLeftButton", "Key_A", []));

        var binds = Resolve();

        // Reading Custom.*.binds unconditionally parses a file the Commander is not using and
        // then confidently reports the wrong keys.
        Assert.Equal("KeyboardMouseOnly", binds.PresetName);
        Assert.Equal("Key_A", Assert.Single(binds.Bindings).Key);
    }

    [Fact]
    public void NoStartPresetMeansNoAnswerRatherThanAGuess()
    {
        Binds(Path.Combine(Bindings, "Custom.4.2.binds"), ("YawLeftButton", "Key_Q", []));

        var binds = Resolve();

        Assert.False(binds.IsKnown);
        Assert.Empty(binds.Bindings);
    }

    [Fact]
    public void TwoStartPresetFilesAreOrderedRatherThanCrashingStartup()
    {
        // Both, which is a real machine: Elite left StartPreset.start behind when it began
        // writing StartPreset.4.start, so an install that predates that change has the pair.
        // The versions are int[], and sorting them with the default comparer throws
        // "At least one object must implement IComparable" - but only from the second file on,
        // because a one-element sort never compares anything. It killed startup before the
        // window existed, and every other test here writes exactly one file.
        StartPreset("StartPreset.start", "KeyboardMouseOnly");
        StartPreset("StartPreset.4.start", "Custom");

        Binds(Path.Combine(Bindings, "Custom.4.2.binds"), ("YawLeftButton", "Key_Q", []));

        var binds = Resolve();

        // And the higher version is the one that answers, not whichever the file system listed
        // first.
        Assert.Equal("Custom", binds.PresetName);
    }

    // ---- Trap 2: built-in presets are not in the user profile ---------------------------

    [Fact]
    public void AShippedPresetIsFoundInTheGameDirectory()
    {
        StartPreset("StartPreset.4.start", "KeyboardMouseOnly");
        Binds(Path.Combine(Game, "ControlSchemes", "KeyboardMouseOnly.binds"), ("UseBoostJuice", "Key_Tab", []));

        var binds = Resolve();

        // Only searching Options/Bindings finds nothing for a Commander who never customised
        // their controls — which is the common case, not an edge case.
        Assert.True(binds.IsKnown);
        Assert.Equal("UseBoostJuice", Assert.Single(binds.Bindings).Action);
    }

    // ---- Trap 3: the version suffix moves ------------------------------------------------

    [Fact]
    public void TheStartPresetVersionAndTheBindsVersionAreNotTheSameNumber()
    {
        StartPreset("StartPreset.4.start", "Custom");
        Binds(Path.Combine(Bindings, "Custom.4.2.binds"), ("YawLeftButton", "Key_Q", []));

        var binds = Resolve();

        // Custom.4.2.binds alongside StartPreset.4.start is a real machine's layout. Neither
        // number may be hardcoded and they are not the same number.
        Assert.True(binds.IsKnown);
        Assert.EndsWith("Custom.4.2.binds", binds.SourceFile);
    }

    [Fact]
    public void TheHighestVersionWinsNumericallyNotAlphabetically()
    {
        StartPreset("StartPreset.4.start", "Custom");
        Binds(Path.Combine(Bindings, "Custom.4.2.binds"), ("YawLeftButton", "Key_Q", []));
        Binds(Path.Combine(Bindings, "Custom.4.10.binds"), ("YawLeftButton", "Key_Z", []));

        var binds = Resolve();

        // A string comparison puts 4.10 below 4.2 and quietly selects a file two revisions
        // stale.
        Assert.EndsWith("Custom.4.10.binds", binds.SourceFile);
        Assert.Equal("Key_Z", Assert.Single(binds.Bindings).Key);
    }

    // ---- Parsing -------------------------------------------------------------------------

    [Fact]
    public void UnboundSlotsAreNotBindings()
    {
        StartPreset("StartPreset.4.start", "Custom");

        File.WriteAllText(Path.Combine(Bindings, "Custom.4.0.binds"),
            """
            <Root PresetName="Test">
              <YawLeftButton>
                <Primary Device="{NoDevice}" Key="" />
                <Secondary Device="{NoDevice}" Key="" />
              </YawLeftButton>
              <UseBoostJuice>
                <Primary Device="Keyboard" Key="Key_Tab" />
                <Secondary Device="{NoDevice}" Key="" />
              </UseBoostJuice>
            </Root>
            """);

        var binds = Resolve();

        // Most of the file is unbound. Treating those as bindings would have every unbound
        // action colliding with every other one.
        Assert.Equal("UseBoostJuice", Assert.Single(binds.Bindings).Action);
    }

    [Fact]
    public void ModifiersAreOrderIndependent()
    {
        StartPreset("StartPreset.4.start", "Custom");
        Binds(Path.Combine(Bindings, "Custom.4.0.binds"),
            ("SomeAction", "Key_X", ["Key_LeftAlt", "Key_LeftControl"]));

        var binds = Resolve();

        // Elite writes modifiers in binding order. "Ctrl+Alt+X" and "Alt+Ctrl+X" are the same
        // key to the Commander pressing it.
        Assert.Single(binds.Using("Ctrl+Alt+X"));
        Assert.Single(binds.Using("Alt+Ctrl+X"));
        Assert.Single(binds.Using("LeftControl+LeftAlt+X"));
    }

    [Fact]
    public void AMalformedBindingsFileIsAStateNotACrash()
    {
        StartPreset("StartPreset.4.start", "Custom");
        File.WriteAllText(Path.Combine(Bindings, "Custom.4.0.binds"), "<Root><Unclosed>");

        var binds = Resolve();

        // An unreadable bindings file is a capability being unavailable, not a reason for d47
        // to fail to start.
        Assert.Empty(binds.Bindings);
    }

    // ---- The collision this exists for ----------------------------------------------------

    [Fact]
    public void AKeyEliteIsAlreadyUsingIsReportedWithEveryActionUsingIt()
    {
        StartPreset("StartPreset.4.start", "Custom");
        Binds(Path.Combine(Bindings, "Custom.4.0.binds"),
            ("HeadLookToggle", "Key_H", []),
            ("GalaxyMapOpen", "Key_H", ["Key_LeftShift"]),
            ("QuickCommsPanel", "Key_H", []));

        var binds = Resolve();

        // The whole item: a double-bound push-to-talk key has no symptom other than not
        // working, in one direction or the other.
        var collisions = binds.Using("H");
        Assert.Equal(2, collisions.Count);
        Assert.Contains(collisions, binding => binding.Action == "HeadLookToggle");
        Assert.Contains(collisions, binding => binding.Action == "QuickCommsPanel");

        // Shift+H is a different gesture and is not a collision with plain H.
        Assert.Equal("GalaxyMapOpen", Assert.Single(binds.Using("Shift+H")).Action);
    }

    [Fact]
    public void SharedGesturesAreListedWithTheActionsThatShareThem()
    {
        StartPreset("StartPreset.4.start", "Custom");
        Binds(Path.Combine(Bindings, "Custom.4.0.binds"),
            ("HeadLookToggle", "Key_H", []),
            ("QuickCommsPanel", "Key_H", []),
            ("UseBoostJuice", "Key_Tab", []));

        var shared = Resolve().Shared();

        // Elite binds the same key across control modes on purpose, so this reports what is
        // shared and leaves the judgement to the caller rather than calling it a fault.
        var entry = Assert.Single(shared);
        Assert.Equal("h", entry.Key);
        Assert.Equal(2, entry.Value.Count);
    }

    [Fact]
    public void AnUnusedKeyCollidesWithNothing()
    {
        StartPreset("StartPreset.4.start", "Custom");
        Binds(Path.Combine(Bindings, "Custom.4.0.binds"), ("UseBoostJuice", "Key_Tab", []));

        Assert.Empty(Resolve().Using("Ctrl+Alt+X"));
    }
}
