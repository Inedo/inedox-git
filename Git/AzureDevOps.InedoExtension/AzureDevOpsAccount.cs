using System.ComponentModel;
using System.Security;
using Inedo.Documentation;
using Inedo.Extensibility.Git;
using Inedo.Serialization;
using Inedo.Web;

namespace Inedo.Extensions.AzureDevOps
{
    [DisplayName("Azure DevOps Account")]
    [Description("Use an Azure DevOps account to connect to Azure DevOps resources")]
    [PersistFrom("Inedo.Extensions.AzureDevOps.Credentials.AzureDevOpsSecureCredentials,AzureDevOps")]
    public sealed class AzureDevOpsAccount : GitServiceCredentials<AzureDevOpsServiceInfo>
    {
        [Required]
        [Persistent]
        [DisplayName("User name")]
        public override string UserName { get; set; }

        [Required]
        [Persistent(Encrypted = true)]
        [DisplayName("Personal access token")]
        [FieldEditMode(FieldEditMode.Password)]
        public override SecureString Password { get; set; }

        public override RichDescription GetCredentialDescription() => new(this.UserName);

        public override RichDescription GetServiceDescription()
        {
            return string.IsNullOrEmpty(this.ServiceUrl) || !this.TryGetServiceUrlHostName(out var hostName)
                ? new("Azure DevOps")
                : new("Azure DevOps (", new Hilite(hostName), ")");
        }
    }
}
