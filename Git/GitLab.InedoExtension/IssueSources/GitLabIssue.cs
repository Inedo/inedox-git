using System;
using System.Text.Json;
using Inedo.Extensibility.IssueSources;

namespace Inedo.Extensions.GitLab.IssueSources
{
    internal sealed class GitLabIssue : IIssueTrackerIssue
    {
        public GitLabIssue(JsonElement obj)
        {
            this.Id = obj.GetProperty("iid").GetString();
            this.Title = GetValueOrDefault(obj, "title");
            this.Description = GetValueOrDefault(obj, "description");
            this.Status = GetValueOrDefault(obj, "state");
            this.IsClosed = string.Equals(this.Status, "closed", StringComparison.OrdinalIgnoreCase);
            this.SubmittedDate = obj.GetProperty("created_at").GetDateTime().ToUniversalTime();

            if (obj.TryGetProperty("author", out var authorElement) && authorElement.ValueKind == JsonValueKind.Object)
                this.Submitter = GetValueOrDefault(authorElement, "username");

            this.Url = GetValueOrDefault(obj, "web_url");

            if (obj.TryGetProperty("labels", out var labelsArray) && labelsArray.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in labelsArray.EnumerateArray())
                {
                    this.Type = item.GetString();
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

        private static string GetValueOrDefault(JsonElement obj, string name)
        {
            if (obj.TryGetProperty(name, out var value))
                return value.GetString();
            else
                return null;
        }
    }
}
