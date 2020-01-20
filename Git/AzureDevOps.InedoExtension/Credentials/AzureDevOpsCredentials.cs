using System.ComponentModel;
using System.Security;
using Inedo.Documentation;
using Inedo.Extensibility;
using Inedo.Extensibility.Credentials;
using Inedo.Extensibility.SecureResources;
using Inedo.Extensions.Credentials;
using Inedo.Serialization;
using Inedo.Web;

namespace Inedo.Extensions.AzureDevOps.Credentials
{
    [ScriptAlias("AzureDevOps")]
    [DisplayName("Azure DevOps")]
    [Description("Credentials for Azure DevOps that can be either username/password for source control and a personal access token for issue tracking.")]
    public sealed class AzureDevOpsCredentials : GitCredentialsBase
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

        [Persistent]
        [Undisclosed]
        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public override string RepositoryUrl { get; set; }

        [Persistent]
        [Undisclosed]
        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public override string UserName { get; set; }

        [Persistent]
        [Undisclosed]
        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public override SecureString Password => this.Token;


        public override RichDescription GetDescription() => new RichDescription(this.InstanceUrl);

        public override SecureCredentials ToSecureCredentials() => string.IsNullOrEmpty(this.UserName) ? null : new AzureDevOpsSecureCredentials
        {
            UserName = this.UserName,
            Token = this.Token
        };
        public override SecureResource ToSecureResource() => new AzureDevOpsSecureResource
        {
            InstanceUrl = this.InstanceUrl
        };
    }
}
