using System.Security;
using Inedo.Serialization;
using System.ComponentModel;
using Inedo.Documentation;
#if BuildMaster
using Inedo.BuildMaster.Extensibility;
using Inedo.BuildMaster.Extensibility.Credentials;
using Inedo.BuildMaster.Web;
#elif Otter
using Inedo.Otter.Extensibility;
using Inedo.Otter.Extensibility.Credentials;
using Inedo.Otter.Extensions;
#endif

namespace Inedo.Extensions.Credentials
{
    [ScriptAlias("Git")]
    [DisplayName("Git")]
    [Description("Generic credentials for Git.")]
    public class GitCredentials : ResourceCredentials
    {
        [Persistent]
        [DisplayName("Repository URL")]
        public virtual string RepositoryUrl { get; set; }
        [Persistent]
        [DisplayName("User name")]
        public string UserName { get; set; }
        [Persistent(Encrypted = true)]
        [DisplayName("Password")]
        [FieldEditMode(FieldEditMode.Password)]
        public SecureString Password { get; set; }

        public override RichDescription GetDescription()
        {
            return new RichDescription(this.UserName, "@", this.RepositoryUrl);
        }
    }
}
