using System.Text.RegularExpressions;
using D47.Core.Diagnostics;
using Xunit;

namespace D47.Core.Tests.Diagnostics;

/// <summary>
/// The log-level rows actually reach the code they name (bugs.md 1).
/// <para>
/// <b>What went wrong, and why nothing noticed.</b> Each subsystem was mapped to one namespace,
/// and three of the eight named namespaces that do not exist: <c>D47.Voice</c>,
/// <c>D47.Input</c> and <c>D47.Core.Llm</c>. A Serilog override is matched against the start of
/// a logger's <c>SourceContext</c>, so a prefix matching nothing is simply never applied — no
/// error, no warning, and a settings row that reads back exactly what you set it to while
/// changing nothing at all. A wrong string here fails silently in both directions, which is
/// precisely the kind of thing that needs a test rather than a reader.
/// </para>
/// </summary>
public class LogRoutingTests
{
    /// <summary>
    /// Every namespace declared anywhere under <c>src/</c>.
    /// <para>
    /// Read from the source rather than by reflecting over loaded assemblies, because the
    /// subsystems span projects this test assembly does not reference — the speech providers and
    /// the OpenVR runtime among them, which are exactly the entries that were wrong.
    /// </para>
    /// </summary>
    private static IReadOnlyList<string> DeclaredNamespaces()
    {
        var source = Path.Combine(RepositoryRoot(), "src");
        var found = new HashSet<string>(StringComparer.Ordinal);

        foreach (var file in Directory.EnumerateFiles(source, "*.cs", SearchOption.AllDirectories))
        {
            // Skip build output: obj/ holds generated files that name namespaces nobody wrote.
            if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                || file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            {
                continue;
            }

            foreach (Match match in Regex.Matches(File.ReadAllText(file), @"^namespace\s+([\w.]+)", RegexOptions.Multiline))
            {
                found.Add(match.Groups[1].Value);
            }
        }

        return [.. found];
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "d47.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("no repository root above the test binary");
    }

    /// <summary>
    /// The assertion that was missing. A prefix naming no real namespace controls nothing, and
    /// says so to nobody.
    /// </summary>
    [Fact]
    public void EveryPrefixMatchesCodeThatExists()
    {
        var namespaces = DeclaredNamespaces();
        var dead = new List<string>();

        foreach (var (subsystem, prefixes) in Subsystems.SourcePrefixes)
        {
            foreach (var prefix in prefixes)
            {
                var matches = namespaces.Any(name =>
                    name.Equals(prefix, StringComparison.Ordinal)
                    || name.StartsWith(prefix + ".", StringComparison.Ordinal));

                if (!matches)
                {
                    dead.Add($"{subsystem} -> {prefix}");
                }
            }
        }

        Assert.True(
            dead.Count == 0,
            "These log prefixes match no namespace in src/, so their level rows control nothing:"
            + Environment.NewLine + string.Join(Environment.NewLine, dead));
    }

    /// <summary>Every subsystem has somewhere to point, or its settings row is decoration.</summary>
    [Fact]
    public void EverySubsystemIsRouted()
    {
        Assert.All(
            Subsystems.All,
            subsystem =>
            {
                Assert.True(Subsystems.SourcePrefixes.ContainsKey(subsystem), $"{subsystem} has no prefixes");
                Assert.NotEmpty(Subsystems.SourcePrefixes[subsystem]);
            });
    }

    /// <summary>
    /// The speech loop is reachable as one subsystem. It spans four projects, and the single
    /// string this replaced could not have named more than one of them — which is how the
    /// original defect happened rather than being a typo.
    /// </summary>
    [Theory]
    [InlineData("D47.App.Voice.VoicePipeline")]
    [InlineData("D47.Core.Audio.SpeechPipeline")]
    [InlineData("D47.Core.Listening.ListenGate")]
    [InlineData("D47.Tts.ElevenLabsTtsProvider")]
    public void TheWholeSpeechLoopAnswersToTheVoiceRow(string source)
    {
        Assert.Contains(
            Subsystems.SourcePrefixes[Subsystems.Voice],
            prefix => source.StartsWith(prefix, StringComparison.Ordinal));
    }

    /// <summary>
    /// <c>D47.App</c> and <c>D47.App.Voice</c> both match a voice logger, and the design relies
    /// on the longer one winning — Serilog applies the most specific override. If that were not
    /// true, the App row would quietly govern the speech loop and turning Voice down would do
    /// nothing again, by a different route.
    /// </summary>
    [Fact]
    public void WherePrefixesOverlapTheMoreSpecificSubsystemOwnsIt()
    {
        const string voiceLogger = "D47.App.Voice.VoicePipeline";

        var claimants = Subsystems.SourcePrefixes
            .Where(entry => entry.Value.Any(p => voiceLogger.StartsWith(p, StringComparison.Ordinal)))
            .ToList();

        // Both do claim it; that is the situation being asserted about, not a fault.
        Assert.Equal(2, claimants.Count);

        var longest = claimants
            .SelectMany(entry => entry.Value.Select(prefix => (entry.Key, prefix)))
            .Where(candidate => voiceLogger.StartsWith(candidate.prefix, StringComparison.Ordinal))
            .OrderByDescending(candidate => candidate.prefix.Length)
            .First();

        Assert.Equal(Subsystems.Voice, longest.Key);
    }
}
