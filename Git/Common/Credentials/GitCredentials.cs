using System.ComponentModel;
using System.Security;
using Inedo.Documentation;
using Inedo.Extensibility;
using Inedo.Extensibility.Credentials;
using Inedo.Serialization;
using Inedo.Web;

namespace Inedo.Extensions.Credentials
{
    [ScriptAlias("Git")]
    [DisplayName("Git")]
    [Description("Generic credentials for Git.")]
    [PersistFrom("Inedo.Extensions.Credentials.GeneralGitCredentials,Git")]
    [PersistFrom("Inedo.Extensions.Git.Credentials.GeneralGitCredentials,Git")]
    [PersistFrom("Inedo.Extensions.Credentials.GitCredentials,Git")]
    [PersistFrom("Inedo.Extensions.Credentials.GitCredentials,GitHub")]
    public
#if !Git
    abstract
#endif
    class GitCredentials : CascadedResourceCredentials
    {
        [Persistent]
        [DisplayName("Repository URL")]
        public virtual string RepositoryUrl { get; set; }
        [Persistent]
        [DisplayName("User name")]
        public virtual string UserName { get; set; }
        [Persistent(Encrypted = true)]
        [DisplayName("Password")]
        [FieldEditMode(FieldEditMode.Password)]
        public virtual SecureString Password { get; set; }

        public override RichDescription GetDescription()
        {
            return new RichDescription(AH.CoalesceString(this.UserName, "Anonymous"), "@", this.RepositoryUrl);
        }
    }
}
