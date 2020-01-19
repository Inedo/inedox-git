using Inedo.Documentation;
using Inedo.Extensibility.Credentials;
using Inedo.Serialization;
using Inedo.Web;
using System.ComponentModel;
using System.Security;

namespace Inedo.Extensions.GitHub.Credentials
{
    [DisplayName("GitHub Account")]
    [Description("Use an account on GitHub account to connect to GitHub resources")]
    public sealed class GitHubSecureCredentials : SecureCredentials
    {
        [Persistent]
        [DisplayName("User name")]
        [Required]
        public string UserName { get; set; }

        [Persistent(Encrypted = true)]
        [DisplayName("Password or Personal access token")]
        [FieldEditMode(FieldEditMode.Password)]
        [Required]
        public SecureString Password { get; set; }

        public override RichDescription GetDescription() => new RichDescription(this.UserName);
    }
}
