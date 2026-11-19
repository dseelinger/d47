using System.Diagnostics;
using D47.Core.Interface;
using D47.Core.Knowledge;
using Xunit;

namespace D47.App.Tests;

/// <summary><c>tools/codex-pictures-rekey.py</c> adds a kind row for each old star or planet stock row and keeps the rest (#2603).</summary>
[Trait("Category", "Integration")]
public sealed class TheRekeyScriptAddsAKindRowForEachStockPictureTests
{
    private static string Fixture => Path.Combine(AppSource.RepositoryRoot(), "tests", "fixtures", "codex", "stock-index-2026-11-19.json");

    private static IReadOnlyList<CodexPictureRow> Rekey(string index)
    {
        var output = Path.Combine(TempFolders.Create("d47-codex-rekey-script"), "index.json");
        var start = new ProcessStartInfo("python")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        start.ArgumentList.Add("-I");
        start.ArgumentList.Add(Path.Combine(AppSource.RepositoryRoot(), "tools", "codex-pictures-rekey.py"));
        start.ArgumentList.Add("--index");
        start.ArgumentList.Add(index);
        start.ArgumentList.Add("--out");
        start.ArgumentList.Add(output);

        using var python = Process.Start(start)!;
        python.StandardOutput.ReadToEnd();
        var error = python.StandardError.ReadToEnd();
        python.WaitForExit();

        Assert.True(python.ExitCode == 0, error);

        var parsed = CodexPictureIndex.Parse(File.ReadAllText(output));
        Assert.Empty(parsed.Reasons);

        return parsed.Rows;
    }

    private static string Written(IReadOnlyList<CodexPictureRow> rows)
    {
        var path = Path.Combine(TempFolders.Create("d47-codex-rekey-script"), "once.json");
        File.WriteAllText(path, CodexPictureIndex.Write(rows));

        return path;
    }

    [Fact]
    public void TodaysIndexGainsSixteenKindRowsAndKeepsEveryOtherRow()
    {
        var before = CodexPictureIndex.Parse(File.ReadAllText(Fixture)).Rows;
        var after = Rekey(Fixture);

        var old = before.Where(row => CodexKeys.EntryIdOf(row.EntryKey) is { } id && CodexEntryKinds.Of(id) is not null).ToList();
        var added = after.Where(row => CodexKeys.IsKind(row.EntryKey)).ToList();

        Assert.Equal(16, old.Count);
        Assert.Equal(16, added.Count);
        Assert.Equal(before.OrderBy(row => row.Asset, StringComparer.Ordinal), after.Except(added).OrderBy(row => row.Asset, StringComparer.Ordinal));

        foreach (var row in old)
        {
            var kind = CodexEntryKinds.Of(CodexKeys.EntryIdOf(row.EntryKey)!.Value)!;

            Assert.Contains(row with { EntryKey = kind }, added);
        }
    }

    [Fact]
    public void ASecondRunAddsNothing()
    {
        var once = Rekey(Fixture);

        Assert.Equal(once, Rekey(Written(once)));
    }
}
