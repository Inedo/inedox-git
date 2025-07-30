using Inedo.Diagnostics;
using LibGit2Sharp;

#nullable enable

namespace Inedo.Extensions.Git;

internal sealed class LibGitSharpRepoManRepository : IRepoManRepository<LibGitSharpRepoManRepository>
{
    private readonly Repository repository;
    private bool disposed;

    private LibGitSharpRepoManRepository(Repository repository)
    {
        this.repository = repository;
    }

    public static Task<LibGitSharpRepoManRepository> CloneAsync(string repoPath, RepoManConfig config, CancellationToken cancellationToken)
    {
        var cloneOptions = new CloneOptions { IsBare = true };
        cloneOptions.FetchOptions.CredentialsProvider = (a, b, c) => GetCredentials(config);
        cloneOptions.FetchOptions.OnTransferProgress = config.TransferProgress is not null ? handleTransferProgress : null;
        cloneOptions.FetchOptions.OnProgress = progressHandler;
        cloneOptions.FetchOptions.CertificateCheck = (_, valid, _) => config.IgnoreCertificateCheck || valid;

        Directory.CreateDirectory(repoPath);

        Repository.Clone(
            config.RepositoryUri.ToString(),
            repoPath,
            cloneOptions
        );

        return Task.FromResult(new LibGitSharpRepoManRepository(new Repository(repoPath)));

        bool progressHandler(string serverProgressOutput)
        {
            if (cancellationToken.IsCancellationRequested)
                return false;

            config.Log?.LogDebug(serverProgressOutput);
            return true;
        }

        bool handleTransferProgress(TransferProgress p)
        {
            config.TransferProgress(new RepoTransferProgress(p.TotalObjects, p.ReceivedObjects, p.ReceivedBytes));
            return true;
        }
    }

    private static LibGit2Sharp.Credentials GetCredentials(RepoManConfig config)
    {
        return string.IsNullOrEmpty(config.UserName)
            ? new DefaultCredentials()
            : new UsernamePasswordCredentials { Username = config.UserName, Password = config.Password };
    }

    public static bool IsValid(string path) => Repository.IsValid(path);

    public static LibGitSharpRepoManRepository Open(string path) => new(new Repository(path));

    public Task FetchAsync(RepoManConfig config, CancellationToken cancellationToken)
    {
        Commands.Fetch(
            this.repository,
            "origin",
            [],
            new FetchOptions
            {
                CredentialsProvider = (a, b, c) => GetCredentials(config),
                TagFetchMode = TagFetchMode.All,
                OnTransferProgress = config.TransferProgress is not null ? handleTransferProgress : null,
                OnProgress = progressHandler,
                CertificateCheck = (_, valid, _) => config.IgnoreCertificateCheck || valid
            },
            null
        );

        return Task.CompletedTask;

        bool progressHandler(string serverProgressOutput)
        {
            if (cancellationToken.IsCancellationRequested)
                return false;

            config.Log?.LogDebug(serverProgressOutput);
            return true;
        }

        bool handleTransferProgress(TransferProgress p)
        {
            config.TransferProgress(new RepoTransferProgress(p.TotalObjects, p.ReceivedObjects, p.ReceivedBytes));
            return true;
        }
    }

    public Task TagAsync(string commitSha, string tag, bool force, RepoManConfig config, CancellationToken cancellationToken)
    {
        var t = this.repository.Tags[tag];
        if (t != null)
        {
            if (t.Target.Sha.Equals(commitSha, StringComparison.OrdinalIgnoreCase))
            {
                config.Log?.LogDebug($"Tag {tag} already exists for {commitSha}.");
                return Task.CompletedTask;
            }
            else
            {
                if (force)
                {
                    config.Log?.LogWarning($"Tag {tag} already exists but refers to {t.Target.Sha}. Attempt to retag to {commitSha}...");
                }
                else
                {
                    config.Log?.LogError($"Tag {tag} already exists but refers to {t.Target.Sha}.");
                    return Task.CompletedTask;
                }
            }
        }

        config.Log?.LogDebug("Creating tag...");
        var createdTag = this.repository.Tags.Add(tag, commitSha, force);
        var pushRef = $"{createdTag.CanonicalName}:{createdTag.CanonicalName}";
        if (force)
            pushRef = $"+{pushRef}";

        config.Log?.LogDebug("Pushing tag...");
        this.repository.Network.Push(
            this.repository.Network.Remotes["origin"],
            force ? '+' + pushRef : pushRef,
            new PushOptions { CredentialsProvider = (a, b, c) => GetCredentials(config) }
        );

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        if (!this.disposed)
        {
            this.repository.Dispose();
            this.disposed = true;
        }
    }

    public (IRepoManTree tree, string commitSha) GetTree(string objectish)
    {
        var commit = this.repository.Lookup<Commit>($"refs/remotes/origin/{objectish}")
            ?? this.repository.Lookup<Commit>(objectish)
            ?? throw new ArgumentException($"Could not find commit for {objectish}.");

        return (new RepoManTree(commit.Tree, this.repository), commit.Sha);
    }

    public string? GetOriginUrl() => this.repository.Network.Remotes["origin"]?.Url;

    private sealed class RepoManTree(Tree tree, Repository repo) : IRepoManTree
    {
        private readonly Tree tree = tree;
        private readonly Repository repo = repo;

        public IEnumerable<IRepoManTreeEntry> Entries => tree.Select(e => new RepoManTreeEntry(e, repo));
    }

    private sealed class RepoManTreeEntry(TreeEntry entry, Repository repo) : IRepoManTreeEntry
    {
        private readonly TreeEntry entry = entry;
        private readonly Repository repo = repo;

        public string Path => this.entry.Path;
        public string Name => this.entry.Name;
        public RepoManFileMode Mode => (RepoManFileMode)this.entry.Mode;
        public string TargetId => this.entry.Target.Sha;

        public DateTime GetModifiedTimestamp()
        {
            var commit = this.repo.Head.Commits.First(c =>
            {
                var cId = c.Tree[this.entry.Path]?.Target?.Sha;
                var pId = c.Parents?.FirstOrDefault()?[this.entry.Path]?.Target?.Sha;
                return cId != pId;
            });

            return commit.Author.When.UtcDateTime;
        }
        public IRepoManTree GetTargetTree() => new RepoManTree(this.entry.Target.Peel<Tree>(), this.repo);
        public Stream GetContentStream()
        {
            var blob = this.entry.Target.Peel<Blob>();
            return blob.GetContentStream();
        }
        public string GetContentText()
        {
            var blob = this.entry.Target.Peel<Blob>();
            return blob.GetContentText();
        }
    }
}
