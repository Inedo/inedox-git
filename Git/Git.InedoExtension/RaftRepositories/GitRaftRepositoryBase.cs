using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Inedo.Agents;
using Inedo.Data;
using Inedo.Diagnostics;
using Inedo.Documentation;
using Inedo.ExecutionEngine;
using Inedo.Extensibility;
using Inedo.Extensibility.RaftRepositories;
using Inedo.Extensibility.UserDirectories;
using Inedo.Extensions.Clients;
using Inedo.Extensions.Clients.CommandLine;
using Inedo.Extensions.Clients.LibGitSharp;
using Inedo.IO;
using Inedo.Serialization;
using LibGit2Sharp;

namespace Inedo.Extensions.Git.RaftRepositories
{
    public abstract class GitRaftRepositoryBase : RaftRepository, ILogSink
    {
        private readonly Lazy<Repository> lazyRepository;
        private readonly Lazy<Dictionary<RuntimeVariableName, string>> lazyVariables;
        private readonly Lazy<TreeDefinition> lazyCurrentTree;
        private readonly Lazy<GitClient> lazyClient;
        private bool variablesDirty;
        private bool disposed;

        protected GitRaftRepositoryBase()
        {
            this.lazyRepository = new Lazy<Repository>(this.OpenRepository);
            this.lazyClient = new Lazy<GitClient>(this.GetLocalGitClient);
            this.lazyVariables = new Lazy<Dictionary<RuntimeVariableName, string>>(this.ReadVariables);
            this.lazyCurrentTree = new Lazy<TreeDefinition>(this.GetCurrentTree);
        }

        public abstract string LocalRepositoryPath { get; }
        public abstract GitRepositoryInfo RepositoryInfo { get; }

        [Required]
        [Persistent]
        [DisplayName("Branch")]
        public string BranchName { get; set; } = "master";

        [Persistent]
        [DisplayName("Read only")]
        public bool ReadOnly { get; set; }

        public sealed override bool IsReadOnly => this.ReadOnly || this.OpenOptions.HasFlag(OpenRaftOptions.ReadOnly);
        public sealed override bool SupportsVersioning => true;

        private protected bool Dirty { get; private set; }
        private protected Repository Repo => this.lazyRepository.Value;
        private protected GitClient Client => this.lazyClient.Value;
        private Dictionary<RuntimeVariableName, string> Variables => this.lazyVariables.Value;
        private TreeDefinition CurrentTree  => this.lazyCurrentTree.Value;

        private bool OptimizeLoadTime => this.OpenOptions.HasFlag(OpenRaftOptions.OptimizeLoadTime);
        private string RepositoryRoot { get; set; }

        public void Log(IMessage message)
        {
#if !DEBUG
            if (message.Level > MessageLevel.Debug)
#endif
            {
                Logger.Log(message.Level, message.Message, $"'{this.RaftName}' Raft", message.Details);
            }
        }

        public sealed override Task<IEnumerable<RaftItem>> GetRaftItemsAsync()
        {
            if (this.disposed)
                throw new ObjectDisposedException(nameof(GitRaftRepositoryBase));

            return Task.FromResult(Enum.GetValues(typeof(RaftItemType)).Cast<RaftItemType>().SelectMany(this.GetRaftItems));
        }

        public override Task<IEnumerable<RaftItem>> GetRaftItemsAsync(RaftItemType type)
        {
            if (this.disposed)
                throw new ObjectDisposedException(nameof(GitRaftRepositoryBase));

            return Task.FromResult(this.GetRaftItems(type));
        }
        private IEnumerable<RaftItem> GetRaftItems(RaftItemType type)
        {
            var branch = this.Repo.Branches[this.BranchName];
            // If we don't have a branch, there are no items in it.
            if (branch?.Tip?.Tree == null)
                yield break;

            string typeName;
            try
            {
                typeName = GetStandardTypeName(type);
            }
            catch
            {
                // Not supported by this product (for example, when called from GetRaftItemsAsync()).
                yield break;
            }

            var itemType = branch.Tip.Tree[AH.ConcatNE(this.RepositoryRoot, "/") + typeName];
            // If we don't have a directory, there are no items in it.
            if (itemType?.TargetType != TreeEntryTargetType.Tree)
                yield break;

            foreach (var entry in (Tree)itemType.Target)
            {
                // If it's not a file, it's not an item.
                if (entry.TargetType != TreeEntryTargetType.Blob)
                    continue;

                Commit commit = null;
                if (!this.OptimizeLoadTime)
                {
                    // Commit may still be null if the repository is a shallow clone.
                    commit = this.Repo.Commits.QueryBy(
                        entry.Path,
                        new CommitFilter { IncludeReachableFrom = branch }
                    ).FirstOrDefault()?.Commit;
                }

                yield return MakeRaftItem(type, entry.Name, (Blob)entry.Target, commit);
            }
        }

