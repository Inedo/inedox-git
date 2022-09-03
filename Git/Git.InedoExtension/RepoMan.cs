using System.Collections.Immutable;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using Inedo.Diagnostics;
using LibGit2Sharp;

namespace Inedo.Extensions.Git;

#nullable enable

internal sealed class RepoMan : IDisposable
{
    private readonly Repository repo;
    private readonly string rootPath;
    private readonly string? userName;
    private readonly string? password;
    private readonly Uri repositoryUri;
    private readonly ILogSink? log;
    private readonly Action<RepoTransferProgress>? transferProgress;

    private RepoMan(Repository repo, Uri repositoryUri, string rootPath, string? userName, string? password, ILogSink? log, Action<RepoTransferProgress>? transferProgress)
    {
        this.repo = repo;
        this.repositoryUri = repositoryUri;
        this.rootPath = rootPath;
        this.userName = userName;
        this.password = password;
        this.log = log;
        this.transferProgress = transferProgress;
    }

    public static RepoMan FetchOrClone(string rootPath, Uri repositoryUri, string? userName, string? password, ILogSink? log, Action<RepoTransferProgress>? transferProgress)
    {
        ArgumentNullException.ThrowIfNull(rootPath);
        if (!Path.IsPathRooted(rootPath))
            throw new ArgumentException("Root path must be rooted.");
        ArgumentNullException.ThrowIfNull(repositoryUri);

        Repository? repo = null;
        try
        {
            var repoPath = Path.Combine(rootPath, GetRepositoryDiskName(repositoryUri));
            log?.LogDebug($"Repository path is {repoPath}");

            if (Directory.Exists(repoPath) && Repository.IsValid(repoPath))
            {
                repo = new Repository(repoPath);
                var origin = repo.Network.Remotes["origin"];
                if (origin == null)
                    throw new InvalidOperationException("Repository has no origin.");

                log?.LogDebug($"Fetching from origin ({origin.Url})...");
                var sw = Stopwatch.StartNew();

                Commands.Fetch(
                    repo,
                    "origin",
                    Enumerable.Empty<string>(),
                    new FetchOptions
                    {
                        CredentialsProvider = getCredentials,
                        TagFetchMode = TagFetchMode.All,
                        OnTransferProgress = transferProgress != null ? handleTransferProgress : null
                    },
                    null
                );

                sw.Stop();
                log?.LogDebug($"Fetch completed in {sw.Elapsed}.");
            }
            else
            {
                log?.LogDebug($"Repository does not exist or is not valid. Cloning from {repositoryUri}...");

                Directory.CreateDirectory(repoPath);
                var sw = Stopwatch.StartNew();

                Repository.Clone(
                    repositoryUri.ToString(),
                    repoPath,
                    new CloneOptions
                    {
                        IsBare = true,
                        CredentialsProvider = getCredentials,
                        OnTransferProgress = transferProgress != null ? handleTransferProgress : null
                    }
                );

                sw.Stop();
                log?.LogDebug($"Clone completed in {sw.Elapsed}.");

                repo = new Repository(repoPath);
            }

            return new RepoMan(repo, repositoryUri, rootPath, userName, password, log, transferProgress);

            LibGit2Sharp.Credentials getCredentials(string u, string n, SupportedCredentialTypes t) => string.IsNullOrEmpty(userName) ? new DefaultCredentials() : new UsernamePasswordCredentials { Username = userName, Password = password };

            bool handleTransferProgress(TransferProgress p)
            {
                transferProgress!(new RepoTransferProgress(p.TotalObjects, p.ReceivedObjects, p.ReceivedBytes));
                return true;
            }
        }
        catch
        {
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

    public void Export(string outputDirectory, string objectish, bool recurseSubmodules)
    {
        this.log?.LogDebug($"Checking out code from {objectish} to {outputDirectory}...");

        var commit = this.repo.Lookup<Commit>(objectish) ?? this.repo.Lookup<Commit>("refs/remotes/origin/" + objectish);
        if (commit == null)
            throw new ArgumentException($"Could not find commit for {objectish}.");

        this.log?.LogDebug($"Lookup succeeded; found commit {commit.Sha}.");

        var tree = commit.Tree;
        IReadOnlyDictionary<string, RepoMan>? submodules = null;
        try
        {
            if (recurseSubmodules)
                submodules = this.UpdateSubmodules(tree);

            exportTree(tree, outputDirectory, string.Empty);

            void exportTree(Tree tree, string outdir, string repopath)
            {
                Directory.CreateDirectory(outdir);

                foreach (var entry in tree)
                {
                    if (entry.TargetType == TreeEntryTargetType.Blob)
                    {
                        var blob = entry.Target.Peel<Blob>();
                        using var stream = blob.GetContentStream();
                        using var output = File.Create(Path.Combine(outdir, entry.Path), 8192, FileOptions.SequentialScan);
                        stream.CopyTo(output);
                        // handle file modes
                    }
                    else if (entry.TargetType == TreeEntryTargetType.Tree)
                    {
                        exportTree(entry.Target.Peel<Tree>(), Path.Combine(outdir, entry.Name), (repopath + "/" + entry.Name).Trim('/'));
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
                            subrepo.Export(Path.Combine(outdir, entry.Name), hash, true);
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

    public void Tag(string objectish, string tag, bool force)
    {
        var createdTag = this.repo.Tags.Add(tag, objectish, force);
        var pushRef = $"{createdTag.CanonicalName}:{createdTag.CanonicalName}";
        if (force)
            pushRef = $"+{pushRef}";

        this.repo.Network.Push(
            this.repo.Network.Remotes["origin"],
            force ? '+' + pushRef : pushRef,
            new PushOptions { CredentialsProvider = getCredentials }
        );

        LibGit2Sharp.Credentials getCredentials(string u, string n, SupportedCredentialTypes t) => string.IsNullOrEmpty(userName) ? new DefaultCredentials() : new UsernamePasswordCredentials { Username = this.userName, Password = this.password };
    }

    private IReadOnlyDictionary<string, RepoMan>? UpdateSubmodules(Tree tree)
    {
        this.log?.LogDebug("Looking for submodules...");

        if (tree[".gitmodules"] is not TreeEntry entry || entry.TargetType != TreeEntryTargetType.Blob)
        {
            this.log?.LogDebug("No submodules in repository.");
            return ImmutableDictionary.Create<string, RepoMan>();
        }

        var dict = new Dictionary<string, RepoMan>();
        try
        {
            var blob = entry.Target.Peel<Blob>();
            using var reader = new StreamReader(blob.GetContentStream(), Encoding.UTF8);
            foreach (var (name, path, url) in GetSubmodules(reader))
            {
                this.log?.LogDebug($"Found {name} submodule (path={path}, url={url})");
                var rubbish = new Uri(this.repositoryUri.ToString().TrimEnd('/') + "/");
                var submoduleUri = new Uri(rubbish, url);
                var subrepo = FetchOrClone(this.rootPath, submoduleUri, this.userName, this.password, this.log, this.transferProgress);
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

    public void Dispose()
    {
        this.repo.Dispose();
    }
}

internal readonly record struct RepoTransferProgress(int TotalObjects, int ReceivedObjects, long ReceivedBytes);
