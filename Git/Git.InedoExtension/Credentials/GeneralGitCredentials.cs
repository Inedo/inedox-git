using System.ComponentModel;
using Inedo.Extensibility;
using Inedo.Extensions.Credentials;
using Inedo.Serialization;

namespace Inedo.Extensions.Git.Credentials
{
    [ScriptAlias("Git")]
    [DisplayName("Git")]
    [Description("Generic credentials for Git.")]
    [PersistFrom("Inedo.Extensions.Credentials.GeneralGitCredentials,Git")]
    [PersistFrom("Inedo.Extensions.Credentials.GitCredentials,Git")]
    [PersistFrom("Inedo.Extensions.Credentials.GitCredentials,GitHub")]
    public sealed class GeneralGitCredentials : GitCredentialsBase
    {
    }
}
