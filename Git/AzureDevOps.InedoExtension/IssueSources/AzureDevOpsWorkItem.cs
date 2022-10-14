using Inedo.Extensibility.IssueSources;
using Inedo.Extensions.AzureDevOps.Client;

namespace Inedo.Extensions.AzureDevOps.IssueSources
{
    internal sealed class AzureDevOpsWorkItem : IIssueTrackerIssue
    {
        public AzureDevOpsWorkItem(AdoWorkItem w, HashSet<string> closedStates)
        {
            this.Id = w.Id.ToString();
            this.Title = w.Fields.GetValueOrDefault("System.Title")?.ToString();
            this.Description = w.Fields.GetValueOrDefault("System.Description")?.ToString();
            this.Status = w.Fields.GetValueOrDefault("System.State")?.ToString();
            this.IsClosed = closedStates.Contains(this.Status, StringComparer.OrdinalIgnoreCase);
            this.SubmittedDate = AH.ParseDate(w.Fields.GetValueOrDefault("System.CreatedDate")?.ToString()) ?? DateTime.MinValue;
            this.Submitter = w.Fields.GetValueOrDefault("System.CreatedBy")?.ToString();
            this.Type = w.Fields.GetValueOrDefault("System.WorkItemType")?.ToString();
            this.Url = w.Links.Html.Href;
        }

        public string Id { get; }
        public string Title { get; }
        public string Description { get; }
        public bool IsClosed { get; }
        public string Status { get; }
        public DateTime SubmittedDate { get; }
        public string Submitter { get; }
        public string Type { get; }
        public string Url { get; }
    }
}
