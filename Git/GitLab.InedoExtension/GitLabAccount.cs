using System.ComponentModel;
using System.Security;
using Inedo.Documentation;
using Inedo.Extensibility.Git;
using Inedo.Serialization;
using Inedo.Web;

namespace Inedo.Extensions.GitLab
{
    [DisplayName("GitLab Account")]
    [Description("Use an account on GitLab to connect to GitLab resources")]
    [PersistFrom("Inedo.Extensions.GitLab.Credentials.GitLabSecureCredentials,GitLab")]
    public sealed class GitLabAccount : GitServiceCredentials<GitLabServiceInfo>
    {
        [Persistent]
        [DisplayName("User name")]
        [Required]
        public override string UserName { get; set; }

        [Persistent(Encrypted = true)]
        [DisplayName(Clients.GitLabClient.PasswordDisplayName)]
        [FieldEditMode(FieldEditMode.Password)]
        [Required]
        public SecureString PersonalAccessToken { get; set; }

        public override SecureString Password
        {
            get => this.PersonalAccessToken;
            set => this.PersonalAccessToken = value;
        }

        public override RichDescription GetCredentialDescription() => new (this.UserName);

        public override RichDescription GetServiceDescription()
        {
            return string.IsNullOrEmpty(this.ServiceUrl) || !this.TryGetServiceUrlHostName(out var hostName)
                ? new($"GitLab")
                : new($"GitLab (", new Hilite(hostName), ")");
        }
    }
}
