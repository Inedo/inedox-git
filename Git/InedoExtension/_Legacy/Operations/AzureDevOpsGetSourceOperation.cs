using System.ComponentModel;
using System.Security;
using Inedo.Documentation;
using Inedo.Extensibility;
using Inedo.Extensibility.Credentials;
using Inedo.Extensibility.Git;
using Inedo.Extensibility.Operations;
using Inedo.Extensions.Credentials;
using Inedo.Extensions.Git.Legacy;
using Inedo.Extensions.Operations;

namespace Inedo.Extensions.AzureDevOps.Operations
{
    [Obsolete("Use Git::Checkout-Code instead.")]
    [Description("Gets the source code from an Azure DevOps Git repository.")]
    [ScriptAlias("Get-Source")]
    [ScriptNamespace("AzureDevOps", PreferUnqualified = false)]
    public sealed class AzureDevOpsGetSourceOperation : LegacyGetSourceOperation, ILegacyAzureDevOpsOperation
    {
        [Required]
        [ScriptAlias("From")]
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

        private protected override (UsernamePasswordCredentials, GitRepository) GetCredentialsAndResource(ICredentialResolutionContext context)
        {
            return ILegacyAzureDevOpsOperation.GetCredentialsAndResource(this, context);
        }

        protected override ExtendedRichDescription GetDescription(IOperationConfiguration config)
        {
            return new ExtendedRichDescription(
               new RichDescription("Get Azure DevOps Source"),
               new RichDescription("from ", new Hilite(config[nameof(ResourceName)]), " to ", new Hilite(AH.CoalesceString(config[nameof(this.DiskPath)], "$WorkingDirectory")))
            );
        }
    }
}
