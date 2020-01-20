using System.ComponentModel;
using System.Security;
using Inedo.Documentation;
using Inedo.Extensibility;
using Inedo.Extensibility.Credentials;
using Inedo.Extensibility.SecureResources;
using Inedo.Extensions.Credentials;
using Inedo.Serialization;
using Inedo.Web;
using UsernamePasswordCredentials = Inedo.Extensions.Credentials.UsernamePasswordCredentials;

namespace Inedo.Extensions.Git.Credentials
{
    [ScriptAlias("Git")]
    [DisplayName("Git")]
    [Description("(Legacy) Generic credentials for Git.")]
    [PersistFrom("Inedo.Extensions.Credentials.GeneralGitCredentials,Git")]
    [PersistFrom("Inedo.Extensions.Credentials.GitCredentials,Git")]
    [PersistFrom("Inedo.Extensions.Credentials.GitCredentials,GitHub")]
    [PersistFrom("Inedo.Extensions.Git.Credentials.GeneralGitCredentials,GitHub")]
    public sealed class GeneralGitLegacyResourceCredentials : GitCredentialsBase
    {
        public override RichDescription GetDescription()
        {
            return new RichDescription(AH.CoalesceString(this.UserName, "Anonymous"), "@", this.RepositoryUrl);
        }

        public override SecureCredentials ToSecureCredentials() => string.IsNullOrEmpty(this.UserName) ? null : new UsernamePasswordCredentials
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
