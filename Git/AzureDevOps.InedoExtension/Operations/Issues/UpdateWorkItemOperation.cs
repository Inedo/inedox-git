using System.ComponentModel;
using Inedo.Diagnostics;
using Inedo.Documentation;
using Inedo.ExecutionEngine;
using Inedo.Extensibility;
using Inedo.Extensibility.Operations;
using Inedo.Extensions.AzureDevOps.Client;
using Inedo.Web;

namespace Inedo.Extensions.AzureDevOps.Operations
{
    [DisplayName("Update Azure DevOps Work Item")]
    [Description("Updates an existing work item in Azure DevOps.")]
    [Tag("issue-tracking")]
    [ScriptAlias("Update-WorkItem")]
    [Example(@"
# Update issue stored in package variable to 'In Progress'
Create-WorkItem
(
    Credentials: KarlAzure,
    Project: HDARS,
    Id: $WorkItemId,
    State: In Progress
);
")]
    public sealed class UpdateWorkItemOperation : AzureDevOpsOperation
    {
        [Required]
        [ScriptAlias("Id")]
        [DisplayName("Id")]
        [Description("The ID for issues may be stored as output variables of the Create-WorkItem operation.")]
        public string Id { get; set; }
        [ScriptAlias("Title")]
        [DisplayName("Title")]
        [PlaceholderText("Unchanged")]
        public string Title { get; set; }
        [ScriptAlias("Description")]
        [DisplayName("Description")]
        [PlaceholderText("Unchanged")]
        [FieldEditMode(FieldEditMode.Multiline)]
        public string Description { get; set; }
        [ScriptAlias("IterationPath")]
        [DisplayName("Iteration path")]
        [PlaceholderText("Unchanged")]
        public string IterationPath { get; set; }
        [ScriptAlias("State")]
        [DisplayName("State")]
        [PlaceholderText("Unchanged")]
        public string State { get; set; }
        [ScriptAlias("OtherFields")]
        [DisplayName("OtherFields")]
        [Description("A map variable containing other fields and values to update.")]
        public IDictionary<string, RuntimeValue> OtherFields { get; set; }

        public override async Task ExecuteAsync(IOperationExecutionContext context)
        {
            this.LogInformation($"Updating work item (ID={this.Id}) in Azure DevOps...");
            var (c, r) = this.GetCredentialsAndResource(context);
            var client = new AzureDevOpsClient(AH.CoalesceString(r.LegacyInstanceUrl, c.ServiceUrl), c?.Password);
            try
            {
                await client.UpdateWorkItemAsync(this.Id, this.Title, this.Description, this.IterationPath, this.State, this.OtherFields, context.CancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                this.LogError(ex.Message);
                return;
            }

            this.LogInformation("Work item updated.");
        }

        protected override ExtendedRichDescription GetDescription(IOperationConfiguration config)
        {
            string title = config[nameof(this.Title)];
            string description = config[nameof(this.Description)];
            string iteration = config[nameof(this.IterationPath)];
            string state = config[nameof(this.State)];

            var longDescription = new RichDescription();
            if (!string.IsNullOrEmpty(title))
                longDescription.AppendContent("Title = ", new Hilite(title), "; ");
            if (!string.IsNullOrEmpty(description))
                longDescription.AppendContent("Description = ", new Hilite(description), "; ");
            if (!string.IsNullOrEmpty(iteration))
                longDescription.AppendContent("Iteration = ", new Hilite(iteration), "; ");
            if (!string.IsNullOrEmpty(state))
                longDescription.AppendContent("State = ", new Hilite(state), "; ");

            return new ExtendedRichDescription(
                new RichDescription("Update Azure DevOps Work Item"),
                longDescription
            );
        }
    }
}
