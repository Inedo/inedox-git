﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Inedo.Documentation;
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
    [DisplayName("GitHub Project Source")]
    [Description("Issue source for GitHub based on projects.")]
    public sealed class GitHubProjectIssueSource : IssueSource<GitHubSecureResource>
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

        public override async Task<IEnumerable<IIssueTrackerIssue>> EnumerateIssuesAsync(IIssueSourceEnumerationContext context)
        {
            var resource = (GitHubSecureResource)this.GetResource(new ResourceResolutionContext(context.ProjectId));
            if (resource == null)
                throw new InvalidOperationException("missing resource");
            var credentials = (GitHubSecureCredentials)resource.GetCredentials(new CredentialResolutionContext(context.ProjectId, null));

            var client = new GitHubClient(credentials, resource);
            var projects = await client.GetProjectsAsync(resource.OrganizationName, this.RepositoryName, CancellationToken.None);
            var project = projects.FirstOrDefault(p => string.Equals(p["name"]?.ToString(), this.ProjectName, StringComparison.OrdinalIgnoreCase));
            if (project == null)
            {
                if (this.FailIfMissing)
                    throw new InvalidOperationException($"No project named {this.ProjectName} was found.");

                return InedoLib.EmptyArray<IIssueTrackerIssue>();
            }

            var columns = await client.GetProjectColumnsAsync((string)project["columns_url"], CancellationToken.None);
            var issues = new List<IIssueTrackerIssue>();
            foreach (var column in columns)
            {
                foreach (var card in column.Value)
                {
                    var issueUrl = (string)card["content_url"];
                    if (string.IsNullOrEmpty(issueUrl))
                        continue;

                    var issue = await client.GetIssueAsync(issueUrl);
                    issues.Add(new GitHubIssue(issue, column.Key, this.ClosedStates.Split('\n').Contains(column.Key, StringComparer.OrdinalIgnoreCase)));
                }
            }

            return issues;
        }

        public override RichDescription GetDescription() => new($"GitHub project ", new Hilite(this.ProjectName), " issue source");
    }
}
