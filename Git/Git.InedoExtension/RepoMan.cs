using System.Collections.Immutable;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using Inedo.Diagnostics;
using Inedo.IO;
using LibGit2Sharp;

namespace Inedo.Extensions.Git;

#nullable enable

internal sealed class RepoMan : IDisposable
{
    private static readonly Dictionary<string, RepoLock> repoLocks = new();
    private readonly Repository repo;
    private readonly RepoManConfig config;
    private bool disposed;

    private RepoMan(Repository repo, RepoManConfig config)
    {
        this.repo = repo;
        this.config = config;
    }

    public static async Task<RepoMan> FetchOrCloneAsync(RepoManConfig config, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(config);
        if (!Path.IsPathRooted(config.RootPath))
            throw new ArgumentException("Root path must be rooted.");

        Repository? repo = null;
        await AcquireLockAsync(config, cancellationToken).ConfigureAwait(false);
        try
        {
            var repoPath = Path.Combine(config.RootPath, GetRepositoryDiskName(config.RepositoryUri));
            config.Log?.LogDebug($"Repository path is {repoPath}");

            if (Directory.Exists(repoPath) && Repository.IsValid(repoPath))
            {
                repo = new Repository(repoPath);
                var origin = repo.Network.Remotes["origin"];
                if (origin == null)
                    throw new InvalidOperationException("Repository has no origin.");

                config.Log?.LogDebug($"Fetching from origin ({origin.Url})...");
                var sw = Stopwatch.StartNew();

                Commands.Fetch(
                    repo,
                    "origin",
                    Enumerable.Empty<string>(),
                    new FetchOptions
                    {
                        CredentialsProvider = config.GetCredentials,
                        TagFetchMode = TagFetchMode.All,
                        OnTransferProgress = config.TransferProgressHandler,
                        OnProgress = progressHandler
                    },
                    null
                );

                sw.Stop();
                config.Log?.LogDebug($"Fetch completed in {sw.Elapsed}.");
            }
            else
            {
                config.Log?.LogDebug($"Repository does not exist or is not valid. Cloning from {config.RepositoryUri}...");

                Directory.CreateDirectory(repoPath);
                var sw = Stopwatch.StartNew();

                Repository.Clone(
                    config.RepositoryUri.ToString(),
                    repoPath,
                    new CloneOptions
                    {
                        IsBare = true,
                        CredentialsProvider = config.GetCredentials,
                        OnTransferProgress = config.TransferProgressHandler,
                        OnProgress = progressHandler
                    }
                );

                sw.Stop();
                config.Log?.LogDebug($"Clone completed in {sw.Elapsed}.");

                repo = new Repository(repoPath);
            }

            bool progressHandler(string serverProgressOutput)
            {
                if (cancellationToken.IsCancellationRequested)
                    return false;

                config.Log?.LogDebug(serverProgressOutput);
                return true;
            }

            return new RepoMan(repo, config);
        }
        catch
        {
            ReleaseLock(config);
            repo?.Dispose();
            throw;
        }
    }

    public string GetCommitHash(string objectish)
    {
        var commit = this.repo.Lookup<Commit>(objectish) ?? this.repo.Lookup<Commit>("refs/remotes/origin/" + objectish);
        if (commit == null)
            throw new ArgumentException($"Could not find commit for {objectish}.");

        return commit.Sha;
    }

