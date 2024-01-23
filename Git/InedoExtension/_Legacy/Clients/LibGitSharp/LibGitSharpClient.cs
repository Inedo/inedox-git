using Inedo.Diagnostics;
using Inedo.ExecutionEngine.Executer;
using Inedo.IO;
using LibGit2Sharp;

namespace Inedo.Extensions.Clients.LibGitSharp
{
    internal sealed class LibGitSharpClient : GitClient
    {
        private static Task Complete => Task.FromResult<object>(null);
        private static readonly LfsFilter lfsFilter = new LfsFilter("lfs", new[] { new FilterAttributeEntry("lfs") });

        static LibGitSharpClient()
        {
            if (!GlobalSettings.GetRegisteredFilters().Any(f => f.Name == "lfs"))
            {
                GlobalSettings.RegisterFilter(lfsFilter);
            }
        }

        public LibGitSharpClient(GitRepositoryInfo repository, ILogSink log)
            : base(repository, log)
        {
        }

        public override Task CloneAsync(GitCloneOptions options)
        {
            try
            {
                this.BeginOperation();

                this.log.LogDebug($"Cloning '{this.repository.RemoteRepositoryUrl}' into '{this.repository.LocalRepositoryPath}'...");
                this.log.LogDebug("Clone options: " + options);
                try
                {
                    var cloneOptions = new CloneOptions { BranchName = options.Branch, RecurseSubmodules = options.RecurseSubmodules };
                    cloneOptions.FetchOptions.CredentialsProvider = this.CredentialsHandler;

                    Repository.Clone(
                        this.repository.RemoteRepositoryUrl,
                        this.repository.LocalRepositoryPath,
                        cloneOptions
                    );
                }
                catch (Exception ex)
                {
                    // gitsharp exceptions are not always serializable
                    throw new ExecutionFailureException("Clone failed: " + ex.Message);
                }

                return Complete;
            }
            finally
            {
                this.EndOperation();
            }
        }

        public override Task<IEnumerable<RemoteBranchInfo>> EnumerateRemoteBranchesAsync()
        {
            bool endOperation = false;
            try
            {
                this.log.LogDebug("Enumerating remote branches...");

                if (!this.repository.HasLocalRepository || !Repository.IsValid(this.repository.LocalRepositoryPath))
                {
                    if (this.repository.HasLocalRepository)
                        this.log.LogDebug($"Repository not found at '{this.repository.LocalRepositoryPath}'...");

                    if (this.repository.HasLocalRepository && DirectoryEx.Exists(this.repository.LocalRepositoryPath))
                    {
                        var contents = DirectoryEx.GetFileSystemInfos(this.repository.LocalRepositoryPath, MaskingContext.Default);
                        if (contents.Count > 0)
                            throw new InvalidOperationException("Specified local repository path is invalid.");
                    }

                    var refs = Repository.ListRemoteReferences(this.repository.RemoteRepositoryUrl, this.CredentialsHandler);
                    return Task.FromResult(getBranches(refs));
                }
                else
                {
                    this.BeginOperation();
                    endOperation = true;

                    this.log.LogDebug($"Repository found at '{this.repository.LocalRepositoryPath}'...");
                    using (var repository = new Repository(this.repository.LocalRepositoryPath))
                    {
                        var origin = repository.Network.Remotes["origin"];
                        this.log.LogDebug($"Using remote: origin, '{origin.Name}'.");
                        var refs = repository.Network.ListReferences(origin);
                        return Task.FromResult(getBranches(refs));
                    }
                }
            }
            catch (Exception ex)
            {
                // gitsharp exceptions are not always serializable
                throw new ExecutionFailureException(ex.Message);
            }
            finally
            {
                if (endOperation)
                    this.EndOperation();
            }

            IEnumerable<RemoteBranchInfo> getBranches(IEnumerable<Reference> refs)
            {
                var branches = new HashSet<RemoteBranchInfo>();

                foreach (var r in refs)
                {
                    var direct = r.ResolveToDirectReference();
                    if (direct.CanonicalName.StartsWith("refs/heads/"))
                        branches.Add(new RemoteBranchInfo(direct.CanonicalName.Substring("refs/heads/".Length), direct.TargetIdentifier));
                }

                return branches.ToArray();
            }
        }

