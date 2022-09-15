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
using Inedo.Web;

namespace Inedo.Extensions.Git.Operations
{
    [Obsolete("Use Git::Checkout-Code instead.")]
    [DisplayName("Get Source from Git Repository")]
    [Description("Gets the source code from a general Git repository.")]
    [Tag("source-control")]
    [ScriptAlias("Get-Source")]
    [ScriptAlias("Git-GetSource", Obsolete = true)]
    [ScriptNamespace("Git", PreferUnqualified = false)]
    [DefaultProperty(nameof(ResourceName))]
    public sealed class GeneralGetSourceOperation : LegacyGetSourceOperation, ILegacyGeneralGitConfiguration
    {
        [ScriptAlias("From")]
        [ScriptAlias("Credentials")]
        [DisplayName("From Git resource")]
        [SuggestableValue(typeof(SecureResourceSuggestionProvider<GitSecureResourceBase>))]
        public string ResourceName { get; set; }

        [Category("Connection/Identity")]
        [ScriptAlias("UserName")]
        [DisplayName("User name")]
        [PlaceholderText("Use user name from Git resource's credentials")]
        public string UserName { get; set; }

        [Category("Connection/Identity")]
        [ScriptAlias("Password")]
        [DisplayName("Password")]
        [PlaceholderText("Use team project from Git resource's credential")]
        public SecureString Password { get; set; }

        [ScriptAlias("RepositoryUrl")]
        [DisplayName("Repository URL")]
        [PlaceholderText("Use URL from Git resource")]
        public string RepositoryUrl { get; set; }

        private protected override (UsernamePasswordCredentials, GitSecureResourceBase) GetCredentialsAndResource(ICredentialResolutionContext context)
        {
            return ILegacyGeneralGitConfiguration.GetCredentialsAndResource(this, context);
        }

        protected override ExtendedRichDescription GetDescription(IOperationConfiguration config)
        {
            return new ExtendedRichDescription(
               new RichDescription("Get Git Source"),
               new RichDescription("from ", new Hilite(config.DescribeSource()), " to ", new DirectoryHilite(AH.CoalesceString(config[nameof(this.DiskPath)], "$WorkingDirectory")))
            );
        }
    }
}
