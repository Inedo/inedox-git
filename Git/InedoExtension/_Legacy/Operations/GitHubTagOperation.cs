using System.ComponentModel;
using System.Security;
using Inedo.Documentation;
using Inedo.Extensibility;
using Inedo.Extensibility.Credentials;
using Inedo.Extensibility.Git;
using Inedo.Extensibility.Operations;
using Inedo.Extensions.Credentials;
using Inedo.Extensions.Credentials.Git;
using Inedo.Extensions.Git.Legacy;
using Inedo.Extensions.Operations;

namespace Inedo.Extensions.GitHub.Operations
{
    [Obsolete("Use Git::Ensure-Tag instead.")]
    [Description("Tags the source code in a GitHub repository.")]
    [ScriptAlias("Tag")]
    [ScriptAlias("GitHub-Tag", Obsolete = true)]
    [ScriptNamespace("GitHub", PreferUnqualified = false)]
    public sealed class GitHubTagOperation : LegacyTagOperation, ILegacyGitHubOperation
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

        private protected override (UsernamePasswordCredentials, GitRepository) GetCredentialsAndResource(ICredentialResolutionContext context)
        {
            return ILegacyGitHubOperation.GetCredentialsAndResource(this, context);
        }

        protected override ExtendedRichDescription GetDescription(IOperationConfiguration config)
        {
            return new ExtendedRichDescription(
               new RichDescription("Tag GitHub Source"),
               new RichDescription("in ", new Hilite(config[nameof(ResourceName)]), " with ", new Hilite(config[nameof(this.Tag)]))
            );
        }
    }
}
