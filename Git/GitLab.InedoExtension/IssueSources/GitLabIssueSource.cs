using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Inedo.Documentation;
using Inedo.Extensibility.Credentials;
using Inedo.Extensibility.IssueSources;
using Inedo.Extensibility.SecureResources;
using Inedo.Extensions.GitLab.Clients;
using Inedo.Extensions.GitLab.Credentials;
using Inedo.Extensions.GitLab.SuggestionProviders;
using Inedo.Serialization;
using Inedo.Web;

namespace Inedo.Extensions.GitLab.IssueSources
{
    [DisplayName("GitLab Issue Source")]
    [Description("Issue source for GitLab.")]
    public sealed class GitLabIssueSource : IssueSource<GitLabSecureResource>, IHasCredentials<GitLabLegacyResourceCredentials>
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
        [DisplayName("Project name")]
        [PlaceholderText("Use project name from credentials")]
        [SuggestableValue(typeof(ProjectNameSuggestionProvider))]
        public string ProjectName { get; set; }

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
            + "<a href=\"https://docs.gitlab.com/ce/api/issues.html#list-project-issues\" target=\"_blank\">GitLab API List Issues for a Project</a> "
            + "for more information.<br /><br />"
            + "For example, to filter by all issues with no labels that contain the word 'cheese' in their title or description:<br /><br />"
            + "<pre>labels=No+Label&amp;search=cheese</pre>")]
        public string CustomFilterQueryString { get; set; }


        public override async Task<IEnumerable<IIssueTrackerIssue>> EnumerateIssuesAsync(IIssueSourceEnumerationContext context)
        {
            var resource = SecureResource.TryCreate(this.ResourceName, new ResourceResolutionContext(context.ProjectId)) as GitLabSecureResource;
            var credentials = resource?.GetCredentials(new CredentialResolutionContext(context.ProjectId, null)) as GitLabSecureCredentials;
            if (resource == null)
            {
                var rc = SecureCredentials.TryCreate(this.ResourceName, new CredentialResolutionContext(context.ProjectId, null)) as GitLabLegacyResourceCredentials;
                resource = (GitLabSecureResource)rc?.ToSecureResource();
                credentials = (GitLabSecureCredentials)rc?.ToSecureCredentials();
            }
            if (resource == null)
                throw new InvalidOperationException("A resource must be supplied to enumerate GitLab issues.");

            string projectName = AH.CoalesceString(this.ProjectName, resource.ProjectName);
            if (string.IsNullOrEmpty(projectName))
                throw new InvalidOperationException("A project name must be defined in either the issue source or associated GitLab resource in order to enumerate GitLab issues.");

            var client = new GitLabClient(credentials, resource);

            var filter = new GitLabIssueFilter
            {
                Milestone = this.MilestoneTitle,
                Labels = this.Labels,
                CustomFilterQueryString = this.CustomFilterQueryString
            };

            var issues = await client.GetIssuesAsync(projectName, filter, CancellationToken.None).ConfigureAwait(false);

            return from i in issues
                   select new GitLabIssue(i);
        }

        public override RichDescription GetDescription()
        {
            return new RichDescription("Get Issues from GitLab");
        }
    }
}