        private RaftItem MakeRaftItem(RaftItemType type, string name, Blob blob, Commit commit = null)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentNullException(nameof(name), "missing name for Git raft item");
            if (blob == null)
                throw new ArgumentNullException(nameof(blob), "cannot make Git raft item from nonexistent file");

            return new RaftItem(type, name, commit?.Committer?.When ?? DateTimeOffset.MinValue, AH.CoalesceString(commit?.Committer?.Name, "(unknown)"), blob.Size, commit?.Sha);
        }

        private bool FindEntry(RaftItemType? type, string name, string version, out string path, out Blob blob, out Commit commit)
        {
            if (this.disposed)
                throw new ObjectDisposedException(nameof(GitRaftRepositoryBase));
            if (string.IsNullOrEmpty(name))
                throw new ArgumentNullException(nameof(name), "raft item name is empty");

            EnsureRelativePath(name);
            if (PathEx.GetFileName(name) != name)
                throw new NotSupportedException($"Git raft item names cannot contain directory separators: {name}");

            var itemPath = name;
            if (type.HasValue)
                itemPath = PathEx.Combine('/', GetStandardTypeName(type.Value), itemPath);
            if (!string.IsNullOrEmpty(this.RepositoryRoot))
                itemPath = PathEx.Combine('/', this.RepositoryRoot, itemPath);

            path = itemPath;
            commit = null;
            blob = null;

            var branch = this.Repo.Branches[this.BranchName];
            // If the branch doesn't exist, there are no items.
            if (branch?.Tip?.Tree == null)
                return false;

            if (string.IsNullOrEmpty(version))
            {
                var currentEntry = branch.Tip.Tree[itemPath];
                // Make sure the item exists in the latest version of the raft.
                if (currentEntry?.TargetType != TreeEntryTargetType.Blob)
                    return false;

                if (!this.OptimizeLoadTime)
                {
                    // Commit can still be null if this is a shallow clone.
                    commit = this.Repo.Commits.QueryBy(
                        itemPath,
                        new CommitFilter
                        {
                            IncludeReachableFrom = branch
                        }
                    ).FirstOrDefault()?.Commit;
                }
            }
            else
            {
                commit = this.Repo.Lookup<Commit>(new ObjectId(version));
                if (commit == null || !branch.Commits.Contains(commit))
                    return false;
            }

            var entry = (commit ?? branch.Tip).Tree[itemPath];
            // If the entry doesn't exist or is not a file, there is no raft item.
            if (entry?.TargetType != TreeEntryTargetType.Blob)
                return false;
            blob = (Blob)entry.Target;

            // Sanity check: make sure the commit actually changed the file.
            if (commit != null && (commit.Parents.Any() && commit.Parents.All(p => p.Tree[itemPath]?.Equals(entry) ?? false)))
                return false;

            return true;
        }

        public override Task<RaftItem> GetRaftItemAsync(RaftItemType type, string name, string version)
        {
            if (this.FindEntry(type, name, version, out var _, out var blob, out var commit))
            {
                return Task.FromResult(MakeRaftItem(type, name, blob, commit));
            }
            return Task.FromResult<RaftItem>(null);
        }

