using System.ComponentModel;
using System.Security;
using Inedo.Documentation;
using Inedo.Extensibility.Git;
using Inedo.Serialization;
using Inedo.Web;

namespace Inedo.Extensions.GitHub
{
    [DisplayName("GitHub Account")]
    [Description("Use an account on GitHub to connect to GitHub resources")]
    [PersistFrom("Inedo.Extensions.GitHub.Credentials.GitHubSecureCredentials,GitHub")]
    public sealed class GitHubAccount : GitServiceCredentials<GitHubServiceInfo>
    {
        [Persistent]
        [DisplayName("User name")]
        [Required]
        public override string UserName { get; set; }

        [Persistent(Encrypted = true)]
        [DisplayName("Personal access token")]
        [FieldEditMode(FieldEditMode.Password)]
        [Required]
        public override SecureString Password { get; set; }

        public override RichDescription GetCredentialDescription() => new(this.UserName);

        public override RichDescription GetServiceDescription()
        {
            return string.IsNullOrEmpty(this.ServiceUrl) || !this.TryGetServiceUrlHostName(out var hostName)
                ? new("GitHub")
                : new("GitHub (", new Hilite(hostName), ")");
        }
    }
}
