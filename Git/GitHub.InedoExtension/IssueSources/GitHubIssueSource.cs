﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Inedo.Documentation;
using Inedo.Extensibility;
using Inedo.Extensibility.Credentials;
using Inedo.Extensibility.IssueSources;
using Inedo.Extensibility.SecureResources;
using Inedo.Extensions.GitHub.Clients;
using Inedo.Extensions.GitHub.Credentials;
using Inedo.Extensions.GitHub.SuggestionProviders;
using Inedo.Serialization;
using Inedo.Web;

namespace Inedo.Extensions.GitHub.IssueSources
{
    [DisplayName("GitHub Issue Source")]
    [Description("Issue source for GitHub.")]
    public sealed class GitHubIssueSource : IssueSource, IHasCredentials<GitHubCredentials>
    {
        string IHasCredentials.CredentialName { get; set; }

        [Required]
        [Persistent]
        [DisplayName("Resource name")]
        public string CredentialName
        {
            get => this.ResourceName;
            set => this.ResourceName = value;
        }

        [Persistent]
        [DisplayName("Repository name")]
        [PlaceholderText("Use repository name from credentials")]
        [SuggestableValue(typeof(RepositoryNameSuggestionProvider))]
        public string RepositoryName { get; set; }

        [Persistent]
        [DisplayName("Milestone")]
        [SuggestableValue(typeof(MilestoneSuggestionProvider))]
        public string MilestoneTitle { get; set; }

        [Persistent]
        [DisplayName("Labels")]
        [PlaceholderText("Any")]
        [Description("A list of comma separated label names. Example: bug,ui,@high")]
        public string Labels { get; set; }

        [Persistent]
        [FieldEditMode(FieldEditMode.Multiline)]
        [DisplayName("Custom filter query")]
        [PlaceholderText("Use above fields")]
        [Description("If a custom filter query string is set, the above filters are ignored. See "
            + "<a href=\"https://developer.github.com/v3/issues/#list-issues-for-a-repository\" target=\"_blank\">GitHub API List Issues for a Repository</a> "
            + "for more information.<br /><br />" 
            + "For example, to filter by all issues assigned to 'BuildMasterUser' without a set milestone:<br /><br />" 
            + "<pre>milestone=none&amp;assignee=BuildMasterUser&amp;state=all</pre>")]
        public string CustomFilterQueryString { get; set; }

        public override async Task<IEnumerable<IIssueTrackerIssue>> EnumerateIssuesAsync(IIssueSourceEnumerationContext context)
        {
            var rc = this.TryGetCredentials(environmentId: null, applicationId: context.ProjectId) as GitHubCredentials;
            var credContext = new CredentialResolutionContext(context.ProjectId, null);
            var resource = (GitHubSecureResource)(rc?.ToSecureResource() ?? SecureResource.TryCreate(this.ResourceName, credContext));
            var credentials = (GitHubSecureCredentials)(rc?.ToSecureCredentials() ?? SecureCredentials.TryCreate(this.CredentialName, credContext));

            if (credentials == null)
                throw new InvalidOperationException("Credentials must be supplied to enumerate GitHub issues.");

            string repositoryName = AH.CoalesceString(this.RepositoryName, resource.RepositoryName);
            if (string.IsNullOrEmpty(repositoryName))
                throw new InvalidOperationException("A repository name must be defined in either the issue source or associated GitHub credentials in order to enumerate GitHub issues.");

            var client = new GitHubClient(credentials, resource);

            string ownerName = AH.CoalesceString(resource.OrganizationName, credentials.UserName);

            var filter = new GitHubIssueFilter
            {
                Labels = this.Labels,
                CustomFilterQueryString = this.CustomFilterQueryString
            };

            if (!string.IsNullOrEmpty(this.MilestoneTitle))
            {
                int? milestoneId = await client.FindMilestoneAsync(this.MilestoneTitle, ownerName, repositoryName, CancellationToken.None).ConfigureAwait(false);
                if (milestoneId == null)
                    throw new InvalidOperationException($"Milestone '{this.MilestoneTitle}' not found in repository '{repositoryName}' owned by '{ownerName}'.");

                filter.Milestone = milestoneId.ToString();
            }
            else
            {
                filter.Milestone = "*";
            }

            var issues = await client.GetIssuesAsync(ownerName, repositoryName, filter, CancellationToken.None).ConfigureAwait(false);

            return from i in issues
                   select new GitHubIssue(i);
        }

        public override RichDescription GetDescription()
        {
            return new RichDescription("Get Issues from GitHub");
        }
    }
}
