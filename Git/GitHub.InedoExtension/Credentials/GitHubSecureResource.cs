﻿using System;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Inedo.Documentation;
using Inedo.Extensibility.Credentials;
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
        public string RepositoryName { get; set; }

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

        public override async Task<string> GetRepositoryUrlAsync(ICredentialResolutionContext context, CancellationToken cancellationToken)
        {
            var github = new GitHubClient((GitHubSecureCredentials)this.GetCredentials(context), this);

            var repo = (from r in await github.GetRepositoriesAsync(cancellationToken).ConfigureAwait(false)
                        where string.Equals((string)r["name"], this.RepositoryName, StringComparison.OrdinalIgnoreCase)
                        select r).FirstOrDefault();

            if (repo == null)
                throw new InvalidOperationException($"Repository '{this.RepositoryName}' not found on GitHub.");

            return (string)repo["clone_url"];
        }
    }
}
