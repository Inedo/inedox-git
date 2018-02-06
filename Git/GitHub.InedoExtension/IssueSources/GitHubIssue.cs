using System;
using System.Collections.Generic;
using System.Linq;
using Inedo.BuildMaster.Extensibility.IssueTrackerConnections;

namespace Inedo.Extensions.GitHub.IssueSources
{
    public sealed class GitHubIssue : IIssueTrackerIssue
    {
        public GitHubIssue(Dictionary<string, object> issue)
        {
            this.Id = issue["number"].ToString();
            this.Title = issue["title"].ToString();
            var labels = issue["labels"] as IEnumerable<Dictionary<string, object>>;
            this.Type = labels?.FirstOrDefault()?["name"]?.ToString();
            this.Description = issue["body"]?.ToString() ?? string.Empty;
            this.Status = issue["state"].ToString();
            this.IsClosed = string.Equals(this.Status, "closed", StringComparison.OrdinalIgnoreCase);
            var created = issue["created_at"].ToString();
            this.SubmittedDate = DateTime.Parse(created).ToUniversalTime();
            if (issue["user"] is Dictionary<string, object> user)
                this.Submitter = user["login"].ToString();
            this.Url = issue["html_url"].ToString();
        }

        public string Id { get; }
        public string Title { get; }
        public string Type { get; }
        public string Description { get; }
        public string Status { get; }
        public bool IsClosed { get; }
        public DateTime SubmittedDate { get; }
        public string Submitter { get; }
        public string Url { get; }
    }
}
