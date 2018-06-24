using System;
using System.ComponentModel;
using System.Linq;
using System.Security;
using System.Threading.Tasks;
using Inedo.Documentation;
using Inedo.Extensibility;
using Inedo.Extensibility.UserDirectories;
using Inedo.IO;
using Inedo.Serialization;
using LibGit2Sharp;

namespace Inedo.Extensions.RaftRepositories
{
    [DisplayName("Git")]
    [Description("The raft is persisted as a Git repository that is automatically synchronized with an external Git repository.")]
    [PersistFrom("Inedo.Otter.Extensions.RaftRepositories.ExternalGitRaftRepository,OtterCoreEx")]
    public sealed class ExternalGitRaftRepository : GitRaftRepositoryBase
    {
        private Lazy<string> localRepoPath;

        public ExternalGitRaftRepository()
        {
            this.localRepoPath = new Lazy<string>(this.GetLocalRepoPath);
        }

        [Required]
        [Persistent]
        [DisplayName("Remote repository URL")]
        [PlaceholderText("Git clone URL")]
        public string RemoteRepositoryUrl { get; set; }

        [Persistent]
        [DisplayName("User name")]
        [PlaceholderText("anonymous")]
        public string UserName { get; set; }

        [Persistent(Encrypted = true)]
        public SecureString Password { get; set; }

        public override string LocalRepositoryPath => this.localRepoPath.Value;

        public override Task<ConfigurationTestResult> TestConfigurationAsync()
        {
            if (string.IsNullOrWhiteSpace(this.RemoteRepositoryUrl))
                return Task.FromResult(ConfigurationTestResult.Failure("Remote repository URL is not specified."));

            return Task.FromResult(ConfigurationTestResult.Success);
        }

        public override async Task CommitAsync(IUserDirectoryUser user)
        {
            await base.CommitAsync(user).ConfigureAwait(false);

            this.Repo.Branches.Update(this.Repo.Branches[this.BranchName], b => b.TrackedBranch = "refs/remotes/origin/" + this.BranchName);

            this.Repo.Network.Push(
                this.Repo.Branches[this.BranchName],
                new PushOptions
                {
                    CredentialsProvider = this.CredentialsHandler
                }
            );
        }

        protected override Repository OpenRepository()
        {
            if (DirectoryEx.Exists(this.LocalRepositoryPath))
            {
                if (Repository.IsValid(this.LocalRepositoryPath))
                {
                    var repository = new Repository(this.LocalRepositoryPath);

                    if (!string.IsNullOrEmpty(this.RemoteRepositoryUrl))
                    {

                        Commands.Fetch(repository, "origin", Enumerable.Empty<string>(), 
                            new FetchOptions { CredentialsProvider = CredentialsHandler }, null);

                        if (repository.Refs["refs/heads/" + this.BranchName] == null)
                        {
                            //Must use an ObjectId to create a DirectReference (SymbolicReferences will cause an error when committing)
                            var objId = new ObjectId(repository.Refs["refs/remotes/origin/" + this.BranchName].TargetIdentifier);
                            repository.Refs.Add("refs/heads/" + this.BranchName, objId);
                        }

                        repository.Refs.UpdateTarget("refs/heads/" + this.BranchName, "refs/remotes/origin/" + this.BranchName);
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

        private string GetLocalRepoPath()
        {
            if (string.IsNullOrWhiteSpace(this.RaftName))
                throw new InvalidOperationException("The raft does not have a name.");

            return PathEx.Combine(SDK.GetCommonTempPath(), "GitRafts", this.RaftName);
        }
        private LibGit2Sharp.Credentials CredentialsHandler(string url, string usernameFromUrl, SupportedCredentialTypes types)
        {
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

        protected override GitRaftRepositoryBase CreateCopy()
        {
            return new ExternalGitRaftRepository
            {
                RemoteRepositoryUrl = this.RemoteRepositoryUrl,
                UserName = this.UserName,
                Password = this.Password
            };
        }
    }
}
