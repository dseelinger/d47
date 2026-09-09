using System.Text.RegularExpressions;
using D47.Core.Diagnostics;
using Xunit;

namespace D47.Core.Tests.Diagnostics;

/// <summary>The log-level rows actually reach the code they name.</summary>
public class LogRoutingTests
{
    /// <summary>Every namespace declared anywhere under <c>src/</c>.</summary>
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

    /// <summary>The assertion that was missing.</summary>
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

    /// <summary>The speech loop is reachable as one subsystem.</summary>
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
    /// <c>D47.App</c> and <c>D47.App.Voice</c> both match a voice logger, and the design relies on the
    /// longer one winning — Serilog applies the most specific override.
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
