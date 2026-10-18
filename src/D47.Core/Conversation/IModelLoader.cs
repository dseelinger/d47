namespace D47.Core.Conversation;

/// <summary>What the server reports about one model's loaded context. Tokens is null when the model is not loaded.</summary>
public sealed record LoadedContext(int? Tokens, int Maximum, int Instances);

/// <summary>What a reload did; Detail is the server's reason when it failed.</summary>
public sealed record ContextReload(bool Loaded, string? Detail);

/// <summary>A provider whose server can report and change the context a model is loaded with.</summary>
public interface IModelLoader
{
    /// <summary>The model's loaded context, or null when the server does not report one.</summary>
    Task<LoadedContext?> LoadedContextAsync(string model, CancellationToken cancellationToken);

    /// <summary>Loads the model with <paramref name="tokens"/> of context in place of its loaded instance.</summary>
    Task<ContextReload> ReloadAsync(string model, int tokens, CancellationToken cancellationToken);
}
