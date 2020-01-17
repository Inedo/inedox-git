using Inedo.Documentation;
using Inedo.Extensibility.Credentials;
using Inedo.Serialization;
using Inedo.Web;
using System.ComponentModel;
using System.Security;

namespace Inedo.Extensions.AzureDevOps.Credentials
{
    [DisplayName("Azure DevOps Account")]
    [Description("Use an Azure DevOps account to connect to Azure DevOps resources")]
    public sealed class AzureDevOpsSecureCredentials : SecureCredentials
    {
        [Persistent]
        [DisplayName("User name")]
        [Required]
        public string UserName { get; set; }

        [Persistent(Encrypted = true)]
        [DisplayName("Personal access token")]
        [FieldEditMode(FieldEditMode.Password)]
        [Required]
        public SecureString Token { get; set; }

        public override RichDescription GetDescription() => new RichDescription(this.UserName);
    }
}
