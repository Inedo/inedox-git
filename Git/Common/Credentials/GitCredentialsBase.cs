using System.ComponentModel;
using System.Security;
using Inedo.Documentation;
using Inedo.Extensibility.Credentials;
using Inedo.Serialization;
using Inedo.Web;

namespace Inedo.Extensions.Credentials
{
    public abstract class GitCredentialsBase : CascadedResourceCredentials
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
