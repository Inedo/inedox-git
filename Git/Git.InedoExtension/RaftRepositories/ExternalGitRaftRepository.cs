using System;
using System.ComponentModel;
using System.Linq;
using System.Security;
using System.Threading.Tasks;
using Inedo.Diagnostics;
using Inedo.Documentation;
using Inedo.Extensibility;
using Inedo.Extensibility.UserDirectories;
using Inedo.Extensions.Clients;
using Inedo.IO;
using Inedo.Serialization;
using LibGit2Sharp;

namespace Inedo.Extensions.Git.RaftRepositories
{
    [DisplayName("Git")]
    [Description("The raft is persisted as a Git repository that is automatically synchronized with an external Git repository.")]
    [PersistFrom("Inedo.Extensions.RaftRepositories.ExternalGitRaftRepository,Git")]
    [PersistFrom("Inedo.Otter.Extensions.RaftRepositories.ExternalGitRaftRepository,OtterCoreEx")]
    public sealed class ExternalGitRaftRepository : GitRaftRepositoryBase
    {
        private readonly Lazy<string> localRepoPath;

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
        public override GitRepositoryInfo RepositoryInfo => new GitRepositoryInfo(new WorkspacePath(this.LocalRepositoryPath), this.RemoteRepositoryUrl, this.UserName, this.Password);

        public override async Task<ConfigurationTestResult> TestConfigurationAsync()
        {
            if (string.IsNullOrWhiteSpace(this.RemoteRepositoryUrl))
                return ConfigurationTestResult.Failure("Remote repository URL is not specified.");

            try
            {
                await this.Client.EnumerateRemoteBranchesAsync();
                return ConfigurationTestResult.Success;
            }
            catch (Exception ex)
            {
                this.LogDebug("Git raft repository configuration test failed.", ex.ToString());
                return ConfigurationTestResult.Failure("Could not list remote branches: " + ex.Message);
            }
        }
        public override RichDescription GetDescription()
        {
            return new RichDescription("External Git raft: ", new Hilite(this.RemoteRepositoryUrl));
        }

        public override async Task CommitAsync(IUserDirectoryUser user)
        {
            await base.CommitAsync(user).ConfigureAwait(false);

            this.Repo.Branches.Update(this.Repo.Branches[this.BranchName], b => b.TrackedBranch = "refs/remotes/origin/" + this.BranchName);

            await this.Client.PushAsync(new GitPushOptions { Ref = "refs/heads/" + this.BranchName }).ConfigureAwait(false);
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
                        this.Client.UpdateAsync(new GitUpdateOptions { Branch = this.BranchName, IsBare = true }).WaitAndUnwrapExceptions();
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
                this.Client.CloneAsync(new GitCloneOptions
                {
                    Branch = this.BranchName,
                    IsBare = true
                }).WaitAndUnwrapExceptions();
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
