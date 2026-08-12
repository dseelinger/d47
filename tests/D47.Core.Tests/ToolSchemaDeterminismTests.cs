using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using Xunit;

namespace D47.Core.Tests;

/// <summary>
/// Tool definitions serialize first in an LLM request and caching is a prefix match, so a
/// single changed byte here invalidates the whole cached prefix rather than a tail of it
/// (architecture.md §6). These tests are the reason that claim is safe to rely on.
/// </summary>
public class ToolSchemaDeterminismTests
{
    private static ToolDefinition Tool(IReadOnlyList<ToolParameter> parameters) => new()
    {
        Name = "do_thing",
        Description = "Does a thing.",
        Parameters = parameters,
        Handler = (_, _) => Task.FromResult(ToolResult.Ok("done")),
    };

    private static ToolParameter Param(string name, bool required = false) => new()
    {
        Name = name,
        Type = ToolParameterType.String,
        Description = $"The {name}.",
        Required = required,
    };

    [Fact]
    public void RepeatedSerializationIsByteIdentical()
    {
        var tool = Tool([Param("alpha"), Param("beta", required: true)]);

        Assert.Equal(ToolSchemaWriter.Canonical(tool), ToolSchemaWriter.Canonical(tool));
    }

    [Fact]
    public void ReorderingParametersInSourceDoesNotChangeTheBytes()
    {
        var one = Tool([Param("alpha"), Param("beta", required: true), Param("gamma")]);
        var other = Tool([Param("gamma"), Param("beta", required: true), Param("alpha")]);

        Assert.Equal(ToolSchemaWriter.Canonical(one), ToolSchemaWriter.Canonical(other));
    }

    [Fact]
    public void TwoIndependentlyBuiltRegistriesProduceIdenticalSchemas()
    {
        using var install = new TempInstall();

        static IReadOnlyDictionary<string, string> SchemasFor(TempInstall install) =>
            CapabilityRegistry
                .Build([DiagnosticsCapability.Create(install.Paths, new FakeVerbosityControl(), "1.0.0")])
                .All
                .Single()
                .ToolSchemas;

        var first = SchemasFor(install);
        var second = SchemasFor(install);

        Assert.Equal(first.Keys.Order(), second.Keys.Order());
        foreach (var (name, schema) in first)
        {
            Assert.Equal(schema, second[name]);
        }
    }

    [Fact]
    public void SchemaShapeIsWhatAProviderExpects()
    {
        var schema = ToolSchemaWriter.Canonical(Tool([Param("alpha", required: true)]));

        // Compact and fully specified: no indentation to keep stable, and additionalProperties
        // closed so an invented argument is a schema violation rather than a silent pass.
        Assert.Equal(
            """{"type":"object","properties":{"alpha":{"type":"string","description":"The alpha."}},"required":["alpha"],"additionalProperties":false}""",
            schema);
    }

    [Fact]
    public void ClosedVocabulariesReachTheSchemaAsEnums()
    {
        var tool = Tool(
        [
            new ToolParameter
            {
                Name = "mode",
                Type = ToolParameterType.String,
                Description = "Which mode.",
                AllowedValues = ["docked", "supercruise"],
            },
        ]);

        Assert.Contains(
            "\"enum\":[\"docked\",\"supercruise\"]",
            ToolSchemaWriter.Canonical(tool),
            StringComparison.Ordinal);
    }
}
