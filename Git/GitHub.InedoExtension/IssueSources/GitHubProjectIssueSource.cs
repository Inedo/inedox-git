using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using Inedo.Documentation;
using Inedo.Extensibility.Credentials;
using Inedo.Extensibility.IssueSources;
using Inedo.Extensibility.SecureResources;
using Inedo.Extensions.GitHub.Clients;
using Inedo.Extensions.GitHub.SuggestionProviders;
using Inedo.Serialization;
using Inedo.Web;

namespace Inedo.Extensions.GitHub.IssueSources
{
    [DisplayName("GitHub Project Source")]
    [Description("Issue source for GitHub based on projects.")]
    public sealed class GitHubProjectIssueSource : IssueSource<GitHubRepository>
    {
        [Persistent]
        [DisplayName("Repository name")]
        [PlaceholderText("(use organization projects)")]
        [SuggestableValue(typeof(RepositoryNameSuggestionProvider))]
        public string RepositoryName { get; set; }

        [Persistent]
        [DisplayName("Project name")]
        [DefaultValue("$ReleaseNumber")]
        [SuggestableValue(typeof(ProjectNameSuggestionProvider))]
        public string ProjectName { get; set; }

        [Persistent]
        [DisplayName("Closed states (one per line)")]
        [DefaultValue("Done")]
        [FieldEditMode(FieldEditMode.Multiline)]
        public string ClosedStates { get; set; } = "Done";

        [Persistent]
        [DisplayName("Fail if project not found")]
        [DefaultValue(true)]
        public bool FailIfMissing { get; set; } = true;

        public override async IAsyncEnumerable<IIssueTrackerIssue> EnumerateIssuesAsync(IIssueSourceEnumerationContext context, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var resource = (GitHubRepository)this.GetResource(new ResourceResolutionContext(context.ProjectId));
            if (resource == null)
                throw new InvalidOperationException("missing resource");
            var credentials = (GitHubAccount)resource.GetCredentials(new CredentialResolutionContext(context.ProjectId, null));

            var client = new GitHubClient(credentials, resource, context.Log);
            GitHubProject project = null;
            await foreach (var p in client.GetProjectsAsync(resource.OrganizationName, this.RepositoryName, cancellationToken))
            {
                if (string.Equals(p.Name, this.ProjectName, StringComparison.OrdinalIgnoreCase))
                {
                    project = p;
                    break;
                }
            }

            if (project == null)
            {
                if (this.FailIfMissing)
                    throw new InvalidOperationException($"No project named {this.ProjectName} was found.");

                yield break;
            }

            await foreach (var column in client.GetProjectColumnsAsync(project.ColumnsUrl, cancellationToken))
            {
                foreach (var issueUrl in column.IssueUrls)
                {
                    var issue = await client.GetIssueAsync(issueUrl, column.Name, this.ClosedStates.Split('\n').Contains(column.Name, StringComparer.OrdinalIgnoreCase), cancellationToken);
                    yield return issue;
                }
            }
        }

        public override RichDescription GetDescription() => new("GitHub project ", new Hilite(this.ProjectName), " issue source");
    }
}
