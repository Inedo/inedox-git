using System;
using System.Collections.Generic;
using System.Linq;
using Inedo.BuildMaster.Extensibility.IssueTrackerConnections;

namespace Inedo.Extensions.GitLab.IssueSources
{
    public sealed class GitLabIssue : IIssueTrackerIssue
    {
        public GitLabIssue(Dictionary<string, object> issue)
        {
            this.Id = issue["iid"].ToString();
            this.Title = issue["title"].ToString();
            var labels = issue["labels"] as IEnumerable<string>;
            this.Type = labels?.FirstOrDefault();
            this.Description = issue["description"].ToString();
            this.Status = issue["state"].ToString();
            this.IsClosed = string.Equals(this.Status, "closed", StringComparison.OrdinalIgnoreCase);
            var created = issue["created_at"].ToString();
            this.SubmittedDate = DateTime.Parse(created).ToUniversalTime();
            var author = issue["author"] as Dictionary<string, object>;
            if (author != null)
                this.Submitter = author["username"].ToString();
            this.Url = issue["web_url"].ToString();
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
