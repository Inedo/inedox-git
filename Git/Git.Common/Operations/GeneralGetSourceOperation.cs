using System.ComponentModel;
using Inedo.Documentation;
using Inedo.Extensions.Credentials;

#if BuildMaster
using Inedo.BuildMaster.Extensibility;
using Inedo.BuildMaster.Extensibility.Credentials;
#elif Otter
using Inedo.Otter.Extensibility;
using Inedo.Otter.Extensibility.Credentials;
#endif

namespace Inedo.Extensions.Operations
{
    [DisplayName("Get Source from Git Repository")]
    [Description("Gets the source code from a general Git repository.")]
    [Tag("source-control")]
    [ScriptAlias("Git-GetSource")]
    [ScriptNamespace("Git")]
    [Example(@"
# pulls source from a remote repository and archives/exports the contents to a target directory
Git-GetSource(
    Credentials: Hdars-Git,
    RepositoryUrl: https://github.com/Inedo/git-test.git,
    DiskPath: ~\Sources
);
")]
    public sealed class GeneralGetSourceOperation : GetSourceOperation, IHasCredentials<GitCredentials>
    {
    }
}
