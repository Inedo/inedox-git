using System.ComponentModel;
using System.Security;
using Inedo.Documentation;
using Inedo.ExecutionEngine;
using Inedo.Extensibility;
using Inedo.Extensibility.Credentials;
using Inedo.Extensibility.RaftRepositories;
using Inedo.Extensibility.UserDirectories;
using Inedo.IO;
using Inedo.Serialization;
using Inedo.Web;
using LibGit2Sharp;

namespace Inedo.Extensions.Git.RaftRepositories
{
    [DisplayName("Git")]
    [Description("The raft is persisted as a Git repository that is synchronized with an external Git repository.")]
    [AppliesTo(InedoProduct.BuildMaster | InedoProduct.Otter)]
    public sealed class GitRaftRepository2 : RaftRepository2, ISyncRaft
    {
        private readonly Lazy<string> localRepoPath;
        private readonly Lazy<Repository> lazyRepository;
        private readonly Lazy<Dictionary<RuntimeVariableName, string>> lazyVariables;
        private readonly Lazy<TreeDefinition> lazyCurrentTree;
        private bool variablesDirty;
        private bool disposed;

        public GitRaftRepository2()
        {
            this.localRepoPath = new Lazy<string>(this.GetLocalRepoPath);
            this.lazyRepository = new Lazy<Repository>(this.OpenRepository);
            this.lazyVariables = new Lazy<Dictionary<RuntimeVariableName, string>>(this.ReadVariables);
            this.lazyCurrentTree = new Lazy<TreeDefinition>(this.GetCurrentTree);
        }

        [Required]
        [Persistent]
        [DisplayName("Branch")]
        public string BranchName { get; set; } = "master";
        [Persistent]
        [DisplayName("Read only")]
        public bool ReadOnly { get; set; }
        [Required]
        [Persistent]
        [DisplayName("Remote repository URL")]
        [PlaceholderText("i.e. the Git clone URL")]
        public string RemoteRepositoryUrl { get; set; }
        [Persistent]
        [DisplayName("Secure credential")]
        [SuggestableValue(typeof(SecureCredentialsSuggestionProvider<Extensions.Credentials.UsernamePasswordCredentials>))]
        [PlaceholderText("a Username & Password secure credential")]
        public string CredentialName { get; set; }
        [Persistent]
        [DisplayName("User name")]
        [PlaceholderText("use User name from secure credential")]
        public string UserName { get; set; }
        [Persistent(Encrypted = true)]
        [PlaceholderText("use Password from secure credential")]
        public SecureString Password { get; set; }

        public sealed override bool IsReadOnly => this.ReadOnly;
        public override bool SupportsVersioning => true;

        internal Repository Repo => !this.disposed ? this.lazyRepository.Value : throw new ObjectDisposedException(nameof(GitRaftRepository2));
        internal string CurrentBranchName => this.BranchName;

        private string LocalRepositoryPath => this.localRepoPath.Value;
        private bool Dirty { get; set; }
        private string RepositoryRoot { get; set; }

        public override RichDescription GetDescription()
        {
            return new RichDescription(
                "Git repository at ",
                new Hilite(this.RemoteRepositoryUrl),
                " using branch ",
                new Hilite(this.BranchName)
            );
        }

        public override IEnumerable<RaftItem2> GetRaftItems() => this.GetRaftItemsInternal(null);
        public override IEnumerable<RaftItem2> GetRaftItems(RaftItemType type) => this.GetRaftItemsInternal(type);
        public override RaftItem2 GetRaftItem(RaftItemType type, string name, string version)
        {
            if (this.disposed)
                throw new ObjectDisposedException(nameof(GitRaftRepository2));

            var entry = this.FindEntry(type, name, version);
            if (entry == null)
                return null;

            var path = name.Replace('\\', '/');
            var fullPath = PathEx.Combine('/', GetStandardTypeName(type), path);

            return new GitRaftItem2(type, path, entry.Target, this.GetLatestCommitForItem(this.Repo, fullPath));
        }
        public override IEnumerable<RaftItem2> GetRaftItemVersions(RaftItemType type, string name)
        {
            if (this.disposed)
                throw new ObjectDisposedException(nameof(GitRaftRepository2));
            if (string.IsNullOrEmpty(name))
                throw new ArgumentNullException(name);

            var entry = this.FindEntry(type, name);
            if (entry != null)
            {
                List<LogEntry> commits;

                GitRepoLock.EnterLock(this.LocalRepositoryPath);
                try
                {
                    commits = this.lazyRepository.Value.Commits.QueryBy(
                        entry.Path,
                        new CommitFilter
                        {
                            IncludeReachableFrom = this.lazyRepository.Value.Branches[this.CurrentBranchName],
                            FirstParentOnly = false,
                            SortBy = CommitSortStrategies.Time
                        }
                    ).ToList();
                }
                finally
                {
                    GitRepoLock.ReleaseLock(this.LocalRepositoryPath);
                }

                foreach (var c in commits)
                    yield return new GitRaftItem2(type, name, entry.Target, c.Commit);
            }
        }