        public sealed override Task<Stream> OpenRaftItemAsync(RaftItemType type, string name, FileMode fileMode, FileAccess fileAccess, string version)
        {
            if (this.disposed)
                throw new ObjectDisposedException(nameof(GitRaftRepositoryBase));
            if (this.ReadOnly && fileAccess.HasFlag(FileAccess.Write))
                throw new NotSupportedException("Cannot open raft item for writing: Raft is read-only.");
            if (!string.IsNullOrEmpty(version) && fileAccess.HasFlag(FileAccess.Write))
                throw new NotSupportedException("Cannot write to specific version of Git raft item.");

            var exists = this.FindEntry(type, name, version, out var itemPath, out var blob, out var commit);

            bool dirty = false;
            Stream stream;
            switch (fileMode)
            {
                case FileMode.CreateNew:
                    if (exists)
                        throw new InvalidOperationException($"A {type} named \"{name}\" already exists in this raft.");
                    goto case FileMode.Create;

                case FileMode.Create:
                    if (!fileAccess.HasFlag(FileAccess.Write))
                        throw new ArgumentOutOfRangeException(nameof(fileAccess), fileAccess, "Cannot create a file without write access.");

                    dirty = true;
                    stream = new SlimMemoryStream();
                    break;

                case FileMode.Open:
                    if (!exists)
                        return Task.FromResult<Stream>(null);

                    stream = this.OpenBlob(blob, itemPath);
                    break;

                case FileMode.OpenOrCreate:
                    if (!exists)
                        goto case FileMode.Create;

                    goto case FileMode.Open;

                case FileMode.Truncate:
                    if (!exists)
                        return Task.FromResult<Stream>(null);

                    if (!fileAccess.HasFlag(FileAccess.Write))
                        throw new ArgumentOutOfRangeException(nameof(fileAccess), fileAccess, "Cannot truncate a file without write access.");

                    dirty = true;
                    stream = new SlimMemoryStream();
                    break;

                case FileMode.Append:
                    if (!fileAccess.HasFlag(FileAccess.Write))
                        throw new ArgumentOutOfRangeException(nameof(fileAccess), fileAccess, "Cannot append to a file without write access.");

                    if (exists)
                    {
                        stream = this.OpenBlob(blob, itemPath);
                        stream.Seek(0, SeekOrigin.End);
                    }
                    else
                    {
                        dirty = true;
                        stream = new SlimMemoryStream();
                    }
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(fileMode), fileMode, "Unsupported FileMode: " + fileMode);
            }

