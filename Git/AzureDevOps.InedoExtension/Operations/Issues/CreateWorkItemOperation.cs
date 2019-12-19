﻿using System.ComponentModel;
using System.Threading.Tasks;
using Inedo.Diagnostics;
using Inedo.Documentation;
using Inedo.Extensibility;
using Inedo.Extensibility.Operations;
using Inedo.Extensions.AzureDevOps.Clients.Rest;
using Inedo.Extensions.AzureDevOps.SuggestionProviders;
using Inedo.Web;

namespace Inedo.Extensions.AzureDevOps.Operations
{
    [DisplayName("Create Azure DevOps Work Item")]
    [Description("Creates a work item in Azure DevOps.")]
    [Tag("issue-tracking")]
    [ScriptAlias("Create-WorkItem")]
    [Example(@"
# create issue for the HDARS project
Create-WorkItem
(
    Credentials: KarlAzure,
    Project: HDARS,
    Type: Task,
    Title: QA Testing Required for $ApplicationName,
    Description: This issue was created by BuildMaster on $Date
);
")]
    public sealed class CreateWorkItemOperation : AzureDevOpsOperation
    {
        [ScriptAlias("Credentials")]
        [DisplayName("Credentials")]
        public override string CredentialName { get; set; }
        [Required]
        [ScriptAlias("Project")]
        [DisplayName("Project")]
        [SuggestableValue(typeof(ProjectNameSuggestionProvider))]
        public string Project { get; set; }
        [Required]
        [ScriptAlias("Type")]
        [DisplayName("Work item type")]
        [SuggestableValue(typeof(WorkItemTypeSuggestionProvider))]
        public string Type { get; set; }
        [Required]
        [ScriptAlias("Title")]
        [DisplayName("Title")]
        public string Title { get; set; }
        [ScriptAlias("Description")]
        [DisplayName("Description")]
        [FieldEditMode(FieldEditMode.Multiline)]
        public string Description { get; set; }
        [ScriptAlias("IterationPath")]
        [DisplayName("Iteration path")]
        [SuggestableValue(typeof(IterationPathSuggestionProvider))]
        public string IterationPath { get; set; }

        [Output]
        [ScriptAlias("WorkItemId")]
        [DisplayName("Set work item ID to variable")]
        [Description("The Azure DevOps work item ID can be output into a runtime variable.")]
        [PlaceholderText("e.g. $WorkItemId")]
        public string WorkItemId { get; set; }

        public override async Task ExecuteAsync(IOperationExecutionContext context)
        {
            this.LogInformation("Creating work item in Azure DevOps...");

            var client = new RestApi(this, this);
            try
            {
                var result = await client.CreateWorkItemAsync(this.Project, this.Type, this.Title, this.Description, this.IterationPath).ConfigureAwait(false);

                this.LogDebug($"Work item (ID={result.id}) created.");
                this.WorkItemId = result.id.ToString();
            }
            catch (AzureDevOpsRestException ex)
            {
                this.LogError(ex.FullMessage);
                return;
            }
            this.LogInformation("Work item created.");
        }

        protected override ExtendedRichDescription GetDescription(IOperationConfiguration config)
        {
            return new ExtendedRichDescription(
                new RichDescription("Create Azure DevOps Work Item for project ", config[nameof(this.Project)])
            );
        }
    }
}
