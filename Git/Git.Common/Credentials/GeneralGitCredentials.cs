using System.ComponentModel;
using Inedo.Serialization;

#if BuildMaster
using Inedo.BuildMaster.Extensibility;
#elif Otter
using Inedo.Otter.Extensibility;
#elif Hedgehog
using Inedo.Extensibility;
#endif

namespace Inedo.Extensions.Credentials
{
    [ScriptAlias("Git")]
    [DisplayName("Git")]
    [Description("Generic credentials for Git.")]
    [PersistFrom("Inedo.Extensions.Credentials.GitCredentials,Git")]
    [PersistFrom("Inedo.Extensions.Credentials.GitCredentials,GitHub")]
    public sealed class GeneralGitCredentials : GitCredentialsBase
    {
    }
}
