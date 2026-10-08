using D47.Core;
using D47.Core.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Configuration;

/// <summary>A view state written while the headset kept its own tab and roots still reads the window's (#948).</summary>
public class AViewStateWithTheHeadsetsOwnTabStillLoadsTests
{
    [Fact]
    public void TheWindowsTabAndRootsAreReadPastTheHeadsetsKeys()
    {
        var paths = new AppPaths(Path.Combine(
            Path.GetTempPath(), "d47-view-state-vr-keys", Guid.NewGuid().ToString("n")));
        paths.EnsureCreated();

        File.WriteAllText(paths.ViewStateFile, """
            {
              "panelRoots": { "Commander": "standing" },
              "panelRootsVr": { "Commander": "checklist" },
              "lastTab": "Commander",
              "lastTabVr": "Transcript"
            }
            """);

        var state = new ViewStateStore(paths, NullLogger<ViewStateStore>.Instance).Load();

        Assert.Equal("Commander", state.LastTab);
        Assert.Equal("standing", state.PanelRoots["Commander"]);
    }
}
