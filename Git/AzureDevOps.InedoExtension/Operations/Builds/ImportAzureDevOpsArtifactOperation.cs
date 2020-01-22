using System.ComponentModel;
using System.Threading.Tasks;
using Inedo.Diagnostics;
using Inedo.Documentation;
using Inedo.Extensibility;
using Inedo.Extensibility.Credentials;
using Inedo.Extensibility.Operations;
using Inedo.Extensions.AzureDevOps;
using Inedo.Extensions.AzureDevOps.Clients;
using Inedo.Extensions.AzureDevOps.Operations;
using Inedo.Extensions.AzureDevOps.SuggestionProviders;
using Inedo.Web;

namespace Inedo.BuildMasterExtensions.AzureDevOps.Operations
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
        [SuggestableValue(typeof(BuildNumberSuggestionProvider))]
        public string BuildNumber { get; set; }

        [Required]
        [ScriptAlias("ArtifactName")]
        [DisplayName("Artifact name")]
        [SuggestableValue(typeof(ArtifactNameSuggestionProvider))]
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
            
            this.AzureDevOpsBuildNumber = await ArtifactImporter.DownloadAndImportAsync(
                c?.Token,
                r.InstanceUrl,
                this,
                r.ProjectName,
                this.BuildNumber,
                this.BuildDefinition,
                context,
                this.ArtifactName
            );

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
