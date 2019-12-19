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
        [Description("The instance URL (with optional team project); follows the format: https://dev.azure.com/{organization}[/{team-project}]")]
        public string InstanceUrl { get; set; }

        [Persistent(Encrypted = true)]
        [DisplayName("Access token")]
        [FieldEditMode(FieldEditMode.Password)]
        public SecureString Token { get; set; }

        [Persistent]
        [DisplayName("Domain")]
        public string Domain { get; set; }

        public override RichDescription GetDescription()
        {
            var desc = new RichDescription(this.UserName);
            if (!string.IsNullOrEmpty(this.Domain))
                desc.AppendContent("@", this.Domain);
            return desc;
        }

        string IAzureDevOpsConnectionInfo.ProjectUrl => this.InstanceUrl;
        string IAzureDevOpsConnectionInfo.Token => AH.Unprotect(this.Token);
    }
}
