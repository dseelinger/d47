using D47.Core.Checklists;
using Xunit;

namespace D47.Core.Tests.Checklists;

/// <summary>The list a Commander wrote themselves is the custom list.</summary>
public class TheCommandersWordForTheirOwnListTests
{
    [Fact]
    public void TheWordIsCustom()
    {
        Assert.Equal("custom", ChecklistScope.Word(ChecklistGroup.Universal));
        Assert.Equal("custom", ChecklistScope.Universal.ToString());
    }

    [Theory]
    [InlineData(ChecklistGroup.Ship, "ship")]
    [InlineData(ChecklistGroup.System, "system")]
    [InlineData(ChecklistGroup.Suit, "suit")]
    [InlineData(ChecklistGroup.Weapon, "weapon")]
    public void AndTheOthersAreUnchanged(ChecklistGroup group, string word)
    {
        Assert.Equal(word, ChecklistScope.Word(group));
    }

    /// <summary>The half that must not have moved.</summary>
    [Fact]
    public void WhatIsWrittenDownIsUntouched()
    {
        using var install = new TempInstall();
        var path = Path.Combine(install.Paths.Data, "checklist.json");

        Directory.CreateDirectory(install.Paths.Data);
        File.WriteAllText(path, """
            {
              "commanders": [
                {
                  "commanderFid": "",
                  "items": [
                    {
                      "key": "note-1",
                      "scope": { "group": "universal" },
                      "kind": "authored",
                      "text": "buy limpets",
                      "state": "open"
                    }
                  ]
                }
              ]
            }
            """);

        var checklists = TestSurface.Checklists(install.Paths);

        // Read off disk rather than trusted from memory: the store polls on write time, and this file was
        // written behind its back on purpose.
        Assert.True(checklists.List.Poll());

        var item = Assert.Single(checklists.Document.In(ChecklistScope.Universal));

        Assert.Equal("buy limpets", item.Text);

        // Read back as the old word, shown as the new one.
        Assert.Equal(ChecklistGroup.Universal, item.Scope.Group);
        Assert.Equal("custom", item.Scope.ToString());
    }

    /// <summary>And a Commander filtering the page asks for it by the word they can see.</summary>
    [Fact]
    public void TheFilterOffersTheWordTheCommanderReads()
    {
        using var install = new TempInstall();
        var checklists = TestSurface.Checklists(install.Paths);

        checklists.AddNote(ChecklistScope.Universal, "buy limpets");

        Assert.Contains("custom", checklists.Filters());
        Assert.DoesNotContain("universal", checklists.Filters());
    }
}
