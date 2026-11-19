using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

/// <summary>The region-first file on disk reads back as it was written.</summary>
[Trait("Category", "Integration")]
public sealed class RegionFirstsSurviveARestartTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "d47-region-firsts-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    [Fact]
    public void ARecordWrittenToDiskIsReadBackWhole()
    {
        var path = Path.Combine(_folder, RegionFirstBook.FileName);
        var files = new DiskFileSystem();
        var table = CodexTable.Parse("# Table date: 2026-10-19\n2311001\tFungoida\tBiology\tOrganic\t4:12\n");

        string[] lines =
        [
            "{\"timestamp\":\"2026-10-10T09:00:00Z\",\"event\":\"FSDJump\",\"StarSystem\":\"Sol\",\"SystemAddress\":10477373803,\"StarPos\":[1.5,-2.25,30.0]}",
            "{\"timestamp\":\"2026-10-10T10:00:00Z\",\"event\":\"CodexEntry\",\"EntryID\":2311001,\"Name\":\"$Codex_Ent_Fungoida;\""
            + ",\"Name_Localised\":\"Fungoida\",\"SubCategory\":\"$Codex_SubCategory_Organic_Structures;\""
            + ",\"Region\":\"$Codex_RegionName_18;\",\"System\":\"Sol\",\"SystemAddress\":10477373803,\"BodyID\":5"
            + ",\"Latitude\":-12.5,\"Longitude\":101.25,\"IsNewEntry\":true}",
        ];

        var ledger = lines.Aggregate(CodexLedger.None, (held, line) =>
        {
            Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
            return held.Apply(parsed!);
        });

        var written = new RegionFirstBook(path, files, NullLogger.Instance, work => work());
        written.Observe("F1", CodexLedger.None.With(ledger), table);

        var read = new RegionFirstBook(path, files, NullLogger.Instance).Find("F1", 2311001, 18);

        Assert.NotNull(read);
        Assert.Equal(written.Find("F1", 2311001, 18), read);
    }
}