        public override IReadOnlyDictionary<RuntimeVariableName, string> GetVariables()
        {
            if (this.disposed)
                throw new ObjectDisposedException(nameof(GitRaftRepository2));

            return this.lazyVariables.Value.ToDictionary(p => p.Key, p => p.Value);
        }
        public override void SetVariable(RuntimeVariableName name, string value)
        {
            if (this.disposed)
                throw new ObjectDisposedException(nameof(GitRaftRepository2));
            if (name == null)
                throw new ArgumentNullException(nameof(name));
            if (this.ReadOnly)
                throw new NotSupportedException();

            var vars = this.lazyVariables.Value;
            if (vars.TryGetValue(name, out var currentValue) && currentValue == value)
                return;

            vars[name] = value;
            this.variablesDirty = true;
        }
        public override bool DeleteVariable(RuntimeVariableName name)
        {
            if (this.disposed)
                throw new ObjectDisposedException(nameof(GitRaftRepository2));
            if (name == null)
                throw new ArgumentNullException(nameof(name));
            if (this.ReadOnly)
                throw new NotSupportedException();

            bool deleted = this.lazyVariables.Value.Remove(name);
            this.variablesDirty = deleted;
            return deleted;
        }

        public override void WriteRaftItem(RaftItemType type, string name, Stream content, DateTimeOffset? timestamp = null, string userName = null)
        {
            if (this.disposed)
                throw new ObjectDisposedException(nameof(GitRaftRepository2));
            if (string.IsNullOrEmpty(name))
                throw new ArgumentNullException(nameof(name));
            if (this.ReadOnly)
                throw new NotSupportedException();

            EnsureRelativePath(name);

            GitRepoLock.EnterLock(this.LocalRepositoryPath);
            try
            {
                var itemPath = PathEx.Combine('/', GetStandardTypeName(type), name);
                if (!string.IsNullOrEmpty(this.RepositoryRoot))
                    itemPath = PathEx.Combine('/', this.RepositoryRoot, itemPath);

                var blob = this.Repo.ObjectDatabase.CreateBlob(content ?? Stream.Null);
                this.lazyCurrentTree.Value.Add(itemPath, blob, Mode.NonExecutableFile);
                this.Dirty = true;
            }
            finally
            {
                GitRepoLock.ReleaseLock(this.LocalRepositoryPath);
            }
        }
        public override void WriteRaftItem(RaftItemType type, string name, byte[] content, DateTimeOffset? timestamp = null, string userName = null)
        {
            using var stream = new MemoryStream(content, false);
            this.WriteRaftItem(type, name, stream, timestamp, userName);
        }
        public override void WriteRaftItem(RaftItemType type, string name, string content, DateTimeOffset? timestamp = null, string userName = null)
        {
            using var stream = new MemoryStream();
            using (var writer = new StreamWriter(stream, InedoLib.UTF8Encoding, 16, true))
            {
                writer.Write(content ?? string.Empty);
            }

            stream.Position = 0;
            this.WriteRaftItem(type, name, stream, timestamp, userName);
        }

        public override void DeleteRaftItem(RaftItemType type, string name)
        {
            if (this.disposed)
                throw new ObjectDisposedException(nameof(GitRaftRepository2));
            if (string.IsNullOrEmpty(name))
                throw new ArgumentNullException(name);
            if (this.ReadOnly)
                throw new NotSupportedException();

            EnsureRelativePath(name);

            GitRepoLock.EnterLock(this.LocalRepositoryPath);
            try
            {
                var entry = this.FindEntry(type, name);
                if (entry != null)
                {
                    this.lazyCurrentTree.Value.Remove(entry.Path);
                    this.Dirty = true;
                }
            }
            finally
            {
                GitRepoLock.ReleaseLock(this.LocalRepositoryPath);
            }
        }

