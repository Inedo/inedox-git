using System.ComponentModel;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
using Inedo.Documentation;
using Inedo.Extensibility;
using Inedo.Extensibility.Credentials;
using Inedo.Extensibility.Operations;
using Inedo.Extensions.Credentials;
using Inedo.Extensions.Credentials.Git;
using Inedo.Extensions.Git.Credentials;
using Inedo.Extensions.Operations;
using Inedo.Web;
using UsernamePasswordCredentials = Inedo.Extensions.Credentials.UsernamePasswordCredentials;

namespace Inedo.Extensions.Git.Operations
{
    [DisplayName("Get Source from Git Repository")]
    [Description("Gets the source code from a general Git repository.")]
    [Tag("source-control")]
    [ScriptAlias("Get-Source")]
    [ScriptAlias("Git-GetSource", Obsolete = true)]
    [ScriptNamespace("Git", PreferUnqualified = false)]
    [DefaultProperty(nameof(ResourceName))]
    [Example(@"
# pulls source from a git resource and archives/exports the contents to the $WorkingDirectory
Git::Get-Source MyGitResource;

# pulls source from a remote repository and archives/exports the contents to a target directory
Git::Get-Source
(
    RepositoryUrl: https://github.com/Inedo/git-test.git,
    DiskPath: ~\Sources
);
")]
    public sealed class GeneralGetSourceOperation : GetSourceOperation, IGitConfiguration
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

        private UsernamePasswordCredentials credential;
        private GitSecureResourceBase resource;

        public override async Task ExecuteAsync(IOperationExecutionContext context)
        {
            (this.credential, this.resource) = await this.GetCredentialsAndResourceAsync(context);
            await base.ExecuteAsync(context);
        }
        protected override UsernamePasswordCredentials GetCredentials() => this.credential;

        protected async override Task<string> GetRepositoryUrlAsync(ICredentialResolutionContext context, CancellationToken cancellationToken)
        {
            return AH.CoalesceString(this.RepositoryUrl, await this.resource.GetRepositoryUrlAsync(context, cancellationToken));
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
