using System.Text.Json.Nodes;

namespace D47.Core.Contribution;

/// <summary>One EDDN message: the schema it follows, its header and its body.</summary>
public sealed record EddnEnvelope(string SchemaRef, JsonObject Header, JsonObject Message)
{
    /// <summary>The JSON body EDDN takes.</summary>
    public string ToJson() => new JsonObject
    {
        ["$schemaRef"] = SchemaRef,
        ["header"] = Header.DeepClone(),
        ["message"] = Message.DeepClone(),
    }.ToJsonString();
}
