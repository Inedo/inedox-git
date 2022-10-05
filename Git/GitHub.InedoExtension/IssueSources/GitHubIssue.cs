using System;
using System.Text.Json;
using Inedo.Extensibility.IssueSources;

namespace Inedo.Extensions.GitHub.IssueSources
{
    internal sealed class GitHubIssue : IIssueTrackerIssue
    {
        public GitHubIssue(JsonElement issue, string overrideStatus = null, bool? overrideClosed = null)
        {
            this.Id = issue.GetProperty("number").GetRawText();
            this.Title = issue.GetProperty("title").GetString();
            this.Description = GetValue(issue, "body") ?? string.Empty;
            this.Status = overrideStatus ?? issue.GetProperty("state").GetString();
            this.IsClosed = overrideClosed ?? string.Equals(this.Status, "closed", StringComparison.OrdinalIgnoreCase);
            this.SubmittedDate = issue.GetProperty("created_at").GetDateTime().ToUniversalTime();
            this.Url = issue.GetProperty("html_url").GetString();

            if (issue.TryGetProperty("user", out var authorElement) && authorElement.ValueKind == JsonValueKind.Object)
                this.Submitter = GetValue(authorElement, "login");

            if (issue.TryGetProperty("labels", out var labelsArray) && labelsArray.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in labelsArray.EnumerateArray())
                {
                    this.Type = GetValue(item, "name");
                    break;
                }
            }
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

        private static string GetValue(JsonElement e, string name)
        {
            if (e.TryGetProperty(name, out var value))
                return value.GetString();
            else
                return null;
        }
    }
}
