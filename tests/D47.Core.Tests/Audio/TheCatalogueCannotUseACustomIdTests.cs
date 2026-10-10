using D47.Core.Audio;
using D47.Core.Storage;
using Xunit;

namespace D47.Core.Tests.Audio;

public class TheCatalogueCannotUseACustomIdTests
{
    private readonly MemoryFileSystem _files = new();

    private readonly string _folder = Path.Combine(@"C:\d47-memory", "custom-id");

    [Fact]
    public void ACatalogRowWithAMyIdIsLeftOutAndLogged()
    {
        _files.WriteText(
            Path.Combine(_folder, ChatterboxCatalog.TableName),
            "id\tname\tgender\tlocale\tpitch\tpace\trole\tsource\tsha256\tbytes\n"
            + "my-12345678\tMine\tfemale\ten\tmid\teven\t\ta clip\t" + new string('a', 64) + "\t1000\n"
            + "plain-one\tPlain\tfemale\ten\tmid\teven\t\ta clip\t" + new string('b', 64) + "\t1000\n");
        var log = new RecordingLogger();

        var voices = ChatterboxCatalog.Load(_files, _folder, _folder, [], log);

        Assert.Equal(["plain-one"], voices.Select(voice => voice.Voice.Id));
        Assert.Contains(log.Entries, entry => entry.Message.Contains("my-12345678", StringComparison.Ordinal));
    }

    [Fact]
    public void AVoicesRowWithAMyIdIsLeftOutAndLogged()
    {
        _files.WriteText(
            Path.Combine(_folder, ChatterboxVoices.TableName),
            "id\tname\tgender\tlocale\trole\tsource\nmy-12345678\tMine\tfemale\ten\t\ta clip\n");
        _files.WriteBytes(Path.Combine(_folder, "my-12345678.wav"), WavWriter.ToBytes(new float[6 * 24_000], 24_000));
        var log = new RecordingLogger();

        Assert.Empty(ChatterboxVoices.Load(_files, _folder, log));
        Assert.Contains(log.Entries, entry => entry.Message.Contains("my-12345678", StringComparison.Ordinal));
    }
}
