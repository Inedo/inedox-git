using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
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
    [AppliesTo(InedoProduct.BuildMaster)]
    public sealed class GitRaftRepository2 : RaftRepository2
    {
        private static readonly object gitRaftLock = new object();
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
            if (entry != null)
                return new GitRaftItem2(type, entry, this, string.IsNullOrEmpty(version) ? null : this.Repo.Lookup<Commit>(version));
            else
                return null;
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
                LogEntry[] commits;

                lock (gitRaftLock)
                {
                    commits = this.lazyRepository.Value.Commits.QueryBy(
                        entry.Path,
                        new CommitFilter
                        {
                            IncludeReachableFrom = this.lazyRepository.Value.Branches[this.CurrentBranchName],
                            FirstParentOnly = false,
                            SortBy = CommitSortStrategies.Time
                        }
                    ).ToArray();
                }

                foreach (var c in commits)
                    yield return new GitRaftItem2(type, entry, this, c.Commit);
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
            lock (gitRaftLock)
            {
                if (this.disposed)
                    throw new ObjectDisposedException(nameof(GitRaftRepository2));
                if (string.IsNullOrEmpty(name))
                    throw new ArgumentNullException(nameof(name));
                if (this.ReadOnly)
                    throw new NotSupportedException();

                EnsureRelativePath(name);

                var itemPath = PathEx.Combine('/', GetStandardTypeName(type), name);
                if (!string.IsNullOrEmpty(this.RepositoryRoot))
                    itemPath = PathEx.Combine('/', this.RepositoryRoot, itemPath);

                var blob = this.Repo.ObjectDatabase.CreateBlob(content ?? Stream.Null);
                this.lazyCurrentTree.Value.Add(itemPath, blob, Mode.NonExecutableFile);
                this.Dirty = true;
            }
        }
        public override void WriteRaftItem(RaftItemType type, string name, byte[] content, DateTimeOffset? timestamp = null, string userName = null)
        {
            using (var stream = new MemoryStream(content, false))
            {
                this.WriteRaftItem(type, name, stream, timestamp, userName);
            }
        }
        public override void WriteRaftItem(RaftItemType type, string name, string content, DateTimeOffset? timestamp = null, string userName = null)
        {
            using (var stream = new MemoryStream())
            {
                using (var writer = new StreamWriter(stream, InedoLib.UTF8Encoding, 16, true))
                {
                    writer.Write(content ?? string.Empty);
                }

                stream.Position = 0;
                this.WriteRaftItem(type, name, stream, timestamp, userName);
            }
        }

        public override void DeleteRaftItem(RaftItemType type, string name)
        {
            lock (gitRaftLock)
            {
                if (this.disposed)
                    throw new ObjectDisposedException(nameof(GitRaftRepository2));
                if (string.IsNullOrEmpty(name))
                    throw new ArgumentNullException(name);
                if (this.ReadOnly)
                    throw new NotSupportedException();

                EnsureRelativePath(name);

                var entry = this.FindEntry(type, name);
                if (entry != null)
                {
                    this.lazyCurrentTree.Value.Remove(entry.Path);
                    this.Dirty = true;
                }
            }
        }

        public override IEnumerable<string> GetProjects(bool recursive)
        {
            var repo = this.lazyRepository.Value;

            Commit tip;
            lock (gitRaftLock)
            {
                tip = repo.Branches[this.CurrentBranchName]?.Tip;
                if (tip == null)
                    return Enumerable.Empty<string>();
            }

            return getProjects(tip.Tree, null);

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
            lock (gitRaftLock)
            {
                if (this.disposed)
                    throw new ObjectDisposedException(nameof(GitRaftRepository2));
                if (user == null)
                    throw new ArgumentNullException(nameof(user));
                if (this.ReadOnly)
                    throw new NotSupportedException();

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

        private string GetLocalRepoPath()
        {
            if (string.IsNullOrWhiteSpace(this.RaftName))
                throw new InvalidOperationException("The raft does not have a name.");

            return PathEx.Combine(SDK.GetCommonTempPath(), "GitRafts", this.RaftName);
        }
        private Repository OpenRepository()
        {
            lock (gitRaftLock)
            {
                if (DirectoryEx.Exists(this.LocalRepositoryPath))
                {
                    if (Repository.IsValid(this.LocalRepositoryPath))
                    {
                        var repository = new Repository(this.LocalRepositoryPath);

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

                        return repository;
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
            using (var stream = blob.GetContentStream())
            using (var reader = new StreamReader(stream, InedoLib.UTF8Encoding))
            {
                return ReadStandardVariableData(reader).ToDictionary(p => p.Key, p => p.Value);
            }
        }
        private void SaveVariables()
        {
            lock (gitRaftLock)
            {
                var repo = this.lazyRepository.Value;

                using (var buffer = new SlimMemoryStream())
                {
                    var writer = new StreamWriter(buffer, InedoLib.UTF8Encoding);
                    WriteStandardVariableData(this.lazyVariables.Value, writer);
                    writer.Flush();

                    buffer.Position = 0;

                    var blob = repo.ObjectDatabase.CreateBlob(buffer, "variables");

                    this.lazyCurrentTree.Value.Add(string.IsNullOrEmpty(this.RepositoryRoot) ? "variables" : PathEx.Combine('/', this.RepositoryRoot, "variables"), blob, Mode.NonExecutableFile);

                    this.Dirty = true;
                }
            }
        }
        private TreeDefinition GetCurrentTree()
        {
            lock (gitRaftLock)
            {
                var repo = this.lazyRepository.Value;

                var branch = repo.Branches[this.CurrentBranchName];
                if (branch?.Tip == null)
                    return new TreeDefinition();

                return TreeDefinition.From(branch.Tip);
            }
        }
        private TreeEntry FindEntry(string name, string hash = null)
        {
            lock (gitRaftLock)
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
        }
        private TreeEntry FindEntry(RaftItemType type, string name, string hash = null) => this.FindEntry(PathEx.Combine('/', GetStandardTypeName(type), name), hash);
        private IEnumerable<RaftItem2> GetRaftItemsInternal(RaftItemType? type)
        {
            if (this.disposed)
                throw new ObjectDisposedException(nameof(GitRaftRepository2));

            Tree tipTree;
            lock (gitRaftLock)
            {
                var repo = this.lazyRepository.Value;
                var tip = repo.Branches[this.CurrentBranchName]?.Tip;
                if (tip == null)
                    yield break;

                if (string.IsNullOrEmpty(this.RepositoryRoot))
                {
                    tipTree = tip.Tree;
                }
                else
                {
                    var rootItem = tip[this.RepositoryRoot];
                    if (rootItem?.TargetType != TreeEntryTargetType.Tree)
                        yield break;

                    tipTree = (Tree)rootItem.Target;
                }
            }

            foreach (var rootItem in tipTree)
            {
                var itemType = TryParseStandardTypeName(rootItem.Name);
                if (itemType != null && (!type.HasValue || type == itemType))
                {
                    foreach (var item in (Tree)rootItem.Target)
                    {
                        if (item.TargetType != TreeEntryTargetType.Tree)
                            yield return new GitRaftItem2(itemType.Value, item, this);
                    }
                }
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
                RaftName = this.RaftName
            };
        }
    }
}
