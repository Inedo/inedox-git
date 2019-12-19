using System.ComponentModel;
using Inedo.Documentation;
using Inedo.Extensibility;
using Inedo.Extensibility.Credentials;
using Inedo.Extensibility.Operations;
using Inedo.Extensions.AzureDevOps.Credentials;

namespace Inedo.Extensions.AzureDevOps.Operations
{
    public abstract class AzureDevOpsOperation : ExecuteOperation, IHasCredentials<AzureDevOpsCredentials>, IAzureDevOpsConnectionInfo
    {
        private protected AzureDevOpsOperation()
        {
        }

        public abstract string CredentialName { get; set; }

        [Category("Connection/Identity")]
        [ScriptAlias("Url")]
        [DisplayName("Instance URL")]
        [PlaceholderText("Use instance URL from credentials")]
        [MappedCredential(nameof(AzureDevOpsCredentials.InstanceUrl))]
        [Description("The instance URL, follows the format: https://dev.azure.com/{organization}")]
        public string InstanceUrl { get; set; }

        [Category("Connection/Identity")]
        [ScriptAlias("UserName")]
        [DisplayName("User name")]
        [PlaceholderText("Use user name from credentials")]
        [MappedCredential(nameof(AzureDevOpsCredentials.UserName))]
        public string UserName { get; set; }

        [Category("Connection/Identity")]
        [ScriptAlias("Token")]
        [DisplayName("Personal access token")]
        [PlaceholderText("Use access token from credentials")]
        [MappedCredential(nameof(AzureDevOpsCredentials.Token))]
        public string Password { get; set; }
    }
}