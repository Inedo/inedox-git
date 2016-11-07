using System.ComponentModel;
using Inedo.Documentation;

#if BuildMaster
using Inedo.BuildMaster.Extensibility;
#elif Otter
using Inedo.Otter.Extensibility;
#endif

namespace Inedo.Extensions.Operations
{
    [DisplayName("Tag Git Source")]
    [Description("Tags the source code in a general Git repository.")]
    [Tag("source-control")]
    [ScriptAlias("Git-Tag")]
    [Example(@"
# tags the current source tree with the current release name and package number
Git-GetSource(
    Credentials: Hdars-Git,
    RepositoryUrl: https://github.com/Inedo/git-test.git,
    Tag: $ReleaseName.$PackageNumber
);
")]
    public sealed class GeneralTagOperation : TagOperation
    {
    }
}
