using D47.Core.Audio;
using D47.Core.Storage;
using Xunit;

namespace D47.Core.Tests.Audio;

/// <summary>A given name in the shipped table is cast by it first, before any stored answer or model question.</summary>
public class AShippedGivenNameIsCastWithNoQuestionTests
{
    private static readonly string[] Accents = ["American"];

    private static readonly Dictionary<string, string> Table = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Marianne"] = "female",
        ["Dan"] = "male",
    };

    [Fact]
    public void AFirstLineFromAnNpcInTheTableWaitsForNothingAndIsNeverAsked()
    {
        var asked = new List<IReadOnlyList<string>>();
        var names = new NameAccents(shipped: Table)
        {
            Ask = (_, given, _, _) =>
            {
                asked.Add(given);

                return Task.FromResult<NameAnswers?>(null);
            },
        };

        Assert.Equal("female", names.SexOfGivenName("Marianne"));
        Assert.Null(names.WhenGivenNameAnswered("p", Accents, "Marianne Okafor"));

        names.Enqueue("p", Accents, ["Marianne Okafor"]);

        Assert.DoesNotContain(asked, given => given.Contains("Marianne"));
    }

    [Fact]
    public void ATableRowBeatsADifferentStoredAnswerForTheSameGivenName()
    {
        var files = new MemoryFileSystem();
        files.WriteText("given.json", """{ "Dan": "female", "Pat": "male" }""");

        var names = new NameAccents(files, givenFile: "given.json", shipped: Table);

        Assert.Equal("male", names.SexOfGivenName("Dan"));
        Assert.Equal("male", names.SexOfGivenName("Pat"));
    }
}

/// <summary>The shipped table file is well formed.</summary>
[Trait("Category", "Integration")]
public class TheShippedGivenNameTableIsWellFormedTests
{
    [Fact]
    public void TheShippedTableHoldsOnlyTheThreeStatesSortedWithNoRepeat()
    {
        var rows = ReadRows();

        Assert.All(rows, row => Assert.Contains(row.State, new[] { "male", "female", "indeterminate" }));
        Assert.Equal(rows.Select(row => row.Name).OrderBy(name => name, StringComparer.OrdinalIgnoreCase), rows.Select(row => row.Name));
        Assert.Equal(rows.Count, rows.Select(row => row.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.All(rows, row => Assert.DoesNotContain(' ', row.Name));
    }

    [Fact]
    public void TheShippedTableIsEmbeddedAndParsed()
    {
        Assert.Equal(ReadRows().Count, FirstNames.Shipped.Count);
    }

    private static List<(string Name, string State)> ReadRows()
    {
        var folder = AppContext.BaseDirectory;

        while (!File.Exists(Path.Combine(folder, "d47.slnx")))
        {
            folder = Path.GetDirectoryName(folder) ?? throw new InvalidOperationException("d47.slnx not found");
        }

        return [.. File.ReadAllLines(Path.Combine(folder, "src", "D47.Core", "Knowledge", "FirstNames.tsv"))
            .Where(line => line.Length > 0 && !line.StartsWith('#'))
            .Select(line => line.Split('\t'))
            .Select(cells => (cells[0], cells[1]))];
    }
}
