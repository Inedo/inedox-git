using System;
using System.Collections.Generic;
using System.Linq;
using Inedo.Extensibility.IssueSources;
using Newtonsoft.Json.Linq;

namespace Inedo.Extensions.GitHub.IssueSources
{
    public sealed class GitHubIssue : IIssueTrackerIssue
    {
        public GitHubIssue(JObject issue, string overrideStatus = null, bool? overrideClosed = null)
        {
            this.Id = issue["number"].ToString();
            this.Title = issue["title"].ToString();
            var labels = issue["labels"] as IEnumerable<JObject>;
            this.Type = labels?.FirstOrDefault()?["name"]?.ToString();
            this.Description = issue["body"]?.ToString() ?? string.Empty;
            this.Status = overrideStatus ?? issue["state"].ToString();
            this.IsClosed = overrideClosed ?? string.Equals(this.Status, "closed", StringComparison.OrdinalIgnoreCase);
            var created = issue["created_at"].ToString();
            this.SubmittedDate = DateTime.Parse(created).ToUniversalTime();
            if (issue["user"] is JObject user)
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
