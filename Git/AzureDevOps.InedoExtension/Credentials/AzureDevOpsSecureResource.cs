using System.ComponentModel;
using Inedo.Documentation;
using Inedo.Extensibility.SecureResources;
using Inedo.Serialization;

namespace Inedo.Extensions.AzureDevOps.Credentials
{
    [DisplayName("Azure DevOps Project")]
    [Description("Connect to an Azure DevOps project for source code, issue tracking, etc. integration")]
    public sealed class AzureDevOpsSecureResource : SecureResource<AzureDevOpsSecureCredentials>
    {
        [Required]
        [Persistent]
        [DisplayName("Instance URL")]
        [Description("The instance URL, follows the format: https://dev.azure.com/{organization}")]
        public string InstanceUrl { get; set; }

        public override RichDescription GetDescription() => new RichDescription(this.InstanceUrl);
    }
}