            return Task.FromResult<Stream>(new RaftItemStream(stream, this, itemPath, fileAccess, dirty));
        }
        public sealed override Task DeleteRaftItemAsync(RaftItemType type, string name)
        {
            if (this.disposed)
                throw new ObjectDisposedException(nameof(GitRaftRepositoryBase));
            if (this.ReadOnly)
                throw new NotSupportedException("Cannot delete items from a read-only Git raft.");

            if (this.FindEntry(type, name, null, out var itemPath, out var _, out var _))
            {
                this.CurrentTree.Remove(itemPath);
                this.Dirty = true;
            }

            return InedoLib.NullTask;
        }

        public override Task<IEnumerable<RaftItemVersion>> GetRaftItemVersionsAsync(RaftItemType type, string name)
        {
            if (this.disposed)
                throw new ObjectDisposedException(nameof(GitRaftRepositoryBase));

            if (this.FindEntry(type, name, null, out var itemPath, out var _, out var _))
            {
                var branch = this.Repo.Branches[this.BranchName];
                return Task.FromResult(from log in this.Repo.Commits.QueryBy(itemPath, new CommitFilter { IncludeReachableFrom = branch })
                                       let commit = log.Commit
                                       where commit != null
                                       select new RaftItemVersion(commit.Sha, commit.Committer?.When, AH.CoalesceString(commit.Committer?.Name, "(unknown)")));
            }

            return Task.FromResult(Enumerable.Empty<RaftItemVersion>());
        }

        public sealed override Task<IReadOnlyDictionary<RuntimeVariableName, string>> GetVariablesAsync()
        {
            if (this.disposed)
                throw new ObjectDisposedException(nameof(GitRaftRepositoryBase));

            return Task.FromResult<IReadOnlyDictionary<RuntimeVariableName, string>>(new Dictionary<RuntimeVariableName, string>(this.Variables));
        }
        public sealed override Task SetVariableAsync(RuntimeVariableName name, string value)
        {
            if (this.disposed)
                throw new ObjectDisposedException(nameof(GitRaftRepositoryBase));
            if (name == null)
                throw new ArgumentNullException(nameof(name));
            if (this.ReadOnly)
                throw new NotSupportedException();

            var vars = this.Variables;
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

            bool deleted = this.Variables.Remove(name);
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

                var tree = this.Repo.ObjectDatabase.CreateTree(this.CurrentTree);

                var branch = this.Repo.Branches[this.BranchName];
                var parentCommit = branch?.Tip ?? this.Repo.Head.Tip;
                var parentTree = parentCommit?.Tree;

                var message = GetCommitMessage(this.Repo.Diff.Compare<TreeChanges>(parentTree, tree));

                var commit = this.Repo.ObjectDatabase.CreateCommit(signature, signature, message, tree, new[] { parentCommit }.Where(c => c != null), false);

                if (branch == null)
                    branch = this.Repo.Branches.Add(this.BranchName, commit);
                else
                    this.Repo.Refs.UpdateTarget(branch.Reference, commit.Id);

                this.Repo.Refs.UpdateTarget(this.Repo.Refs.Head, this.Repo.Refs["refs/heads/" + this.BranchName]);
            }

            return InedoLib.NullTask;
        }

        private static readonly ChangeKind[] interestingChanges = new[] { ChangeKind.Added, ChangeKind.Copied, ChangeKind.Deleted, ChangeKind.Modified, ChangeKind.Renamed };

        private static string GetCommitMessage(TreeChanges treeChanges)
        {
            var changes = from tc in treeChanges
                          group tc by tc.Status into status
                          where interestingChanges.Contains(status.Key)
                          select new
                          {
                              change = status.Key,
                              files = from c in status
                                      select new
                                      {
                                          oldPath = parsePath(c.OldPath),
                                          newPath = parsePath(c.Path)
                                      }
                          };

            switch (changes.Count())
            {
                case 0:
                    return $"Updated by {SDK.ProductName}.\n";

                case 1:
                    var change = changes.First();
                    if (change.files.Count() != 1)
                        goto default;

                    var file = change.files.First();
                    return $"{formatChange(change.change, file.oldPath, file.newPath)} with {SDK.ProductName}\n";

                default:
                    return $"{summarizeChanges()} with {SDK.ProductName}\n\n{string.Join("\n", changes.SelectMany(c => c.files.Select(f => formatChange(c.change, f.oldPath, f.newPath))))}\n";
            }

            string formatChange(ChangeKind change, (string[] project, RaftItemType? type, string name) oldPath, (string[] project, RaftItemType? type, string name) newPath)
            {
                if (oldPath != (null, null, null) && newPath != (null, null, null)
                    && ((oldPath.project == null ? newPath.project != null : newPath.project?.SequenceEqual(oldPath.project) ?? true)
                    || oldPath.type != newPath.type || oldPath.name != newPath.name))
                {
                    return $"{change} {formatPath(oldPath, newPath)} to {formatPath(newPath, oldPath)}";
                }

                if (newPath == (null, null, null))
                {
                    newPath = oldPath;
                }

                return $"{change} {formatPath(newPath, (null, null, null))}";
            }

            string formatPath((string[] project, RaftItemType? type, string name) path, (string[] project, RaftItemType? type, string name) reference)
            {
                string formatted = path.name ?? "(unknown)";
                if (path.project != null && (reference.project == null || !path.project.SequenceEqual(reference.project)))
                    formatted = $"{formatted} (project {string.Join("/", path.project)})";
                else if (path.project == null && reference.project != null)
                    formatted = $"{formatted} (global)";

                if (path.type != reference.type)
                    formatted = $"{(path.type?.ToString() ?? "generic")} {formatted}";

                return formatted;
            }

            string summarizeChanges()
            {
                var summaries = from c in changes
                                let summary = from f in c.files
                                              group f by (f.newPath.type ?? f.oldPath.type)?.ToString() ?? f.newPath.name ?? f.oldPath.name into ft
                                              let count = ft.Count()
                                              select count == 1 ? ft.Key : $"{ft.Key} \u00d7{count}"
                                select $"{c.change} {formatList(summary)}";

                return formatList(summaries.Take(1).Concat(summaries.Skip(1).Select(s => s.ToLowerInvariant())));
            }

            string formatList(IEnumerable<string> list)
            {
                switch (list.Count())
                {
                    case 0:
                    case 1:
                        return list.FirstOrDefault();
                    case 2:
                        return string.Join(" and ", list);
                    default:
                        var sep = list.Any(s => s.Contains(",")) ? "; " : ", ";
                        return string.Join(sep, list.Take(list.Count() - 1).Concat(new[] { "and " + list.Last() }));
                }
            }

            (string[] project, RaftItemType? type, string name) parsePath(string path)
            {
                if (string.IsNullOrEmpty(path))
                    return (null, null, null);

                path = path.Replace('\\', '/');
                string[] project = null;
                if (path.StartsWith("project/"))
                {
                    var parts = path.Substring("project/".Length).Split(new[] { "/project/" }, StringSplitOptions.None);
                    project = new string[parts.Length];
                    for (int i = 0; i < parts.Length - 1; i++)
                    {
                        // Invalid path.
                        if (string.IsNullOrEmpty(parts[i]) || parts[i].Contains("/"))
                            return (null, null, null);

                        project[i] = parts[i];
                    }
                    path = parts[parts.Length - 1];

                    // Invalid path.
                    if (string.IsNullOrEmpty(path))
                        return (null, null, null);

                    var slash = path.IndexOf('/');
                    if (slash == -1)
                    {
                        project[parts.Length - 1] = path;
                        return (project, null, null);
                    }

                    project[parts.Length - 1] = path.Substring(0, slash);
                    path = path.Substring(slash + 1);
                }

                var typeName = path.Split('/');
                if (typeName.Length == 1)
                    return (project, null, typeName[0]);

                // Invalid path.
                if (typeName.Length != 2)
                    return (null, null, null);

                var type = TryParseStandardTypeName(typeName[0]);
                // Invalid path.
                if (!type.HasValue)
                    return (null, null, null);

                return (project, type, typeName[1]);
            }
        }

        public sealed override Task<IEnumerable<string>> GetProjectsAsync(bool recursive)
        {
            return Task.FromResult(inner());

            IEnumerable<string> inner()
            {
                var repo = this.Repo;

                var tip = repo.Branches[this.BranchName]?.Tip;
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
                        if (subtree.TargetType != TreeEntryTargetType.Tree)
                            continue;

                        var current = string.IsNullOrEmpty(currentProject) ? subtree.Name : (currentProject + "/" + subtree.Name);
                        yield return current;

                        if (recursive)
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
                        this.Repo.Dispose();
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
            if (this.FindEntry(null, "variables", null, out var itemPath, out var blob, out var _))
            {
                var variables = ReadStandardVariableData(new StringReader(blob.GetContentText(new FilteringOptions(itemPath), InedoLib.UTF8Encoding)));
                return variables.ToDictionary(kv => kv.Key, kv => kv.Value);
            }
            return new Dictionary<RuntimeVariableName, string>();
        }
        private void SaveVariables()
        {
            var repo = this.Repo;

            using (var buffer = new SlimMemoryStream())
            {
                var writer = new StreamWriter(buffer, InedoLib.UTF8Encoding);
                WriteStandardVariableData(this.Variables, writer);
                writer.Flush();

                buffer.Position = 0;

                var blob = repo.ObjectDatabase.CreateBlob(buffer, "variables");

                this.CurrentTree.Add(string.IsNullOrEmpty(this.RepositoryRoot) ? "variables" : PathEx.Combine('/', this.RepositoryRoot, "variables"), blob, Mode.NonExecutableFile);

                this.Dirty = true;
            }
        }
        private Stream OpenBlob(Blob blob, string itemPath)
        {
            using (var stream = blob.GetContentStream(new FilteringOptions(itemPath)))
            {
                var temp = TemporaryStream.Create(blob.Size);
                try
                {
                    stream.CopyToAsync(temp);
                    temp.Position = 0;
                    return temp;
                }
                catch
                {
                    try { temp.Dispose(); } catch { }
                    throw;
                }
            }
        }
        private TreeDefinition GetCurrentTree()
        {
            var repo = this.Repo;

            var branch = repo.Branches[this.BranchName];
            if (branch?.Tip == null)
                return new TreeDefinition();

            return TreeDefinition.From(branch.Tip);
        }

        private GitClient GetLocalGitClient()
        {
            try
            {
                // Hopefully this will be added to the SDK so it's less hacky, but this works on BuildMaster, Otter, and Hedgehog currently.
                var db = Type.GetType($"Inedo.{SDK.ProductName}.Data.DB,{SDK.ProductName}CoreEx") ?? Type.GetType($"Inedo.{SDK.ProductName}.Data.DB,{SDK.ProductName}");
                var serversGetServers = db.GetMethod("Servers_GetServers", BindingFlags.Static | BindingFlags.Public, Type.DefaultBinder, new[] { typeof(YNIndicator) }, new ParameterModifier[0]);

                var variablesGetVariablesAccessibleFromScope = db.GetMethod("Variables_GetVariablesAccessibleFromScope", BindingFlags.Static|BindingFlags.Public);
                var serverIdParamIndex = variablesGetVariablesAccessibleFromScope.GetParameters().TakeWhile(p => p.Name != "Server_Id").Count();
                var expandRolesAndEnvironmentsIndex = variablesGetVariablesAccessibleFromScope.GetParameters().TakeWhile(p => p.Name != "ExpandRolesAndEnvironments_Indicator").Count();
                var includeSystemVariablesIndex = variablesGetVariablesAccessibleFromScope.GetParameters().TakeWhile(p => p.Name != "IncludeSystemVariables_Indicator").Count();

                var servers = (IList)serversGetServers.Invoke(null, new object[] { (YNIndicator)false }); // IncludeInactive_Indicator
                if (servers.Count != 0)
                {
                    var serverType = servers[0].GetType();
                    var agentConfiguration = serverType.GetProperty("Agent_Configuration");
                    var serverId = serverType.GetProperty("Server_Id");
                    var localServer = servers.Cast<object>().Where(server =>
                    {
                        var configXml = (string)agentConfiguration.GetValue(server);
                        return configXml.StartsWith($@"<Inedo.{SDK.ProductName}.Extensibility.Agents.Local.LocalAgent Assembly=""{SDK.ProductName}Extensions"">") ||
                            configXml.StartsWith($@"<Inedo.{SDK.ProductName}.Extensions.Agents.Local.LocalAgent Assembly=""{SDK.ProductName}CoreEx"">");
                    }).SingleOrDefault();

                    if (localServer != null)
                    {
                        var args = new object[variablesGetVariablesAccessibleFromScope.GetParameters().Length];
                        args[serverIdParamIndex] = (int?)serverId.GetValue(localServer);
                        args[expandRolesAndEnvironmentsIndex] = (YNIndicator)true;
                        args[includeSystemVariablesIndex] = (YNIndicator)true;
                        var variables = (IList)variablesGetVariablesAccessibleFromScope.Invoke(null, args);
                        if (variables.Count != 0)
                        {
                            var variableType = variables[0].GetType();
                            var variableName = variableType.GetProperty("Variable_Name");
                            var variableValue = variableType.GetProperty("Variable_Value");
                            var defaultGitExePath = variables.Cast<object>().FirstOrDefault(v => string.Equals((string)variableName.GetValue(v), "DefaultGitExePath", StringComparison.OrdinalIgnoreCase));
                            if (defaultGitExePath != null)
                            {
                                var gitExePath = InedoLib.UTF8Encoding.GetString((byte[])variableValue.GetValue(defaultGitExePath));
                                var localExecuter = new LocalExecuter();
                                return new GitCommandLineClient(gitExePath, localExecuter, localExecuter, this.RepositoryInfo, this, CancellationToken.None);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                this.LogInformation("Exception thrown when attempting to find local server for Git raft", ex.ToString());

                // use LibGit2Sharp as a safe fallback.
            }

            return new LibGitSharpClient(this.RepositoryInfo, this);
        }
        private sealed class LocalExecuter : LocalFileOperationsExecuter, IRemoteProcessExecuter
        {
            public override string GetBaseWorkingDirectory() => SDK.GetCommonTempPath();

            IRemoteProcess IRemoteProcessExecuter.CreateProcess(RemoteProcessStartInfo startInfo) => new LocalProcess(startInfo);

            Task<string> IRemoteProcessExecuter.GetEnvironmentVariableValueAsync(string name)
            {
                if (string.IsNullOrEmpty(name))
                    throw new ArgumentNullException(nameof(name));

                return Task.FromResult(Environment.GetEnvironmentVariable(name));
            }
        }
        public abstract override RichDescription GetDescription();
        public abstract override Task<ConfigurationTestResult> TestConfigurationAsync();

        private sealed class RaftItemStream : Stream
        {
            private readonly Stream wrapped;
            private readonly GitRaftRepositoryBase raft;
            private readonly string path;
            private readonly FileAccess access;
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

            public override bool CanRead => this.access.HasFlag(FileAccess.Read);
            public override bool CanSeek => this.wrapped.CanSeek;
            public override bool CanWrite => this.access.HasFlag(FileAccess.Write);
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
                        this.raft.CurrentTree.Add(this.path, blob, Mode.NonExecutableFile);
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
