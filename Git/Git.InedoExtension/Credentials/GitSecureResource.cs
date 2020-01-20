using System.ComponentModel;
using Inedo.Documentation;
using Inedo.Extensibility.SecureResources;
using Inedo.Extensions.Credentials;
using Inedo.Serialization;

namespace Inedo.Extensions.Git.Credentials
{
    [DisplayName("Git Repository")]
    [Description("Connect to a Git repository for source code integration")]
    public sealed class GitSecureResource : SecureResource<UsernamePasswordCredentials, GitSecureCredentialsBase>
    {
        [Persistent]
        [DisplayName("Repository URL")]
        public string RepositoryUrl { get; set; }

        public override RichDescription GetDescription() => new RichDescription(this.RepositoryUrl);
    }
}
