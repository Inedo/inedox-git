using System.ComponentModel;
using Inedo.Extensibility;
using Inedo.Extensibility.Credentials;
using Inedo.Extensibility.SecureResources;
using Inedo.Extensions.Credentials;
using Inedo.Extensions.GitHub.Credentials;
using Inedo.Serialization;
using UsernamePasswordCredentials = Inedo.Extensions.Credentials.UsernamePasswordCredentials;

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
        public override SecureCredentials ToSecureCredentials() => new UsernamePasswordCredentials
        {
            UserName = this.UserName,
            Password = this.Password
        };
        public override SecureResource ToSecureResource() => new GitSecureResource
        {
            RepositoryUrl = this.RepositoryUrl
        };
    }
}
