using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Inedo.Documentation;
using Inedo.Extensibility;
using Inedo.Extensibility.Credentials;
using Inedo.Extensibility.Operations;
using Inedo.Extensions.Credentials;
using Inedo.Extensions.Git.Credentials;
using Inedo.Extensions.Git.SuggestionProviders;
using Inedo.Extensions.Operations;
using Inedo.Web;

namespace Inedo.Extensions.Git.Operations
{
    [DisplayName("Get Source from Git Repository")]
    [Description("Gets the source code from a general Git repository.")]
    [Tag("source-control")]
    [ScriptAlias("Get-Source")]
    [ScriptAlias("Git-GetSource", Obsolete = true)]
    [ScriptNamespace("Git", PreferUnqualified = false)]
    [Example(@"
# pulls source from a remote repository and archives/exports the contents to a target directory
Git::Get-Source(
    Credentials: Hdars-Git,
    RepositoryUrl: https://github.com/Inedo/git-test.git,
    DiskPath: ~\Sources
);
")]
    public sealed class GeneralGetSourceOperation : GetSourceOperation<GeneralGitCredentials>, IGitConfiguration
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
               new RichDescription("Get Git Source"),
               new RichDescription("from ", new Hilite(source), " to ", new DirectoryHilite(AH.CoalesceString(config[nameof(this.DiskPath)], "$WorkingDirectory")))
            );
        }
    }
}
