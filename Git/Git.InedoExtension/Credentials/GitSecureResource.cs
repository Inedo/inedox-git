using System.ComponentModel;
using Inedo.Documentation;
using Inedo.Extensibility.SecureResources;
using Inedo.Serialization;

namespace Inedo.Extensions.GitHub.Credentials
{
    [DisplayName("Git Repository")]
    [Description("Connect to a Git repository for source code integration")]
    public sealed class GitSecureResource : SecureResource<Extensions.Credentials.UsernamePasswordCredentials>
    {
        [Persistent]
        [DisplayName("Repository URL")]
        public string RepositoryUrl { get; set; }

        public override RichDescription GetDescription() => new RichDescription(this.RepositoryUrl);
    }
}
