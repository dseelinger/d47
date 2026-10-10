using System.Text;
using D47.Core.Journal;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

[Trait("Category", "Integration")]
public class AShipLockerEliteHoldsOpenStillReadsTests
{
    [Fact]
    public void ReadingWorksWhileAnotherHandleHasTheFileOpenForWriting()
    {
        using var install = new TempInstall();
        var path = Path.Combine(install.Root, SuitInventoryReader.ShipLockerFile);
        File.WriteAllText(path, "");

        using (var writer = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete))
        {
            writer.Write(Encoding.UTF8.GetBytes("""{ "event":"ShipLocker", "Components":[ { "Name":"graphene", "Count":212 } ] }"""));
            writer.Flush();

            var reader = new SuitInventoryReader(install.Root, new DiskFileSystem(), NullLogger.Instance);

            Assert.True(reader.Poll());
            Assert.Equal(212, Assert.Single(reader.Current.ShipLocker).Count);
        }
    }
}
