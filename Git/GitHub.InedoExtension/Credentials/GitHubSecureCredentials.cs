using System.ComponentModel;
using System.Security;
using Inedo.Documentation;
using Inedo.Extensions.Credentials.Git;
using Inedo.Serialization;
using Inedo.Web;
using UsernamePasswordCredentials = Inedo.Extensions.Credentials.UsernamePasswordCredentials;

namespace Inedo.Extensions.GitHub.Credentials
{
    [DisplayName("GitHub Account")]
    [Description("Use an account on GitHub account to connect to GitHub resources")]
    public sealed class GitHubSecureCredentials : GitSecureCredentialsBase
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

        public override UsernamePasswordCredentials ToUsernamePassword() => string.IsNullOrEmpty(this.UserName) ? null : new UsernamePasswordCredentials
        {
            UserName = this.UserName,
            Password = this.Password
        };
    }
}
