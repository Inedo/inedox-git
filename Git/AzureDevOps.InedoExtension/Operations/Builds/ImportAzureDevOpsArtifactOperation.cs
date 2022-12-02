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
    [DisplayName("Import Azure DevOps Artifact")]
    [Description("Downloads an artifact from Azure DevOps and saves it to the artifact library.")]
    [ScriptAlias("Import-Artifact")]
    [Tag("artifacts")]
    [Tag("azure-devops")]
    public sealed class ImportAzureDevOpsArtifactOperation : AzureDevOpsOperation
    {
        [Required]
        [ScriptAlias("BuildDefinition")]
        [DisplayName("Build definition")]
        [SuggestableValue(typeof(BuildDefinitionNameSuggestionProvider))]
        public string BuildDefinition { get; set; }

        [ScriptAlias("BuildNumber")]
        [DisplayName("Build number")]
        [PlaceholderText("latest")]
        public string BuildNumber { get; set; }

        [Required]
        [ScriptAlias("ArtifactName")]
        [DisplayName("Artifact name")]
        public string ArtifactName { get; set; }

        [Output]
        [ScriptAlias("AzureDevOpsBuildNumber")]
        [DisplayName("Set build number to variable")]
        [Description("The Azure DevOps build number can be output into a runtime variable.")]
        [PlaceholderText("e.g. $AzureDevOpsBuildNumber")]
        public string AzureDevOpsBuildNumber { get; set; }

        public async override Task ExecuteAsync(IOperationExecutionContext context)
        {
            this.LogInformation($"Importing {this.ArtifactName} artifact with build number \"{this.BuildNumber ?? "latest"}\" from Azure DevOps...");

            var (c, r) = this.GetCredentialsAndResource(context);

            var client = new AzureDevOpsClient(r.LegacyInstanceUrl, c.Password);

            AdoBuild build = null;

            await foreach (var b in client.GetBuildsAsync(this.ProjectName, context.CancellationToken))
            {
                if (!string.Equals(this.BuildDefinition, b.Definition?.Name, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (string.IsNullOrEmpty(this.BuildNumber) || string.Equals(b.BuildNumber, this.BuildNumber, StringComparison.OrdinalIgnoreCase))
                {
                    build = b;
                    break;
                }
            }

            if (build == null)
                throw new ExecutionFailureException($"Build {this.BuildNumber} not found.");

            AdoArtifact artifact = null;

            await foreach (var a in client.GetBuildArtifactsAsync(this.ProjectName, build.Id, context.CancellationToken))
            {
                if (string.Equals(a.Name, this.ArtifactName, StringComparison.OrdinalIgnoreCase))
                {
                    artifact = a;
                    break;
                }
            }

            if (artifact != null)
                throw new ExecutionFailureException($"Artifact {this.ArtifactName} not found on build {build.BuildNumber}.");

            using var artifactStream = await client.DownloadBuildArtifactAsync(artifact.Resource.DownloadUrl, context.CancellationToken);
            await context.CreateBuildMasterArtifactAsync(this.ArtifactName, artifactStream, false, context.CancellationToken);

            this.LogInformation("Import complete.");
        }

        protected override ExtendedRichDescription GetDescription(IOperationConfiguration config)
        {
            var shortDesc = new RichDescription("Import Azure DevOps ", new Hilite(config[nameof(this.ArtifactName)]), " Artifact");

            var longDesc = new RichDescription("from ", new Hilite(config.DescribeSource()), " using ");
            if (string.IsNullOrEmpty(config[nameof(this.BuildNumber)]))
                longDesc.AppendContent("the last successful build");
            else
                longDesc.AppendContent("build ", new Hilite(config[nameof(this.BuildNumber)]));

            longDesc.AppendContent(" of ");

            if (string.IsNullOrEmpty(config[nameof(this.BuildDefinition)]))
                longDesc.AppendContent("any build definition");
            else
                longDesc.AppendContent("build definition ", new Hilite(config[nameof(this.BuildDefinition)]));

            longDesc.AppendContent(".");

            return new ExtendedRichDescription(shortDesc, longDesc);
        }
    }
}