        public override IEnumerable<string> GetProjects(bool recursive)
        {
            var repo = this.lazyRepository.Value;

            Commit tip;
            GitRepoLock.EnterLock(this.LocalRepositoryPath);
            try
            {
                tip = repo.Branches[this.CurrentBranchName]?.Tip;
                if (tip == null)
                    return Enumerable.Empty<string>();

                return getProjects(tip.Tree, null).ToList();
            }
            finally
            {
                GitRepoLock.ReleaseLock(this.LocalRepositoryPath);
            }

            IEnumerable<string> getProjects(Tree tree, string currentProject)
            {
                var projectsEntry = tree["projects"];
                if (projectsEntry?.TargetType != TreeEntryTargetType.Tree)
                    yield break;

                foreach (var subtree in (Tree)projectsEntry.Target)
                {
                    var current = string.IsNullOrEmpty(currentProject) ? subtree.Name : (currentProject + "/" + subtree.Name);
                    yield return current;

                    if (recursive && subtree.TargetType == TreeEntryTargetType.Tree)
                    {
                        foreach (var subproject in getProjects((Tree)subtree.Target, current))
                            yield return subproject;
                    }
                }
            }
        }
        public override RaftRepository2 GetProjectScopedRaftRepository(string project)
        {
            var instance = this.CreateCopy();
            if (!string.IsNullOrEmpty(project))
            {
                var projectPath = string.Join("/", project.Split(new[] { '/', '\\' }).Select(p => "projects/" + p));

                if (!string.IsNullOrEmpty(instance.RepositoryRoot))
                    instance.RepositoryRoot = PathEx.Combine('/', instance.RepositoryRoot, projectPath);
                else
                    instance.RepositoryRoot = projectPath;
            }

            return instance;
        }

        public override void Commit(IUserDirectoryUser user)
        {
            if (this.disposed)
                throw new ObjectDisposedException(nameof(GitRaftRepository2));
            if (user == null)
                throw new ArgumentNullException(nameof(user));
            if (this.ReadOnly)
                throw new NotSupportedException();

            GitRepoLock.EnterLock(this.LocalRepositoryPath);
            try
            {
                if (this.variablesDirty)
                    this.SaveVariables();

                if (this.Dirty)
                {
                    var signature = new Signature(AH.CoalesceString(user.DisplayName, user.Name), AH.CoalesceString(user.EmailAddress, "none@example.com"), DateTime.Now);

                    var repo = this.lazyRepository.Value;
                    var tree = repo.ObjectDatabase.CreateTree(this.lazyCurrentTree.Value);

                    var branch = repo.Branches[this.CurrentBranchName];
                    if (branch == null)
                    {
                        var commit = repo.ObjectDatabase.CreateCommit(signature, signature, $"Updated by {SDK.ProductName}.", tree, repo.Head.Tip == null ? Enumerable.Empty<Commit>() : new[] { repo.Head.Tip }, false);
                        branch = repo.Branches.Add(this.CurrentBranchName, commit);

                        repo.Refs.Add("refs/head", repo.Refs.Head);
                    }
                    else
                    {
                        var commit = repo.ObjectDatabase.CreateCommit(signature, signature, $"Updated by {SDK.ProductName}.", tree, new[] { branch.Tip }, false);
                        repo.Refs.UpdateTarget(repo.Refs[branch.CanonicalName], commit.Id);
                    }

                    this.Repo.Branches.Update(this.Repo.Branches[this.CurrentBranchName], b => b.TrackedBranch = "refs/remotes/origin/" + this.CurrentBranchName);

                    this.Repo.Network.Push(
                        this.Repo.Branches[this.CurrentBranchName],
                        new PushOptions
                        {
                            CredentialsProvider = this.CredentialsHandler
                        }
                    );
                }
            }
            finally
            {
                GitRepoLock.ReleaseLock(this.LocalRepositoryPath);
            }
        }

        public override ConfigurationTestResult TestConfiguration()
        {
            if (string.IsNullOrWhiteSpace(this.RemoteRepositoryUrl))
                return ConfigurationTestResult.Failure("Remote repository URL is not specified.");

            return ConfigurationTestResult.Success;
        }

        protected override void Dispose(bool disposing)
        {
            if (!this.disposed)
            {
                if (disposing)
                {
                    if (this.lazyRepository.IsValueCreated)
                        this.lazyRepository.Value.Dispose();
                }

                this.disposed = true;
            }

            base.Dispose(disposing);
        }

