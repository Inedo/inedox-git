using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Inedo.Documentation;
using Inedo.ExecutionEngine;
using Inedo.Extensibility.RaftRepositories;
using Inedo.Extensibility.UserDirectories;
using Inedo.IO;
using Inedo.Serialization;
using Inedo.Web;
using LibGit2Sharp;

namespace Inedo.Extensions.Git.RaftRepositories
{
    public abstract class GitRaftRepositoryBase : RaftRepository, IMultiEnvironmentRaft
    {
        private Lazy<Repository> lazyRepository;
        private readonly Lazy<Dictionary<RuntimeVariableName, string>> lazyVariables;
        private Lazy<TreeDefinition> lazyCurrentTree;
        private readonly Lazy<Dictionary<string, string>> lazyEnvironmentBranches;
        private string currentEnvironmentBranch;
        private bool variablesDirty;
        private bool disposed;

        protected GitRaftRepositoryBase()
        {
            this.lazyRepository = new Lazy<Repository>(this.OpenRepository);
            this.lazyVariables = new Lazy<Dictionary<RuntimeVariableName, string>>(this.ReadVariables);
            this.lazyCurrentTree = new Lazy<TreeDefinition>(this.GetCurrentTree);
            this.lazyEnvironmentBranches = new Lazy<Dictionary<string, string>>(this.GetEnvironmentBranchMap);
        }

        public abstract string LocalRepositoryPath { get; }

        [Required]
        [Persistent]
        [DisplayName("Default branch")]
        public string BranchName { get; set; } = "master";

        [Persistent]
        [DisplayName("Read only")]
        public bool ReadOnly { get; set; }

        [Persistent]
        [FieldEditMode(FieldEditMode.Multiline)]
        [DisplayName("Environment branches")]
        [Description("When this raft is used with an Otter Configuration plan, the branch used by this raft may be selected by environment. Enter environment-branch mappings one per line in the format \"Environment:Branch\". If no match is found, or if the raft is used from an Orchestration job, the default branch is used.")]
        [PlaceholderText("always use default branch")]
        public string EnvironmentBranches { get; set; }

        public sealed override bool IsReadOnly => this.ReadOnly;
        public sealed override bool SupportsVersioning => true;
        public bool HasMultipleEnvironments => this.GetEnvironmentBranchMap()?.Any() ?? false;
        public string ActualEnvironment { get; private set; }

        private protected bool Dirty { get; private set; }
        private protected Repository Repo => this.lazyRepository.Value;
        private protected string CurrentBranchName => this.currentEnvironmentBranch ?? this.BranchName;

        private bool OptimizeLoadTime => (this.OpenOptions & OpenRaftOptions.OptimizeLoadTime) != 0;
        private string RepositoryRoot { get; set; }

        public Task<bool> SetEnvironmentAsync(string environmentName)
        {
            if (string.IsNullOrWhiteSpace(environmentName))
                throw new ArgumentNullException(nameof(environmentName));
            if (this.lazyRepository.IsValueCreated)
                throw new InvalidOperationException("SetEnvironmentAsync cannot be called after the Raft repository has been used.");

            var d = this.GetEnvironmentBranchMap();
            if (d == null || !d.TryGetValue(environmentName, out var branch))
                return Task.FromResult(false);

            this.currentEnvironmentBranch = branch;
            this.ActualEnvironment = environmentName;
            return Task.FromResult(true);
        }
        public IEnumerable<string> GetEnvironments() => this.GetEnvironmentBranchMap()?.Keys ?? Enumerable.Empty<string>();

