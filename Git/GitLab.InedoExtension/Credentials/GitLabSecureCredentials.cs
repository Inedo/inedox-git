using Inedo.Documentation;
using Inedo.Extensibility.Credentials;
using Inedo.Serialization;
using Inedo.Web;
using System.ComponentModel;
using System.Security;

namespace Inedo.Extensions.GitLab.Credentials
{
    [DisplayName("GitLab Account")]
    [Description("Use an account on GitLab account to connect to GitLab resources")]
    public sealed class GitLabSecureCredentials : SecureCredentials
    {
        [Persistent]
        [DisplayName("User name")]
        [Required]
        public string UserName { get; set; }

        [Persistent(Encrypted = true)]
        [DisplayName("Personal access token")]
        [FieldEditMode(FieldEditMode.Password)]
        [Required]
        public SecureString PersonalAccessToken { get; set; }

        public override RichDescription GetDescription() => new RichDescription(this.UserName);
    }
}
