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
    [DisplayName("Tag Azure DevOps Source")]
    [Description("Tags the source code in an Azure DevOps project.")]
    [Tag("source-control")]
    [ScriptAlias("Tag")]
    [Example(@"
# tags the current source tree with the current release name and build number
AzureDevOps::Tag
(
    Credentials: Hdars-Azure,
    Project: HdarsOrg,
    Repository: HdarsApp,
    Tag: $ReleaseName.$BuildNumber
);
")]
    public sealed class GitLabTagOperation : TagOperation<AzureDevOpsCredentials>
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
            string source = AH.CoalesceString(config[nameof(this.ProjectName)], config[nameof(this.CredentialName)]);

            return new ExtendedRichDescription(
               new RichDescription("Tag Azure DevOps Source"),
               new RichDescription("in ", new Hilite(source), " with ", new Hilite(config[nameof(this.Tag)]))
            );
        }
    }
}
