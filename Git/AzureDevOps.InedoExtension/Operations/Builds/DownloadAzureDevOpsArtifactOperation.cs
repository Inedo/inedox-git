using System.ComponentModel;
using Inedo.Diagnostics;
using Inedo.Documentation;
using Inedo.Extensibility;
using Inedo.Extensibility.Operations;
using Inedo.Extensions.AzureDevOps.SuggestionProviders;
using Inedo.IO;
using Inedo.Web;

namespace Inedo.Extensions.AzureDevOps.Operations
{
    [DisplayName("Download Azure DevOps Artifact")]
    [Description("Downloads a build artifact from Azure DevOps.")]
    [ScriptAlias("Download-Artifact")]
    [Tag("artifacts")]
    [Tag("azure-devops")]
    [Serializable]
    public sealed class DownloadAzureDevOpsArtifactOperation : RemoteAzureDevOpsOperation
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
        public string ArtifactName { get; set; }

        [Output]
        [ScriptAlias("AzureDevOpsBuildNumber")]
        [DisplayName("Set build number to variable")]
        [Description("The Azure DevOps build number can be output into a runtime variable.")]
        [PlaceholderText("e.g. $AzureDevOpsBuildNumber")]
        public string AzureDevOpsBuildNumber { get; set; }

        [ScriptAlias("TargetDirectory")]
        [DisplayName("Target directory")]
        [PlaceholderText("$WorkingDirectory")]
        [FieldEditMode(FieldEditMode.ServerDirectoryPath)]
        public string TargetDirectory { get; set; }

        [ScriptAlias("ExtractFiles")]
        [DisplayName("Extract files")]
        [DefaultValue(true)]
        public bool ExtractFilesToTargetDirectory { get; set; } = true;

        protected override async Task<object> RemoteExecuteAsync(IRemoteOperationExecutionContext context)
        {
            this.LogInformation($"Downloading artifact {this.ArtifactName} with build number \"{this.BuildNumber ?? "latest"}\" from Azure DevOps...");

            using var client = new AzureDevOpsClient(this.InstanceUrl, this.Token);

            using (var artifact = await client.DownloadArtifactAsync(this.ProjectName, this.BuildDefinition, this.BuildNumber, this.ArtifactName, context.CancellationToken).ConfigureAwait(false))
            {
                string targetDirectory = context.ResolvePath(this.TargetDirectory);
                if (this.ExtractFilesToTargetDirectory)
                {
                    this.LogDebug("Extracting artifact files to: " + targetDirectory);
                    AH.ExtractZip(artifact, targetDirectory);
                }
                else
                {
                    string path = PathEx.Combine(targetDirectory, this.ArtifactName);
                    this.LogDebug("Saving artifact as zip file to: " + path);

                    using (var file = FileEx.Open(path, FileMode.Create, FileAccess.Write, FileShare.None, FileOptions.Asynchronous | FileOptions.SequentialScan))
                    {
                        await artifact.CopyToAsync(file).ConfigureAwait(false);
                    }
                }
            }

            this.LogInformation("Artifact downloaded.");

            return null;
        }

        protected override ExtendedRichDescription GetDescription(IOperationConfiguration config)
        {
            var shortDesc = new RichDescription("Download Azure DevOps ", new Hilite(config[nameof(this.ArtifactName)]), " Artifact");

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
