using System.Collections;
using System.Reflection;
using D47.Llm;

namespace D47.Llm.Tests;

/// <summary>Empties the refusals and context sizes the providers share across a process, so a test starts from none.</summary>
internal static class EndpointDemotionsReset
{
    public static void Run()
    {
        foreach (var name in new[] { "Refused", "Contexts" })
        {
            var field = typeof(EndpointDemotions).GetField(name, BindingFlags.NonPublic | BindingFlags.Static)
                ?? throw new InvalidOperationException($"EndpointDemotions has no {name} table to empty.");

            ((IDictionary)field.GetValue(null)!).Clear();
        }
    }
}
