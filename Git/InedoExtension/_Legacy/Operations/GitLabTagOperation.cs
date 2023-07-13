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

namespace Inedo.Extensions.GitLab.Operations
{
    [Obsolete("Use Git::Ensure-Tag instead.")]
    [DisplayName("Tag GitLab Source")]
    [Description("Tags the source code in a GitLab project.")]
    [Tag("source-control")]
    [ScriptAlias("Tag")]
    [ScriptAlias("GitLab-Tag", Obsolete = true)]
    [ScriptNamespace("GitLab", PreferUnqualified = false)]
    public sealed class GitLabTagOperation : LegacyTagOperation, ILegacyGitLabOperation
    {
        [ScriptAlias("From")]
        [ScriptAlias("Credentials")]
        [DisplayName("From GitLab resource")]
        public string ResourceName { get; set; }

        [Category("Connection/Identity")]
        [ScriptAlias("UserName")]
        [DisplayName("User name")]
        [PlaceholderText("Use user name from GitLab resource's credentials")]
        public string UserName { get; set; }

        [Category("Connection/Identity")]
        [ScriptAlias("Password")]
        [DisplayName("Password")]
        [PlaceholderText("Use password from GitLab resource's credentials")]
        public SecureString Password { get; set; }

        [Category("Connection/Identity")]
        [ScriptAlias("Namespace")]
        [ScriptAlias("Group", Obsolete = true)]
        [DisplayName("Namespace")]
        [PlaceholderText("Use namespace from GitLab resource")]
        public string GroupName { get; set; }

        [Category("Connection/Identity")]
        [ScriptAlias("Project")]
        [DisplayName("Project name")]
        [PlaceholderText("Use project from GitLab resource")]
        public string ProjectName { get; set; }

        [Category("Connection/Identity")]
        [ScriptAlias("ApiUrl")]
        [DisplayName("API URL")]
        [PlaceholderText("Use URL from GitLab resource")]
        public string ApiUrl { get; set; }

        private protected override (UsernamePasswordCredentials, GitRepository) GetCredentialsAndResource(ICredentialResolutionContext context)
        {
            return ILegacyGitLabOperation.GetCredentialsAndResource(this, context);
        }

        protected override ExtendedRichDescription GetDescription(IOperationConfiguration config)
        {
            return new ExtendedRichDescription(
               new RichDescription("Tag GitLab Source"),
               new RichDescription("in ", new Hilite(config[nameof(ResourceName)]), " with ", new Hilite(config[nameof(this.Tag)]))
            );
        }
    }
}