    public async Task ExportAsync(RepoExportOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        this.config.Log?.LogDebug($"Checking out code from {options.Objectish} to {options.OutputDirectory}...");

        var commit = this.repo.Lookup<Commit>($"refs/remotes/origin/{options.Objectish}") ?? this.repo.Lookup<Commit>(options.Objectish);
        if (commit == null)
            throw new ArgumentException($"Could not find commit for {options.Objectish}.");

        this.config.Log?.LogDebug($"Lookup succeeded; found commit {commit.Sha}.");

        var tree = commit.Tree;
        IReadOnlyDictionary<string, RepoMan>? submodules = null;
        try
        {
            if (options.RecurseSubmodules)
                submodules = await this.UpdateSubmodulesAsync(tree, cancellationToken).ConfigureAwait(false);

            int warnCount = 0;
            await exportTree(tree, options.OutputDirectory, string.Empty).ConfigureAwait(false);

            async Task exportTree(Tree tree, string outdir, string repopath)
            {
                Directory.CreateDirectory(outdir);

                foreach (var entry in tree)
                {
                    if (entry.TargetType == TreeEntryTargetType.Blob)
                    {
                        var blob = entry.Target.Peel<Blob>();

                        if (options.CreateSymbolicLinks && entry.Mode == Mode.SymbolicLink)
                        {
                            var linkTarget = blob.GetContentText().Trim();
                            File.CreateSymbolicLink(Path.Combine(outdir, entry.Path), linkTarget);
                        }
                        else
                        {
                            using var stream = blob.GetContentStream();
                            using var output =  CreateFile(Path.Combine(outdir, entry.Path), entry.Mode);
                            stream.CopyTo(output);
                        }
                        
                        if (options.SetLastModified)
                        {
                            var commit = this.repo.Head.Commits.First(c =>
                            {
                                var cId = c.Tree[entry.Path]?.Target?.Sha;
                                var pId = c.Parents?.FirstOrDefault()?[entry.Path]?.Target?.Sha;
                                return cId != pId;
                            });

                            if (commit != null)
                                FileEx.SetLastWriteTime(Path.Combine(outdir, entry.Path), commit.Author.When.UtcDateTime);
                            else if (warnCount++ < 5)
                                this.config.Log?.LogWarning($"Could not find LastModified for {entry.Path}.");
                        }
                    }
                    else if (entry.TargetType == TreeEntryTargetType.Tree)
                    {
                        await exportTree(entry.Target.Peel<Tree>(), Path.Combine(outdir, entry.Name), (repopath + "/" + entry.Name).Trim('/')).ConfigureAwait(false);
                    }
                    else if (entry.TargetType == TreeEntryTargetType.GitLink)
                    {
                        if (submodules == null)
                        {
                            // do nothing; submodules are ignored
                        }
                        else if (submodules.TryGetValue((repopath + "/" + entry.Name).Trim('/'), out var subrepo))
                        {
                            var hash = entry.Target.Id.Sha;

                            await subrepo.ExportAsync(
                                options with
                                {
                                    OutputDirectory = Path.Combine(outdir, entry.Name),
                                    Objectish = hash,
                                    RecurseSubmodules = true
                                },
                                cancellationToken
                            ).ConfigureAwait(false);
                        }
                        else
                        {
                            // error message for when submodule was not found
                            throw new Exception();
                        }
                    }
                }
            }
        }
        finally
        {
            if (submodules != null)
            {
                foreach (var r in submodules.Values)
                    r.Dispose();
            }
        }
    }

    public void Tag(string commitSha, string tag, bool force)
    {
        var t = this.repo.Tags[tag];
        if (t != null)
        {
            if (t.Target.Sha.Equals(commitSha, StringComparison.OrdinalIgnoreCase))
            {
                this.config.Log?.LogDebug($"Tag {tag} already exists for {commitSha}.");
                return;
            }
            else
            {
                if (force)
                {
                    this.config.Log?.LogWarning($"Tag {tag} already exists but refers to {t.Target.Sha}. Attempt to retag to {commitSha}...");
                }
                else
                {
                    this.config.Log?.LogError($"Tag {tag} already exists but refers to {t.Target.Sha}.");
                    return;
                }
            }
        }

        this.config.Log?.LogDebug("Creating tag...");
        var createdTag = this.repo.Tags.Add(tag, commitSha, force);
        var pushRef = $"{createdTag.CanonicalName}:{createdTag.CanonicalName}";
        if (force)
            pushRef = $"+{pushRef}";

        this.config.Log?.LogDebug("Pushing tag...");
        this.repo.Network.Push(
            this.repo.Network.Remotes["origin"],
            force ? '+' + pushRef : pushRef,
            new PushOptions { CredentialsProvider = this.config.GetCredentials }
        );
    }

    public void Dispose()
    {
        if (!this.disposed)
        {
            this.repo.Dispose();
            ReleaseLock(this.config);
            this.disposed = true;
        }
    }