        public sealed override Task<IEnumerable<RaftItem>> GetRaftItemsAsync()
        {
            if (this.disposed)
                throw new ObjectDisposedException(nameof(GitRaftRepositoryBase));

            return Task.FromResult(inner());

            IEnumerable<RaftItem> inner()
            {
                var repo = this.lazyRepository.Value;
                var tip = repo.Branches[this.CurrentBranchName]?.Tip;
                if (tip == null)
                    yield break;

                Tree tipTree;
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

                foreach (var rootItem in tipTree)
                {
                    var itemType = TryParseStandardTypeName(rootItem.Name);
                    if (itemType != null)
                    {
                        foreach (var item in (Tree)rootItem.Target)
                        {
                            if (item.TargetType != TreeEntryTargetType.Tree)
                            {
                                if (this.OptimizeLoadTime)
                                {
                                    yield return new RaftItem(itemType.Value, item.Name, DateTimeOffset.Now);
                                }
                                else
                                {
                                    var commits = repo.Commits.QueryBy(item.Path,
                                        new CommitFilter {
                                            IncludeReachableFrom = repo.Branches[this.CurrentBranchName],
                                            FirstParentOnly = false,
                                            SortBy = CommitSortStrategies.Time, }
                                        );
                                    var commit = commits.FirstOrDefault();
                                    if (commit != null)
                                    {
                                        var committer = commit.Commit.Committer;
                                        yield return new RaftItem(itemType.Value, item.Name, committer.When.UtcDateTime, committer.Name, null, commits?.Count().ToString());
                                    }
                                    else
                                    {
                                        // Handles situations where the commits are empty, even though the
                                        // file has been committed and pushed.  There is likely a root cause, but
                                        // it is not known as of yet.
                                        yield return new RaftItem(itemType.Value, item.Name, DateTimeOffset.Now);
                                    }

                                }
                            }
                        }
                    }
                }
            }
        }

        public override Task<RaftItem> GetRaftItemAsync(RaftItemType type, string name, string version)
        {
            if (this.disposed)
                throw new ObjectDisposedException(nameof(GitRaftRepositoryBase));

            return Task.FromResult(inner());

            RaftItem inner()
            {
                var entry = this.FindEntry(type, name, version);
                if (entry != null)
                {
                    long? size = null;
                    if (entry.TargetType == TreeEntryTargetType.Blob)
                    {
                        var blob = (Blob)entry.Target;
                        size = blob.Size;
                    }
                    Commit commit = null;
                    if (string.IsNullOrEmpty(version))
                    {
                        commit = this.lazyRepository.Value.Commits.QueryBy(entry.Path, new CommitFilter
                        {
                            IncludeReachableFrom = this.lazyRepository.Value.Branches[this.CurrentBranchName],
                            FirstParentOnly = false,
                            SortBy = CommitSortStrategies.Time
                        }).FirstOrDefault()?.Commit;
                    }
                    else
                    {
                        commit = this.lazyRepository.Value.Lookup<Commit>(version);
                    }

                    if(commit != null)
                    {
                        return new RaftItem(type, name, commit.Committer?.When ?? DateTimeOffset.Now, commit.Committer?.Name ?? "NA", size, commit.Id?.ToString() ?? "NA");
                    }
                    else
                    {
                        // Handles situations where the commits are empty, even though the
                        // file has been committed and pushed.  There is likely a root cause, but
                        // it is not known as of yet.
                        return new RaftItem(type, name, DateTimeOffset.Now);
                    }
                }

                return null;
            }
        }