        private Commit GetLatestCommitForItem(Repository repository, string path)
        {
            return repository.Commits.QueryBy(
                string.IsNullOrWhiteSpace(this.RepositoryRoot) ? path : PathEx.Combine('/', this.RepositoryRoot, path),
                new CommitFilter
                {
                    IncludeReachableFrom = repository.Branches[this.CurrentBranchName],
                    SortBy = CommitSortStrategies.Time
                }
            ).FirstOrDefault()?.Commit;
        }

        private string GetLocalRepoPath()
        {
            if (string.IsNullOrWhiteSpace(this.RaftName))
                throw new InvalidOperationException("The raft does not have a name.");

            return PathEx.Combine(SDK.GetCommonTempPath(), "GitRafts", this.RaftName);
        }
        private void Fetch(Repository repository)
        {
            try
            {
                if (!string.IsNullOrEmpty(this.RemoteRepositoryUrl))
                {
                    Commands.Fetch(repository, "origin", Enumerable.Empty<string>(), new FetchOptions { CredentialsProvider = CredentialsHandler }, null);
                    if (repository.Refs["refs/heads/" + this.CurrentBranchName] == null)
                    {
                        //Must use an ObjectId to create a DirectReference (SymbolicReferences will cause an error when committing)
                        var objId = new ObjectId(repository.Refs["refs/remotes/origin/" + this.CurrentBranchName].TargetIdentifier);
                        repository.Refs.Add("refs/heads/" + this.CurrentBranchName, objId);
                    }

                    repository.Refs.UpdateTarget("refs/heads/" + this.CurrentBranchName, "refs/remotes/origin/" + this.CurrentBranchName);
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to fetch repository: {ex.Message}", ex);
            }
        }
        private Repository OpenRepository()
        {
            GitRepoLock.EnterLock(this.LocalRepositoryPath);
            try
            {
                if (DirectoryEx.Exists(this.LocalRepositoryPath))
                {
                    if (Repository.IsValid(this.LocalRepositoryPath))
                    {
                        var repo = new Repository(this.LocalRepositoryPath);
                        return repo;
                    }

                    if (DirectoryEx.GetFileSystemInfos(this.LocalRepositoryPath, MaskingContext.Default).Any())
                        throw new InvalidOperationException("The specified local repository path does not appear to be a Git repository but already contains files or directories.");
                }
                else
                {
                    DirectoryEx.Create(this.LocalRepositoryPath);
                }

                if (!string.IsNullOrEmpty(this.RemoteRepositoryUrl))
                {
                    Repository.Clone(
                        this.RemoteRepositoryUrl,
                        this.LocalRepositoryPath,
                        new CloneOptions
                        {
                            CredentialsProvider = this.CredentialsHandler,
                            IsBare = true
                        }
                    );
                }
                else
                {
                    Repository.Init(this.LocalRepositoryPath, true);
                }

                return new Repository(this.LocalRepositoryPath);
            }
            finally
            {
                GitRepoLock.ReleaseLock(this.LocalRepositoryPath);
            }
        }
        private LibGit2Sharp.Credentials CredentialsHandler(string url, string usernameFromUrl, SupportedCredentialTypes types)
        {
            if (!string.IsNullOrEmpty(this.CredentialName))
            {
                var credential = (Extensions.Credentials.UsernamePasswordCredentials)SecureCredentials.Create(this.CredentialName, new CredentialResolutionContext(null, null));
                return new SecureUsernamePasswordCredentials
                {
                    Username = credential.UserName,
                    Password = credential.Password
                };
            }

            if (string.IsNullOrEmpty(this.UserName))
            {
                return new DefaultCredentials();
            }
            else
            {
                return new SecureUsernamePasswordCredentials
                {
                    Username = this.UserName,
                    Password = this.Password
                };
            }
        }
        private Dictionary<RuntimeVariableName, string> ReadVariables()
        {
            var repo = this.lazyRepository.Value;

            var entry = this.FindEntry("variables");
            if (entry == null)
                return new Dictionary<RuntimeVariableName, string>();

            var blob = entry.Target as Blob;
            using var stream = blob.GetContentStream();
            using var reader = new StreamReader(stream, InedoLib.UTF8Encoding);
            return ReadStandardVariableData(reader).ToDictionary(p => p.Key, p => p.Value);
        }
        private void SaveVariables()
        {
            GitRepoLock.EnterLock(this.LocalRepositoryPath);
            try
            {
                var repo = this.lazyRepository.Value;

                using var buffer = new TemporaryStream();
                var writer = new StreamWriter(buffer, InedoLib.UTF8Encoding);
                WriteStandardVariableData(this.lazyVariables.Value, writer);
                writer.Flush();

                buffer.Position = 0;

                var blob = repo.ObjectDatabase.CreateBlob(buffer, "variables");

                this.lazyCurrentTree.Value.Add(string.IsNullOrEmpty(this.RepositoryRoot) ? "variables" : PathEx.Combine('/', this.RepositoryRoot, "variables"), blob, Mode.NonExecutableFile);

                this.Dirty = true;
            }
            finally
            {
                GitRepoLock.ReleaseLock(this.LocalRepositoryPath);
            }
        }
        private TreeDefinition GetCurrentTree()
        {
            GitRepoLock.EnterLock(this.LocalRepositoryPath);
            try
            {
                var repo = this.lazyRepository.Value;

                var branch = repo.Branches[this.CurrentBranchName];
                if (branch?.Tip == null)
                    return new TreeDefinition();

                return TreeDefinition.From(branch.Tip);
            }
            finally
            {
                GitRepoLock.ReleaseLock(this.LocalRepositoryPath);
            }
        }
        private TreeEntry FindEntry(string name, string hash = null)
        {
            GitRepoLock.EnterLock(this.LocalRepositoryPath);
            try
            {
                var repo = this.lazyRepository.Value;
                var path = string.IsNullOrEmpty(this.RepositoryRoot) ? name : PathEx.Combine('/', this.RepositoryRoot, name);

                Tree root;

                if (!string.IsNullOrEmpty(hash))
                    root = repo.Lookup<Commit>(hash)?.Tree;
                else
                    root = repo.Branches[this.CurrentBranchName]?.Tip.Tree;

                return root?[path];
            }
            finally
            {
                GitRepoLock.ReleaseLock(this.LocalRepositoryPath);
            }
        }
        private TreeEntry FindEntry(RaftItemType type, string name, string hash = null) => this.FindEntry(PathEx.Combine('/', GetStandardTypeName(type), name), hash);
        private IEnumerable<RaftItem2> GetRaftItemsInternal(RaftItemType? type)
        {
            if (this.disposed)
                throw new ObjectDisposedException(nameof(GitRaftRepository2));

            GitRepoLock.EnterLock(this.LocalRepositoryPath);
            try
            {
                var repo = this.lazyRepository.Value;
                var tip = repo.Branches[this.CurrentBranchName]?.Tip;
                if (tip == null)
                    yield break;

                if (type.HasValue)
                {
                    foreach (var item in this.GetTree(tip, type.GetValueOrDefault(), GetStandardTypeName(type.GetValueOrDefault())))
                        yield return item;
                }
                else
                {
                    foreach (var rootItem in tip.Tree)
                    {
                        var itemType = TryParseStandardTypeName(rootItem.Name);
                        if (itemType != null && (!type.HasValue || type == itemType))
                        {
                            foreach (var item in this.GetTree(tip, itemType.GetValueOrDefault(), rootItem.Name))
                                yield return item;
                        }
                    }
                }
            }
            finally
            {
                GitRepoLock.ReleaseLock(this.LocalRepositoryPath);
            }
        }
        private GitRaftRepository2 CreateCopy()
        {
            return new GitRaftRepository2
            {
                RemoteRepositoryUrl = this.RemoteRepositoryUrl,
                UserName = this.UserName,
                Password = this.Password,
                BranchName = this.BranchName,
                ReadOnly = this.ReadOnly,
                RepositoryRoot = this.RepositoryRoot,
                RaftId = this.RaftId,
                RaftName = this.RaftName,
                CredentialName = this.CredentialName
            };
        }

        private IEnumerable<GitRaftItem2> GetTree(Commit initialCommit, RaftItemType itemType, string path = null)
        {
            return getTree(initialCommit, itemType, string.IsNullOrEmpty(this.RepositoryRoot) ? path : PathEx.Combine('/', this.RepositoryRoot, path ?? string.Empty));

            IEnumerable<GitRaftItem2> getTree(Commit initialCommit, RaftItemType itemType, string path = null)
            {
                var items = new Dictionary<string, TreeEntryLookup>();
                var foundItems = new HashSet<string>();

                var initialTree = string.IsNullOrEmpty(path) ? initialCommit.Tree : (initialCommit[path]?.Target as Tree);
                if (initialTree == null)
                    yield break;

                if (initialTree != null)
                {
                    foreach (var entry in initialTree)
                    {
                        if (entry.TargetType != TreeEntryTargetType.GitLink)
                            items.Add(entry.Name, new TreeEntryLookup(entry.Name, initialCommit, entry.Mode == Mode.Directory, entry.Target));
                    }
                }

                var commits = this.Repo.Commits.QueryBy(
                    new CommitFilter
                    {
                        IncludeReachableFrom = initialCommit,
                        SortBy = CommitSortStrategies.Time
                    }
                );

                var lastCommit = initialCommit;

                foreach (var commit in commits.Skip(1))
                {
                    if (foundItems.Count >= items.Count)
                        break;

                    var tree = string.IsNullOrEmpty(path) ? commit.Tree : (commit[path]?.Target as Tree);

                    if (tree == null)
                    {
                        lastCommit = commit;
                        continue;
                    }

                    foreach (var entry in tree)
                    {
                        if (entry.TargetType == TreeEntryTargetType.GitLink)
                            continue;

                        if (foundItems.Contains(entry.Name))
                        {
                            // if we've already found a commit for this entry, continue
                            continue;
                        }

                        if (!items.TryGetValue(entry.Name, out var prevItem))
                        {
                            // indicates item was deleted in current commit; mark it as found and continue
                            foundItems.Add(entry.Name);
                            continue;
                        }

                        if (prevItem.Target.Id != entry.Target.Id)
                        {
                            // if the target has changed to a different blob, then the last commit we looked at changed it
                            prevItem.Commit = lastCommit;
                            foundItems.Add(entry.Name);
                        }
                    }

                    lastCommit = commit;
                }

                foreach (var item in items.Values)
                {
                    if (!foundItems.Contains(item.Name))
                        item.Commit = lastCommit;
                }

                var typeName = GetStandardTypeName(itemType);

                foreach (var v in items.Values)
                {
                    if (v.Directory)
                    {
                        foreach (var item in getTree(initialCommit, itemType, PathEx.Combine('/', path ?? string.Empty, v.Name)))
                            yield return item;
                    }
                    else
                    {
                        var itemRootPath = TrimFolder(this.RepositoryRoot ?? string.Empty, path);
                        if (itemRootPath.Equals(typeName, StringComparison.OrdinalIgnoreCase))
                            itemRootPath = default;
                        else
                            itemRootPath = TrimFolder(typeName, itemRootPath);

                        yield return new GitRaftItem2(itemType, PathEx.Combine('/', itemRootPath, v.Name), v.Target, v.Commit);
                    }
                }
            }
        }

        private static ReadOnlySpan<char> TrimFolder(ReadOnlySpan<char> root, ReadOnlySpan<char> path)
        {
            if (root.IsEmpty)
                return path;

            int trimLength = root.Length;
            if (root[^1] is not '/' and not '\\')
                trimLength++;

            return path[trimLength..];
        }

        private static IEnumerable<TreeEntry> IterateFullTree(Tree root)
        {
            foreach (var entry in root)
            {
                if (entry.TargetType == TreeEntryTargetType.Tree)
                {
                    foreach (var e in IterateFullTree((Tree)entry.Target))
                        yield return e;
                }
                else
                {
                    yield return entry;
                }
            }
        }

        Task ISyncRaft.SynchronizeAsync(CancellationToken cancellationToken)
        {
            GitRepoLock.EnterLock(this.LocalRepositoryPath);
            try
            {
                this.Fetch(this.Repo);
                return Task.CompletedTask;
            }
            finally
            {
                GitRepoLock.ReleaseLock(this.LocalRepositoryPath);
            }
        }

        private readonly struct CachedItemCommit
        {
            public CachedItemCommit(ObjectId oid, Commit commit)
            {
                this.ItemId = oid;
                this.Commit = commit;
            }

            public ObjectId ItemId { get; }
            public Commit Commit { get; }
        }

        private sealed class TreeEntryLookup
        {
            public TreeEntryLookup(string name, Commit commit, bool directory, GitObject target)
            {
                this.Name = name;
                this.Commit = commit;
                this.Directory = directory;
                this.Target = target;
            }

            public string Name { get; }
            public Commit Commit { get; set; }
            public bool Directory { get; }
            public GitObject Target { get; }
        }
    }
}
