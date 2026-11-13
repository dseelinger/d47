using Avalonia.Input;
using D47.App.Input;
using D47.Core.Input;
using Xunit;

namespace D47.App.Tests;

public class EveryStoredKeyResolvesInCoreTests
{
    [Fact]
    public void EveryKeyVirtualKeysMapsResolvesInCoreToTheSameCode()
    {
        var mismatches = Enum.GetValues<Key>()
            .Select(key => (Name: key.ToString(), Expected: VirtualKeys.Of(key)))
            .Where(key => key.Expected is not null)
            .Where(key => EliteKeys.GestureCode(key.Name) != key.Expected)
            .Select(key => $"{key.Name}: expected {key.Expected:X2}")
            .ToArray();

        Assert.Empty(mismatches);
    }
}