        public sealed override Task<Stream> OpenRaftItemAsync(RaftItemType type, string name, FileMode fileMode, FileAccess fileAccess, string version)
        {
            if (this.disposed)
                throw new ObjectDisposedException(nameof(GitRaftRepositoryBase));
            if (string.IsNullOrEmpty(name))
                throw new ArgumentNullException(nameof(name));
            if (this.ReadOnly && (fileAccess & FileAccess.Write) != 0)
                throw new NotSupportedException();

            EnsureRelativePath(name);

            var itemPath = PathEx.Combine('/', GetStandardTypeName(type), name);
            if (!string.IsNullOrEmpty(this.RepositoryRoot))
                itemPath = PathEx.Combine('/', this.RepositoryRoot, itemPath);

            var entry = this.FindEntry(type, name, version);

            return Task.FromResult(inner());

            Stream inner()
            {
                switch (fileMode)
                {
                    case FileMode.CreateNew:
                        if (entry != null)
                            throw new InvalidOperationException($"A {type} named \"{name}\" already exists in this raft.");
                        goto case FileMode.Create;

                    case FileMode.Create:
                        if ((fileAccess & FileAccess.Write) == 0)
                            throw new ArgumentOutOfRangeException(nameof(fileAccess));
                        return new RaftItemStream(new SlimMemoryStream(), this, itemPath, fileAccess, dirty: entry == null);

                    case FileMode.Open:
                        if (entry == null)
                            return null;
                        return new RaftItemStream(this.OpenEntry(entry), this, itemPath, fileAccess);

                    case FileMode.OpenOrCreate:
                        if (entry == null)
                            return new RaftItemStream(new SlimMemoryStream(), this, itemPath, fileAccess);
                        return new RaftItemStream(this.OpenEntry(entry), this, itemPath, fileAccess, dirty: entry == null);

                    case FileMode.Truncate:
                        if (entry == null)
                            return null;
                        if ((fileAccess & FileAccess.Write) == 0)
                            throw new ArgumentOutOfRangeException(nameof(fileAccess));
                        return new RaftItemStream(new SlimMemoryStream(), this, itemPath, fileAccess, dirty: true);

                    case FileMode.Append:
                        if ((fileAccess & FileAccess.Write) == 0)
                            throw new ArgumentOutOfRangeException(nameof(fileAccess));
                        var s = new RaftItemStream(this.OpenEntry(entry), this, itemPath, fileAccess);
                        s.Seek(0, SeekOrigin.End);
                        return s;

                    default:
                        throw new ArgumentOutOfRangeException(nameof(fileMode));
                }
            }
        }
        public sealed override Task DeleteRaftItemAsync(RaftItemType type, string name)
        {
            if (this.disposed)
                throw new ObjectDisposedException(nameof(GitRaftRepositoryBase));
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

            return InedoLib.NullTask;
        }

        public override Task<IEnumerable<RaftItemVersion>> GetRaftItemVersionsAsync(RaftItemType type, string name)
        {
            if (this.disposed)
                throw new ObjectDisposedException(nameof(GitRaftRepositoryBase));
            if (string.IsNullOrEmpty(name))
                throw new ArgumentNullException(name);

            return Task.FromResult(inner());

            IEnumerable<RaftItemVersion> inner()
            {
                var entry = this.FindEntry(type, name);
                if (entry != null)
                {
                    var commits = this.lazyRepository.Value.Commits.QueryBy(entry.Path, new CommitFilter
                    {
                        IncludeReachableFrom = this.lazyRepository.Value.Branches[this.CurrentBranchName],
                        FirstParentOnly = false,
                        SortBy = CommitSortStrategies.Time
                    });
                    foreach (var c in commits)
                    {
                        var commit = c.Commit;
                        var committer = commit.Committer;
                        yield return new RaftItemVersion(commit.Id.ToString(), committer.When, committer.Name);
                    }
                }
            }
        }

