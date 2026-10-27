using System.Diagnostics;
using Xunit;

namespace D47.Core.Tests.Knowledge;

[Trait("Category", "Integration")]
public sealed class TheStarterGeneratorReadsJournalLinesTests
{
    [Fact]
    public void ALockedSystemAfterNewCommanderIsAStarterAndAnOpenOneIsNot()
    {
        Assert.Equal(["HIP 97950"], Run(
            """{"event":"Location","StarSystem":"Sol"}""",
            """{"event":"NewCommander","Name":"X"}""",
            """{"event":"LoadGame"}""",
            """{"event":"Location","StarSystem":"HIP 97950"}""",
            """{"event":"Location","StarSystem":"Sol"}"""));

        Assert.Empty(Run(
            """{"event":"NewCommander","Name":"X"}""",
            """{"event":"Location","StarSystem":"Ross 354"}""",
            """{"event":"Location","StarSystem":"Sol"}"""));
    }

    private static string[] Run(params string[] lines)
    {
        var root = AppContext.BaseDirectory;

        while (!File.Exists(Path.Combine(root, "d47.slnx")))
        {
            root = Path.GetDirectoryName(root) ?? throw new InvalidOperationException("d47.slnx not found");
        }

        const string Script = """
            import importlib.util, json, sys
            spec = importlib.util.spec_from_file_location("gen_starters", sys.argv[1])
            module = importlib.util.module_from_spec(spec)
            spec.loader.exec_module(module)
            found = module.starters(sys.stdin.read().splitlines(), {"HIP 97950"})
            print(json.dumps(sorted(found)))
            """;

        var start = new ProcessStartInfo("python")
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };
        start.ArgumentList.Add("-I");
        start.ArgumentList.Add("-c");
        start.ArgumentList.Add(Script);
        start.ArgumentList.Add(Path.Combine(root, "tools", "gen-starters.py"));

        using var process = Process.Start(start)!;
        process.StandardInput.Write(string.Join('\n', lines));
        process.StandardInput.Close();
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();

        return System.Text.Json.JsonSerializer.Deserialize<string[]>(output)!;
    }
}
