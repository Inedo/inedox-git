using System.ComponentModel;
using System.Security;
using Inedo.Documentation;
using Inedo.Extensions.Credentials.Git;
using Inedo.Serialization;
using Inedo.Web;
using UsernamePasswordCredentials = Inedo.Extensions.Credentials.UsernamePasswordCredentials;

namespace Inedo.Extensions.GitLab.Credentials
{
    [DisplayName("GitLab Account")]
    [Description("Use an account on GitLab account to connect to GitLab resources")]
    public sealed class GitLabSecureCredentials : GitSecureCredentialsBase
    {
        [Persistent]
        [DisplayName("User name")]
        [Required]
        public override string UserName { get; set; }

        [Persistent(Encrypted = true)]
        [DisplayName("Personal access token")]
        [FieldEditMode(FieldEditMode.Password)]
        [Required]
        public SecureString PersonalAccessToken { get; set; }

        public override SecureString Password
        {
            get => this.PersonalAccessToken;
            set => this.PersonalAccessToken = value;
        }

        public override RichDescription GetDescription() => new(this.UserName);

        public override UsernamePasswordCredentials ToUsernamePassword() => string.IsNullOrEmpty(this.UserName) ? null : new UsernamePasswordCredentials
        {
            UserName = this.UserName,
            Password = this.PersonalAccessToken
        };
    }
}