        public sealed override Task<IReadOnlyDictionary<RuntimeVariableName, string>> GetVariablesAsync()
        {
            if (this.disposed)
                throw new ObjectDisposedException(nameof(GitRaftRepositoryBase));

            return Task.FromResult<IReadOnlyDictionary<RuntimeVariableName, string>>(new ReadOnlyDictionary<RuntimeVariableName, string>(this.lazyVariables.Value.ToDictionary(p => p.Key, p => p.Value)));
        }
        public sealed override Task SetVariableAsync(RuntimeVariableName name, string value)
        {
            if (this.disposed)
                throw new ObjectDisposedException(nameof(GitRaftRepositoryBase));
            if (name == null)
                throw new ArgumentNullException(nameof(name));
            if (this.ReadOnly)
                throw new NotSupportedException();

            var vars = this.lazyVariables.Value;
            if (vars.TryGetValue(name, out var currentValue) && currentValue == value)
                return InedoLib.NullTask;

            vars[name] = value;
            this.variablesDirty = true;
            return InedoLib.NullTask;
        }
        public sealed override Task<bool> DeleteVariableAsync(RuntimeVariableName name)
        {
            if (this.disposed)
                throw new ObjectDisposedException(nameof(GitRaftRepositoryBase));
            if (name == null)
                throw new ArgumentNullException(nameof(name));
            if (this.ReadOnly)
                throw new NotSupportedException();

            bool deleted = this.lazyVariables.Value.Remove(name);
            this.variablesDirty = deleted;
            return Task.FromResult(deleted);
        }

        public override Task CommitAsync(IUserDirectoryUser user)
        {
            if (this.disposed)
                throw new ObjectDisposedException(nameof(GitRaftRepositoryBase));
            if (user == null)
                throw new ArgumentNullException(nameof(user));
            if (this.ReadOnly)
                throw new NotSupportedException();

            if (this.variablesDirty)
                this.SaveVariables();

            if (this.Dirty)
            {
                var signature = new Signature(AH.CoalesceString(user.DisplayName, user.Name), AH.CoalesceString(user.EmailAddress ?? "none@example.com"), DateTime.Now);

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
            }

            return InedoLib.NullTask;
        }

