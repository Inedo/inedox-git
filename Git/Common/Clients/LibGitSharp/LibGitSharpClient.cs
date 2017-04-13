using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Inedo.Diagnostics;
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

        public LibGitSharpClient(GitRepositoryInfo repository, ILogger log)
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

                    var trimmedRefs = from r in refs
                                      where r.CanonicalName.StartsWith("refs/heads/")
                                      let trimmed = r.CanonicalName.Substring("refs/heads/".Length)
                                      select trimmed;

                    return Task.FromResult(trimmedRefs);
                }
                else
                {
                    this.log.LogDebug($"Repository found at '{this.repository.LocalRepositoryPath}'...");
                    using (var repository = new Repository(this.repository.LocalRepositoryPath))
                    {
                        var origin = repository.Network.Remotes["origin"];
                        this.log.LogDebug($"Using remote: origin, '{origin.Name}'.");
                        var refs = repository.Network.ListReferences(origin);

                        var trimmedRefs = from r in refs
                                          where r.CanonicalName.StartsWith("refs/heads/")
                                          let trimmed = r.CanonicalName.Substring("refs/heads/".Length)
                                          select trimmed;

                        return Task.FromResult(trimmedRefs);
                    }
                }
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

        public override Task TagAsync(string tag)
        {
            try
            {
                this.BeginOperation();

                this.log.LogDebug($"Using repository at '{this.repository.LocalRepositoryPath}'...");
                using (var repository = new Repository(this.repository.LocalRepositoryPath))
                {
                    this.log.LogDebug($"Creating tag '{tag}'...");
                    var createdTag = repository.ApplyTag(tag);

                    this.log.LogDebug($"Pushing '{createdTag.CanonicalName}' to remote 'origin'...");

                    repository.Network.Push(
                        repository.Network.Remotes["origin"],
                        createdTag.CanonicalName,
                        new PushOptions { CredentialsProvider = this.CredentialsHandler }
                    );
                }

                return Complete;
            }
            finally
            {
                this.EndOperation();
            }
        }

        public override Task UpdateAsync(GitUpdateOptions options)
        {
            try
            {
                this.log.LogDebug($"Using repository at '{this.repository.LocalRepositoryPath}'...");
                using (var repository = new Repository(this.repository.LocalRepositoryPath))
                {
                    this.log.LogDebug("Fetching commits from origin...");
                    repository.Fetch("origin", new FetchOptions { CredentialsProvider = CredentialsHandler, Prune = true });
                    var refName = "FETCH_HEAD";
                    if (options.Branch != null)
                        refName = options.Branch;
                    else if (options.Tag != null)
                        refName = options.Tag;
                    this.log.LogDebug($"Resetting the index and working tree to {refName}...");
                    repository.Reset(ResetMode.Hard, refName);
                    repository.RemoveUntrackedFiles();

                    if (options.RecurseSubmodules)
                    {
                        foreach (var submodule in repository.Submodules)
                        {
                            repository.Submodules.Update(submodule.Name, new SubmoduleUpdateOptions { CredentialsProvider = CredentialsHandler, Init = true });
                        }
                    }
                }

                return Complete;
            }
            finally
            {
                this.EndOperation();
            }
        }

        public override Task ArchiveAsync(string targetDirectory)
        {
            return CopyNonGitFilesAsync(new FileArchiver(targetDirectory), this.repository.LocalRepositoryPath, targetDirectory);
        }

        private LibGit2Sharp.Credentials CredentialsHandler(string url, string usernameFromUrl, SupportedCredentialTypes types)
        {
            if (string.IsNullOrEmpty(this.repository.UserName))
            {
                this.log.LogDebug($"Connecting with default credentials...");
                return new DefaultCredentials();
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
