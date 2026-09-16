using D47.Core;

namespace D47.Donations.Store;

/// <summary>
/// Everything this utility writes, under <c>%LOCALAPPDATA%\d47-donations\</c> and never inside the
/// repository or d47's own <c>data\</c>.
/// </summary>
public sealed class UtilityPaths
{
    public const string FolderName = "d47-donations";

    public UtilityPaths(string root)
    {
        Root = Path.GetFullPath(root);
        Downloads = Path.Combine(Root, "downloads");
        Secrets = new AppPaths(Root);
        StateFile = Path.Combine(Secrets.Data, "state.json");
    }

    public static UtilityPaths ForCurrentUser() =>
        new(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            FolderName));

    public string Root { get; }

    /// <summary>The one folder downloads go to.</summary>
    public string Downloads { get; }

    /// <summary>When the utility was last opened, and which donation each local zip came from.</summary>
    public string StateFile { get; }

    /// <summary>
    /// Rooted here rather than on d47's install, so <see cref="Core.Configuration.SecretStore"/> keeps
    /// the R2 credential apart from the Commander's keys.
    /// </summary>
    public AppPaths Secrets { get; }

    public void EnsureCreated()
    {
        Directory.CreateDirectory(Downloads);
        Directory.CreateDirectory(Secrets.Data);
    }
}
