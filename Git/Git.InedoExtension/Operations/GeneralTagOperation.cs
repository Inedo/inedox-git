using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Inedo.Documentation;
using Inedo.Extensions.Credentials;
using Inedo.Extensibility;
using Inedo.Extensibility.Credentials;
using Inedo.Extensibility.Operations;
using Inedo.Extensions.Git.Credentials;
using Inedo.Extensions.Operations;
using Inedo.Web;
using Inedo.Extensions.Git.SuggestionProviders;

namespace Inedo.Extensions.Git.Operations
{
    [DisplayName("Tag Git Source")]
    [Description("Tags the source code in a general Git repository.")]
    [Tag("source-control")]
    [ScriptAlias("Tag")]
    [ScriptAlias("Git-Tag", Obsolete = true)]
    [ScriptNamespace("Git", PreferUnqualified = false)]
    [Example(@"
# tags the current source tree with the current release name and build number
Git::Tag(
    Credentials: Hdars-Git,
    RepositoryUrl: https://github.com/Inedo/git-test.git,
    Tag: $ReleaseName.$BuildNumber
);
")]
    public sealed class GeneralTagOperation : TagOperation<GeneralGitCredentials>, IGitConfiguration
    {
        [ScriptAlias("From")]
        [DisplayName("From resource")]
        [SuggestableValue(typeof(GitSecureResourceSuggestionProvider))]
        public string ResourceName { get; set; }

        [ScriptAlias("Credentials")]
        [DisplayName("Credentials")]
        public override string CredentialName { get; set; }

        [ScriptAlias("RepositoryUrl")]
        [DisplayName("Repository URL")]
        [PlaceholderText("Use repository from credentials")]
        [MappedCredential(nameof(GitCredentialsBase.RepositoryUrl))]
        public string RepositoryUrl { get; set; }

        protected override Task<string> GetRepositoryUrlAsync(CancellationToken cancellationToken, ICredentialResolutionContext context)
        {
            var (credentials, resource) = this.GetCredentialsAndResource(context);
            var url = AH.CoalesceString(resource.RepositoryUrl, this.RepositoryUrl);
            return Task.FromResult(url);
        }

        protected override ExtendedRichDescription GetDescription(IOperationConfiguration config)
        {
            string source = AH.CoalesceString(config[nameof(this.RepositoryUrl)], config[nameof(this.CredentialName)]);

            return new ExtendedRichDescription(
               new RichDescription("Tag Git Source"),
               new RichDescription("in ", new Hilite(source), " with ", new DirectoryHilite(config[nameof(this.Tag)]))
            );
        }
    }
}
