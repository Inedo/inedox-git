using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Inedo.Documentation;
using Inedo.Extensions.Credentials;
using Inedo.Extensibility;
using Inedo.Extensibility.Credentials;
using Inedo.Extensibility.Operations;

namespace Inedo.Extensions.Operations
{
    [DisplayName("Tag Git Source")]
    [Description("Tags the source code in a general Git repository.")]
    [Tag("source-control")]
    [ScriptAlias("Tag")]
    [ScriptAlias("Git-Tag", Obsolete = true)]
    [ScriptNamespace("Git", PreferUnqualified = false)]
    [Example(@"
# tags the current source tree with the current release name and package number
Git::Tag(
    Credentials: Hdars-Git,
    RepositoryUrl: https://github.com/Inedo/git-test.git,
    Tag: $ReleaseName.$PackageNumber
);
")]
    public sealed class GeneralTagOperation : TagOperation<GeneralGitCredentials>
    {
        [ScriptAlias("Credentials")]
        [DisplayName("Credentials")]
        public override string CredentialName { get; set; }

        [ScriptAlias("RepositoryUrl")]
        [DisplayName("Repository URL")]
        [PlaceholderText("Use repository from credentials")]
        [MappedCredential(nameof(GitCredentialsBase.RepositoryUrl))]
        public string RepositoryUrl { get; set; }

        protected override Task<string> GetRepositoryUrlAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(this.RepositoryUrl);
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
