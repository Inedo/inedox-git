using System.ComponentModel;
using Inedo.Documentation;

#if BuildMaster
using Inedo.BuildMaster.Extensibility;
using Inedo.BuildMaster.Extensibility.VariableFunctions;
using IVariableFunctionContext = Inedo.BuildMaster.Extensibility.IGenericBuildMasterContext;
#elif Otter
using Inedo.Otter.Extensibility;
using Inedo.Otter.Extensibility.VariableFunctions;
using IVariableFunctionContext = Inedo.Otter.IOtterContext;
#elif Hedgehog
using Inedo.Extensibility;
using Inedo.Extensibility.VariableFunctions;
#endif

namespace Inedo.Extensions.VariableFunctions
{
    [ScriptAlias("DefaultGitExePath")]
    [Description("The path to the git executable to use for git operations; if not specified, a built-in library is used")]
    [Tag("git")]
    [ExtensionConfigurationVariable(Required = false)]
    public sealed class DefaultGitExePathVariableFunction : ScalarVariableFunction
    {
        protected override object EvaluateScalar(IVariableFunctionContext context)
        {
            return string.Empty;
        }
    }
}
