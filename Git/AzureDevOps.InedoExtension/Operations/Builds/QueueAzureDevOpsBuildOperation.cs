using System;
using System.ComponentModel;
using System.Linq;
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
    [DisplayName("Queue Azure DevOps Build")]
    [Description("Queues a build in Azure DevOps, optionally waiting for its completion.")]
    [ScriptAlias("Queue-Build")]
    [Tag("builds")]
    [Tag("azure-devops")]
    public sealed class QueueAzureDevOpsBuildOperation : AzureDevOpsOperation
    {
        [ScriptAlias("Credentials")]
        [DisplayName("Credentials")]
        public override string CredentialName { get; set; }

        [ScriptAlias("Project")]
        [DisplayName("Project")]
        [SuggestableValue(typeof(ProjectNameSuggestionProvider))]
        public string Project { get; set; }

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
            var api = new RestApi(this, this);

            this.LogDebug("Finding Azure DevOps build definition...");
            var definitionResult = await api.GetBuildDefinitionsAsync(this.Project);
            var definition = definitionResult.FirstOrDefault(d => string.IsNullOrEmpty(this.BuildDefinition) || string.Equals(d.name, this.BuildDefinition, StringComparison.OrdinalIgnoreCase));

            if (definition == null)
                throw new InvalidOperationException("Could not find a build definition named: " + AH.CoalesceString(this.BuildDefinition, "any"));

            this.LogInformation($"Queueing Azure DevOps build of {this.Project}, build definition {definition.name}...");

            var queuedBuild = await api.QueueBuildAsync(this.Project, definition.id);

            this.LogInformation($"Build number \"{queuedBuild.buildNumber}\" created for definition \"{queuedBuild.definition.name}\".");

            this.AzureDevOpsBuildNumber = queuedBuild.buildNumber;

            if (this.WaitForCompletion)
            {
                string lastStatus = queuedBuild.status;
                this.LogInformation($"Current build status is \"{lastStatus}\", waiting for \"completed\" status...");

                while (!string.Equals(queuedBuild.status, "completed", StringComparison.OrdinalIgnoreCase))
                {
                    await Task.Delay(4000, context.CancellationToken);
                    queuedBuild = await api.GetBuildAsync(this.Project, queuedBuild.id);
                    if (queuedBuild.status != lastStatus)
                    {
                        this.LogInformation($"Current build status changed from \"{lastStatus}\" to \"{queuedBuild.status}\"...");
                        lastStatus = queuedBuild.status;
                    }
                }

                this.LogInformation("Build status result is \"completed\".");

                if (this.ValidateBuild)
                {
                    this.LogInformation("Validating build status result is \"succeeded\"...");
                    if (!string.Equals("succeeded", queuedBuild.result, StringComparison.OrdinalIgnoreCase))
                    {
                        this.LogError("Build status result was not \"succeeded\".");
                        return;
                    }
                    this.LogInformation("Build status result was \"succeeded\".");
                }
            }

            this.LogInformation($"Azure DevOps build {queuedBuild.buildNumber} created.");
        }

        protected override ExtendedRichDescription GetDescription(IOperationConfiguration config)
        {
            return new ExtendedRichDescription(
                new RichDescription(
                    "Queue Azure DevOps Build for ", new Hilite(config[nameof(this.Project)])
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
