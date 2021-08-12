using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Inedo.Diagnostics;
using Inedo.Documentation;
using Inedo.ExecutionEngine;
using Inedo.Extensibility;
using Inedo.Extensibility.Operations;
using Inedo.Extensions.AzureDevOps.Clients.Rest;
using Inedo.Extensions.AzureDevOps.SuggestionProviders;
using Inedo.Serialization;
using Inedo.Web;

namespace Inedo.Extensions.AzureDevOps.Operations.Issues
{
    [DisplayName("Find Azure DevOps Work Items")]
    [Description("Finds Work Items in Azure DevOps.")]
    [Tag("issue-tracking")]
    [ScriptAlias("Find-WorkItems")]
    public class FindWorkItemsOperation : AzureDevOpsOperation
    {
        [ScriptAlias("IterationPath")]
        [DisplayName("Iteration path")]
        [PlaceholderText("Unchanged")]
        [SuggestableValue(typeof(IterationPathSuggestionProvider))]
        public string IterationPath { get; set; }

        [ScriptAlias("Filter")]
        [DisplayName("Filter")]
        [SuggestableValue(typeof(IterationPathSuggestionProvider))]
        [Description("Filter WIQL that will be appended to the WIQL where clause."
            + "See the <a href=\"https://docs.microsoft.com/en-us/azure/devops/boards/queries/wiql-syntax?view=azure-devops\">Azure DevOps Query Language documentation</a> "
            + "for more information.")]
        public string Filter { get; set; }

        [ScriptAlias("CustomWiql")]
        [DisplayName("Custom WIQL")]
        [PlaceholderText("Use above fields")]
        [FieldEditMode(FieldEditMode.Multiline)]
        [Description("Custom WIQL will ignore the project name and iteration path if supplied. "
            + "See the <a href=\"https://docs.microsoft.com/en-us/azure/devops/boards/queries/wiql-syntax?view=azure-devops\">Azure DevOps Query Language documentation</a> "
            + "for more information.")]
        public string CustomWiql { get; set; }

        [Persistent]
        [DisplayName("Closed states")]
        [Description("The state name used to determined if an issue is closed; when not specified, this defaults to Resolved,Closed,Done.")]
        [Category("Advanced")]
        public string ClosedStates { get; set; } = "Resolved,Closed,Done";

        [Output]
        [DisplayName("Output variable")]
        [ScriptAlias("Output")]
        [Description("The output variable should be a list variable. For example: @YouTrackIssueIDs")]
        public RuntimeValue Output { get; set; }

        public override async Task ExecuteAsync(IOperationExecutionContext context)
        {
            var (c, r) = this.GetCredentialsAndResource(context);
            var client = new RestApi(c?.Token, r.InstanceUrl, this);
            string wiql = this.GetWiql(context.Log);
            var closedStates = this.ClosedStates.Split(',');

            var workItems = (await client.GetWorkItemsAsync(wiql).ConfigureAwait(false)).Select(wi => new AzureDevOpsWorkItem(wi, closedStates));
            this.Output = new RuntimeValue(
                    workItems.Select(
                        i => new RuntimeValue(
                            new Dictionary<string, RuntimeValue>
                            {
                                ["Id"] = i.Id,
                                ["Title"] = i.Title ?? string.Empty,
                                ["Description"] = i.Description ?? string.Empty,
                                ["Status"] = i.Status
                            }
                        )
                    ).ToList()
                );
        }

        protected override ExtendedRichDescription GetDescription(IOperationConfiguration config)
        {
            string iteration = config[nameof(this.IterationPath)];
            var longDescription = new RichDescription();
            longDescription.AppendContent("Get Work Items from ");
            if (config[nameof(this.CustomWiql)] == null)
                longDescription.AppendContent(new Hilite(AH.CoalesceString(config[nameof(this.ProjectName)], config[nameof(this.ResourceName)])),
                " in Azure DevOps for iteration path ",
                new Hilite(this.IterationPath));
            else
                longDescription.AppendContent("custom WIQL");


            return new ExtendedRichDescription(
                new RichDescription("Finds Azure DevOps Work Items"),
                longDescription
            );
        }

        private string GetWiql(ILogSink log)
        {
            if (!string.IsNullOrEmpty(this.CustomWiql))
            {
                log.LogDebug("Using custom WIQL query to filter issues...");
                return this.CustomWiql;
            }

            log.LogDebug($"Constructing WIQL query for project '{this.ProjectName}' and iteration path '{this.IterationPath}'...");

            var buffer = new StringBuilder();
            buffer.Append("SELECT [System.Id], [System.State], [System.Title], [System.Description] FROM WorkItems ");

            bool projectSpecified = !string.IsNullOrEmpty(this.ProjectName);
            bool iterationPathSpecified = !string.IsNullOrEmpty(this.IterationPath);

            if (!projectSpecified && !iterationPathSpecified)
                return buffer.ToString();

            buffer.Append("WHERE ");

            if (projectSpecified)
                buffer.AppendFormat("[System.TeamProject] = '{0}' ", this.ProjectName.Replace("'", "''"));

            if (projectSpecified && iterationPathSpecified)
                buffer.Append("AND ");

            if (iterationPathSpecified)
                buffer.AppendFormat("[System.IterationPath] UNDER '{0}' ", this.IterationPath.Replace("'", "''"));


            bool filterSpecified = !string.IsNullOrEmpty(this.Filter);

            if (projectSpecified && iterationPathSpecified && filterSpecified)
                buffer.Append("AND ");

            if (filterSpecified)
                buffer.Append(this.Filter);

            return buffer.ToString();
        }
    }
}
