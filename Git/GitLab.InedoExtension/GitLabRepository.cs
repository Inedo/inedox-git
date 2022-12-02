using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Inedo.Documentation;
using Inedo.Extensibility.Credentials;
using Inedo.Extensibility.Git;
using Inedo.Extensions.GitLab.Clients;
using Inedo.Extensions.GitLab.SuggestionProviders;
using Inedo.Serialization;
using Inedo.Web;

namespace Inedo.Extensions.GitLab
{
    [DisplayName("GitLab Project")]
    [Description("Connect to a GitLab project for source code, issue tracking, etc. integration")]
    [PersistFrom("Inedo.Extensions.GitLab.Credentials.GitLabSecureResource,GitLab")]
    public sealed class GitLabRepository : GitServiceRepository<GitLabAccount>, IMissingPersistentPropertyHandler
    {
        [Persistent]
        [DisplayName("[Obsolete] API Url")]
        [PlaceholderText("use the credential's URL")]
        [Description("In earlier versions, the GitLab API URL was specified on the project/repository. This should not be used going forward.")]
        public string LegacyApiUrl { get; set; }

        [Persistent]
        [DisplayName("Namespace")]
        [PlaceholderText("e.g. username (default) or group/sub_group")]
        [Description("BuildMaster will use the user account as the default namespace when searching for a project name")]
        [SuggestableValue(typeof(GroupNameSuggestionProvider))]
        public string GroupName { get; set; }

        [Persistent]
        [DisplayName("Project")]
        [PlaceholderText("e.g. log4net")]
        [SuggestableValue(typeof(ProjectNameSuggestionProvider))]
        [Required]
        public string ProjectName { get; set; }

        public override string Namespace
        { 
            get => this.GroupName;
            set => this.GroupName = value;
        }
        public override string RepositoryName
        {
            get => this.ProjectName;
            set => this.ProjectName = value;
        }

        public override RichDescription GetDescription()
        {
            var group = string.IsNullOrEmpty(this.GroupName) ? "" : $"{this.GroupName}\\";
            return new RichDescription($"{group}{this.ProjectName}");
        }

        public override async Task<IGitRepositoryInfo> GetRepositoryInfoAsync(ICredentialResolutionContext context, CancellationToken cancellationToken = default)
        {
            var project = await new GitLabClient(this, context).GetProjectAsync(this, cancellationToken).ConfigureAwait(false);
            if (project == null)
                throw new InvalidOperationException($"Project {this.ProjectName} not found on GitLab.");

            return project;
        }
        public override IAsyncEnumerable<GitRemoteBranch> GetRemoteBranchesAsync(ICredentialResolutionContext context, CancellationToken cancellationToken = default)
        {
            return new GitLabClient(this, context).GetBranchesAsync(this, cancellationToken);
        }
        public override IAsyncEnumerable<GitPullRequest> GetPullRequestsAsync(ICredentialResolutionContext context, bool includeClosed = false, CancellationToken cancellationToken = default)
        {
            return new GitLabClient(this, context).GetPullRequestsAsync(this, includeClosed, cancellationToken);
        }
        public override Task SetCommitStatusAsync(ICredentialResolutionContext context, string commit, string status, string description = null, string statusContext = null, CancellationToken cancellationToken = default)
        {
            return new GitLabClient(this, context).SetCommitStatusAsync(this, commit, status, description, statusContext, cancellationToken);
        }
        public override Task MergePullRequestAsync(ICredentialResolutionContext context, string id, string headCommit, string commitMessage = null, string method = null, CancellationToken cancellationToken = default)
        {
            return new GitLabClient(this, context).MergeMergeRequestAsync(this, int.Parse(id), commitMessage, headCommit, method == "squash", cancellationToken);
        }
        public override async Task<string> CreatePullRequestAsync(ICredentialResolutionContext context, string sourceBranch, string targetBranch, string title, string description = null, CancellationToken cancellationToken = default)
        {
            var id = await new GitLabClient(this, context).CreateMergeRequestAsync(this, sourceBranch, targetBranch, title, description, cancellationToken).ConfigureAwait(false);
            return id.ToString();
        }

        void IMissingPersistentPropertyHandler.OnDeserializedMissingProperties(IReadOnlyDictionary<string, string> missingProperties)
        {
            if (missingProperties.ContainsKey("ApiUrl"))
                this.LegacyApiUrl = missingProperties["ApiUrl"];
        }
    }
}
