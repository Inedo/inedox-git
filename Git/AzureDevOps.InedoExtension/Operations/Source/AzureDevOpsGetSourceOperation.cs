using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Inedo.Documentation;
using Inedo.Extensibility;
using Inedo.Extensibility.Credentials;
using Inedo.Extensibility.Operations;
using Inedo.Extensions.AzureDevOps.Credentials;
using Inedo.Extensions.Operations;

namespace Inedo.Extensions.AzureDevOps.Operations
{
    [DisplayName("Get Source from Azure DevOps Repository")]
    [Description("Gets the source code from an Azure DevOps Git repository.")]
    [Tag("source-control")]
    [ScriptAlias("Get-Source")]
    [Example(@"
# pulls source from a remote repository and archives/exports the contents to a target directory
AzureDevOps::Get-Source
(
    Credentials: Hdars-Azure,
    Project: HdarsOrg,
    Repository: HdarsApp,
    DiskPath: ~\Sources
);
")]
    public sealed class GitHubGetSourceOperation : GetSourceOperation<AzureDevOpsCredentials>
    {
        [ScriptAlias("Credentials")]
        [DisplayName("Credentials")]
        public override string CredentialName { get; set; }       

        [Required]
        [Category("Azure DevOps")]
        [ScriptAlias("Project")]
        [DisplayName("Project name")]
        public string ProjectName { get; set; }
        
        [Required]
        [Category("Azure DevOps")]
        [ScriptAlias("Repository")]
        [DisplayName("Repository name")]
        public string RepositoryName { get; set; }

        [Category("Advanced")]
        [ScriptAlias("InstanceUrl")]
        [DisplayName("Instance URL")]
        [MappedCredential(nameof(AzureDevOpsCredentials.InstanceUrl))]
        [PlaceholderText("Use instance URL from credentials")]
        public string InstanceUrl { get; set; }

        protected override Task<string> GetRepositoryUrlAsync(CancellationToken cancellationToken)
        {
            string url = $"{this.InstanceUrl.Trim('/')}/{Uri.EscapeDataString(this.ProjectName)}/_git/{Uri.EscapeDataString(this.RepositoryName)}";
            return Task.FromResult(url);
        }

        protected override ExtendedRichDescription GetDescription(IOperationConfiguration config)
        {
            string source = AH.CoalesceString(config[nameof(this.RepositoryName)], config[nameof(this.CredentialName)]);

            return new ExtendedRichDescription(
               new RichDescription("Get Azure DevOps Source"),
               new RichDescription("from ", new Hilite(source), " to ", new Hilite(AH.CoalesceString(config[nameof(this.DiskPath)], "$WorkingDirectory")))
            );
        }
    }
}
