using System.ComponentModel;
using Inedo.Documentation;
using Inedo.Extensibility;
using Inedo.Extensibility.VariableFunctions;

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
