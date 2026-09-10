using Xunit;

// SteamVrRuntime allows one session per process and holds the claim in a static, so two of these
// tests running at once would fight over it.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
