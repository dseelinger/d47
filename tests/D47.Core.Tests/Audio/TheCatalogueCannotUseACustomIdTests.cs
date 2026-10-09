using D47.Core.Audio;
using Xunit;

namespace D47.Core.Tests.Audio;

public class TheCatalogueCannotUseACustomIdTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "d47-custom-id-" + Guid.NewGuid().ToString("N"));

    public TheCatalogueCannotUseACustomIdTests() => Directory.CreateDirectory(_folder);

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        Directory.Delete(_folder, recursive: true);
    }

    [Fact]
    public void ACatalogRowWithAMyIdIsLeftOutAndLogged()
    {
        File.WriteAllText(
            Path.Combine(_folder, ChatterboxCatalog.TableName),
            "id\tname\tgender\tlocale\tpitch\tpace\trole\tsource\tsha256\tbytes\n"
            + "my-12345678\tMine\tfemale\ten\tmid\teven\t\ta clip\t" + new string('a', 64) + "\t1000\n"
            + "plain-one\tPlain\tfemale\ten\tmid\teven\t\ta clip\t" + new string('b', 64) + "\t1000\n");
        var log = new RecordingLogger();

        var voices = ChatterboxCatalog.Load(_folder, _folder, [], log);

        Assert.Equal(["plain-one"], voices.Select(voice => voice.Voice.Id));
        Assert.Contains(log.Entries, entry => entry.Message.Contains("my-12345678", StringComparison.Ordinal));
    }

    [Fact]
    public void AVoicesRowWithAMyIdIsLeftOutAndLogged()
    {
        File.WriteAllText(
            Path.Combine(_folder, ChatterboxVoices.TableName),
            "id\tname\tgender\tlocale\trole\tsource\nmy-12345678\tMine\tfemale\ten\t\ta clip\n");
        File.WriteAllBytes(Path.Combine(_folder, "my-12345678.wav"), WavWriter.ToBytes(new float[6 * 24_000], 24_000));
        var log = new RecordingLogger();

        Assert.Empty(ChatterboxVoices.Load(_folder, log));
        Assert.Contains(log.Entries, entry => entry.Message.Contains("my-12345678", StringComparison.Ordinal));
    }
}
