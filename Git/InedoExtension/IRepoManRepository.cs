#nullable enable

namespace Inedo.Extensions.Git;

internal interface IRepoManRepository : IDisposable
{
    static abstract bool IsValid(string path);

    string? GetOriginUrl();
    (IRepoManTree tree, string commitSha) GetTree(string objectish);
    Task TagAsync(string commitSha, string tag, bool force, RepoManConfig config, CancellationToken cancellationToken);
    Task FetchAsync(RepoManConfig config, CancellationToken cancellationToken);
}

internal interface IRepoManRepository<TSelf> : IRepoManRepository
    where TSelf : IRepoManRepository<TSelf>
{
    static abstract TSelf Open(string path);

    static abstract Task<TSelf> CloneAsync(string repoPath, RepoManConfig config, CancellationToken cancellationToken);
}

internal interface IRepoManTree
{
    IRepoManTreeEntry? this[string name] => this.Entries.FirstOrDefault(e => e.Name == name);

    IEnumerable<IRepoManTreeEntry> Entries { get; }
}

internal interface IRepoManTreeEntry
{
    string Path { get; }
    string Name { get; }
    RepoManFileMode Mode { get; }
    string TargetId { get; }

    DateTime GetModifiedTimestamp();
    IRepoManTree GetTargetTree();
    Stream GetContentStream();
    string GetContentText();
}

internal enum RepoManFileMode : uint
{
    Unknown = 0,
    Directory = 0x4000,
    File = 0x81A4,
    GroupWritableFile = 0x81B4,
    ExecutableFile = 0x81ED,
    SymbolicLink = 0xA000,
    GitLink = 0xE000
}
