using D47.Core.Capabilities;
using D47.Core.Configuration;
using Xunit;

namespace D47.Core.Tests.Configuration;

public class ClearingAToggleRestoresItsDefaultTests
{
    [Fact]
    public void EveryToggleRowWritesItsDefaultWhenCleared()
    {
        var surface = TestSurface.For(new MemoryInstall());
        var defaults = D47Settings.Defaults;
        var wrong = new List<string>();
        var checkedRows = 0;

        foreach (var row in surface.Registry.All.SelectMany(c => c.Descriptor.Settings))
        {
            if (row.Kind != SettingKind.Toggle || row.Binding?.Write is not { } write)
            {
                continue;
            }

            var defaultValue = row.Binding.Read(defaults);
            var opposite = defaultValue == "true" ? "false" : "true";
            var flipped = write(defaults, opposite);
            Assert.True(row.Binding.Read(flipped) == opposite, $"{row.Key} does not take '{opposite}'.");

            checkedRows++;
            if (row.Binding.Read(write(flipped, null)) != defaultValue)
            {
                wrong.Add($"{row.Key} (default {defaultValue})");
            }
        }

        Assert.True(checkedRows > 0);
        Assert.True(wrong.Count == 0, $"Cleared, these write something other than their default: {string.Join(", ", wrong)}");
    }
}