        public override Task<bool> IsRepositoryValidAsync()
        {
            try
            {
                this.BeginOperation();

                return Task.FromResult(Repository.IsValid(this.repository.LocalRepositoryPath));
            }
            finally
            {
                this.EndOperation();
            }
        }

        public override Task TagAsync(string tag, string commit, string message, bool force = false)
        {
            try
            {
                this.BeginOperation();

                this.log.LogDebug($"Using repository at '{this.repository.LocalRepositoryPath}'...");
                using (var repository = new Repository(this.repository.LocalRepositoryPath))
                {
                    Tag createdTag;
                    if (string.IsNullOrEmpty(message))
                    {
                        // lightweight tag
                        if (string.IsNullOrEmpty(commit))
                        {
                            this.log.LogDebug($"Creating lightweight tag \"{tag}\"...");
                            createdTag = repository.Tags.Add(tag, repository.Head.Tip, force);
                        }
                        else
                        {
                            this.log.LogDebug($"Creating lightweight tag \"{tag}\" for commit {commit}...");
                            createdTag = repository.Tags.Add(tag, commit, force);
                        }
                    }
                    else
                    {
                        var signature = repository.Config.BuildSignature(DateTimeOffset.Now);
                        if (signature == null)
                        {
                            var hostname = "example.com";
                            if (Uri.TryCreate(SDK.BaseUrl, UriKind.Absolute, out var baseUrl))
                                hostname = baseUrl.Host;

                            signature = new Signature(SDK.ProductName, SDK.ProductName.ToLower() + "@" + hostname, DateTimeOffset.Now);

                            var gitConfigPath = PathEx.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".gitconfig");
                            this.log.LogWarning($"git is not configured on this server. Using default values for name and email.");
                            this.log.LogDebug($"To change these values, {(FileEx.Exists(gitConfigPath) ? "edit" : "create")} '{gitConfigPath}' {(FileEx.Exists(gitConfigPath) ? "to add" : "with")} contents similar to the following:");
                            this.log.LogDebug($"[user]\n\tname = {signature.Name}\n\temail = {signature.Email}");
                        }

                        // annotated tag
                        if (string.IsNullOrEmpty(commit))
                        {
                            this.log.LogDebug($"Creating annotated tag \"{tag}\" with message \"{message}\"...");
                            createdTag = repository.Tags.Add(tag, repository.Head.Tip, signature, message, force);
                        }
                        else
                        {
                            this.log.LogDebug($"Creating annotated tag \"{tag}\" for commit {commit} with message \"{message}\"...");
                            createdTag = repository.Tags.Add(tag, commit, signature, message, force);
                            
                        }
                    }

                    this.log.LogDebug($"Pushing '{createdTag.CanonicalName}' to remote 'origin'...");

                    var pushRef = $"{createdTag.CanonicalName}:{createdTag.CanonicalName}";

                    repository.Network.Push(
                        repository.Network.Remotes["origin"],
                        force ? '+' + pushRef : pushRef,
                        new PushOptions { CredentialsProvider = this.CredentialsHandler }
                    );
                }

                return Complete;
            }
            catch (Exception ex)
            {
                // gitsharp exceptions are not always serializable
                throw new ExecutionFailureException("Tag failed: " + ex.Message);
            }
            finally
            {
                this.EndOperation();
            }
        }

