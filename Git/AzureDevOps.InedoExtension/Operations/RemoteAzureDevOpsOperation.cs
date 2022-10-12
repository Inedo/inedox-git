using System.ComponentModel;
using System.Security;
using Inedo.Documentation;
using Inedo.Extensibility;
using Inedo.Extensibility.Credentials;
using Inedo.Extensibility.Operations;
using Inedo.Extensions.AzureDevOps.SuggestionProviders;
using Inedo.Web;

namespace Inedo.Extensions.AzureDevOps.Operations
{
    public abstract class RemoteAzureDevOpsOperation : RemoteExecuteOperation, IAzureDevOpsConfiguration
    {
        private protected RemoteAzureDevOpsOperation()
        {
        }

        [DisplayName("From AzureDevOps resource")]
        [SuggestableValue(typeof(SecureResourceSuggestionProvider<AzureDevOpsRepository>))]
        [Required]
        public string ResourceName { get; set; }

        [Category("Connection/Identity")]
        [ScriptAlias("Project")]
        [DisplayName("Project name")]
        [SuggestableValue(typeof(ProjectNameSuggestionProvider))]
        [PlaceholderText("Use team project from AzureDevOps resource")]
        public string ProjectName { get; set; }

        [Category("Connection/Identity")]
        [ScriptAlias("Repository")]
        [DisplayName("Repository name")]
        [PlaceholderText("Use the project name")]
        public string RepositoryName { get; set; }

        [Category("Connection/Identity")]
        [ScriptAlias("Url")]
        [DisplayName("Project collection URL")]
        [PlaceholderText("Use team project from AzureDevOps resource")]
        public string InstanceUrl { get; set; }

        [Category("Connection/Identity")]
        [ScriptAlias("UserName")]
        [DisplayName("User name")]
        [PlaceholderText("Use user name from AzureDevOps resource's credentials")]
        public string UserName { get; set; }

        [Category("Connection/Identity")]
        [ScriptAlias("Token")]
        [DisplayName("Personal access token")]
        [PlaceholderText("Use team project from AzureDevOps resource's credential")]
        public SecureString Token { get; set; }

        protected override Task BeforeRemoteExecuteAsync(IOperationExecutionContext context)
        {
            var (c, r) = this.GetCredentialsAndResource((ICredentialResolutionContext)context);
            this.RepositoryName = AH.CoalesceString(this.RepositoryName, r?.RepositoryName);
            this.ProjectName = AH.CoalesceString(this.ProjectName, r?.ProjectName);
            this.InstanceUrl = AH.CoalesceString(this.InstanceUrl, c?.ServiceUrl, r?.LegacyInstanceUrl);
            this.UserName = AH.CoalesceString(this.UserName, c?.UserName);
            this.Token ??= c?.Password;

            return base.BeforeRemoteExecuteAsync(context);
        }
    }
}
