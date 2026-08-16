using D47.Core.Listening;

namespace D47.Core.Tests.Listening;

/// <summary>
/// A model store holding exactly the models it was named, and installing nothing.
/// <para>
/// Shared rather than per-file: it started private to <c>AwaitingDownloadTests</c> and the second
/// caller is what makes it a double rather than a fixture.
/// </para>
/// </summary>
public sealed class FakeModelStore(params string[] installed) : IModelStore
{
    public string Directory => "nowhere";

    public bool IsInstalled(WhisperModel model) => installed.Contains(model.Id);

    public string? PathOf(WhisperModel model) => IsInstalled(model) ? "nowhere" : null;

    public IReadOnlyList<string> Installed() => installed;

    public Task<ModelOffer?> DescribeAsync(WhisperModel model, CancellationToken cancellationToken = default) =>
        Task.FromResult<ModelOffer?>(null);

    public bool Remove(WhisperModel model) => false;

    public Task<ModelInstallResult> InstallAsync(
        WhisperModel model,
        IProgress<ModelProgress>? progress = null,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new ModelInstallResult(ModelInstall.Failed, "nothing is fetched in a test"));
}
