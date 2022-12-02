using System.ComponentModel;
using Inedo.Documentation;
using Inedo.Extensibility;
using Inedo.Extensibility.VariableFunctions;

namespace Inedo.Extensions.VariableFunctions
{
    [Tag("git")]
    [ScriptAlias("DefaultGitExePath")]
    [Obsolete("This is only used by the legacy git operations.")]
    [Description("The path to the git executable to use for legacy git operations; if not specified, a built-in library is used")]
    [ExtensionConfigurationVariable(Required = false)]
    public sealed class DefaultGitExePathVariableFunction : ScalarVariableFunction
    {
        protected override object EvaluateScalar(IVariableFunctionContext context) => string.Empty;
    }
}
