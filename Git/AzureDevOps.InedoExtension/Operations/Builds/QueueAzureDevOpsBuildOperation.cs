using System.ComponentModel;
using Inedo.Diagnostics;
using Inedo.Documentation;
using Inedo.ExecutionEngine.Executer;
using Inedo.Extensibility;
using Inedo.Extensibility.Operations;
using Inedo.Extensions.AzureDevOps.Client;
using Inedo.Extensions.AzureDevOps.SuggestionProviders;
using Inedo.Web;

namespace Inedo.Extensions.AzureDevOps.Operations
{
    [DisplayName("Queue Azure DevOps Build")]
    [Description("Queues a build in Azure DevOps, optionally waiting for its completion.")]
    [ScriptAlias("Queue-Build")]
    [Tag("builds")]
    [Tag("azure-devops")]
    public sealed class QueueAzureDevOpsBuildOperation : AzureDevOpsOperation
    {
        [Required]
        [ScriptAlias("BuildDefinition")]
        [DisplayName("Build definition")]
        [SuggestableValue(typeof(BuildDefinitionNameSuggestionProvider))]
        public string BuildDefinition { get; set; }

        [ScriptAlias("WaitForCompletion")]
        [DisplayName("Wait for completion")]
        [DefaultValue(true)]
        public bool WaitForCompletion { get; set; } = true;

        [ScriptAlias("Validate")]
        [DisplayName("Validate success")]
        [DefaultValue(true)]
        public bool ValidateBuild { get; set; } = true;

        [Output]
        [ScriptAlias("AzureDevOpsBuildNumber")]
        [DisplayName("Set build number to variable")]
        [Description("The Azure DevOps build number can be output into a runtime variable.")]
        [PlaceholderText("e.g. $AzureDevOpsBuildNumber")]
        public string AzureDevOpsBuildNumber { get; set; }

        public async override Task ExecuteAsync(IOperationExecutionContext context)
        {
            var (c, r) = this.GetCredentialsAndResource(context);
            var client = new AzureDevOpsClient(r.LegacyInstanceUrl, c.Password);

            this.LogDebug("Finding Azure DevOps build definition...");
            AdoBuildDef definition = null;
            await foreach (var d in client.GetBuildDefinitionsAsync(r.ProjectName, context.CancellationToken))
            {
                if (string.IsNullOrEmpty(this.BuildDefinition) || string.Equals(d.Name, this.BuildDefinition, StringComparison.OrdinalIgnoreCase))
                {
                    definition = d;
                    break;
                }
            }

            if (definition == null)
                throw new ExecutionFailureException("Could not find a build definition named: " + AH.CoalesceString(this.BuildDefinition, "any"));

            this.LogInformation($"Queueing Azure DevOps build of {r.ProjectName}, build definition {definition.Name}...");

            var queuedBuild = await client.QueueBuildAsync(r.ProjectName, definition.Id, context.CancellationToken);

            this.LogInformation($"Build number \"{queuedBuild.BuildNumber}\" created for definition \"{queuedBuild.Definition.Name}\".");

            this.AzureDevOpsBuildNumber = queuedBuild.BuildNumber;

            if (this.WaitForCompletion)
            {
                string lastStatus = queuedBuild.Status;
                this.LogInformation($"Current build status is \"{lastStatus}\", waiting for \"completed\" status...");

                while (!string.Equals(queuedBuild.Status, "completed", StringComparison.OrdinalIgnoreCase))
                {
                    await Task.Delay(4000, context.CancellationToken);
                    queuedBuild = await client.GetBuildAsync(r.ProjectName, queuedBuild.Id, context.CancellationToken);
                    if (queuedBuild.Status != lastStatus)
                    {
                        this.LogInformation($"Current build status changed from \"{lastStatus}\" to \"{queuedBuild.Status}\"...");
                        lastStatus = queuedBuild.Status;
                    }
                }

                this.LogInformation("Build status result is \"completed\".");

                if (this.ValidateBuild)
                {
                    this.LogInformation("Validating build status result is \"succeeded\"...");
                    if (!string.Equals("succeeded", queuedBuild.Result, StringComparison.OrdinalIgnoreCase))
                    {
                        this.LogError("Build status result was not \"succeeded\".");
                        return;
                    }

                    this.LogInformation("Build status result was \"succeeded\".");
                }
            }

            this.LogInformation($"Azure DevOps build {queuedBuild.BuildNumber} created.");
        }

        protected override ExtendedRichDescription GetDescription(IOperationConfiguration config)
        {
            return new ExtendedRichDescription(
                new RichDescription(
                    "Queue Azure DevOps Build for ", new Hilite(config.DescribeSource())
                ),
                new RichDescription(
                    "using the build definition ",
                    new Hilite(config[nameof(this.BuildDefinition)]),
                    this.WaitForCompletion ? " and wait until the build completes" + (config[nameof(this.ValidateBuild)] == "true" ? " successfully" : "") : "",
                    "."
                )
            );
        }
    }
}
