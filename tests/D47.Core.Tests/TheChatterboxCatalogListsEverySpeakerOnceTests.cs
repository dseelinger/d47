using Xunit;

namespace D47.Core.Tests;

[Trait("Category", "Gate")]
public class TheChatterboxCatalogListsEverySpeakerOnceTests
{
    private static readonly string[] Columns =
        ["id", "name", "gender", "locale", "pitch", "pace", "role", "source", "sha256", "bytes"];

    [Fact]
    public void TheHeaderIsExactAndEveryRowHasEveryColumn()
    {
        Assert.Equal(Columns, ReadLines(Catalog())[0].Split('	'));
        Assert.All(Rows(), row => Assert.Equal(Columns.Length, row.Length));
    }

    [Fact]
    public void IdsAreUniqueAndNamesAreUniqueWithinGender()
    {
        var rows = Rows();

        Assert.Equal(rows.Count, rows.Select(row => row[0]).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.All(
            rows.GroupBy(row => row[2]),
            group => Assert.Equal(group.Count(), group.Select(row => row[1]).Distinct(StringComparer.OrdinalIgnoreCase).Count()));
    }

    [Fact]
    public void EveryBandIsOneOfItsThreeValuesAndEveryRowHasAGender()
    {
        Assert.All(Rows(), row =>
        {
            Assert.Contains(row[2], new[] { "female", "male" });
            Assert.Contains(row[4], new[] { "low", "mid", "high" });
            Assert.Contains(row[5], new[] { "slow", "even", "brisk" });
        });
    }

    [Fact]
    public void EveryRowHasASourceAHashAndASize()
    {
        Assert.All(Rows(), row =>
        {
            Assert.False(string.IsNullOrWhiteSpace(row[7]));
            Assert.Matches("^[0-9a-f]{64}$", row[8]);
            Assert.True(long.Parse(row[9]) > 0);
        });
    }

    [Fact]
    public void TheShippedVoicesKeepTheirIdNameRoleAndSource()
    {
        var catalog = Rows().ToDictionary(row => row[0]);
        var shipped = ReadLines(Path.Combine(Folder(), "voices.tsv")).Skip(1).Select(line => line.Split('\t')).ToList();

        Assert.Equal(12, shipped.Count);
        Assert.All(shipped, voice =>
        {
            var row = catalog[voice[0]];
            Assert.Equal(voice[1], row[1]);
            Assert.Equal(voice[2], row[2]);
            Assert.Equal(voice[4], row[6]);
            Assert.Equal(voice[5], row[7]);
        });
        Assert.All(
            catalog.Values.Where(row => shipped.All(voice => voice[0] != row[0])),
            row => Assert.Equal(string.Empty, row[6]));
    }

    [Fact]
    public void TheAppListsEveryRowWithTheShippedVoicesFirst()
    {
        var log = Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
        var shipped = D47.Core.Audio.ChatterboxVoices.Load(new D47.Core.Storage.DiskFileSystem(), Folder(), log);

        var voices = D47.Core.Audio.ChatterboxCatalog.Load(Folder(), Path.GetTempPath(), shipped, log);

        Assert.Equal(Rows().Count, voices.Count);
        Assert.Equal(shipped.Select(voice => voice.Voice.Id), voices.Take(12).Select(voice => voice.Voice.Id));
        Assert.All(voices.Take(12), voice => Assert.True(voice.Shipped && voice.Pitch is not null));
        Assert.All(voices.Skip(12), voice => Assert.False(voice.Shipped));
    }

    private static List<string[]> Rows() => ReadLines(Catalog()).Skip(1).Select(line => line.Split('\t')).ToList();

    private static string[] ReadLines(string path) => File.ReadAllLines(path).Where(line => line.Length > 0).ToArray();

    private static string Folder() => Path.Combine(RepositoryRoot(), "assets", "voices", "chatterbox");

    private static string Catalog() => Path.Combine(Folder(), "catalog.tsv");

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "d47.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException(
                $"Could not find the repository root: no d47.slnx above {AppContext.BaseDirectory}.");
    }
}
