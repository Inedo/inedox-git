using System.Security;
using Inedo.Serialization;
using System.ComponentModel;
using Inedo.Documentation;

#if BuildMaster
using Inedo.BuildMaster.Extensibility.Credentials;
using Inedo.BuildMaster.Web;
#elif Otter
using Inedo.Otter.Extensibility.Credentials;
using Inedo.Otter.Extensions;
#elif Hedgehog
using Inedo.Extensibility.Credentials;
using Inedo.Web;
#endif

namespace Inedo.Extensions.Credentials
{
    public abstract class GitCredentialsBase : ResourceCredentials
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
