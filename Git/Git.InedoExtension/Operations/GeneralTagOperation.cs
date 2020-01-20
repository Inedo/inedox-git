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
    [DisplayName("Tag Git Source")]
    [Description("Tags the source code in a general Git repository.")]
    [Tag("source-control")]
    [ScriptAlias("Tag")]
    [ScriptAlias("Git-Tag", Obsolete = true)]
    [ScriptNamespace("Git", PreferUnqualified = false)]
    [DefaultProperty(nameof(ResourceName))]
    [Example(@"
# tags the current source tree with the current release name and build number
Git::Tag Hdars-Git 
(
    Tag: $ReleaseName.$BuildNumber
);
")]
    public sealed class GeneralTagOperation : TagOperation, IGitConfiguration
    {
        [ScriptAlias("From")]
        [ScriptAlias("Credentials")]
        [DisplayName("From Git resource")]
        [SuggestableValue(typeof(SecureResourceSuggestionProvider<GitSecureResource>))]
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
               new RichDescription("Tag Git Source"),
               new RichDescription("in ", new Hilite(config.DescribeSource()), " with ", new DirectoryHilite(config[nameof(this.Tag)]))
            );
        }
    }
}
