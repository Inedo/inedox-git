using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Inedo.Diagnostics;
using Inedo.Documentation;
using Inedo.Extensibility.Credentials;
using Inedo.Extensibility.IssueSources;
using Inedo.Extensions.AzureDevOps.Clients.Rest;
using Inedo.Extensions.AzureDevOps.Credentials;
using Inedo.Extensions.AzureDevOps.SuggestionProviders;
using Inedo.Serialization;
using Inedo.Web;

namespace Inedo.Extensions.AzureDevOps.IssueSources
{
    [DisplayName("Azure DevOps Issue Source")]
    [Description("Issue source for Azure DevOps.")]
    public sealed class AzureDevOpsIssueSource : IssueSource, IHasCredentials<AzureDevOpsCredentials>
    {
        [Persistent]
        [DisplayName("Credentials")]
        public string CredentialName { get; set; }
        [Persistent]
        [DisplayName("Project")]
        [SuggestableValue(typeof(ProjectNameSuggestionProvider))]
        public string Project { get; set; }
        [Persistent]
        [DisplayName("Iteration path")]
        [SuggestableValue(typeof(IterationPathSuggestionProvider))]
        public string IterationPath { get; set; }
        [Persistent]
        [DisplayName("Closed states")]
        [Description("The state name used to determined if an issue is closed; when not specified, this defaults to Resolved,Closed,Done.")]
        public string ClosedStates { get; set; } = "Resolved,Closed,Done";
        [Persistent]
        [DisplayName("Custom WIQL")]
        [PlaceholderText("Use above fields")]
        [FieldEditMode(FieldEditMode.Multiline)]
        [Description("Custom WIQL will ignore the project name and iteration path if supplied. "
            + "See the <a href=\"https://docs.microsoft.com/en-us/azure/devops/boards/queries/wiql-syntax?view=azure-devops\">Azure DevOps Query Language documentation</a> "
            + "for more information.")]
        public string CustomWiql { get; set; }

        public override async Task<IEnumerable<IIssueTrackerIssue>> EnumerateIssuesAsync(IIssueSourceEnumerationContext context)
        {
            context.Log.LogDebug("Enumerating Azure DevOps issue source...");

            var credentials = this.TryGetCredentials<AzureDevOpsCredentials>();
            var client = new RestApi(credentials, context.Log);
            string wiql = this.GetWiql(context.Log);

            var workItems = await client.GetWorkItemsAsync(wiql).ConfigureAwait(false);
            var closedStates = this.ClosedStates.Split(',');

            return from w in workItems
                   select new AzureDevOpsWorkItem(w, closedStates);
        }

        private string GetWiql(ILogSink log)
        {
            if (!string.IsNullOrEmpty(this.CustomWiql))
            {
                log.LogDebug("Using custom WIQL query to filter issues...");
                return this.CustomWiql;
            }

            log.LogDebug($"Constructing WIQL query for project '{this.Project}' and iteration path '{this.IterationPath}'...");

            var buffer = new StringBuilder();
            buffer.Append("SELECT [System.Id] FROM WorkItems ");

            bool projectSpecified = !string.IsNullOrEmpty(this.Project);
            bool iterationPathSpecified = !string.IsNullOrEmpty(this.IterationPath);

            if (!projectSpecified && !iterationPathSpecified)
                return buffer.ToString();

            buffer.Append("WHERE ");

            if (projectSpecified)
                buffer.AppendFormat("[System.TeamProject] = '{0}' ", this.Project.Replace("'", "''"));

            if (projectSpecified && iterationPathSpecified)
                buffer.Append("AND ");

            if (iterationPathSpecified)
                buffer.AppendFormat("[System.IterationPath] UNDER '{0}' ", this.IterationPath.Replace("'", "''"));

            return buffer.ToString();
        }

        public override RichDescription GetDescription()
        {
            if (!string.IsNullOrEmpty(this.CustomWiql))
                return new RichDescription("Get Issues from Azure DevOps Using Custom WIQL");
            else
                return new RichDescription(
                    "Get Issues from ", new Hilite(this.Project), " in Azure DevOps for iteration path ", new Hilite(this.IterationPath)
                );
        }
    }
}
