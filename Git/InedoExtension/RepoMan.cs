using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using Inedo.Diagnostics;
using Inedo.IO;

namespace Inedo.Extensions.Git;

#nullable enable

internal sealed partial class RepoMan : IDisposable
{
    private static readonly Dictionary<string, RepoLock> repoLocks = [];
    private readonly IRepoManRepository repo;
    private readonly RepoManConfig config;
    private bool disposed;

    private RepoMan(IRepoManRepository repo, RepoManConfig config)
    {
        this.repo = repo;
        this.config = config;
    }

    public static Task<RepoMan> FetchOrCloneAsync(RepoManConfig config, CancellationToken cancellationToken = default)
    {
        _ = bool.TryParse(SDK.GetConfigValue("Web.UseNewGitBackend"), out bool useLilGit);
        if (useLilGit)
        {
            config.Log?.LogDebug("Git backend: lilgit");
            return FetchOrCloneInternalAsync<LilGitRepoManRepository>(config, cancellationToken);
        }
        else
        {
            config.Log?.LogDebug("Git backend: libgit2sharp");
            return FetchOrCloneInternalAsync<LibGitSharpRepoManRepository>(config, cancellationToken);
        }
    }

    private static async Task<RepoMan> FetchOrCloneInternalAsync<TRepo>(RepoManConfig config, CancellationToken cancellationToken = default)
        where TRepo : class, IRepoManRepository<TRepo>
    {
        ArgumentNullException.ThrowIfNull(config);
        if (!Path.IsPathRooted(config.RootPath))
            throw new ArgumentException("Root path must be rooted.");

        TRepo? repo = null;
        await AcquireLockAsync(config, cancellationToken).ConfigureAwait(false);
        try
        {
            var repoPath = Path.Combine(config.RootPath, GetRepositoryDiskName(config.RepositoryUri));
            config.Log?.LogDebug($"Repository path is {repoPath}");

            try
            {
                if (Directory.Exists(repoPath) && !TRepo.IsValid(repoPath))
                    DirectoryEx.Delete(repoPath);
            }
            catch
            {
                DirectoryEx.Delete(repoPath);
            }

            if (Directory.Exists(repoPath) && TRepo.IsValid(repoPath))
            {
                repo = TRepo.Open(repoPath);
                var origin = repo.GetOriginUrl() ?? throw new InvalidOperationException("Repository has no origin.");

                config.Log?.LogDebug($"Fetching from origin ({origin})...");
                var sw = Stopwatch.StartNew();
                await repo.FetchAsync(config, cancellationToken);
                sw.Stop();
                config.Log?.LogDebug($"Fetch completed in {sw.Elapsed}.");
            }
            else
            {
                config.Log?.LogDebug($"Repository does not exist or is not valid. Cloning from {config.RepositoryUri}...");

                Directory.CreateDirectory(repoPath);
                var sw = Stopwatch.StartNew();
                repo = await TRepo.CloneAsync(repoPath, config, cancellationToken);
                sw.Stop();
                config.Log?.LogDebug($"Clone completed in {sw.Elapsed}.");
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

    public async Task<string> ExportAsync(RepoExportOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        this.config.Log?.LogDebug($"Checking out code from {options.Objectish} to {options.OutputDirectory}...");
        var (tree, commitSha) = this.repo.GetTree(options.Objectish);
        this.config.Log?.LogDebug($"Lookup succeeded; found commit {commitSha}.");

        //var tree = commit.Tree;
        IReadOnlyDictionary<string, RepoMan>? submodules = null;
        try
        {
            if (options.RecurseSubmodules)
                submodules = await this.UpdateSubmodulesAsync(tree, cancellationToken).ConfigureAwait(false);

            await exportTree(tree, options.OutputDirectory, string.Empty).ConfigureAwait(false);

            if (options.WriteMinimalGitData)
            {
                this.config.Log?.LogDebug("Writing minimal git repo data...");
                this.WriteMinimalGitData(options.OutputDirectory, commitSha);
            }

            async Task exportTree(IRepoManTree tree, string outdir, string repopath)
            {
                Directory.CreateDirectory(outdir);

                foreach (var entry in tree.Entries)
                {
                    if (entry.Mode == RepoManFileMode.Directory)
                    {
                        await exportTree(entry.GetTargetTree(), Path.Combine(outdir, entry.Name), (repopath + "/" + entry.Name).Trim('/')).ConfigureAwait(false);
                    }
                    else if (entry.Mode == RepoManFileMode.GitLink)
                    {
                        if (submodules == null)
                        {
                            // do nothing; submodules are ignored
                        }
                        else if (submodules.TryGetValue((repopath + "/" + entry.Name).Trim('/'), out var subrepo))
                        {
                            await subrepo.ExportAsync(
                                options with
                                {
                                    OutputDirectory = Path.Combine(outdir, entry.Name),
                                    Objectish = entry.TargetId,
                                    RecurseSubmodules = true
                                },
                                cancellationToken
                            ).ConfigureAwait(false);
                        }
                        else
                        {
                            this.config.Log?.LogWarning($"Could not resolve GitLink \"{repopath + "/" + entry.Name}\" for submodule.");
                        }
                    }
                    else
                    {
                        if (options.CreateSymbolicLinks && entry.Mode == RepoManFileMode.SymbolicLink)
                        {
                            var linkTarget = entry.GetContentText();
                            File.CreateSymbolicLink(Path.Combine(outdir, entry.Name), linkTarget);
                        }
                        else
                        {
                            using var stream = entry.GetContentStream();
                            using var output =  CreateFile(Path.Combine(outdir, entry.Name), entry.Mode);
                            stream.CopyTo(output);
                        }
                        
                        if (options.SetLastModified)
                            FileEx.SetLastWriteTime(Path.Combine(outdir, entry.Name), entry.GetModifiedTimestamp());
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

        return commitSha;
    }

    public Task TagAsync(string commitSha, string tag, bool force, CancellationToken cancellationToken = default)
    {
        return this.repo.TagAsync(commitSha, tag, force, this.config, cancellationToken);
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

    private void WriteMinimalGitData(string rootPath, string commitSha)
    {
        var gitPath = Path.Combine(rootPath, ".git");
        Directory.CreateDirectory(gitPath);
        File.WriteAllText(Path.Combine(gitPath, "HEAD"), commitSha);

        using var writer = new StreamWriter(File.Create(Path.Combine(gitPath, "config")), InedoLib.UTF8Encoding) { NewLine = "\n" };
        writer.WriteLine("[remote \"origin\"]");
        writer.WriteLine($"\turl = {this.config.RepositoryUri}");
    }

    private async Task<IReadOnlyDictionary<string, RepoMan>?> UpdateSubmodulesAsync(IRepoManTree tree, CancellationToken cancellationToken = default)
    {
        this.config.Log?.LogDebug("Looking for submodules...");

        if (tree[".gitmodules"] is not IRepoManTreeEntry entry || entry.Mode == RepoManFileMode.Directory)
        {
            this.config.Log?.LogDebug("No submodules in repository.");
            return null;
        }

        var dict = new Dictionary<string, RepoMan>();
        try
        {
            using var reader = new StreamReader(entry.GetContentStream(), Encoding.UTF8);
            foreach (var s in GetSubmodules(reader))
            {
                this.config.Log?.LogDebug($"Found {s.Name} submodule (path={s.Path}, url={s.Url})");
                var rubbish = new Uri(this.config.RepositoryUri.ToString().TrimEnd('/') + "/");
                var submoduleUri = new Uri(rubbish, s.Url);
                var subrepo = await FetchOrCloneAsync(config with { RepositoryUri = submoduleUri, Submodule = s }, cancellationToken).ConfigureAwait(false);
                dict.Add(s.Path, subrepo);
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

    private static FileStream CreateFile(string fileName, RepoManFileMode mode)
    {
        var stream = File.Create(fileName, 8192, FileOptions.SequentialScan);

        if (OperatingSystem.IsLinux() && mode == RepoManFileMode.ExecutableFile)
            File.SetUnixFileMode(stream.SafeFileHandle, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

        return stream;
    }

    private static string GetRepositoryDiskName(Uri uri)
    {
        return Regex.Replace(uri.Authority + uri.PathAndQuery, $"[{Regex.Escape(new string(Path.GetInvalidFileNameChars()))}]", "$");
    }

    private static IEnumerable<SubmoduleInfo> GetSubmodules(TextReader reader)
    {
        string? line;
        string? name = null;
        string? path = null;
        string? url = null;
        while ((line = reader.ReadLine()) != null)
        {
            var m = SubmoduleSectionRegex().Match(line);
            if (m.Success)
            {
                if (name != null && path != null && url != null)
                    yield return new SubmoduleInfo(name, path, url);

                name = m.Groups[1].Value;
                continue;
            }

            if (name != null)
            {
                m = ConfigValueRegex().Match(line);
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
            yield return new SubmoduleInfo(name, path, url);
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

    [GeneratedRegex("""^\s*\[\s*submodule\s+"(?<1>[^"]+)"\s*\]\s*$""", RegexOptions.ExplicitCapture)]
    private static partial Regex SubmoduleSectionRegex();
    [GeneratedRegex(@"^\s*(?<1>[a-z]+)\s*=\s*(?<2>.+)$", RegexOptions.ExplicitCapture)]
    private static partial Regex ConfigValueRegex();
}
