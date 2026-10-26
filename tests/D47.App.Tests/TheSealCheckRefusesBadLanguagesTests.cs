using System.Diagnostics;
using System.Text.Json;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// <c>tools/seal-stories.py</c> refuses a cast member's language and its lines' translations as
/// <c>StoryCatalog.Faults</c> does (#1751).
/// </summary>
[Trait("Category", "Integration")]
public sealed class TheSealCheckRefusesBadLanguagesTests
{
    private const string Check = """
        import importlib.util, json, sys
        spec = importlib.util.spec_from_file_location("seal", sys.argv[1])
        seal = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(seal)
        speaker = json.loads(sys.argv[2])
        found = seal.language_fault("cast 1 (mio)", speaker)
        found += seal.translation_fault("opening line", json.loads(sys.argv[3]), {"mio"})
        print(json.dumps(found))
        """;

    private const string Mio = """{"id":"mio","provider":"edge","voice":"ja-JP-NanamiNeural","pinned":true,"language":"ja"}""";

    private const string Said = """[{"speaker":"mio","text":"Tasukete.","translation":"Help."}]""";

    private static IReadOnlyList<string> Faults(string speaker, string lines)
    {
        var start = new ProcessStartInfo("python")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        start.ArgumentList.Add("-I");
        start.ArgumentList.Add("-c");
        start.ArgumentList.Add(Check);
        start.ArgumentList.Add(Path.Combine(AppSource.RepositoryRoot(), "tools", "seal-stories.py"));
        start.ArgumentList.Add(speaker);
        start.ArgumentList.Add(lines);

        using var python = Process.Start(start)!;
        var output = python.StandardOutput.ReadToEnd();
        var error = python.StandardError.ReadToEnd();
        python.WaitForExit();

        Assert.True(python.ExitCode == 0, error);
        return JsonSerializer.Deserialize<List<string>>(output)!;
    }

    [Fact]
    public void APinnedEdgeJapaneseSpeakerWithATranslationHasNoFault() =>
        Assert.Empty(Faults(Mio, Said));

    [Theory]
    [InlineData("""{"id":"mio","provider":"edge","voice":"ja-JP-NanamiNeural","pinned":true,"language":"fr"}""", "not one of ja")]
    [InlineData("""{"id":"mio","provider":"edge","voice":"ja-JP-NanamiNeural","language":"ja"}""", "is not pinned")]
    [InlineData("""{"id":"mio","provider":"kokoro","voice":"af_heart","pinned":true,"language":"ja"}""", "not edge")]
    [InlineData("""{"id":"mio","provider":"edge","voice":"en-US-AriaNeural","pinned":true,"language":"ja"}""", "not an Edge ja- voice")]
    [InlineData("""{"id":"mio","provider":"edge","pinned":true,"language":"ja","versions":{"forMan":{"name":"Ken","voice":"ja-JP-KeitaNeural"},"forWoman":{"name":"Mio","voice":"own"}}}""", "version forWoman has a language and a voice")]
    public void ABadLanguageIsRefused(string speaker, string fault) =>
        Assert.Contains(Faults(speaker, Said), found => found.Contains(fault, StringComparison.Ordinal));

    [Theory]
    [InlineData("""[{"speaker":"mio","text":"Tasukete."}]""", "has no translation")]
    [InlineData("""[{"speaker":"mio","text":"{they} wa","translation":"Help."}]""", "{their} or {they} token")]
    public void ALineInALanguageNeedsATranslationAndNoPronounToken(string lines, string fault) =>
        Assert.Contains(fault, string.Join("\n", Faults(Mio, lines)));
}
