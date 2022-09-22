using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Inedo.Documentation;
using Inedo.Extensibility.Credentials;
using Inedo.Extensibility.Git;
using Inedo.Extensions.Credentials.Git;
using Inedo.Extensions.GitHub.Clients;
using Inedo.Extensions.GitHub.SuggestionProviders;
using Inedo.Serialization;
using Inedo.Web;

namespace Inedo.Extensions.GitHub.Credentials
{
    [DisplayName("GitHub Project")]
    [Description("Connect to a GitHub project for source code, issue tracking, etc. integration")]
    public sealed class GitHubSecureResource : GitSecureResourceBase<GitHubSecureCredentials>
    {
        [Persistent]
        [DisplayName("API URL")]
        [PlaceholderText(GitHubClient.GitHubComUrl)]
        [Description("Leave this value blank to connect to github.com. For local installations of GitHub enterprise, an API URL must be specified.")]
        public string ApiUrl { get; set; }

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
            var host = "GitHub.com";
            if (!string.IsNullOrWhiteSpace(this.ApiUrl))
            {
                if (Uri.TryCreate(this.ApiUrl, UriKind.Absolute, out var uri))
                    host = uri.Host;
                else
                    host = "(unknown)";
            }

            var group = string.IsNullOrEmpty(this.OrganizationName) ? "" : $"{this.OrganizationName}\\";
            return new RichDescription($"{group}{this.RepositoryName} @ {host}");
        }

        public override async Task<string> GetRepositoryUrlAsync(ICredentialResolutionContext context, CancellationToken cancellationToken = default)
        {
            return (await this.GetRepositoryInfoAsync(context, cancellationToken).ConfigureAwait(false)).RepositoryUrl;
        }
        public override async Task<IGitRepositoryInfo> GetRepositoryInfoAsync(ICredentialResolutionContext context, CancellationToken cancellationToken = default)
        {
            var github = new GitHubClient((GitHubSecureCredentials)this.GetCredentials(context), this);
            return await github.GetRepositoryAsync(this.RepositoryName, cancellationToken).ConfigureAwait(false);
        }
        public override IAsyncEnumerable<GitRemoteBranch> GetRemoteBranchesAsync(ICredentialResolutionContext context, CancellationToken cancellationToken = default)
        {
            var github = new GitHubClient((GitHubSecureCredentials)this.GetCredentials(context), this);
            return github.GetBranchesAsync(this.RepositoryName, cancellationToken);
        }
        public override IAsyncEnumerable<GitPullRequest> GetPullRequestsAsync(ICredentialResolutionContext context, bool includeClosed = false, CancellationToken cancellationToken = default)
        {
            var github = new GitHubClient((GitHubSecureCredentials)this.GetCredentials(context), this);
            return github.GetPullRequestsAsync(this.RepositoryName, includeClosed, cancellationToken);
        }
        public override Task SetCommitStatusAsync(ICredentialResolutionContext context, string commit, string status, string description = null, string statusContext = null, CancellationToken cancellationToken = default)
        {
            var github = new GitHubClient((GitHubSecureCredentials)this.GetCredentials(context), this);
            return github.SetCommitStatusAsync(this.RepositoryName, commit, status, description, statusContext, cancellationToken);
        }
    }
}
