using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Inedo.Documentation;
using Inedo.Extensibility.Credentials;
using Inedo.Extensibility.Git;
using Inedo.Extensions.GitHub.Clients;
using Inedo.Extensions.GitHub.SuggestionProviders;
using Inedo.Serialization;
using Inedo.Web;

namespace Inedo.Extensions.GitHub
{
    [DisplayName("GitHub Project")]
    [Description("Connect to a GitHub project for source code, issue tracking, etc. integration")]
    [PersistFrom("Inedo.Extensions.GitHub.Credentials.GitHubSecureResource,GitHub")]
    public sealed class GitHubRepository : GitServiceRepository<GitHubAccount>, IMissingPersistentPropertyHandler
    {
        [Persistent]
        [DisplayName("[Obsolete] API Url")]
        [PlaceholderText("use the credential's URL")]
        [Description("In earlier versions, the GitHub Enterprise API URL was specified on the repository. This should not be used going forward.")]
        public string LegacyApiUrl { get; set; }

        [Persistent]
        [DisplayName("Organization name")]
        [PlaceholderText("e.g. apache")]
        [SuggestableValue(typeof(OrganizationNameSuggestionProvider))]
        public string OrganizationName { get; set; }

        [Persistent]
        [DisplayName("Repository")]
        [PlaceholderText("e.g. log4net")]
        [SuggestableValue(typeof(RepositoryNameSuggestionProvider))]
        public override string RepositoryName { get; set; }

        public override string Namespace
        {
            get => this.OrganizationName;
            set => this.OrganizationName = value;
        }


        public override RichDescription GetDescription()
        {
            var group = string.IsNullOrEmpty(this.OrganizationName) ? "" : $"{this.OrganizationName}/";
            return new RichDescription($"{group}{this.RepositoryName}");
        }

        public override async Task<IGitRepositoryInfo> GetRepositoryInfoAsync(ICredentialResolutionContext context, CancellationToken cancellationToken = default)
        {
            var github = new GitHubClient((GitHubAccount)this.GetCredentials(context), this);
            return await github.GetRepositoryAsync(this.OrganizationName, this.RepositoryName, cancellationToken).ConfigureAwait(false);
        }
        public override IAsyncEnumerable<GitRemoteBranch> GetRemoteBranchesAsync(ICredentialResolutionContext context, CancellationToken cancellationToken = default)
        {
            var github = new GitHubClient((GitHubAccount)this.GetCredentials(context), this);
            return github.GetBranchesAsync(this.OrganizationName, this.RepositoryName, cancellationToken);
        }
        public override IAsyncEnumerable<GitPullRequest> GetPullRequestsAsync(ICredentialResolutionContext context, bool includeClosed = false, CancellationToken cancellationToken = default)
        {
            var github = new GitHubClient((GitHubAccount)this.GetCredentials(context), this);
            return github.GetPullRequestsAsync(this.OrganizationName, this.RepositoryName, includeClosed, cancellationToken);
        }
        public override Task SetCommitStatusAsync(ICredentialResolutionContext context, string commit, string status, string description = null, string statusContext = null, CancellationToken cancellationToken = default)
        {
            var github = new GitHubClient((GitHubAccount)this.GetCredentials(context), this);
            return github.SetCommitStatusAsync(this.OrganizationName, this.RepositoryName, commit, status, description, statusContext, cancellationToken);
        }
        public override Task MergePullRequestAsync(ICredentialResolutionContext context, string id, string headCommit, string commitMessage = null, string method = null, CancellationToken cancellationToken = default)
        {
            var github = new GitHubClient((GitHubAccount)this.GetCredentials(context), this);
            return github.MergePullRequestAsync(this.OrganizationName, this.RepositoryName, int.Parse(id), headCommit, commitMessage, method, cancellationToken);
        }
        public override async Task<string> CreatePullRequestAsync(ICredentialResolutionContext context, string sourceBranch, string targetBranch, string title, string description = null, CancellationToken cancellationToken = default)
        {
            var github = new GitHubClient((GitHubAccount)this.GetCredentials(context), this);
            return (await github.CreatePullRequestAsync(this.OrganizationName, this.RepositoryName, sourceBranch, targetBranch, title, description, cancellationToken).ConfigureAwait(false)).ToString();
        }

        void IMissingPersistentPropertyHandler.OnDeserializedMissingProperties(IReadOnlyDictionary<string, string> missingProperties)
        {
            if (missingProperties.TryGetValue("ApiUrl", out var url))
                this.LegacyApiUrl = url;
        }
    }
}
