using System.ComponentModel;
using Inedo.Extensibility;
using Inedo.Serialization;

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
