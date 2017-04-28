using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Inedo.BuildMaster.Extensibility.Credentials;
using Inedo.BuildMaster.Extensibility.IssueSources;
using Inedo.BuildMaster.Extensibility.IssueTrackerConnections;
using Inedo.BuildMaster.Web;
using Inedo.BuildMaster.Web.Controls;
using Inedo.Documentation;
using Inedo.Extensions.Clients;
using Inedo.Extensions.Credentials;
using Inedo.Extensions.GitLab.SuggestionProviders;
using Inedo.Serialization;

namespace Inedo.Extensions.GitLab.IssueSources
{
    [DisplayName("GitLab Issue Source")]
    [Description("Issue source for GitLab.")]
    public sealed class GitLabIssueSource : IssueSource, IHasCredentials<GitLabCredentials>
    {
        [Required]
        [Persistent]
        [DisplayName("Credentials")]
        public string CredentialName { get; set; }

        [Persistent]
        [DisplayName("Project name")]
        [PlaceholderText("Use project name from credentials")]
        [SuggestibleValue(typeof(ProjectNameSuggestionProvider))]
        public string ProjectName { get; set; }

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
            + "<a href=\"https://docs.gitlab.com/ce/api/issues.html#list-project-issues\" target=\"_blank\">GitLab API List Issues for a Project</a> "
            + "for more information.<br /><br />" 
            + "For example, to filter by all issues with no labels that contain the word 'cheese' in their title or description:<br /><br />" 
            + "<pre>labels=No+Label&amp;search=cheese</pre>")]
        public string CustomFilterQueryString { get; set; }

        public override async Task<IEnumerable<IIssueTrackerIssue>> EnumerateIssuesAsync(IIssueSourceEnumerationContext context)
        {
            var credentials = this.TryGetCredentials<GitLabCredentials>();
            if (credentials == null)
                throw new InvalidOperationException("Credentials must be supplied to enumerate GitLab issues.");

            string projectName = AH.CoalesceString(this.ProjectName, credentials.ProjectName);
            if (string.IsNullOrEmpty(projectName))
                throw new InvalidOperationException("A project name must be defined in either the issue source or associated GitLab credentials in order to enumerate GitLab issues.");

            var client = new GitLabClient(credentials.ApiUrl, credentials.UserName, credentials.Password, credentials.GroupName);
            
            var filter = new GitLabIssueFilter
            {
                Milestone = this.MilestoneTitle,
                Labels = this.Labels,
                CustomFilterQueryString = this.CustomFilterQueryString
            };

            var issues = await client.GetIssuesAsync(projectName, filter).ConfigureAwait(false);

            return from i in issues
                   select new GitLabIssue(i);
        }

        public override RichDescription GetDescription()
        {
            return new RichDescription("Get Issues from GitLab");
        }
    }
}
