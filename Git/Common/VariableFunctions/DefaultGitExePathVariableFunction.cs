using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using Inedo.Documentation;

#if BuildMaster
using Inedo.BuildMaster.Extensibility;
using Inedo.BuildMaster.Extensibility.VariableFunctions;
#elif Otter
using Inedo.Otter;
using Inedo.Otter.Extensibility;
using Inedo.Otter.Extensibility.VariableFunctions;
#endif

namespace Inedo.Extensions.VariableFunctions
{
    [ScriptAlias("DefaultGitExePath")]
    [Description("The path to the git executable to use for git operations; if not specified, a built-in library is used")]
    [Tag("git")]
#if BuildMaster
    [ExtensionConfigurationVariable(Required = false)]
#endif
    public sealed class DefaultGitExePathVariableFunction : ScalarVariableFunction
    {
#if BuildMaster
        protected override object EvaluateScalar(IGenericBuildMasterContext context)
        {
            return string.Empty;
        }
#elif Otter
        protected override object EvaluateScalar(IOtterContext context)
        {
            return string.Empty;
        }
#endif
    }
}
