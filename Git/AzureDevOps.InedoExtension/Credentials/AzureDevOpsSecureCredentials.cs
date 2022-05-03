using System.ComponentModel;
using System.Security;
using Inedo.Documentation;
using Inedo.Extensions.Credentials.Git;
using Inedo.Serialization;
using Inedo.Web;
using UsernamePasswordCredentials = Inedo.Extensions.Credentials.UsernamePasswordCredentials;

namespace Inedo.Extensions.AzureDevOps.Credentials
{
    [DisplayName("Azure DevOps Account")]
    [Description("Use an Azure DevOps account to connect to Azure DevOps resources")]
    public sealed class AzureDevOpsSecureCredentials : GitSecureCredentialsBase
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

        public override RichDescription GetDescription() => new(this.UserName);

        public override UsernamePasswordCredentials ToUsernamePassword() => string.IsNullOrEmpty(this.UserName) ? null : new UsernamePasswordCredentials
        {
            UserName = this.UserName,
            Password = this.Token
        };
    }
}