    private async Task<IReadOnlyDictionary<string, RepoMan>?> UpdateSubmodulesAsync(Tree tree, CancellationToken cancellationToken = default)
    {
        this.config.Log?.LogDebug("Looking for submodules...");

        if (tree[".gitmodules"] is not TreeEntry entry || entry.TargetType != TreeEntryTargetType.Blob)
        {
            this.config.Log?.LogDebug("No submodules in repository.");
            return ImmutableDictionary.Create<string, RepoMan>();
        }

        var dict = new Dictionary<string, RepoMan>();
        try
        {
            var blob = entry.Target.Peel<Blob>();
            using var reader = new StreamReader(blob.GetContentStream(), Encoding.UTF8);
            foreach (var (name, path, url) in GetSubmodules(reader))
            {
                this.config.Log?.LogDebug($"Found {name} submodule (path={path}, url={url})");
                var rubbish = new Uri(this.config.RepositoryUri.ToString().TrimEnd('/') + "/");
                var submoduleUri = new Uri(rubbish, url);
                var subrepo = await FetchOrCloneAsync(config with { RepositoryUri = submoduleUri }, cancellationToken).ConfigureAwait(false);
                dict.Add(path, subrepo);
            }

            return dict;
        }
        catch
        {
            foreach (var r in dict.Values)
                r.Dispose();

            throw;
        }
    }

    private static Stream CreateFile(string fileName, Mode mode)
    {
        if (OperatingSystem.IsLinux() && mode == Mode.ExecutableFile)
        {
            var info = new Mono.Unix.UnixFileInfo(fileName);
            return info.Create(Mono.Unix.FileAccessPermissions.UserReadWriteExecute);
        }
        else
        {
            return File.Create(fileName, 8192, FileOptions.SequentialScan);
        }
    }

    private static string GetRepositoryDiskName(Uri uri)
    {
        return Regex.Replace(uri.Authority + uri.PathAndQuery, $"[{Regex.Escape(new string(Path.GetInvalidFileNameChars()))}]", "$");
    }

    private static IEnumerable<(string name, string path, string url)> GetSubmodules(TextReader reader)
    {
        var submoduleSectionRegex = new Regex(@"^\s*\[\s*submodule\s+""(?<1>[^""]+)""\s*\]\s*$", RegexOptions.ExplicitCapture);
        var configValueRegex = new Regex(@"^\s*(?<1>[a-z]+)\s*=\s*(?<2>.+)$", RegexOptions.ExplicitCapture);
        string? line;
        string? name = null;
        string? path = null;
        string? url = null;
        while ((line = reader.ReadLine()) != null)
        {
            var m = submoduleSectionRegex.Match(line);
            if (m.Success)
            {
                if (name != null && path != null && url != null)
                    yield return (name, path, url);

                name = m.Groups[1].Value;
                continue;
            }

            if (name != null)
            {
                m = configValueRegex.Match(line);
                if (m.Success)
                {
                    if (m.Groups[1].ValueSpan.Equals("path", StringComparison.Ordinal))
                        path = m.Groups[2].Value;
                    else if (m.Groups[1].ValueSpan.Equals("url", StringComparison.Ordinal))
                        url = m.Groups[2].Value;
                }
            }
        }

        if (name != null && path != null && url != null)
            yield return (name, path, url);
    }

    private static async Task AcquireLockAsync(RepoManConfig config, CancellationToken cancellationToken = default)
    {
        var key = GetRepositoryDiskName(config.RepositoryUri);

        SemaphoreSlim semaphore;
        RepoLock? l;

        lock (repoLocks)
        {
            if (!repoLocks.TryGetValue(key, out l))
            {
                l = new RepoLock();
                repoLocks[key] = l;
            }

            l.Count++;
            semaphore = l.Semaphore;

            if (l.Count > 1)
                config.Log?.LogDebug($"Lock is taken for {config.RepositoryUri}; waiting...");
        }

        try
        {
            await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            lock (repoLocks)
            {
                l.Count--;
            }

            throw;
        }
    }

    private static void ReleaseLock(RepoManConfig config)
    {
        var key = GetRepositoryDiskName(config.RepositoryUri);
        lock (repoLocks)
        {
            if (repoLocks.TryGetValue(key, out var l))
            {
                l.Count--;
                l.Semaphore.Release();
                if (l.Count <= 0)
                {
                    l.Semaphore.Dispose();
                    repoLocks.Remove(key);
                }
            }
        }
    }

    private sealed class RepoLock
    {
        public SemaphoreSlim Semaphore { get; } = new(1, 1);
        public int Count { get; set; }
    }
}
