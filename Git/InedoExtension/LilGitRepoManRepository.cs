using System.Text;
using Inedo.IO;
using LilGit;

#nullable enable

namespace Inedo.Extensions.Git;

internal sealed class LilGitRepoManRepository : IRepoManRepository<LilGitRepoManRepository>
{
    private readonly GitRepository repo;
    private bool disposed;

    private LilGitRepoManRepository(GitRepository repo) => this.repo = repo;

    public static async Task<LilGitRepoManRepository> CloneAsync(string repoPath, RepoManConfig config, CancellationToken cancellationToken)
    {
        await GitRepository.CloneAsync(new GitCloneOptions(GetConnectionInfo(config), repoPath, GetIndexProgressDelegate(config)), cancellationToken: cancellationToken);
        return Open(repoPath);
    }

    public static bool IsValid(string path) => GitRepository.IsRepository(path);

    public static LilGitRepoManRepository Open(string path) => new(new GitRepository(path));

    public void Dispose()
    {
        if (!this.disposed)
        {
            this.repo.Dispose();
            this.disposed = true;
        }
    }

    public Task FetchAsync(RepoManConfig config, CancellationToken cancellationToken)
    {
        return this.repo.FetchAsync(new GitFetchOptions(GetConnectionInfo(config), GetIndexProgressDelegate(config)), cancellationToken);
    }

    public string? GetOriginUrl() => this.repo.GetRemoteUrl();

    public (IRepoManTree tree, string commitSha) GetTree(string objectish)
    {
        if (!GitSha1ObjectId.TryParse(objectish, out var id))
        {
            if (!this.repo.Heads.TryGetValue(objectish, out id))
                throw new ArgumentException($"Could not resolve branch or commit: {objectish}");
        }

        if (!this.repo.TryGetObject<GitCommit>(id, out var commit))
            throw new InvalidOperationException($"Could not resolve commit: {id}");

        if (!this.repo.TryGetObject<GitTree>(commit.TreeId, out var tree))
            throw new InvalidOperationException($"Could not resolve tree: {commit.TreeId}");

        return (new RepoManTree(tree, this.repo, string.Empty, commit), commit.Id.ToString());
    }

    public Task TagAsync(string commitSha, string tag, bool force, RepoManConfig config, CancellationToken cancellationToken)
    {
        this.repo.EnsureTag(tag, GitSha1ObjectId.Parse(commitSha));
        return this.repo.EnsureRemoteTagAsync(GetConnectionInfo(config), tag, force, cancellationToken);
    }

    private static GitRemoteConnectionInfo GetConnectionInfo(RepoManConfig config)
    {
        return new GitRemoteConnectionInfo(
            config.RepositoryUri.ToString(),
            config.UserName,
            config.Password,
            () => SDK.CreateHttpClient(new HttpClientCreationOptions { IgnoreServerCertificateErrors = config.IgnoreCertificateCheck })
        );
    }

    private static IndexProgress? GetIndexProgressDelegate(RepoManConfig config)
    {
        if (config.TransferProgress is null)
            return null;

        var p = config.TransferProgress;
        return (current, total, size) => p(new RepoTransferProgress(total, current, size));
    }

    private sealed class RepoManTree(GitTree tree, GitRepository repo, string path, GitCommit commit) : IRepoManTree
    {
        private readonly GitTree tree = tree;
        private readonly GitRepository repo = repo;
        private readonly string path = path;
        private readonly GitCommit commit = commit;

        public IEnumerable<IRepoManTreeEntry> Entries => this.tree.Entries.Select(e => new RepoManTreeEntry(e, this.repo, this.path, this.commit));
    }

    private sealed class RepoManTreeEntry(GitTreeEntry entry, GitRepository repo, string rootPath, GitCommit commit) : IRepoManTreeEntry
    {
        private readonly GitTreeEntry entry = entry;
        private readonly GitRepository repo = repo;
        private readonly string rootPath = rootPath;
        private readonly GitCommit commit = commit;

        public string Path => string.IsNullOrEmpty(this.rootPath) ? this.Name : $"{this.rootPath}/{this.Name}";
        public string Name => this.entry.Name;
        public RepoManFileMode Mode => (RepoManFileMode)this.entry.FileMode;
        public string TargetId => this.entry.Target.ToString();

        public Stream GetContentStream()
        {
            var blob = this.repo.GetObject<GitBlob>(this.entry.Target);
            return new ReadOnlyMemoryStream(blob.Content);
        }
        public string GetContentText()
        {
            var blob = this.repo.GetObject<GitBlob>(this.entry.Target);
            return Encoding.UTF8.GetString(blob.Content.Span);
        }

        public DateTime GetModifiedTimestamp()
        {
            var lastCommit = this.commit;
            var path = this.Path;
            foreach (var commit in this.repo.GetCommitHistory(this.commit))
            {
                if (!hasObjectAtPath(commit, path, this.entry.Target))
                    break;

                lastCommit = commit;
            }

            return lastCommit.Author!.Timestamp.UtcDateTime;

            bool hasObjectAtPath(GitCommit commit, string path, in GitSha1ObjectId expectedId)
            {
                var tree = this.repo.GetObject<GitTree>(commit.TreeId);

                var parts = path.Split('/');
                for (int i = 0; i < parts.Length; i++)
                {
                    bool found = false;
                    foreach (var e in tree.Entries)
                    {
                        if (e.Name == parts[i])
                        {
                            if (i < parts.Length - 1)
                            {
                                if (e.FileMode != GitFileMode.Directory)
                                    return false;

                                tree = this.repo.GetObject<GitTree>(e.Target);
                                found = true;
                                break;
                            }
                            else
                            {
                                return e.Target == expectedId;
                            }
                        }
                    }

                    if (!found)
                        break;
                }

                return false;
            }
        }

        public IRepoManTree GetTargetTree()
        {
            if (!this.repo.TryGetObject<GitTree>(this.entry.Target, out var tree))
                throw new InvalidOperationException($"Tree {this.entry.Target} not found.");

            return new RepoManTree(tree, this.repo, this.Path, this.commit);
        }
    }
}