        public sealed override Task<IEnumerable<string>> GetProjectsAsync(bool recursive)
        {
            return Task.FromResult(inner());

            IEnumerable<string> inner()
            {
                var repo = this.lazyRepository.Value;

                var tip = repo.Branches[this.CurrentBranchName]?.Tip;
                if (tip == null)
                    return Enumerable.Empty<string>();

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
        }
        public sealed override Task<RaftRepository> GetProjectScopedRaftRepositoryAsync(string project)
        {
            var instance = this.Clone();
            if (!string.IsNullOrEmpty(project))
            {
                var projectPath = string.Join("/", project.Split(new[] { '/', '\\' }).Select(p => "projects/" + p));

                if (!string.IsNullOrEmpty(instance.RepositoryRoot))
                    instance.RepositoryRoot = PathEx.Combine('/', instance.RepositoryRoot, projectPath);
                else
                    instance.RepositoryRoot = projectPath;
            }

            return Task.FromResult<RaftRepository>(instance);
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
        protected abstract Repository OpenRepository();
        protected abstract GitRaftRepositoryBase CreateCopy();

        private GitRaftRepositoryBase Clone()
        {
            var instance = this.CreateCopy();
            instance.BranchName = this.BranchName;
            instance.ReadOnly = this.ReadOnly;
            instance.RepositoryRoot = this.RepositoryRoot;
            instance.RaftId = this.RaftId;
            instance.RaftName = this.RaftName;
            instance.OpenOptions = this.OpenOptions;
            return instance;
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
        private Stream OpenEntry(TreeEntry entry)
        {
            if (!(entry.Target is Blob blob))
                return null;

            using (var s = blob.GetContentStream())
            {
                var buffer = new SlimMemoryStream();
                s.CopyTo(buffer);
                buffer.Position = 0;
                return buffer;
            }
        }
        private TreeDefinition GetCurrentTree()
        {
            var repo = this.lazyRepository.Value;

            var branch = repo.Branches[this.CurrentBranchName];
            if (branch?.Tip == null)
                return new TreeDefinition();

            return TreeDefinition.From(branch.Tip);
        }
        private TreeEntry FindEntry(string name, string hash = null)
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
        private TreeEntry FindEntry(RaftItemType type, string name, string hash = null) => this.FindEntry(PathEx.Combine('/', GetStandardTypeName(type), name), hash);
        private Dictionary<string, string> GetEnvironmentBranchMap()
        {
            if (string.IsNullOrWhiteSpace(this.EnvironmentBranches))
                return null;

            var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var line in Regex.Split(this.EnvironmentBranches, @"\r?\n"))
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                var parts = line.Trim().Split(new[] { ':' }, 2);
                if (parts.Length != 2)
                    continue;

                var environmentName = parts[0].Trim();
                if (string.IsNullOrEmpty(environmentName))
                    continue;

                var branchName = parts[1].Trim();
                if (string.IsNullOrEmpty(branchName))
                    continue;

                d[environmentName] = branchName;
            }

            return d;
        }

        private sealed class RaftItemStream : Stream
        {
            private Stream wrapped;
            private GitRaftRepositoryBase raft;
            private string path;
            private FileAccess access;
            private bool disposed;
            private bool dirty;

            public RaftItemStream(Stream stream, GitRaftRepositoryBase repo, string path, FileAccess access, bool dirty = false)
            {
                this.wrapped = stream;
                this.raft = repo;
                this.path = path;
                this.access = access;
                this.dirty = dirty;
            }

            public override bool CanRead => (this.access & FileAccess.Read) != 0;
            public override bool CanSeek => this.wrapped.CanSeek;
            public override bool CanWrite => (this.access & FileAccess.Write) != 0;
            public override long Length => this.wrapped.Length;
            public override long Position
            {
                get => this.wrapped.Position;
                set => this.wrapped.Position = value;
            }

            public override void Flush()
            {
                if (this.disposed)
                    throw new ObjectDisposedException(nameof(RaftItemStream));

                this.wrapped.Flush();
            }
            public override int Read(byte[] buffer, int offset, int count)
            {
                if (this.disposed)
                    throw new ObjectDisposedException(nameof(RaftItemStream));
                if (!this.CanRead)
                    throw new NotSupportedException();

                return this.wrapped.Read(buffer, offset, count);
            }
            public override int ReadByte()
            {
                if (this.disposed)
                    throw new ObjectDisposedException(nameof(RaftItemStream));
                if (!this.CanRead)
                    throw new NotSupportedException();

                return this.wrapped.ReadByte();
            }
            public override long Seek(long offset, SeekOrigin origin)
            {
                if (this.disposed)
                    throw new ObjectDisposedException(nameof(RaftItemStream));

                return this.wrapped.Seek(offset, origin);
            }
            public override void SetLength(long value)
            {
                if (this.disposed)
                    throw new ObjectDisposedException(nameof(RaftItemStream));
                if (!this.CanWrite)
                    throw new NotSupportedException();

                this.wrapped.SetLength(value);
            }
            public override void Write(byte[] buffer, int offset, int count)
            {
                if (this.disposed)
                    throw new ObjectDisposedException(nameof(RaftItemStream));
                if (!this.CanWrite)
                    throw new NotSupportedException();

                if (count > 0)
                {
                    this.wrapped.Write(buffer, offset, count);
                    this.dirty = true;
                }
            }
            public override void WriteByte(byte value)
            {
                if (this.disposed)
                    throw new ObjectDisposedException(nameof(RaftItemStream));
                if (!this.CanWrite)
                    throw new NotSupportedException();

                this.wrapped.WriteByte(value);
                this.dirty = true;
            }

            protected override void Dispose(bool disposing)
            {
                if (!this.disposed)
                {
                    if (this.dirty)
                    {
                        this.wrapped.Position = 0;
                        var repo = this.raft.lazyRepository.Value;
                        var blob = repo.ObjectDatabase.CreateBlob(this.wrapped);
                        this.raft.lazyCurrentTree.Value.Add(this.path, blob, Mode.NonExecutableFile);
                        this.raft.Dirty = true;
                    }

                    this.wrapped.Dispose();
                    this.disposed = true;
                }

                base.Dispose(disposing);
            }
        }
    }
}
