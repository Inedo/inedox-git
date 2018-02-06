using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Inedo.BuildMaster.Extensibility.Credentials;
using Inedo.BuildMaster.Extensibility.IssueSources;
using Inedo.BuildMaster.Extensibility.IssueTrackerConnections;
using Inedo.BuildMaster.Web;
using Inedo.BuildMaster.Web.Controls;
using Inedo.Documentation;
using Inedo.Extensions.Clients;
using Inedo.Extensions.Credentials;
using Inedo.Extensions.GitHub.SuggestionProviders;
using Inedo.Serialization;

namespace Inedo.Extensions.GitHub.IssueSources
{
    [DisplayName("GitHub Issue Source")]
    [Description("Issue source for GitHub.")]
    public sealed class GitHubIssueSource : IssueSource, IHasCredentials<GitHubCredentials>
    {
        [Required]
        [Persistent]
        [DisplayName("Credentials")]
        public string CredentialName { get; set; }

        [Persistent]
        [DisplayName("Repository name")]
        [PlaceholderText("Use repository name from credentials")]
        [SuggestibleValue(typeof(RepositoryNameSuggestionProvider))]
        public string RepositoryName { get; set; }

        [Persistent]
        [DisplayName("Milestone")]
        [SuggestibleValue(typeof(MilestoneSuggestionProvider))]
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
            var credentials = this.TryGetCredentials<GitHubCredentials>();
            if (credentials == null)
                throw new InvalidOperationException("Credentials must be supplied to enumerate GitHub issues.");

            string repositoryName = AH.CoalesceString(this.RepositoryName, credentials.RepositoryName);
            if (string.IsNullOrEmpty(repositoryName))
                throw new InvalidOperationException("A repository name must be defined in either the issue source or associated GitHub credentials in order to enumerate GitHub issues.");

            var client = new GitHubClient(credentials.ApiUrl, credentials.UserName, credentials.Password, credentials.OrganizationName);
            
            string ownerName = AH.CoalesceString(credentials.OrganizationName, credentials.UserName);

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