        public override Task<string> UpdateAsync(GitUpdateOptions options)
        {
            try
            {
                this.log.LogDebug($"Using repository at '{this.repository.LocalRepositoryPath}'...");
                using (var repository = new Repository(this.repository.LocalRepositoryPath))
                {
                    this.log.LogDebug("Fetching commits from origin...");
                    Commands.Fetch(repository, "origin", new string[0], new FetchOptions { CredentialsProvider = this.CredentialsHandler, Prune = true }, null);
                    var refName = "FETCH_HEAD";
                    if (options.Branch != null)
                        refName = "origin/" + options.Branch;
                    else if (options.Ref != null)
                        refName = options.Ref;
                    this.log.LogDebug($"Resetting the index and working tree to {refName}...");
                    repository.Reset(ResetMode.Hard, refName);
                    repository.RemoveUntrackedFiles();

                    if (options.RecurseSubmodules)
                    {
                        foreach (var submodule in repository.Submodules)
                        {
                            var o = new SubmoduleUpdateOptions { Init = true };
                            o.FetchOptions.CredentialsProvider = this.CredentialsHandler;
                            repository.Submodules.Update(submodule.Name, o);
                        }
                    }

                    return Task.FromResult(repository.Head?.Tip?.Sha);
                }
            }
            catch (Exception ex)
            {
                // gitsharp exceptions are not always serializable
                throw new ExecutionFailureException("Update failed: " + ex.Message);
            }
            finally
            {
                this.EndOperation();
            }
        }

        public override Task ArchiveAsync(string targetDirectory, bool keepInternals = false)
        {
            return CopyFilesAsync(new FileArchiver(targetDirectory), this.repository.LocalRepositoryPath, targetDirectory, keepInternals);
        }

        public override Task<IReadOnlyList<string>> ListRepoFilesAsync()
        {
            try
            {
                this.BeginOperation();

                using (var repository = new Repository(this.repository.LocalRepositoryPath))
                {
                    return Task.FromResult<IReadOnlyList<string>>(repository.Index.Select(e => e.Path).ToArray());
                }
            }
            catch (Exception ex)
            {
                // gitsharp exceptions are not always serializable
                throw new ExecutionFailureException("Listing repository files failed: " + ex.Message);
            }
            finally
            {
                this.EndOperation();
            }
        }

        public override Task<DateTimeOffset?> GetFileLastModifiedAsync(string fileName)
        {
            try
            {
                this.BeginOperation();

                var slashFileName = fileName.Replace('\\', '/');
                using (var repository = new Repository(this.repository.LocalRepositoryPath))
                {
                    var commit = repository.Head.Commits.First(c =>
                    {
                        var cId = c.Tree[slashFileName]?.Target?.Sha;
                        var pId = c.Parents?.FirstOrDefault()?[slashFileName]?.Target?.Sha;
                        return cId != pId;
                    });

                    return Task.FromResult(commit?.Author.When);
                }
            }
            catch (Exception ex)
            {
                // gitsharp exceptions are not always serializable
                throw new ExecutionFailureException("Getting last modified time failed: " + ex.Message);
            }
            finally
            {
                this.EndOperation();
            }
        }

        private LibGit2Sharp.Credentials CredentialsHandler(string url, string usernameFromUrl, SupportedCredentialTypes types)
        {
            if (string.IsNullOrEmpty(this.repository.UserName))
            {
                if (types.HasFlag(SupportedCredentialTypes.Default))
                {
                    this.log.LogDebug($"Connecting with default credentials...");
                    return new DefaultCredentials();
                }
                else
                {
                    this.log.LogDebug($"Connecting anonymously...");
                    return null;
                }
            }
            else
            {
                this.log.LogDebug($"Connecting as user '{this.repository.UserName}'...");
                return new SecureUsernamePasswordCredentials
                {
                    Username = this.repository.UserName,
                    Password = this.repository.Password
                };
            }
        }

        private void BeginOperation()
        {
            lock (lfsFilter.Repositories)
            {
                lfsFilter.Repositories.Add(this.repository);
            }
        }

        private void EndOperation()
        {
            lock (lfsFilter.Repositories)
            {
                lfsFilter.Repositories.Remove(this.repository);
            }
        }
    }
}
