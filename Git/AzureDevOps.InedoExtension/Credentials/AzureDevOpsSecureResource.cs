using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Inedo.Documentation;
using Inedo.Extensibility.Credentials;
using Inedo.Extensions.Credentials.Git;
using Inedo.Serialization;

namespace Inedo.Extensions.AzureDevOps.Credentials
{
    [DisplayName("Azure DevOps Project")]
    [Description("Connect to an Azure DevOps project for source code, issue tracking, etc. integration")]
    public sealed class AzureDevOpsSecureResource : GitSecureResourceBase<AzureDevOpsSecureCredentials>
    {
        [Required]
        [Persistent]
        [DisplayName("Instance URL")]
        [Description("The instance URL generally follows the format https://dev.azure.com/{organization}, but may differ based on hosting")]
        [PlaceholderText("e.g. https://dev.azure.com/kramerica")]
        public string InstanceUrl { get; set; }

        [Persistent]
        [DisplayName("Project name")]
        [Description("While not required, if you don't specify then you'll need to specify a project in each repository")]
        [PlaceholderText("e.g. MyProjectName")]
        public string ProjectName { get; set; }

        [Persistent]
        [DisplayName("Repository name")]
        [PlaceholderText("use the project name")]
        public string RepositoryName { get; set; }

        public override RichDescription GetDescription()
        {
            var prefix = "https://dev.azure.com/";

            var proj = new Hilite(AH.CoalesceString(this.ProjectName, "(unspecified)"));
            var inst = (this.InstanceUrl?.StartsWith(prefix) ?? false)
                ? this.InstanceUrl.Substring(prefix.Length)
                : this.InstanceUrl;
            return new RichDescription(proj, "@", inst);
        }

        public override Task<string> GetRepositoryUrlAsync(ICredentialResolutionContext context, CancellationToken cancellationToken) =>
            Task.FromResult($"{this.InstanceUrl.Trim('/')}/{Uri.EscapeDataString(this.ProjectName)}/_git/{Uri.EscapeDataString(this.RepositoryName)}");
    }
}
