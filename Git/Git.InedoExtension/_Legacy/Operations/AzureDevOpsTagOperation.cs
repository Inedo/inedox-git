using System.ComponentModel;
using System.Security;
using Inedo.Documentation;
using Inedo.Extensibility;
using Inedo.Extensibility.Credentials;
using Inedo.Extensibility.Operations;
using Inedo.Extensions.Credentials;
using Inedo.Extensions.Credentials.Git;
using Inedo.Extensions.Git.Legacy;
using Inedo.Extensions.Operations;

namespace Inedo.Extensions.AzureDevOps.Operations
{
    [Obsolete("Use Git::Ensure-Tag instead.")]
    [DisplayName("Tag Azure DevOps Source")]
    [Description("Tags the source code in an Azure DevOps project.")]
    [Tag("source-control")]
    [ScriptAlias("Tag")]
    [ScriptNamespace("AzureDevOps", PreferUnqualified = false)]
    public sealed class AzureDevOpsTagOperation : LegacyTagOperation, ILegacyAzureDevOpsOperation
    {
        [Required]
        [DisplayName("From AzureDevOps resource")]
        public string ResourceName { get; set; }

        [Category("Connection/Identity")]
        [ScriptAlias("Project")]
        [DisplayName("Project name")]
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

        private protected override (UsernamePasswordCredentials, GitSecureResourceBase) GetCredentialsAndResource(ICredentialResolutionContext context)
        {
            return ILegacyAzureDevOpsOperation.GetCredentialsAndResource(this, context);
        }

        protected override ExtendedRichDescription GetDescription(IOperationConfiguration config)
        {
            return new ExtendedRichDescription(
               new RichDescription("Tag Azure DevOps Source"),
               new RichDescription("in ", new Hilite(config[nameof(ResourceName)]), " with ", new Hilite(config[nameof(this.Tag)]))
            );
        }
    }
}
