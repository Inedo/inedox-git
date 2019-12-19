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
        [DisplayName("Project collection URL")]
        [PlaceholderText("Use team project from credentials")]
        [MappedCredential(nameof(AzureDevOpsCredentials.InstanceUrl))]
        public string ProjectUrl { get; set; }

        [Category("Connection/Identity")]
        [ScriptAlias("UserName")]
        [DisplayName("User name")]
        [PlaceholderText("Use user name from credentials")]
        [MappedCredential(nameof(AzureDevOpsCredentials.UserName))]
        public string UserName { get; set; }

        [Category("Connection/Identity")]
        [ScriptAlias("Password")]
        [DisplayName("Password / token")]
        [PlaceholderText("Use password/token from credentials")]
        [MappedCredential(nameof(AzureDevOpsCredentials.Token))]
        public string Token { get; set; }

        [Category("Connection/Identity")]
        [ScriptAlias("Domain")]
        [DisplayName("Domain name")]
        [PlaceholderText("Use domain from credentials")]
        [MappedCredential(nameof(AzureDevOpsCredentials.Domain))]
        public string Domain { get; set; }
    }
}