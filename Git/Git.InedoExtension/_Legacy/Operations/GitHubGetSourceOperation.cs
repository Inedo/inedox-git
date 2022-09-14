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

namespace Inedo.Extensions.GitHub.Operations
{
    [Obsolete("Use Git::Checkout-Code instead.")]
    [DisplayName("Get Source from GitHub Repository")]
    [Description("Gets the source code from a GitHub repository.")]
    [Tag("source-control")]
    [ScriptAlias("Get-Source")]
    [ScriptAlias("GitHub-GetSource", Obsolete = true)]
    [ScriptNamespace("GitHub", PreferUnqualified = false)]
    [DefaultProperty(nameof(ResourceName))]
    public sealed class GitHubGetSourceOperation : GetSourceOperation, ILegacyGitHubOperation
    {
        [ScriptAlias("From")]
        [ScriptAlias("Credentials")]
        [DisplayName("From GitHub resource")]
        public string ResourceName { get; set; }

        [Category("Connection/Identity")]
        [ScriptAlias("UserName")]
        [DisplayName("User name")]
        [PlaceholderText("Use user name from GitHub resource's credentials")]
        public string UserName { get; set; }

        [Category("Connection/Identity")]
        [ScriptAlias("Password")]
        [DisplayName("Password")]
        [PlaceholderText("Use password from GitHub resource's credentials")]
        public SecureString Password { get; set; }

        [Category("Connection/Identity")]
        [ScriptAlias("Organization")]
        [DisplayName("Organization name")]
        [PlaceholderText("Use organization from Github resource")]
        public string OrganizationName { get; set; }

        [Category("Connection/Identity")]
        [ScriptAlias("Repository")]
        [DisplayName("Repository name")]
        [PlaceholderText("Use repository from Github resource")]
        public string RepositoryName { get; set; }

        [Category("Connection/Identity")]
        [ScriptAlias("ApiUrl")]
        [DisplayName("API URL")]
        [Description("Use URL from Github resource.")]
        public string ApiUrl { get; set; }

        private protected override (UsernamePasswordCredentials, GitSecureResourceBase) GetCredentialsAndResource(ICredentialResolutionContext context)
        {
            return ILegacyGitHubOperation.GetCredentialsAndResource(this, context);
        }

        protected override ExtendedRichDescription GetDescription(IOperationConfiguration config)
        {
            return new ExtendedRichDescription(
               new RichDescription("Get GitHub Source"),
               new RichDescription("from ", new Hilite(config[nameof(ResourceName)]), " to ", new Hilite(AH.CoalesceString(config[nameof(this.DiskPath)], "$WorkingDirectory")))
            );
        }
    }
}
