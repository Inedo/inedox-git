using System.ComponentModel;
using System.Security;
using Inedo.Documentation;
using Inedo.Extensibility;
using Inedo.Extensions.Credentials;
using Inedo.Serialization;
using Inedo.Web;

namespace Inedo.Extensions.AzureDevOps.Credentials
{
    [ScriptAlias("AzureDevOps")]
    [DisplayName("Azure DevOps")]
    [Description("Credentials for Azure DevOps that can be either username/password for source control and a personal access token for issue tracking.")]
    public sealed class AzureDevOpsCredentials : GitCredentialsBase, IAzureDevOpsConnectionInfo
    {
        [Required]
        [Persistent]
        [DisplayName("Instance URL")]
        [Description("The instance URL, follows the format: https://dev.azure.com/{organization}")]
        public string InstanceUrl { get; set; }

        [Persistent(Encrypted = true)]
        [DisplayName("Access token")]
        [FieldEditMode(FieldEditMode.Password)]
        [Description("A generated personal access token")]
        public SecureString Token { get; set; }

        public override RichDescription GetDescription() => new RichDescription(this.UserName);

        string IAzureDevOpsConnectionInfo.InstanceUrl => this.InstanceUrl;
        string IAzureDevOpsConnectionInfo.Password => AH.Unprotect(this.Token);
    }
}
