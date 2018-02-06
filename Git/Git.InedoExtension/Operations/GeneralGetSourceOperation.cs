using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Inedo.Documentation;
using Inedo.Extensions.Credentials;

#if BuildMaster
using Inedo.BuildMaster.Extensibility;
using Inedo.BuildMaster.Extensibility.Credentials;
using Inedo.BuildMaster.Extensibility.Operations;
#elif Otter
using Inedo.Otter.Extensibility;
using Inedo.Otter.Extensibility.Credentials;
using Inedo.Otter.Extensibility.Operations;
#elif Hedgehog
using Inedo.Extensibility;
using Inedo.Extensibility.Credentials;
using Inedo.Extensibility.Operations;
#endif

namespace Inedo.Extensions.Operations
{
    [DisplayName("Get Source from Git Repository")]
    [Description("Gets the source code from a general Git repository.")]
    [Tag("source-control")]
    [ScriptAlias("Git-GetSource")]
    [ScriptNamespace("Git", PreferUnqualified = true)]
    [Example(@"
# pulls source from a remote repository and archives/exports the contents to a target directory
Git-GetSource(
    Credentials: Hdars-Git,
    RepositoryUrl: https://github.com/Inedo/git-test.git,
    DiskPath: ~\Sources
);
")]
    public sealed class GeneralGetSourceOperation : GetSourceOperation<GeneralGitCredentials>
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
               new RichDescription("Get Git Source"),
               new RichDescription("from ", new Hilite(source), " to ", new DirectoryHilite(config[nameof(this.DiskPath)]))
            );
        }
    }
}
