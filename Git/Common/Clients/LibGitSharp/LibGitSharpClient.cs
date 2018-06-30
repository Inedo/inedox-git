using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Inedo.Diagnostics;
using Inedo.ExecutionEngine.Executer;
using Inedo.IO;
using LibGit2Sharp;

namespace Inedo.Extensions.Clients.LibGitSharp
{
    public sealed class LibGitSharpClient : GitClient
    {
        private static Task Complete => Task.FromResult<object>(null);
        private static readonly LfsFilter lfsFilter = new LfsFilter("lfs", new[] { new FilterAttributeEntry("lfs") });

        static LibGitSharpClient()
        {
            GlobalSettings.RegisterFilter(lfsFilter);
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
                    Repository.Clone(
                        this.repository.RemoteRepositoryUrl,
                        this.repository.LocalRepositoryPath,
                        new CloneOptions
                        {
                            BranchName = options.Branch,
                            CredentialsProvider = this.CredentialsHandler,
                            RecurseSubmodules = options.RecurseSubmodules
                        }
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

        public override Task<IEnumerable<string>> EnumerateRemoteBranchesAsync()
        {
            try
            {
                this.BeginOperation();

                this.log.LogDebug("Enumerating remote branches...");

                if (!Repository.IsValid(this.repository.LocalRepositoryPath))
                {
                    this.log.LogDebug($"Repository not found at '{this.repository.LocalRepositoryPath}'...");
                    if (DirectoryEx.Exists(this.repository.LocalRepositoryPath))
                    {
                        var contents = DirectoryEx.GetFileSystemInfos(this.repository.LocalRepositoryPath, MaskingContext.Default);
                        if (contents.Count > 0)
                            throw new InvalidOperationException("Specified local repository path is invalid.");
                    }

                    var refs = Repository.ListRemoteReferences(this.repository.RemoteRepositoryUrl, this.CredentialsHandler);

                    var trimmedRefs = (from r in refs
                                       where r.CanonicalName.StartsWith("refs/heads/")
                                       let trimmed = r.CanonicalName.Substring("refs/heads/".Length)
                                       select trimmed).ToList();

                    return Task.FromResult(trimmedRefs.AsEnumerable());
                }
                else
                {
                    this.log.LogDebug($"Repository found at '{this.repository.LocalRepositoryPath}'...");
                    using (var repository = new Repository(this.repository.LocalRepositoryPath))
                    {
                        var origin = repository.Network.Remotes["origin"];
                        this.log.LogDebug($"Using remote: origin, '{origin.Name}'.");
                        var refs = repository.Network.ListReferences(origin);

                        var trimmedRefs = (from r in refs
                                           where r.CanonicalName.StartsWith("refs/heads/")
                                           let trimmed = r.CanonicalName.Substring("refs/heads/".Length)
                                           select trimmed).ToList();

                        return Task.FromResult(trimmedRefs.AsEnumerable());
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
                this.EndOperation();
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
                    else if (options.Tag != null)
                        refName = options.Tag;
                    this.log.LogDebug($"Resetting the index and working tree to {refName}...");
                    repository.Reset(ResetMode.Hard, refName);
                    repository.RemoveUntrackedFiles();

                    if (options.RecurseSubmodules)
                    {
                        foreach (var submodule in repository.Submodules)
                        {
                            repository.Submodules.Update(submodule.Name, new SubmoduleUpdateOptions { CredentialsProvider = this.CredentialsHandler, Init = true });
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
